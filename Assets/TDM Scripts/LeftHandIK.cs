using UnityEngine;
using UnityEngine.Animations.Rigging;

/// <summary>
/// 왼손 IK 컨트롤러 - 총의 LeftHandTarget을 찾아서 IK 타겟으로 연결
/// </summary>
public class LeftHandIK : MonoBehaviour
{
    [Header("IK 설정")]
    public TwoBoneIKConstraint leftHandIK;
    
    [Header("타겟 이름")]
    public string targetName = "LeftHandTarget";
    
    private WeaponController weaponController;
    private Transform currentTarget;
    
    void Start()
    {
        weaponController = GetComponentInParent<WeaponController>();
        
        // IK가 없으면 자동으로 찾기
        if (leftHandIK == null)
        {
            leftHandIK = GetComponentInChildren<TwoBoneIKConstraint>();
        }
    }
    
    void LateUpdate()
    {
        // 무기 모델에서 LeftHandTarget 찾기
        FindAndSetTarget();
    }
    
    void FindAndSetTarget()
    {
        if (leftHandIK == null) return;
        
        // WeaponController에서 현재 무기 모델 가져오기
        if (weaponController == null) return;
        
        // 현재 장착된 무기 모델의 LeftHandTarget 찾기
        Transform weaponModel = weaponController.GetCurrentWeaponModel();
        if (weaponModel == null)
        {
            // 무기가 없으면 IK 비활성화
            leftHandIK.weight = 0f;
            return;
        }
        
        // 타겟 찾기
        Transform target = weaponModel.Find(targetName);
        if (target != null && target != currentTarget)
        {
            currentTarget = target;
            
            // IK 타겟 설정
            leftHandIK.data.target = target;
            
            // Rig 다시 빌드 (런타임 변경 적용)
            var rigBuilder = GetComponentInParent<RigBuilder>();
            if (rigBuilder != null)
            {
                rigBuilder.Build();
            }
        }
        
        // 타겟이 있으면 IK 활성화
        leftHandIK.weight = (currentTarget != null) ? 1f : 0f;
    }
}
