using UnityEngine;

public class RootMotionHandler : MonoBehaviour
{
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private bool initialized = false;

    void Awake()
    {
        // 컴포넌트 추가 시 현재 위치(T-Pose/Idle 상태)를 기준점으로 잡음
        initialPosition = transform.localPosition;
        initialRotation = transform.localRotation;
        initialized = true;
    }

    void OnEnable()
    {
        // 껐다 켜질 때 위치가 틀어져 있을 수 있으므로 복구
        if (initialized)
        {
            transform.localPosition = initialPosition;
            transform.localRotation = initialRotation;
        }
    }

    // Animator의 Root Motion 처리를 가로챕니다.
    // 이 함수가 존재하면 "Apply Root Motion"이 "Handled by Script"로 바뀝니다.
    void OnAnimatorMove()
    {
        // 애니메이션의 이동/회전 데이터(Root Motion)를 무시합니다.
        // 땅으로 꺼지는 Y축 이동 데이터도 여기서 차단됩니다.
        
        // 확실하게 하기 위해 로컬 위치를 초기값으로 고정 (공중 부양/땅꺼짐 방지)
        if (initialized)
        {
            transform.localPosition = initialPosition;
            transform.localRotation = initialRotation;
        }
    }
}
