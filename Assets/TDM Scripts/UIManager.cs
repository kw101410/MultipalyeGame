using UnityEngine;
using TMPro; // TMP 필수
using System.Collections; // 코루틴용

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [Header("UI 오브젝트 연결 (Inspector 확인)")]
    public TextMeshProUGUI hpText;
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI winLoseText; // 승리 메시지

    [Header("Round Result Panels")]
    public GameObject winPanel;  // 승리 이미지 패널
    public GameObject losePanel; // 패배 이미지 패널
    public TextMeshProUGUI centerMessageText; // 중앙 메시지 (Ready, Fight 등)

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        // 라운드 매니저 찾아서 점수판 연결
        StartCoroutine(ConnectToGameManager());
    }

    // HP 갱신 (팀 컬러 적용)
    // PlayerController에서 호출할 때 teamId도 같이 넘겨줘야 함
    public void UpdateHP(int hp, int teamId = -1)
    {
        if (hpText == null) return;

        hpText.text = $"HP: {hp}";

        // 1. 딸피(30 이하)면 무조건 빨강
        if (hp <= 30)
        {
            hpText.color = Color.red;
        }
        // 2. 아니면 팀 색깔 (0:White/Red, 1:Cyan/Blue) - 니 취향껏
        else
        {
            if (teamId == 0) hpText.color = Color.white;      // 레드팀 기본색
            else if (teamId == 1) hpText.color = Color.cyan;  // 블루팀 기본색
            else hpText.color = Color.white; // 몰루?
        }
    }

    // ---------------------------------------------------------
    // 아래는 점수판 자동 갱신 로직 (아까 그거)
    // ---------------------------------------------------------

    private IEnumerator ConnectToGameManager()
    {
        while (RoundGameManager.Instance == null)
        {
            yield return null;
        }
        
        // 초기 점수 한 번 갱신
        UpdateRoundScore(RoundGameManager.Instance.RedRoundScore.Value, RoundGameManager.Instance.BlueRoundScore.Value);
    }

    // 점수 업데이트 (RoundGameManager에서 호출)
    public void UpdateRoundScore(int red, int blue)
    {
        if (scoreText != null)
        {
            scoreText.text = $"<color=red>RED {red}</color>  :  <color=blue>{blue} BLUE</color>";
        }
    }

    // 중앙 메시지 표시 (Ready..., FIGHT!)
    public void ShowMessage(string msg, float duration)
    {
        if (centerMessageText != null)
        {
            centerMessageText.text = msg;
            centerMessageText.gameObject.SetActive(true);
            StartCoroutine(HideMessageRoutine(duration));
        }
    }

    private IEnumerator HideMessageRoutine(float duration)
    {
        yield return new WaitForSeconds(duration);
        if (centerMessageText != null) centerMessageText.gameObject.SetActive(false);
    }

    // 라운드 결과 (승/패 이미지 띄우기 - 안전하게 코루틴 사용)
    public void ShowRoundResult(int winnerTeamId)
    {
        StartCoroutine(ShowRoundResultRoutine(winnerTeamId));
    }

    private IEnumerator ShowRoundResultRoutine(int winnerTeamId)
    {
        // 1. 내 플레이어(IsOwner) 찾기 (최대 4초 대기 - 네트워크 지연 및 스폰 딜레이 대비)
        PlayerController localPC = null;
        float timeout = 4f;
        while (timeout > 0)
        {
            // 방법 A: SpawnManager를 통해 찾기 (가장 권장됨)
            if (Unity.Netcode.NetworkManager.Singleton.SpawnManager != null) 
            {
                var localObj = Unity.Netcode.NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject();
                if (localObj != null)
                {
                    localPC = localObj.GetComponent<PlayerController>();
                }
            }
            
            // 방법 B: LocalClient 확인
            if (localPC == null && Unity.Netcode.NetworkManager.Singleton.LocalClient?.PlayerObject != null)
            {
                localPC = Unity.Netcode.NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<PlayerController>();
            }
            
            // 방법 C: 씬에 있는 모든 PlayerController 뒤져서 IsOwner 찾기 (최후의 수단)
            if (localPC == null)
            {
                foreach (var pc in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
                {
                    if (pc.IsOwner) 
                    {
                        localPC = pc;
                        break; // 찾았으면 탈출
                    }
                }
            }
            
            // 찾았으면 루프 탈출
            if (localPC != null) break;
            
            yield return null;
            timeout -= Time.deltaTime;
        }

        // 2. 내 팀 확인 및 결과 표시
        int myTeam = -1;
        if (localPC != null) 
        {
            myTeam = localPC.teamId.Value;
        }
        else
        {
             Debug.LogError("[UIManager] ShowRoundResult: Failed to find Local Player!");
        }

        // 3. 승/패 패널 활성화
        if (myTeam != -1)
        {
            bool isWin = (myTeam == winnerTeamId);
            if (isWin && winPanel != null) winPanel.SetActive(true);
            else if (!isWin && losePanel != null) losePanel.SetActive(true);
        }

        // 4. 3초 보여주고 끄기
        yield return new WaitForSeconds(3f);
        
        if (winPanel != null) winPanel.SetActive(false);
        if (losePanel != null) losePanel.SetActive(false);
        
        // 텍스트는 이제 안 씀
        if (winLoseText != null) winLoseText.gameObject.SetActive(false);
    }

    // 최종 결과 (기존 유지 - 문자열)
    public void ShowFinalResult(string winnerTeamName)
    {
        if (winLoseText != null)
        {
            winLoseText.gameObject.SetActive(true);
            winLoseText.text = $"<size=150%>{winnerTeamName} TEAM\nFINAL VICTORY!</size>";
        }
    }
}