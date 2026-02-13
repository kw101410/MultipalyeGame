using UnityEngine;
using Unity.Netcode;

/// <summary>
/// 게임 성능 자동 최적화 매니저
/// - 씬에 빈 오브젝트 만들고 이 스크립트 붙이면 됨
/// - 프레임 상태에 따라 자동으로 퀄리티 조절
/// </summary>
public class PerformanceManager : MonoBehaviour
{
    public static PerformanceManager Instance;

    [Header("목표 FPS")]
    public int targetFPS = 60;

    [Header("자동 퀄리티 조절")]
    public bool autoAdjustQuality = true;

    [Header("현재 FPS (디버그용)")]
    [SerializeField] private float currentFPS;
    [SerializeField] private int currentQualityLevel;

    // FPS 측정용
    private float fpsUpdateInterval = 1f;
    private float fpsAccumulator = 0f;
    private int fpsFrameCount = 0;
    private float fpsNextUpdate = 0f;

    // 퀄리티 자동 조절용
    private float qualityCheckInterval = 5f; // 5초마다 체크
    private float nextQualityCheck = 10f;    // 시작 10초 후부터 체크
    private int lowFPSCount = 0;
    private int highFPSCount = 0;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // 기본 성능 설정 적용
        ApplyBaseSettings();
    }

    void ApplyBaseSettings()
    {
        // 1. 타겟 FPS 설정
        Application.targetFrameRate = targetFPS;

        // 2. VSync 끄기 (targetFrameRate가 작동하려면 꺼야 함)
        QualitySettings.vSyncCount = 0;

        // 3. 그림자 최적화
        QualitySettings.shadowResolution = ShadowResolution.Medium;
        QualitySettings.shadowDistance = 60f;
        QualitySettings.shadowCascades = 2;

        // 4. 기타 렌더링 최적화
        QualitySettings.softParticles = false;
        QualitySettings.realtimeReflectionProbes = false;
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;

        // 5. LOD Bias (1 = 기본, 낮을수록 더 빨리 저퀄로 전환)
        QualitySettings.lodBias = 0.7f;

        // 6. 최대 LOD 레벨
        QualitySettings.maximumLODLevel = 0;

        // 7. 스킨 메시 본 가중치 (4가 기본, 2로 줄이면 빨라짐)
        QualitySettings.skinWeights = SkinWeights.TwoBones;

        currentQualityLevel = QualitySettings.GetQualityLevel();

#if UNITY_EDITOR
        Debug.Log($"[PerformanceManager] 기본 성능 설정 적용 완료 | 타겟 FPS: {targetFPS} | 퀄리티: {currentQualityLevel}");
#endif
    }

    void Update()
    {
        // FPS 측정
        fpsAccumulator += Time.unscaledDeltaTime;
        fpsFrameCount++;

        if (Time.unscaledTime >= fpsNextUpdate)
        {
            currentFPS = fpsFrameCount / fpsAccumulator;
            fpsAccumulator = 0f;
            fpsFrameCount = 0;
            fpsNextUpdate = Time.unscaledTime + fpsUpdateInterval;
        }

        // 자동 퀄리티 조절
        if (autoAdjustQuality && Time.unscaledTime >= nextQualityCheck)
        {
            nextQualityCheck = Time.unscaledTime + qualityCheckInterval;
            AutoAdjustQuality();
        }
    }

    void AutoAdjustQuality()
    {
        int currentLevel = QualitySettings.GetQualityLevel();
        int maxLevel = QualitySettings.names.Length - 1;

        // FPS가 목표의 70% 이하면 퀄리티 낮추기
        if (currentFPS < targetFPS * 0.7f)
        {
            lowFPSCount++;
            highFPSCount = 0;

            // 3번 연속 낮으면 퀄리티 다운
            if (lowFPSCount >= 3 && currentLevel > 0)
            {
                QualitySettings.SetQualityLevel(currentLevel - 1, true);
                currentQualityLevel = currentLevel - 1;
                lowFPSCount = 0;
#if UNITY_EDITOR
                Debug.Log($"[PerformanceManager] 퀄리티 다운: {currentLevel} → {currentLevel - 1} (FPS: {currentFPS:F1})");
#endif
            }
        }
        // FPS가 목표의 90% 이상이면 퀄리티 올리기
        else if (currentFPS > targetFPS * 0.9f)
        {
            highFPSCount++;
            lowFPSCount = 0;

            // 5번 연속 높으면 퀄리티 업 (올릴 때는 더 신중하게)
            if (highFPSCount >= 5 && currentLevel < maxLevel)
            {
                QualitySettings.SetQualityLevel(currentLevel + 1, true);
                currentQualityLevel = currentLevel + 1;
                highFPSCount = 0;

                // 퀄리티 올린 후 기본 최적화 설정은 다시 적용
                QualitySettings.shadowResolution = ShadowResolution.Medium;
                QualitySettings.shadowDistance = 60f;
                QualitySettings.softParticles = false;
#if UNITY_EDITOR
                Debug.Log($"[PerformanceManager] 퀄리티 업: {currentLevel} → {currentLevel + 1} (FPS: {currentFPS:F1})");
#endif
            }
        }
        else
        {
            // 안정적이면 카운터 리셋
            lowFPSCount = 0;
            highFPSCount = 0;
        }
    }

    /// <summary>
    /// 현재 FPS 반환 (UI 표시용)
    /// </summary>
    public float GetCurrentFPS()
    {
        return currentFPS;
    }

    /// <summary>
    /// 수동으로 퀄리티 레벨 설정
    /// </summary>
    public void SetQualityLevel(int level)
    {
        autoAdjustQuality = false; // 수동 설정 시 자동 조절 끄기
        QualitySettings.SetQualityLevel(level, true);
        currentQualityLevel = level;
    }
}
