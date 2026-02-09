using UnityEngine;
using UnityEngine.Animations.Rigging;

/// <summary>
/// [임시 비활성화됨]
/// 왼손 IK 컨트롤러 - 필요시 enableIK = true로 활성화
/// </summary>
public class LeftHandIK : MonoBehaviour
{
    [Header("기능 활성화")]
    public bool enableIK = false;  // 기본 비활성화!
    
    [Header("IK 설정")]
    public TwoBoneIKConstraint leftHandIK;
    
    [Header("타겟 이름")]
    public string targetName = "LeftHandTarget";
    
    private WeaponController weaponController;
    private Transform currentTarget;
    
    void Start()
    {
        if (!enableIK) return;
        
        weaponController = GetComponentInParent<WeaponController>();
        
        if (leftHandIK == null)
        {
            leftHandIK = GetComponentInChildren<TwoBoneIKConstraint>();
        }
    }
    
    void LateUpdate()
    {
        if (!enableIK) return;
        
        FindAndSetTarget();
    }
    
    void FindAndSetTarget()
    {
        if (leftHandIK == null) return;
        if (weaponController == null) return;
        
        Transform weaponModel = weaponController.GetCurrentWeaponModel();
        if (weaponModel == null)
        {
            leftHandIK.weight = 0f;
            return;
        }
        
        Transform target = weaponModel.Find(targetName);
        if (target != null && target != currentTarget)
        {
            currentTarget = target;
            leftHandIK.data.target = target;
            
            var rigBuilder = GetComponentInParent<RigBuilder>();
            if (rigBuilder != null)
            {
                rigBuilder.Build();
            }
        }
        
        leftHandIK.weight = (currentTarget != null) ? 1f : 0f;
    }
}
