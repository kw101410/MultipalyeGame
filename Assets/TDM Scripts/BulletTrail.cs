using UnityEngine;

public class BulletTrail : MonoBehaviour
{
    private LineRenderer line;
    [Tooltip("궤적이 사라지는 속도 (값이 클수록 빨리 사라짐)")]
    public float fadeSpeed = 10f; 

    void Start()
    {
        line = GetComponent<LineRenderer>();
        // 시작하자마자 월드 좌표 사용 강제 (안전장치)
        if (line != null) line.useWorldSpace = true;
    }

    void Update()
    {
        if (line == null) return;

        // 서서히 투명해지기 (Fade Out)
        Color startColor = line.startColor;
        Color endColor = line.endColor;

        startColor.a -= Time.deltaTime * fadeSpeed;
        endColor.a -= Time.deltaTime * fadeSpeed;

        line.startColor = startColor;
        line.endColor = endColor;

        // 완전히 투명해지면 삭제
        if (startColor.a <= 0f)
        {
            Destroy(gameObject);
        }
    }
}
