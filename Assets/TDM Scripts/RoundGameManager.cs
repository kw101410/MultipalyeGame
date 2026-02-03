using Unity.Netcode;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class RoundGameManager : NetworkBehaviour
{
    public static RoundGameManager Instance; // 싱글톤 (니가 갖다 쓰기 편하게)

    [Header("스폰 포인트 (필수 연결)")]
    public Transform spawnPointA; // 레드팀 (Team 0)
    public Transform spawnPointB; // 블루팀 (Team 1)

    [Header("라운드 설정")]
    public int TargetRoundWin = 3; // 3판 선승제

    [Header("점수 (동기화)")]
    public NetworkVariable<int> RedRoundScore = new NetworkVariable<int>(0);
    public NetworkVariable<int> BlueRoundScore = new NetworkVariable<int>(0);

    // 라운드 진행 중인지 체크
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
    }

    // 게임 시작 (약간의 대기 후 시작)
    private IEnumerator StartGameRoutine()
    {
        yield return new WaitForSeconds(2f); // 접속 대기
        StartRound();
    }

    private void StartRound()
    {
        isRoundPlaying = true;

        // ★핵심: 모든 플레이어 부활 및 위치 초기화
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            var player = client.PlayerObject.GetComponent<PlayerController>();
            if (player != null)
            {
                player.Respawn(); // 니가 짠 그 함수 호출
            }
        }

        NotifyRoundStartClientRpc();
    }

    [ClientRpc]
    private void NotifyRoundStartClientRpc()
    {
        Debug.Log(">>> 라운드 시작! <<<");
        // 여기에 UI "Round Start" 띄우는 코드 넣으면 됨
    }

    // ★ 니가 PlayerController에서 호출하는 그 함수
    public void OnPlayerDied(int deadTeamId)
    {
        if (!IsServer || !isRoundPlaying) return;

        // 죽은 놈 팀을 확인했으니, 그 팀이 '전멸'했는지 체크
        if (CheckTeamWipedOut(deadTeamId))
        {
            // deadTeamId가 0(Red)이면 Blue(1) 승리
            int winnerTeam = (deadTeamId == 0) ? 1 : 0;
            EndRound(winnerTeam);
        }
    }

    // 해당 팀이 다 죽었는지 확인 (팩트체크: 비효율적이어도 확실한 방법)
    private bool CheckTeamWipedOut(int teamId)
    {
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            var player = client.PlayerObject.GetComponent<PlayerController>();

            // 같은 팀인데 체력이 0보다 큰 놈이 한 명이라도 있으면 전멸 아님
            if (player != null && player.teamId.Value == teamId && player.hp.Value > 0)
            {
                return false; // 생존자 있음
            }
        }
        return true; // 생존자 없음 -> 전멸
    }

    private void EndRound(int winnerTeam)
    {
        isRoundPlaying = false;

        // 점수 올리기
        if (winnerTeam == 0) RedRoundScore.Value += 1;
        else BlueRoundScore.Value += 1;

        Debug.Log($"라운드 종료! 승리: {(winnerTeam == 0 ? "RED" : "BLUE")}");

        // 최종 우승 체크
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
        // 결과 보여주기 (RPC로 UI 띄워라)
        ShowRoundResultClientRpc(winnerTeam);

        yield return new WaitForSeconds(3f); // 3초 대기

        StartRound(); // 다음 라운드 시작 (여기서 다시 Respawn 호출됨)
    }

    [ClientRpc]
    private void ShowRoundResultClientRpc(int winnerTeam)
    {
        string winner = (winnerTeam == 0) ? "RED" : "BLUE";
        Debug.Log($"{winner} 팀 라운드 승리!");
        // UIManager.Instance.ShowRoundResult(winner);
    }

    private void EndMatch(int finalWinner)
    {
        Debug.Log("매치 종료! 로비 복귀 시퀀스 가동");

        // 1. 모든 클라이언트에게 최종 결과 보여주기
        ShowFinalResultClientRpc(finalWinner);

        // 2. 5초 뒤에 로비로 이동 (코루틴 시작)
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

        // 축포 같은 거 터뜨리고 싶으면 여기서 함수 호출
        // EffectManager.Instance.PlayConfetti(); 
    }

    private IEnumerator ReturnToLobbyRoutine()
    {
        // 5초 대기 (결과 감상 타임)
        yield return new WaitForSeconds(5f);

        // ★ 핵심: 네트워크 씬 전환
        // 그냥 SceneManager.LoadScene 쓰면 너 혼자 이동하고 클라이언트들은 미아 됨.
        // 이걸 써야 서버가 "야 다 따라와!" 하고 줄줄이 데리고 감.

        // "LobbyScene"은 니가 만든 로비 씬 이름으로 바꿔라. (오타 나면 에러 남)
        NetworkManager.Singleton.SceneManager.LoadScene("LobbyScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
    }
}