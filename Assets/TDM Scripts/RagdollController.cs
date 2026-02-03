using UnityEngine;
using System.Collections.Generic;

public class RagdollController : MonoBehaviour
{
    private Rigidbody[] ragdollRigidbodies;
    private Animator anim;
    private CharacterController cc;
    
    private Vector3[] originalPositions;
    private Quaternion[] originalRotations;

    void Awake()
    {
        Init();
    }

    public void Init()
    {
        anim = GetComponentInChildren<Animator>();
        cc = GetComponent<CharacterController>();

        ragdollRigidbodies = GetComponentsInChildren<Rigidbody>();
        
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
        foreach (var rb in ragdollRigidbodies)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }
        
        for (int i = 0; i < ragdollRigidbodies.Length; i++)
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

        foreach (var rb in ragdollRigidbodies)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            
            // 아주 약하게: 높은 댐핑 = 천천히 부드럽게 쓰러짐
            rb.linearDamping = 3.5f;
            rb.angularDamping =20f;
            
            rb.isKinematic = false;
            rb.detectCollisions = true;
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