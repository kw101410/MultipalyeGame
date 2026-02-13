using UnityEngine;
using UnityEngine.Animations.Rigging;

/// <summary>
/// [임시 비활성화됨]
/// 왼손 IK 컨트롤러 - 필요시 enableIK = true로 활성화
/// 최적화: Transform.Find()를 무기 변경 시에만 호출 (매 프레임 → 이벤트 기반)
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
    
    // 최적화: 이전 무기 모델을 캐싱하여 변경 감지
    private Transform cachedWeaponModel;
    private RigBuilder cachedRigBuilder;
    
    void Start()
    {
        if (!enableIK) return;
        
        weaponController = GetComponentInParent<WeaponController>();
        
        if (leftHandIK == null)
        {
            leftHandIK = GetComponentInChildren<TwoBoneIKConstraint>();
        }
        
        // 최적화: RigBuilder 캐싱 (GetComponentInParent 매번 호출 방지)
        cachedRigBuilder = GetComponentInParent<RigBuilder>();
    }
    
    void LateUpdate()
    {
        if (!enableIK) return;
        if (leftHandIK == null || weaponController == null) return;
        
        Transform weaponModel = weaponController.GetCurrentWeaponModel();
        
        // 최적화: 무기 모델이 변경되었을 때만 Find 호출
        if (weaponModel != cachedWeaponModel)
        {
            cachedWeaponModel = weaponModel;
            UpdateTarget(weaponModel);
        }
        
        leftHandIK.weight = (currentTarget != null) ? 1f : 0f;
    }
    
    // 최적화: 무기 변경 시에만 호출 (매 프레임 X)
    void UpdateTarget(Transform weaponModel)
    {
        if (weaponModel == null)
        {
            currentTarget = null;
            return;
        }
        
        // Transform.Find()는 여기서만 호출 (무기 변경 시 1회)
        Transform target = weaponModel.Find(targetName);
        if (target != null)
        {
            currentTarget = target;
            leftHandIK.data.target = target;
            
            if (cachedRigBuilder != null)
            {
                cachedRigBuilder.Build();
            }
        }
        else
        {
            currentTarget = null;
        }
    }
}
