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
    public float rotationFollowAmount = 0.7f;  // 0 = 안 따라감, 1 = 완전히 따라감
    
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
        
        // 부드럽게 이동
        transform.position = Vector3.Lerp(transform.position, targetPosition, smoothSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, smoothSpeed * Time.deltaTime);
    }
}
