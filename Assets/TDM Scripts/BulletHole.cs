using UnityEngine;

public class BulletHole : MonoBehaviour
{
    public float destroyTime = 5f; // 5초 뒤에 사라짐
    public float fadeSpeed = 0.5f;
    private Renderer rend;
    private bool shouldFade = false;

    void Start()
    {
        rend = GetComponent<Renderer>();
        // 일정 시간 뒤에 사라지게 (또는 페이드아웃 시작)
        Destroy(gameObject, destroyTime);
        
        // Destroy 직전 2초부터 서서히 사라지게 하기 위해 Invoke 사용
        Invoke("StartFade", destroyTime - 2f);
    }

    void StartFade()
    {
        shouldFade = true;
    }

    void Update()
    {
        if (shouldFade && rend != null)
        {
            Color color = rend.material.color;
            if (color.a > 0)
            {
                color.a -= Time.deltaTime * fadeSpeed;
                rend.material.color = color;
            }
        }
    }
}
