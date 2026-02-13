using Unity.Netcode;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class RoundGameManager : NetworkBehaviour
{
    public static RoundGameManager Instance; // �̱��� (�ϰ� ���� ���� ���ϰ�)

    [Header("���� ����Ʈ (�ʼ� ����)")]
    public Transform spawnPointA; // ������ (Team 0)
    public Transform spawnPointB; // ����� (Team 1)

    [Header("���� ����")]
    public int TargetRoundWin = 3; // 3�� ������

    [Header("���� (����ȭ)")]
    public NetworkVariable<int> RedRoundScore = new NetworkVariable<int>(0);
    public NetworkVariable<int> BlueRoundScore = new NetworkVariable<int>(0);

    // ���� ���� ������ üũ
    private bool isRoundPlaying = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            StartCoroutine(StartGameRoutine());
        }

        // 점수 변경 시 UI 업데이트 (클라이언트에서 반응)
        RedRoundScore.OnValueChanged += (prev, curr) => UpdateScoreUI();
        BlueRoundScore.OnValueChanged += (prev, curr) => UpdateScoreUI();
        
        // 초기 점수 표시
        UpdateScoreUI();
    }
    
    private void UpdateScoreUI()
    {
        if (UIManager.Instance != null)
        {
            // UIManager에 UpdateRoundScore(red, blue)가 있다고 가정
            // 없으면 에러 날 수 있으니 try-catch 혹은 안전장치 필요
            // 하지만 사용자 요청대로 연결함
            UIManager.Instance.UpdateRoundScore(RedRoundScore.Value, BlueRoundScore.Value);
        }
    }

    // 게임 시작 (약간의 대기 후 시작)
    private IEnumerator StartGameRoutine()
    {
        yield return new WaitForSeconds(2f); // 접속 대기
        StartRound();
    }

    private void StartRound()
    {
        int redCount = 0; 
        int blueCount = 0;

        // 모든 플레이어 부활 및 위치 초기화
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            // PlayerObject가 null이거나 스폰되지 않았으면 스킵
            if (client.PlayerObject == null || !client.PlayerObject.IsSpawned) continue;
            
            var player = client.PlayerObject.GetComponent<PlayerController>();
            if (player != null && player.IsSpawned)
            {
                int spawnIndex = (player.teamId.Value == 0) ? redCount++ : blueCount++;
                player.Respawn(spawnIndex);
            }
        }
        
        // 1. 플레이어 얼리기 (입력 차단)
        FreezeAllPlayersClientRpc(true);
        
        // 2. 5초 카운트다운 시작
        StartCoroutine(RoundStartCountdown());
    }
    
    private IEnumerator RoundStartCountdown()
    {
        // 5초 대기 (준비 시간)
        yield return new WaitForSeconds(5f);
        
        // 3. 플레이어 해제 (입력 허용)
        FreezeAllPlayersClientRpc(false);
        
        NotifyRoundStartClientRpc();
        isRoundPlaying = true; 
    }
    
    [ClientRpc]
    private void FreezeAllPlayersClientRpc(bool freeze)
    {
        // 로컬 플레이어 찾아서 입력 제어
        if (NetworkManager.Singleton.LocalClient?.PlayerObject != null)
        {
            var player = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<PlayerController>();
            if (player != null)
            {
                player.SetInputActive(!freeze);
            }
        }
        
        // UI 메시지 ("Ready..." vs "Fight!")
        if (UIManager.Instance != null)
        {
            if (freeze) UIManager.Instance.ShowMessage("Ready...", 5f); // 5초간 레디 메시지
            else UIManager.Instance.ShowMessage("FIGHT!", 2f); // 시작 메시지
        }
    }

    [ClientRpc]
    private void NotifyRoundStartClientRpc()
    {
        Debug.Log(">>> 라운드 시작! <<<");
        // 여기에 UI "Round Start" 표시 코드 추가 가능
    }

    // 킬 발생 시 PlayerController에서 호출하는 콜백 함수
    public void OnPlayerDied(int deadTeamId)
    {
        Debug.Log($"[OnPlayerDied] Team {deadTeamId} died. IsServer={IsServer}, Playing={isRoundPlaying}");

        if (!IsServer || !isRoundPlaying) return;

        // 해당 팀 전멸 확인
        if (CheckTeamWipedOut(deadTeamId))
        {
            Debug.Log($"[OnPlayerDied] Team {deadTeamId} WIKPED OUT!");
            // deadTeamId가 0(Red)이면 Blue(1) 승리
            int winnerTeam = (deadTeamId == 0) ? 1 : 0;
            EndRound(winnerTeam);
        }
    }
    
    // 팀 전멸 확인 (체크: 살아있는지 확인)
    private bool CheckTeamWipedOut(int teamId)
    {
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            // 최적화: null 안전성 강화
            if (client.PlayerObject == null || !client.PlayerObject.IsSpawned) continue;
            
            var player = client.PlayerObject.GetComponent<PlayerController>();
            
            if (player != null && player.teamId.Value == teamId && player.hp.Value > 0)
            {
                // 아직 살아있는 팀원이 있음
                return false;
            }
        }
        return true;
    }
    
    private void EndRound(int winnerTeam)
    {
        Debug.Log($"[EndRound] Round Ended. Winner: {winnerTeam}");
        isRoundPlaying = false;

        // 점수 올리기
        if (winnerTeam == 0) RedRoundScore.Value += 1;
        else BlueRoundScore.Value += 1;

        Debug.Log($"[EndRound] Scores -> Red: {RedRoundScore.Value}, Blue: {BlueRoundScore.Value}");

        // 최종 승리 체크
        if (RedRoundScore.Value >= TargetRoundWin || BlueRoundScore.Value >= TargetRoundWin)
        {
            EndMatch(winnerTeam);
        }
        else
        {
            // 다음 라운드 준비
            StartCoroutine(NextRoundRoutine(winnerTeam));
        }
    }

    private IEnumerator NextRoundRoutine(int winnerTeam)
    {
        Debug.Log("[NextRoundRoutine] Showing result...");
        
        // 결과 보여주기 (RPC로 UI 띄우기)
        ShowRoundResultClientRpc(winnerTeam);

        yield return new WaitForSeconds(3f); // 3초 대기 (결과 화면 감상)

        Debug.Log("[NextRoundRoutine] 3s passed. Restarting Round...");
        StartRound(); // 다음 라운드 시작 (스폰 -> 5초 대기 -> 시작)
    }

    [ClientRpc]
    private void ShowRoundResultClientRpc(int winnerTeam)
    {
        // UIManager에 승리 팀 ID(int)를 직접 전달하여 승/패 이미지 표시
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowRoundResult(winnerTeam);
        }
    }

    private void EndMatch(int finalWinner)
    {
        // 1. ��� Ŭ���̾�Ʈ���� ���� ��� �����ֱ�
        ShowFinalResultClientRpc(finalWinner);

        // 2. 5�� �ڿ� �κ�� �̵� (�ڷ�ƾ ����)
        StartCoroutine(ReturnToLobbyRoutine());
    }

    [ClientRpc]
    private void ShowFinalResultClientRpc(int winnerTeam)
    {
        string winner = (winnerTeam == 0) ? "RED" : "BLUE";

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowFinalResult(winner);
        }

        // ���� ���� �� �Ͷ߸��� ������ ���⼭ �Լ� ȣ��
        // EffectManager.Instance.PlayConfetti(); 
    }

    private IEnumerator ReturnToLobbyRoutine()
    {
        // 5�� ��� (��� ���� Ÿ��)
        yield return new WaitForSeconds(5f);

        // �� �ٽ�: ��Ʈ��ũ �� ��ȯ
        // �׳� SceneManager.LoadScene ���� �� ȥ�� �̵��ϰ� Ŭ���̾�Ʈ���� �̾� ��.
        // �̰� ��� ������ "�� �� �����!" �ϰ� ������ ������ ��.

        // "LobbyScene"�� �ϰ� ���� �κ� �� �̸����� �ٲ��. (��Ÿ ���� ���� ��)
        NetworkManager.Singleton.SceneManager.LoadScene("LobbyScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
    }
}
