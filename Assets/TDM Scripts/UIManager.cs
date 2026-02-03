using UnityEngine;
using TMPro; // TMP 필수
using System.Collections; // 코루틴용

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [Header("UI 오브젝트 연결 (Inspector 확인)")]
    public TextMeshProUGUI hpText;
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI winLoseText; // 승리 메시지용 (없으면 비워둬도 됨)

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
        // 매니저 뜰 때까지 대기
        while (RoundGameManager.Instance == null)
        {
            yield return null;
        }

        // 점수 변경 이벤트 구독
        RoundGameManager.Instance.RedRoundScore.OnValueChanged += (oldVal, newVal) => UpdateScoreFromManager();
        RoundGameManager.Instance.BlueRoundScore.OnValueChanged += (oldVal, newVal) => UpdateScoreFromManager();

        // 초기 점수 갱신
        UpdateScoreFromManager();
    }

    private void UpdateScoreFromManager()
    {
        if (RoundGameManager.Instance == null || scoreText == null) return;

        int red = RoundGameManager.Instance.RedRoundScore.Value;
        int blue = RoundGameManager.Instance.BlueRoundScore.Value;

        // TMP는 <color> 태그 잘 먹음. 개꿀.
        scoreText.text = $"<color=red>RED {red}</color>  :  <color=blue>{blue} BLUE</color>";
    }

    // 라운드 결과 메시지 (RoundGameManager가 부름)
    public void ShowRoundResult(string winnerTeamName)
    {
        if (winLoseText != null)
        {
            winLoseText.gameObject.SetActive(true);
            winLoseText.text = $"{winnerTeamName} TEAM WIN!";
            StartCoroutine(HideResultText());
        }
    }

    private IEnumerator HideResultText()
    {
        yield return new WaitForSeconds(3f);
        if (winLoseText != null) winLoseText.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        // 구독 취소 (필수)
        if (RoundGameManager.Instance != null)
        {
            RoundGameManager.Instance.RedRoundScore.OnValueChanged -= (old, @new) => UpdateScoreFromManager();
            RoundGameManager.Instance.BlueRoundScore.OnValueChanged -= (old, @new) => UpdateScoreFromManager();
        }
    }
    public void ShowFinalResult(string winnerTeamName)
    {
        if (winLoseText != null)
        {
            winLoseText.gameObject.SetActive(true);
            // 텍스트 크기 좀 키우거나 색깔 바꾸면 좋음 (여기선 텍스트만 변경)
            winLoseText.text = $"<size=150%>{winnerTeamName} TEAM\nFINAL VICTORY!</size>";

            // 이건 자동으로 안 꺼짐 (씬 이동할 때까지 유지)
        }
    }
}