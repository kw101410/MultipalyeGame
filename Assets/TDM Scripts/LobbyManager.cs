using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP; // Unity Transport 사용
using TMPro; // TextMeshPro UI
using UnityEngine.UI; // 일반 UI
using System.Threading.Tasks; // 비동기 작업
using Unity.Services.Core; // UGS 코어
using Unity.Services.Authentication; // 인증
using Unity.Services.Relay; // 릴레이 서비스
using Unity.Services.Relay.Models; // 릴레이 모델

public class LobbyManager : NetworkBehaviour
{
    [Header("Lobby Panel")]
    public GameObject lobbyPanel; // IP 입력 및 방 만들기/들어가기 화면
    public TMP_InputField ipInputField; // IP 주소 입력 (이제는 Join Code 입력용으로 사용)
    public Button createLoopButton; // 방 만들기
    public Button joinLoopButton;   // 방 들어가기
    public TextMeshProUGUI statusText; // 상태 표시

    [Header("Room Panel (Waiting Room)")]
    public GameObject roomPanel; // 대기실 화면
    public GameObject extraUI;   // 로비에서만 보여야 하는 UI (ex: Image (1))
    
    // 플레이어 정보 (Red = Host, Blue = Guest)
    public TextMeshProUGUI redTeamPlayerText;   
    public TextMeshProUGUI blueTeamPlayerText;  

    // 버튼 (Host용 vs Guest용)
    public Button hostStartButton; // Host 전용 시작 버튼
    public Button hostLeaveButton; // Host 전용 나가기 버튼
    
    public Button guestReadyButton; // Guest 전용 준비 버튼
    public Button guestLeaveButton; // Guest 전용 나가기 버튼

    [Header("Lobby BGM")]
    public AudioSource bgmSource;
    public AudioClip lobbyBgm;

    // 게스트의 준비 상태
    public NetworkVariable<bool> isGuestReady = new NetworkVariable<bool>(false);

    // 현재 방의 Join Code (호스트인 경우 저장해둠)
    private string currentJoinCode;

    private async void Start()
    {
        // 로비 배경음 재생
        if (bgmSource != null && lobbyBgm != null)
        {
            bgmSource.clip = lobbyBgm;
            bgmSource.loop = true;
            bgmSource.Play();
        }

        // 버튼 이벤트 연결 (Lobby)
        if (createLoopButton != null) createLoopButton.onClick.AddListener(OnCreateRoomClicked);
        if (joinLoopButton != null) joinLoopButton.onClick.AddListener(OnJoinRoomClicked);

        // 버튼 이벤트 연결 (Room)
        if (hostStartButton != null) hostStartButton.onClick.AddListener(OnStartClicked);
        if (hostLeaveButton != null) hostLeaveButton.onClick.AddListener(OnLeaveClicked);
        
        if (guestReadyButton != null) guestReadyButton.onClick.AddListener(OnReadyClicked);
        if (guestLeaveButton != null) guestLeaveButton.onClick.AddListener(OnLeaveClicked);

        // 기본 텍스트 설정 (안내 문구)
        if (ipInputField != null)
        {
            if (string.IsNullOrEmpty(ipInputField.text) || ipInputField.text == "127.0.0.1")
            {
                ipInputField.text = ""; // 비워둠 (플레이스홀더가 보이게)
                ipInputField.placeholder.GetComponent<TextMeshProUGUI>().text = "Enter Join Code...";
            }
        }

        // 초기 화면 설정
        ShowLobbyPanel();

        // Unity 서비스 초기화 및 익명 로그인
        await Authenticate();
    }

    private async Task Authenticate()
    {
        try
        {
            await UnityServices.InitializeAsync();
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            Debug.Log($"Signed in anonymously as {AuthenticationService.Instance.PlayerId}");
            UpdateStatus("Connected to Unity Services.");
        }
        catch (System.Exception e)
        {
            Debug.LogError(e);
            UpdateStatus("Failed to connect to Unity Services.");
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // 1. 방에 들어왔으므로(Spawn됨) 로비 패널 끄고 대기실 패널 켬
        ShowRoomPanel();

        // 2. 값 변경 시 UI 업데이트 구독
        isGuestReady.OnValueChanged += (prev, curr) => UpdateRoomUI();

        // 3. 클라이언트 접속/해제 이벤트 구독 (인원 변동 시 UI 업데이트)
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

        // 4. 초기 UI 업데이트
        UpdateRoomUI();
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        // 이벤트 해제
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
        
        // 다시 로비 화면으로 (연결 끊김 등)
        ShowLobbyPanel();
        UpdateStatus("Disconnected from Room.");
        currentJoinCode = ""; // 코드 초기화
    }

    private void OnClientConnected(ulong clientId)
    {
        UpdateRoomUI();
    }

    private void OnClientDisconnected(ulong clientId)
    {
        UpdateRoomUI();
        if (IsServer && clientId != NetworkManager.ServerClientId)
        {
            // 게스트가 나가면 준비 상태 초기화
            isGuestReady.Value = false;
        }
    }

    // ------------------------------------------------------------------------
    // Lobby Actions (Relay Integration)
    // ------------------------------------------------------------------------

    private async void OnCreateRoomClicked()
    {
        UpdateStatus("Creating Relay Room...");
        string joinCode = await CreateRelay();
        
        if (!string.IsNullOrEmpty(joinCode))
        {
            currentJoinCode = joinCode;
            UpdateStatus($"Room Created! Code: {joinCode}");
            
            // 호스트 시작
            if (NetworkManager.Singleton.StartHost())
            {
                // 성공
            }
            else
            {
                UpdateStatus("Failed to Start Host.");
            }
        }
        else
        {
            UpdateStatus("Failed to Create Relay.");
        }
    }

    private async Task<string> CreateRelay()
    {
        try
        {
            // 최대 4명 (호스트 포함)
            // 명시적 네임스페이스 사용: Unity.Services.Relay.Models.Allocation
            Unity.Services.Relay.Models.Allocation allocation = await Unity.Services.Relay.RelayService.Instance.CreateAllocationAsync(3);
            string joinCode = await Unity.Services.Relay.RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            
            // Relay 서버 데이터 설정 (IPv4, Port, Allocation ID, Key, Connection Data)
            transport.SetRelayServerData(
                allocation.RelayServer.IpV4,
                (ushort)allocation.RelayServer.Port,
                allocation.AllocationIdBytes,
                allocation.Key,
                allocation.ConnectionData
            );

            return joinCode;
        }
        catch (Unity.Services.Relay.RelayServiceException e)
        {
            Debug.LogError(e);
            return null;
        }
    }

    private async void OnJoinRoomClicked()
    {
        string joinCode = ipInputField.text;
        
        if (string.IsNullOrEmpty(joinCode))
        {
            UpdateStatus("Please enter a Join Code.");
            return;
        }

        UpdateStatus($"Joining Relay Room: {joinCode}...");
        
        bool success = await JoinRelay(joinCode);
        if (success)
        {
            if (NetworkManager.Singleton.StartClient())
            {
                UpdateStatus("Joined Room!");
            }
            else
            {
                UpdateStatus("Failed to Start Client.");
            }
        }
        else
        {
            UpdateStatus("Invalid Join Code or Failed to Join.");
        }
    }

    private async Task<bool> JoinRelay(string joinCode)
    {
        try
        {
            // 명시적 네임스페이스 사용: Unity.Services.Relay.Models.JoinAllocation
            Unity.Services.Relay.Models.JoinAllocation joinAllocation = await Unity.Services.Relay.RelayService.Instance.JoinAllocationAsync(joinCode);
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            
            // Relay 서버 데이터 설정 (Allocation ID, Key, Connection Data, Host Connection Data)
            transport.SetRelayServerData(
                joinAllocation.RelayServer.IpV4,
                (ushort)joinAllocation.RelayServer.Port,
                joinAllocation.AllocationIdBytes,
                joinAllocation.Key,
                joinAllocation.ConnectionData,
                joinAllocation.HostConnectionData // Host Connection Data 필수
            );

            return true;
        }
        catch (Unity.Services.Relay.RelayServiceException e)
        {
            Debug.LogError(e);
            return false;
        }
    }

    // ------------------------------------------------------------------------
    // Room Actions (Ready / Start / Leave)
    // ------------------------------------------------------------------------

    private void OnStartClicked()
    {
        if (IsServer) StartGameServerRpc();
    }

    private void OnReadyClicked()
    {
        // Client only logic for toggling ready
        if (!IsServer) ToggleReadyServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void ToggleReadyServerRpc(ServerRpcParams rpcParams = default)
    {
        isGuestReady.Value = !isGuestReady.Value;
    }

    [ServerRpc]
    private void StartGameServerRpc()
    {
        NetworkManager.Singleton.SceneManager.LoadScene("GameScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
    }

    private void OnLeaveClicked()
    {
        NetworkManager.Singleton.Shutdown();
        ShowLobbyPanel();
    }

    // ------------------------------------------------------------------------
    // UI Updates
    // ------------------------------------------------------------------------

    private void ShowLobbyPanel()
    {
        if (lobbyPanel != null) lobbyPanel.SetActive(true);
        if (roomPanel != null) roomPanel.SetActive(false);
        if (extraUI != null) extraUI.SetActive(true);
    }

    private void ShowRoomPanel()
    {
        if (lobbyPanel != null) lobbyPanel.SetActive(false);
        if (roomPanel != null) roomPanel.SetActive(true);
        if (extraUI != null) extraUI.SetActive(false);
    }

    private void UpdateRoomUI()
    {
        if (NetworkManager.Singleton == null) return;
        int playerCount = NetworkManager.Singleton.ConnectedClientsIds.Count;

        // 1. Red Team (Host) 텍스트 
        if (redTeamPlayerText != null)
        {
            if (IsServer && !string.IsNullOrEmpty(currentJoinCode))
            {
                // Host 화면에는 Join Code 표시
                redTeamPlayerText.text = $"Player 1 (Host)\nCode: <color=yellow>{currentJoinCode}</color>";
            }
            else
            {
                redTeamPlayerText.text = "Player 1 (Host)";
            }
            redTeamPlayerText.color = Color.white;
        }

        // 2. Blue Team (Guest) 텍스트
        if (blueTeamPlayerText != null)
        {
            if (playerCount >= 2)
            {
                bool ready = isGuestReady.Value;
                string readyState = ready ? "<color=green>[READY]</color>" : "<color=red>[NOT READY]</color>";
                
                blueTeamPlayerText.text = $"Player 2 {readyState}";
                blueTeamPlayerText.color = Color.white;
            }
            else
            {
                blueTeamPlayerText.text = "Waiting for Player...";
                blueTeamPlayerText.color = Color.gray;
            }
        }

        // 3. 버튼 상태 (Host vs Guest)
        
        // Host Button Logic
        if (IsServer)
        {
            if (hostStartButton != null)
            {
                hostStartButton.gameObject.SetActive(true);
                bool canStart = (playerCount >= 2 && isGuestReady.Value);
                hostStartButton.interactable = canStart; 
            }
            if (hostLeaveButton != null) hostLeaveButton.gameObject.SetActive(true); // Host Leave 보이기

            // Guest 버튼 숨기기
            if (guestReadyButton != null) guestReadyButton.gameObject.SetActive(false);
            if (guestLeaveButton != null) guestLeaveButton.gameObject.SetActive(false);
        }
        else // Guest (Client)
        {
            // Host 버튼 숨기기
            if (hostStartButton != null) hostStartButton.gameObject.SetActive(false);
            if (hostLeaveButton != null) hostLeaveButton.gameObject.SetActive(false);

            if (guestReadyButton != null)
            {
                guestReadyButton.gameObject.SetActive(true);
                TextMeshProUGUI btnText = guestReadyButton.GetComponentInChildren<TextMeshProUGUI>();
                if (btnText != null) 
                {
                    btnText.text = isGuestReady.Value ? "CANCEL" : "READY";
                }
            }
            if (guestLeaveButton != null) guestLeaveButton.gameObject.SetActive(true); // Guest Leave 보이기
        }
    }

    private void UpdateStatus(string message)
    {
        if (statusText != null) statusText.text = message;
    }
}
