using UnityEngine;
using TMPro; // TMP 필수
using System.Collections; // 코루틴용
using System.Collections.Generic; // List 사용 필수
using UnityEngine.UI; // 이미지 사용

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [Header("UI 오브젝트 연결 (Inspector 확인)")]
    public TextMeshProUGUI hpText;
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI winLoseText; // (사용 안 함, 혹시 몰라 남김)

    [Header("Round Result Panels")]
    public GameObject winPanel;  // 승리 이미지 패널
    public GameObject losePanel; // 패배 이미지 패널
    public TextMeshProUGUI centerMessageText; // 중앙 메시지 (Ready, Fight 등)

    [Header("Final Game Result Panels")]
    public GameObject finalRedWinPanel;  // 최종 레드팀 승리 이미지
    public GameObject finalBlueWinPanel; // 최종 블루팀 승리 이미지

    [Header("Countdown UI")]
    public Image countdownImageDisplay; // 카운트다운 이미지 표시용
    public List<Sprite> numberSprites; // 숫자 이미지 리스트 (Index 0 = '1', Index 1 = '2' ... 순서로 넣으세요)

    [Header("Sound Effects")]
    public AudioSource audioSource; // 효과음용 (OneShot)
    public AudioClip redWinSound;
    public AudioClip blueWinSound;
    public AudioClip countdownSound; // 카운트다운 효과음 (배경음 X)

    [Header("Background Music")]
    public AudioSource bgmSource; // 배경음악용 (Loop) - 별도의 AudioSource 필요
    public AudioClip gameBgm;     // 게임 배경음악

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }
    
    private void Start()
    {
        // 시작 시 모든 결과창 끄기
        if (countdownImageDisplay != null) countdownImageDisplay.gameObject.SetActive(false);
        if (finalRedWinPanel != null) finalRedWinPanel.SetActive(false);
        if (finalBlueWinPanel != null) finalBlueWinPanel.SetActive(false);
        if (winPanel != null) winPanel.SetActive(false);
        if (losePanel != null) losePanel.SetActive(false);
        if (winLoseText != null) winLoseText.gameObject.SetActive(false);

        // 배경음악 재생
        if (bgmSource != null && gameBgm != null)
        {
            bgmSource.clip = gameBgm;
            bgmSource.loop = true;
            bgmSource.Play();
        }

        // 라운드 매니저 찾아서 점수판 연결
        StartCoroutine(ConnectToGameManager());
    }

    // HP 갱신 (팀 컬러 적용)
    // PlayerController에서 호출할 때 teamId도 같이 넘겨줘야 함
    public void UpdateHP(int hp, int teamId = -1)
    {
        if (hpText == null)
        {
            Debug.LogError("[UIManager] hpText is NULL! Check Inspector.");
            return;
        }

        Debug.Log($"[UIManager] UpdateHP Called: {hp} (Team: {teamId})");
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
    // 아래는 점수판 자동 갱신 로직
    // ---------------------------------------------------------

    private IEnumerator ConnectToGameManager()
    {
        while (RoundGameManager.Instance == null)
        {
            yield return null;
        }
        
        // 초기 점수 한 번 갱신
        if (RoundGameManager.Instance.RedRoundScore != null && RoundGameManager.Instance.BlueRoundScore != null)
        {
            UpdateRoundScore(RoundGameManager.Instance.RedRoundScore.Value, RoundGameManager.Instance.BlueRoundScore.Value);
        }
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

    // 카운트다운 업데이트 (0 이하면 숨김)
    public void UpdateCountdown(int count)
    {
        if (countdownImageDisplay == null) return;

        if (count <= 0)
        {
            countdownImageDisplay.gameObject.SetActive(false);
            return;
        }
        
        // 카운트다운 소리 재생 (1초에 한 번)
        if (audioSource != null && countdownSound != null)
        {
            audioSource.PlayOneShot(countdownSound);
        }

        // 인덱스 (1 -> 0, 5 -> 4)
        int index = count - 1; 
        if (numberSprites != null && index >= 0 && index < numberSprites.Count)
        {
            countdownImageDisplay.sprite = numberSprites[index];
            countdownImageDisplay.gameObject.SetActive(true);
        }
        else
        {
            // 이미지가 없으면 끄기
            countdownImageDisplay.gameObject.SetActive(false);
        }
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

        // 4. 승리 팀 소리 재생
        if (audioSource != null)
        {
            AudioClip clipToPlay = (winnerTeamId == 0) ? redWinSound : blueWinSound;
            if (clipToPlay != null) audioSource.PlayOneShot(clipToPlay);
        }

        // 5. 3초 보여주고 끄기
        yield return new WaitForSeconds(3f);
        
        if (winPanel != null) winPanel.SetActive(false);
        if (losePanel != null) losePanel.SetActive(false);
        
        // 텍스트는 이제 안 씀
        if (winLoseText != null) winLoseText.gameObject.SetActive(false);
    }

    // 최종 결과 (텍스트 제거 -> 이미지 사용)
    public void ShowFinalResult(string winnerTeamName)
    {
        // 기존 텍스트(winLoseText)는 사용 안 함
        // winnerTeamName은 "RED" 혹은 "BLUE"로 들어옴 (RoundGameManager 참고)

        if (winnerTeamName == "RED")
        {
            if (finalRedWinPanel != null) finalRedWinPanel.SetActive(true);
            if (finalBlueWinPanel != null) finalBlueWinPanel.SetActive(false);
        }
        else if (winnerTeamName == "BLUE")
        {
            if (finalRedWinPanel != null) finalRedWinPanel.SetActive(false);
            if (finalBlueWinPanel != null) finalBlueWinPanel.SetActive(true);
        }
        else // 혹시 모르니까 무승부 등
        {
            // 그냥 텍스트라도 보여주기 (옵션)
            if (winLoseText != null)
            {
                winLoseText.gameObject.SetActive(true);
                winLoseText.text = "DRAW GAME!";
            }
        }
    }
}