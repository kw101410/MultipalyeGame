using UnityEngine;

/// <summary>
/// 캐릭터 모델에 붙여서 Root Motion을 무시하고 In Place 애니메이션으로 만듭니다.
/// Animator가 있는 오브젝트에 이 스크립트를 추가하세요.
/// </summary>
public class DisableRootMotion : MonoBehaviour
{
    private Animator anim;

    void Awake()
    {
        // 모델이 자식 오브젝트에 있을 수 있으므로 Children도 검색
        anim = GetComponentInChildren<Animator>();
        if (anim != null)
        {
            anim.applyRootMotion = false;
            // Debug.Log($"[{gameObject.name}] DisableRootMotion: Animator found on {anim.gameObject.name}. applyRootMotion set to false.");
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name}] DisableRootMotion: Animator NOT found in children!");
        }
    }

    private void OnAnimatorMove()
    {
        // 의도적으로 비워둠 - Root Motion 무시
    }

    // 확실하게 매 프레임 강제 적용 (Rebind 등으로 풀리는 경우 방지)
    void LateUpdate()
    {
        if (anim != null && anim.applyRootMotion)
        {
            anim.applyRootMotion = false;
        }
    }
}
