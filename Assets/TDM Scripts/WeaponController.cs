using UnityEngine;
using Unity.Netcode;

public enum WeaponType
{
    Pistol,     // 보조무기
    Rifle,      // 주무기 옵션 1
    Sniper,     // 주무기 옵션 2
    Knife       // 근접무기
}

public enum WeaponSlot
{
    Primary,    // 주무기 (라이플 or 스나이퍼)
    Secondary,  // 보조무기 (권총)
    Melee       // 근접무기 (칼)
}

public enum PrimaryWeaponType
{
    Rifle,
    Sniper
}

[System.Serializable]
public class WeaponData
{
    public string weaponName;
    public WeaponType type;
    public int damage;
    public float range;
    public float fireRate; // 발사 간격 (초)
    public int maxAmmo;
    public GameObject model;
}

public class WeaponController : NetworkBehaviour
{
    [Header("무기 데이터")]
    public WeaponData rifleData;
    public WeaponData sniperData;
    public WeaponData pistolData;
    public WeaponData knifeData;
    
    [Header("주무기 선택")]
    public NetworkVariable<PrimaryWeaponType> selectedPrimaryType = new NetworkVariable<PrimaryWeaponType>(PrimaryWeaponType.Rifle);
    
    [Header("현재 슬롯")]
    public NetworkVariable<WeaponSlot> currentSlot = new NetworkVariable<WeaponSlot>(WeaponSlot.Primary);
    private int currentAmmo;
    private float nextFireTime;
    
    [Header("무기 장착 위치")]
    public Transform weaponHolder; // 손 뼈 (RightHand)
    
    private GameObject currentWeaponModel;
    private PlayerController playerController;
    private Camera playerCam;

    void Start()
    {
        playerController = GetComponent<PlayerController>();
        playerCam = GetComponentInChildren<Camera>();
        
        // 기본 무기 데이터 설정 (Inspector에서 안 넣었을 경우)
        if (rifleData == null || string.IsNullOrEmpty(rifleData.weaponName))
        {
            rifleData = new WeaponData { weaponName = "라이플", type = WeaponType.Rifle, damage = 25, range = 100f, fireRate = 0.1f, maxAmmo = 30 };
        }
        if (sniperData == null || string.IsNullOrEmpty(sniperData.weaponName))
        {
            sniperData = new WeaponData { weaponName = "스나이퍼", type = WeaponType.Sniper, damage = 100, range = 500f, fireRate = 1.5f, maxAmmo = 5 };
        }
        if (pistolData == null || string.IsNullOrEmpty(pistolData.weaponName))
        {
            pistolData = new WeaponData { weaponName = "권총", type = WeaponType.Pistol, damage = 20, range = 50f, fireRate = 0.3f, maxAmmo = 12 };
        }
        if (knifeData == null || string.IsNullOrEmpty(knifeData.weaponName))
        {
            knifeData = new WeaponData { weaponName = "칼", type = WeaponType.Knife, damage = 50, range = 2f, fireRate = 0.5f, maxAmmo = -1 };
        }
        
        currentSlot.OnValueChanged += OnSlotChanged;
        selectedPrimaryType.OnValueChanged += OnPrimaryTypeChanged;
        EquipSlot(currentSlot.Value);
    }

    void Update()
    {
        if (!IsOwner) return;
        
        // 무기 슬롯 교체 (1, 2, 3 키)
        if (Input.GetKeyDown(KeyCode.Alpha1)) SwitchSlotServerRpc(WeaponSlot.Primary);   // 주무기
        if (Input.GetKeyDown(KeyCode.Alpha2)) SwitchSlotServerRpc(WeaponSlot.Secondary); // 보조무기
        if (Input.GetKeyDown(KeyCode.Alpha3)) SwitchSlotServerRpc(WeaponSlot.Melee);     // 근접무기
        
        // Q키로 주무기 타입 변경 (라이플 <-> 스나이퍼)
        if (Input.GetKeyDown(KeyCode.Q))
        {
            PrimaryWeaponType newType = selectedPrimaryType.Value == PrimaryWeaponType.Rifle 
                ? PrimaryWeaponType.Sniper 
                : PrimaryWeaponType.Rifle;
            ChangePrimaryTypeServerRpc(newType);
        }
        
        // 마우스 휠로 슬롯 교체
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0)
        {
            int slotIndex = (int)currentSlot.Value + (scroll > 0 ? -1 : 1);
            slotIndex = Mathf.Clamp(slotIndex, 0, 2);
            SwitchSlotServerRpc((WeaponSlot)slotIndex);
        }
    }
    
    [ServerRpc]
    void SwitchSlotServerRpc(WeaponSlot slot)
    {
        currentSlot.Value = slot;
    }
    
    [ServerRpc]
    void ChangePrimaryTypeServerRpc(PrimaryWeaponType newType)
    {
        selectedPrimaryType.Value = newType;
    }
    
    void OnSlotChanged(WeaponSlot oldVal, WeaponSlot newVal)
    {
        EquipSlot(newVal);
    }
    
    void OnPrimaryTypeChanged(PrimaryWeaponType oldVal, PrimaryWeaponType newVal)
    {
        // 주무기 슬롯 사용 중이면 모델 갱신
        if (currentSlot.Value == WeaponSlot.Primary)
        {
            EquipSlot(WeaponSlot.Primary);
        }
        Debug.Log($"주무기 변경: {(newVal == PrimaryWeaponType.Rifle ? "라이플" : "스나이퍼")}");
    }
    
    void EquipSlot(WeaponSlot slot)
    {
        WeaponData weapon = GetWeaponForSlot(slot);
        if (weapon == null) return;
        
        // 기존 무기 모델 제거
        if (currentWeaponModel != null)
        {
            Destroy(currentWeaponModel);
        }
        
        // 새 무기 모델 생성
        if (weapon.model != null && weaponHolder != null)
        {
            currentWeaponModel = Instantiate(weapon.model, weaponHolder);
            currentWeaponModel.transform.localPosition = Vector3.zero;
            currentWeaponModel.transform.localRotation = Quaternion.identity;
        }
        
        // 탄약 초기화
        currentAmmo = weapon.maxAmmo;
        
        Debug.Log($"무기 장착: {weapon.weaponName}");
    }
    
    WeaponData GetWeaponForSlot(WeaponSlot slot)
    {
        switch (slot)
        {
            case WeaponSlot.Primary:
                return selectedPrimaryType.Value == PrimaryWeaponType.Rifle ? rifleData : sniperData;
            case WeaponSlot.Secondary:
                return pistolData;
            case WeaponSlot.Melee:
                return knifeData;
            default:
                return null;
        }
    }
    
    // PlayerController에서 호출
    public bool TryShoot()
    {
        if (Time.time < nextFireTime) return false;
        
        WeaponData weapon = GetCurrentWeapon();
        if (weapon == null) return false;
        
        // 탄약 체크 (칼은 무한)
        if (weapon.maxAmmo > 0 && currentAmmo <= 0) return false;
        
        nextFireTime = Time.time + weapon.fireRate;
        if (weapon.maxAmmo > 0) currentAmmo--;
        
        return true;
    }
    
    public WeaponData GetCurrentWeapon()
    {
        return GetWeaponForSlot(currentSlot.Value);
    }
    
    public int GetCurrentDamage()
    {
        return GetCurrentWeapon()?.damage ?? 10;
    }
    
    public float GetCurrentRange()
    {
        return GetCurrentWeapon()?.range ?? 100f;
    }
    
    public WeaponSlot GetCurrentSlot()
    {
        return currentSlot.Value;
    }
    
    public PrimaryWeaponType GetPrimaryType()
    {
        return selectedPrimaryType.Value;
    }
}
