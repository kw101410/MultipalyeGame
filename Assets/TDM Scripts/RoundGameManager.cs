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
    }

    // ���� ���� (�ణ�� ��� �� ����)
    private IEnumerator StartGameRoutine()
    {
        yield return new WaitForSeconds(2f); // ���� ���
        StartRound();
    }

    private void StartRound()
    {
        isRoundPlaying = true; int redCount = 0; int blueCount = 0;

        // ���ٽ�: ��� �÷��̾� ��Ȱ �� ��ġ �ʱ�ȭ
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            var player = client.PlayerObject.GetComponent<PlayerController>();
            if (player != null)
            {
                int spawnIndex = (player.teamId.Value == 0) ? redCount++ : blueCount++; player.Respawn(spawnIndex); // �ϰ� § �� �Լ� ȣ��
            }
        }

        NotifyRoundStartClientRpc();
    }

    [ClientRpc]
    private void NotifyRoundStartClientRpc()
    {
        Debug.Log(">>> ���� ����! <<<");
        // ���⿡ UI "Round Start" ���� �ڵ� ������ ��
    }

    // �� �ϰ� PlayerController���� ȣ���ϴ� �� �Լ�
    public void OnPlayerDied(int deadTeamId)
    {
        if (!IsServer || !isRoundPlaying) return;

        // ���� �� ���� Ȯ��������, �� ���� '����'�ߴ��� üũ
        if (CheckTeamWipedOut(deadTeamId))
        {
            // deadTeamId�� 0(Red)�̸� Blue(1) �¸�
            int winnerTeam = (deadTeamId == 0) ? 1 : 0;
            EndRound(winnerTeam);
        }
    }

    // �ش� ���� �� �׾����� Ȯ�� (��Ʈüũ: ��ȿ�����̾ Ȯ���� ���)
    private bool CheckTeamWipedOut(int teamId)
    {
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            var player = client.PlayerObject.GetComponent<PlayerController>();

            // ���� ���ε� ü���� 0���� ū ���� �� ���̶� ������ ���� �ƴ�
            if (player != null && player.teamId.Value == teamId && player.hp.Value > 0)
            {
                return false; // ������ ����
            }
        }
        return true; // ������ ���� -> ����
    }

    private void EndRound(int winnerTeam)
    {
        isRoundPlaying = false;

        // ���� �ø���
        if (winnerTeam == 0) RedRoundScore.Value += 1;
        else BlueRoundScore.Value += 1;

        Debug.Log($"���� ����! �¸�: {(winnerTeam == 0 ? "RED" : "BLUE")}");

        // ���� ��� üũ
        if (RedRoundScore.Value >= TargetRoundWin || BlueRoundScore.Value >= TargetRoundWin)
        {
            EndMatch(winnerTeam);
        }
        else
        {
            // ���� ���� �غ�
            StartCoroutine(NextRoundRoutine(winnerTeam));
        }
    }

    private IEnumerator NextRoundRoutine(int winnerTeam)
    {
        // ��� �����ֱ� (RPC�� UI �����)
        ShowRoundResultClientRpc(winnerTeam);

        yield return new WaitForSeconds(3f); // 3�� ���

        StartRound(); // ���� ���� ���� (���⼭ �ٽ� Respawn ȣ���)
    }

    [ClientRpc]
    private void ShowRoundResultClientRpc(int winnerTeam)
    {
        string winner = (winnerTeam == 0) ? "RED" : "BLUE";
        Debug.Log($"{winner} �� ���� �¸�!");
        // UIManager.Instance.ShowRoundResult(winner);
    }

    private void EndMatch(int finalWinner)
    {
        Debug.Log("��ġ ����! �κ� ���� ������ ����");

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
