using UnityEngine;
using Unity.Netcode;

public enum WeaponType
{
    Pistol,
    Rifle,
    Sniper,
    Knife
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
    [Header("무기 설정")]
    public WeaponData[] weapons;
    
    [Header("현재 무기")]
    public NetworkVariable<int> currentWeaponIndex = new NetworkVariable<int>(0);
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
        if (weapons == null || weapons.Length == 0)
        {
            weapons = new WeaponData[]
            {
                new WeaponData { weaponName = "권총", type = WeaponType.Pistol, damage = 20, range = 50f, fireRate = 0.3f, maxAmmo = 12 },
                new WeaponData { weaponName = "라이플", type = WeaponType.Rifle, damage = 25, range = 100f, fireRate = 0.1f, maxAmmo = 30 },
                new WeaponData { weaponName = "스나이퍼", type = WeaponType.Sniper, damage = 100, range = 500f, fireRate = 1.5f, maxAmmo = 5 },
                new WeaponData { weaponName = "칼", type = WeaponType.Knife, damage = 50, range = 2f, fireRate = 0.5f, maxAmmo = -1 } // 무한
            };
        }
        
        currentWeaponIndex.OnValueChanged += OnWeaponChanged;
        EquipWeapon(currentWeaponIndex.Value);
    }

    void Update()
    {
        if (!IsOwner) return;
        
        // 무기 교체 (1, 2, 3, 4 키)
        if (Input.GetKeyDown(KeyCode.Alpha1)) SwitchWeaponServerRpc(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SwitchWeaponServerRpc(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SwitchWeaponServerRpc(2);
        if (Input.GetKeyDown(KeyCode.Alpha4)) SwitchWeaponServerRpc(3);
        
        // 마우스 휠로 무기 교체
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0)
        {
            int newIndex = currentWeaponIndex.Value + (scroll > 0 ? -1 : 1);
            newIndex = Mathf.Clamp(newIndex, 0, weapons.Length - 1);
            SwitchWeaponServerRpc(newIndex);
        }
    }
    
    [ServerRpc]
    void SwitchWeaponServerRpc(int index)
    {
        if (index >= 0 && index < weapons.Length)
        {
            currentWeaponIndex.Value = index;
        }
    }
    
    void OnWeaponChanged(int oldVal, int newVal)
    {
        EquipWeapon(newVal);
    }
    
    void EquipWeapon(int index)
    {
        if (index < 0 || index >= weapons.Length) return;
        
        // 기존 무기 모델 제거
        if (currentWeaponModel != null)
        {
            Destroy(currentWeaponModel);
        }
        
        // 새 무기 모델 생성
        WeaponData weapon = weapons[index];
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
        if (currentWeaponIndex.Value < 0 || currentWeaponIndex.Value >= weapons.Length)
            return null;
        return weapons[currentWeaponIndex.Value];
    }
    
    public int GetCurrentDamage()
    {
        return GetCurrentWeapon()?.damage ?? 10;
    }
    
    public float GetCurrentRange()
    {
        return GetCurrentWeapon()?.range ?? 100f;
    }
}
