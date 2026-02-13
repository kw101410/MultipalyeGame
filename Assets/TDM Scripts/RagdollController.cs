using UnityEngine;
using System.Collections.Generic;

public class RagdollController : MonoBehaviour
{
    private Rigidbody[] ragdollRigidbodies;
    private Animator anim;
    private CharacterController cc;
    
    private Vector3[] originalPositions;
    private Quaternion[] originalRotations;
    
    private Collider[] ragdollColliders; // 추가: 물리 간섭 방지용 콜라이더 목록

    void Awake()
    {
        // Init()은 외부(PlayerController)에서 호출해줘야 정확함
    }

    // 외부에서 활성화된 모델의 Animator를 넣어줌
    public void Init(Animator newAnim)
    {
        anim = newAnim;
        cc = GetComponent<CharacterController>();

        if (anim != null)
        {
            // ★ 중요: 활성화된 모델(Red or Blue) 하위의 Rigidbody만 찾음
            ragdollRigidbodies = anim.GetComponentsInChildren<Rigidbody>();
        }
        else
        {
            ragdollRigidbodies = GetComponentsInChildren<Rigidbody>();
        }
        
        if (ragdollRigidbodies.Length == 0)
        {
            ragdollRigidbodies = new Rigidbody[0];
            ragdollColliders = new Collider[0];
        }
        else
        {
            // 리지드바디에 붙은 콜라이더들 수집
            List<Collider> colList = new List<Collider>();
            foreach (var rb in ragdollRigidbodies)
            {
                colList.AddRange(rb.GetComponents<Collider>());
            }
            ragdollColliders = colList.ToArray();
        }
        
        originalPositions = new Vector3[ragdollRigidbodies.Length];
        originalRotations = new Quaternion[ragdollRigidbodies.Length];
        for (int i = 0; i < ragdollRigidbodies.Length; i++)
        {
            originalPositions[i] = ragdollRigidbodies[i].transform.localPosition;
            originalRotations[i] = ragdollRigidbodies[i].transform.localRotation;
        }

        DisableRagdoll();
    }

    public void DisableRagdoll()
    {
        // 1. 콜라이더 끄기 (물리 간섭 원천 차단)
        if (ragdollColliders != null)
        {
            for (int i = 0; i < ragdollColliders.Length; i++)
                ragdollColliders[i].enabled = false;
        }

        // 2. 리지드바디 끄기
        int len = ragdollRigidbodies.Length;
        for (int i = 0; i < len; i++)
        {
            var rb = ragdollRigidbodies[i];
            
            // 오류 수정: Kinematic 상태에서는 linearVelocity 설정 불가
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }
        
        for (int i = 0; i < len; i++)
        {
            ragdollRigidbodies[i].transform.localPosition = originalPositions[i];
            ragdollRigidbodies[i].transform.localRotation = originalRotations[i];
        }

        if (anim != null) anim.enabled = true;
        if (cc != null) cc.enabled = true;
    }

    // CS:GO 2 스타일 래그돌
    public void EnableRagdoll()
    {
        if (anim != null) anim.enabled = false;
        if (cc != null) cc.enabled = false;

        // 1. 콜라이더 켜기
        if (ragdollColliders != null)
        {
            for (int i = 0; i < ragdollColliders.Length; i++)
                ragdollColliders[i].enabled = true;
        }

        // 2. 리지드바디 켜기
        int len = ragdollRigidbodies.Length;
        for (int i = 0; i < len; i++)
        {
            var rb = ragdollRigidbodies[i];
            
            // 순서 중요: 비-키네마틱으로 먼저 전환해야 속도 설정 가능
            rb.isKinematic = false;
            rb.detectCollisions = true;

            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            
            // 아주 약하게: 높은 댐핑 = 천천히 부드럽게 쓰러짐
            rb.linearDamping = 3.5f;
            rb.angularDamping = 20f;
        }
    }

    // 피격 방향으로 힘 적용 (CS:GO 2처럼 밀려나는 효과)
    public void ApplyForce(Vector3 direction, float force = 300f)
    {
        if (ragdollRigidbodies.Length > 0)
        {
            // Hips(골반)에 힘을 주면 전체가 자연스럽게 밀려남
            ragdollRigidbodies[0].AddForce(direction.normalized * force, ForceMode.Impulse);
        }
    }
}