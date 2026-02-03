using UnityEngine;
using System.Collections.Generic;

public class RagdollController : MonoBehaviour
{
    // 랙돌용 리지드바디들
    private Rigidbody[] ragdollRigidbodies;
    private Animator anim;
    private CharacterController cc; // 혹은 CapsuleCollider

    void Awake()
    {
        anim = GetComponentInChildren<Animator>();
        cc = GetComponent<CharacterController>();

        // 자식들에 있는 모든 리지드바디 찾아오기 (랙돌 뼈들)
        ragdollRigidbodies = GetComponentsInChildren<Rigidbody>();

        // 시작할 땐 랙돌 꺼두기
        DisableRagdoll();
    }

    // 평소 상태 (애니메이션으로 움직임)
    public void DisableRagdoll()
    {
        foreach (var rb in ragdollRigidbodies)
        {
            rb.isKinematic = true; // 물리 끄기 (애니메이션에 따름)
            rb.detectCollisions = false; // 충돌도 끄기 (최적화)
        }

        if (anim != null) anim.enabled = true;
        if (cc != null) cc.enabled = true;
    }

    // 죽었을 때 (물리 엔진 켜기)
    public void EnableRagdoll()
    {
        if (anim != null) anim.enabled = false; // 애니메이터 끄기 (필수)
        if (cc != null) cc.enabled = false;     // 캡슐 콜라이더 끄기 (필수)

        foreach (var rb in ragdollRigidbodies)
        {
            rb.isKinematic = false; // 물리 켜기 (중력 받음)
            rb.detectCollisions = true; // 바닥이랑 부딪혀야 함

            // 약간의 힘을 줘서 픽 쓰러지는 느낌 강화 (선택)
            // rb.AddForce(Vector3.up * 2f, ForceMode.Impulse); 
        }
    }
}