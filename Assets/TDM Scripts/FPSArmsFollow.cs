using UnityEngine;

/// <summary>
/// FPS Arms가 카메라 시점을 따라가게 하는 스크립트
/// - 카메라가 위/아래를 볼 때 팔도 같이 움직임
/// </summary>
public class FPSArmsFollow : MonoBehaviour
{
    [Header("카메라 참조")]
    public Transform cameraTransform;
    
    [Header("위치 오프셋")]
    public Vector3 positionOffset = new Vector3(0, -0.3f, 0.2f);
    
    [Header("회전 설정")]
    [Range(0f, 1f)]
    public float rotationFollowAmount = 1.0f;  // 1 = 카메라와 완전히 일치
    
    [Header("부드러운 움직임")]
    public float smoothSpeed = 10f;
    
    private Vector3 targetPosition;
    private Quaternion targetRotation;
    
    void LateUpdate()
    {
        if (cameraTransform == null) return;
        
        // 카메라 위치 기준으로 오프셋 적용
        targetPosition = cameraTransform.position + cameraTransform.TransformDirection(positionOffset);
        
        // 카메라 회전 따라가기 (수직 회전만 일부 적용)
        float cameraXRotation = cameraTransform.localEulerAngles.x;
        
        // 각도 보정 (180도 이상이면 음수로 변환)
        if (cameraXRotation > 180f) cameraXRotation -= 360f;
        
        // 팔의 회전 계산 (카메라 Y축 회전은 완전히, X축 회전은 부분적으로)
        float armsXRotation = cameraXRotation * rotationFollowAmount;
        targetRotation = Quaternion.Euler(armsXRotation, cameraTransform.eulerAngles.y, 0);
        
        // 최적화: deltaTime 계산 캐싱
        float t = smoothSpeed * Time.deltaTime;
        
        // 최적화: 이미 충분히 가까우면 스냅 (불필요한 보간 제거)
        if ((targetPosition - transform.position).sqrMagnitude < 0.0001f)
        {
            transform.position = targetPosition;
            transform.rotation = targetRotation;
        }
        else
        {
            transform.position = Vector3.Lerp(transform.position, targetPosition, t);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, t);
        }
    }
}
