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
    
    [Header("3인칭 손 위치 오프셋")]
    public Vector3 positionOffset;  // 위치 조정
    public Vector3 rotationOffset;  // 회전 조정
    
    [Header("1인칭 손 위치 오프셋")]
    public Vector3 fpsPositionOffset;   // 1인칭 위치 조정
    public Vector3 fpsRotationOffset;   // 1인칭 회전 조정
    public Vector3 fpsScale = Vector3.one;  // 1인칭 스케일 조정

    [Header("사운드")]
    public AudioClip fireSound;     // 발사 소리
    public AudioClip reloadSound;   // 재장전 소리

    [Header("줌 설정")]
    public bool canZoom;            // 줌 가능 여부
    public float zoomFOV = 20f;     // 줌 했을 때 FOV
    public bool useScopeOverlay;    // 스코프 UI 사용 여부 (무기 모델 숨김)

    [Header("반동 설정")]
    public float recoilX = 2.0f;    // 수직 반동 (카메라 위로)
    public float recoilY = 0.5f;    // 수평 반동 (카메라 좌우)
    public float kickbackForce = 0.15f; // 총기 후퇴 거리 (뒤로 밀림)

    [Header("탄약 설정")]
    public int currentAmmo;         // 현재 탄창에 있는 탄약
    public int currentReserveAmmo;  // 남은 예비 탄약
    public int maxReserveAmmo;      // 최대 예비 탄약 (초기값)
    public float reloadTime = 1.5f; // 재장전 소요 시간
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
    
    [Header("반동 복구 설정")]
    public float kickbackRecoverySpeed = 15f; // 원래 위치로 돌아오는 속도
    private Vector3 currentKickbackPos; // 현재 반동 위치 오프셋
    
    [Header("현재 슬롯")]
    public NetworkVariable<WeaponSlot> currentSlot = new NetworkVariable<WeaponSlot>(WeaponSlot.Primary);
    private int currentAmmo;
    private bool isReloading = false;
    private float nextFireTime;

    // 최적화: Animator 파라미터 해시 캐싱
    private static readonly int ANIM_WEAPON_SLOT = Animator.StringToHash("WeaponSlot");
    private static readonly int ANIM_FPS_WEAPON_MODE = Animator.StringToHash("weaponMode");
    private static readonly int ANIM_FPS_SWORD_ATTACK = Animator.StringToHash("swordAttack");
    
    // 최적화: 스크롤 데드존
    private const float SCROLL_DEADZONE = 0.05f;
    
    [Header("무기 장착 위치 - 오른손")]
    public Transform redWeaponHolder;   // Red Model 오른손 (RightHand)
    public Transform blueWeaponHolder;  // Blue Model 오른손 (RightHand)
    public Transform fpsWeaponHolder;   // FPS Arms 손 (1인칭용)
    
    [Header("칼 장착 위치 - 왼손")]
    public Transform redKnifeHolder;    // Red Model 왼손 (LeftHand)
    public Transform blueKnifeHolder;   // Blue Model 왼손 (LeftHand)
    
    private GameObject currentWeaponModel;     // 현재 활성 팀의 3인칭 무기
    private GameObject currentFPSWeaponModel;  // 1인칭 무기
    private PlayerController playerController;
    private Camera playerCam;
    
    [Header("FPS Arms Animator")]
    public Animator fpsArmsAnimator;  // FPS Arms의 Animator (GunAnimator 컨트롤러)

    [Header("UI & Camera")]
    public GameObject scopeOverlay;   // 스코프 UI 패널 (Inspector에서 할당 필요)

    private AudioSource audioSource; // 3인칭 소리 재생용

    // 줌 관련 변수
    private float defaultFOV;
    private float defaultSensitivity;
    private float defaultSpeed; // 기본 이동 속도
    private bool isZoomed = false;
    private Vector3 defaultArmsPos; // FPS 팔 기본 위치

    void Start()
    {
        playerController = GetComponent<PlayerController>();
        playerCam = GetComponentInChildren<Camera>();

        // 기본 FOV 및 감도 저장
        if (playerCam != null) defaultFOV = playerCam.fieldOfView;
        if (playerController != null) 
        {
            defaultSensitivity = playerController.mouseSensitivity;
            defaultSpeed = playerController.speed;
        }
        
        // FPS 팔 기본 위치 저장 (반동 적용 위해)
        if (fpsArmsAnimator != null)
        {
            defaultArmsPos = fpsArmsAnimator.transform.localPosition;
        }
        
        // 스코프 Auto Find (드래그 앤 드롭 불편 해결)
        if (scopeOverlay == null)
        {
            // 태그로 찾기 (가장 확실함)
            GameObject found = GameObject.FindGameObjectWithTag("ScopeUI");
            if (found != null)
            {
                scopeOverlay = found;
                scopeOverlay.SetActive(false); // 시작 시 꺼둠
            }
            else
            {
                // 태그 없으면 이름으로 백업 검색
                var canvas = FindFirstObjectByType<Canvas>();
                if (canvas != null)
                {
                    Transform t = canvas.transform.Find("ScopeOverlay");
                    if (t != null) 
                    {
                        scopeOverlay = t.gameObject;
                        scopeOverlay.SetActive(false);
                    }
                }
            }
        }

        // AudioSource 컴포넌트 가져오기 또는 추가
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1.0f; // 3D 사운드
            audioSource.minDistance = 2.0f;
            audioSource.maxDistance = 50.0f;
        }

        // 무기 데이터 기본값 보장 (Inspector에서 값을 안 넣어도 동작하도록)
        EnsureWeaponDefaults(ref rifleData, "라이플", WeaponType.Rifle, 25, 100f, 0.1f, 30, canZoom: true, zoomFOV: 40f, useScopeOverlay: false, recoilX: 1.5f, recoilY: 0.5f, kickbackForce: 0.15f, maxReserveAmmo: 120, reloadTime: 2.0f);
        EnsureWeaponDefaults(ref sniperData, "스나이퍼", WeaponType.Sniper, 100, 500f, 1.5f, 5, canZoom: true, zoomFOV: 15f, useScopeOverlay: true, recoilX: 4.0f, recoilY: 0.2f, kickbackForce: 0.35f, maxReserveAmmo: 25, reloadTime: 3.0f);
        EnsureWeaponDefaults(ref pistolData, "권총", WeaponType.Pistol, 20, 50f, 0.3f, 12, canZoom: false, zoomFOV: 60f, useScopeOverlay: false, recoilX: 1.0f, recoilY: 0.2f, kickbackForce: 0.1f, maxReserveAmmo: 36, reloadTime: 1.5f);
        EnsureWeaponDefaults(ref knifeData, "칼", WeaponType.Knife, 50, 2f, 0.5f, -1, canZoom: false, zoomFOV: 60f, useScopeOverlay: false, recoilX: 0f, recoilY: 0f, kickbackForce: 0f, maxReserveAmmo: 0, reloadTime: 0f);
        
        currentSlot.OnValueChanged += OnSlotChanged;
        selectedPrimaryType.OnValueChanged += OnPrimaryTypeChanged;
        // Start에서 EquipSlot 호출하지 않음 - OnNetworkSpawn에서 처리
        currentSlot.OnValueChanged += OnSlotChanged;
        selectedPrimaryType.OnValueChanged += OnPrimaryTypeChanged;
        // Start에서 EquipSlot 호출하지 않음 - OnNetworkSpawn에서 처리
    }
    


    public void TryReload()
    {
        WeaponData weapon = GetCurrentWeapon();
        if (weapon == null || isReloading) return;
        
        // 탄약이 꽉 찼거나 예비 탄약이 없으면 장전 불가 (칼 제외)
        if (weapon.type == WeaponType.Knife) return;
        if (weapon.currentAmmo >= weapon.maxAmmo || weapon.currentReserveAmmo <= 0) return;
        
        StartCoroutine(ReloadRoutine(weapon));
    }

    private System.Collections.IEnumerator ReloadRoutine(WeaponData weapon)
    {
        isReloading = true;
        
        // 재장전 사운드 재생
        PlayWeaponSoundServerRpc(currentSlot.Value, false); // false = reload
        if (audioSource != null && weapon.reloadSound != null)
        {
             audioSource.PlayOneShot(weapon.reloadSound);
        }

        // 재장전 시간 대기
        yield return new WaitForSeconds(weapon.reloadTime);
        
        // 탄약 채우기
        int amountNeeded = weapon.maxAmmo - weapon.currentAmmo;
        int amountToReload = Mathf.Min(amountNeeded, weapon.currentReserveAmmo);
        
        weapon.currentAmmo += amountToReload;
        weapon.currentReserveAmmo -= amountToReload;
        
        isReloading = false;
    }

    void OnGUI()
    {
        if (!IsOwner) return;
        
        WeaponData weapon = GetCurrentWeapon();
        if (weapon != null && weapon.type != WeaponType.Knife)
        {
            string text = $"{weapon.currentAmmo} / {weapon.currentReserveAmmo}";
            if (isReloading) text = "RELOADING...";
            
            GUIStyle style = new GUIStyle();
            style.fontSize = 24;
            style.fontStyle = FontStyle.Bold;
            style.normal.textColor = Color.white;
            style.alignment = TextAnchor.LowerRight; // 우측 하단 정렬
            
            // 화면 우측 하단에 표시
            GUI.Label(new Rect(Screen.width - 250, Screen.height - 80, 200, 50), text, style);
        }
    }

    [ClientRpc]
    public void PlayWeaponSoundClientRpc(WeaponSlot slot, bool isFire)
    {
        // 내 캐릭터(Owner)의 1인칭 사운드는 GunScript에서 이미 재생하므로,
        // 여기서는 남이 쏘는 소리(3인칭)만 재생하거나, 
        // 3인칭 사운드와 1인칭 사운드를 분리해서 처리해야 함.
        // 일단 단순하게 모두 재생 (본인 포함 - GunScript와 겹칠 수 있음)
        // -> GunScript가 있으면 1인칭 소리가 나니까 Owner는 여기서 소리 안 내는 게 좋음
        
        if (IsOwner) return; // 본인은 1인칭 오디오 사용 (GunScript)

        WeaponData data = GetWeaponForSlot(slot);
        AudioClip clip = isFire ? data.fireSound : data.reloadSound;
        
        if (clip != null && audioSource != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
    
    /// <summary>
    /// 무기 데이터가 null이거나 핵심 값(damage, range)이 0이면 기본값으로 채움
    /// </summary>
    void EnsureWeaponDefaults(ref WeaponData data, string name, WeaponType type, int damage, 
        float range, float fireRate, int maxAmmo, bool canZoom, float zoomFOV, bool useScopeOverlay,
        float recoilX, float recoilY, float kickbackForce, int maxReserveAmmo, float reloadTime)
    {
        if (data == null)
        {
            data = new WeaponData { 
                weaponName = name, type = type, damage = damage, range = range, 
                fireRate = fireRate, maxAmmo = maxAmmo,
                canZoom = canZoom, zoomFOV = zoomFOV, useScopeOverlay = useScopeOverlay,
                recoilX = recoilX, recoilY = recoilY, kickbackForce = kickbackForce,
                maxReserveAmmo = maxReserveAmmo, reloadTime = reloadTime,
                currentAmmo = maxAmmo, currentReserveAmmo = maxReserveAmmo
            };
            return;
        }
        
        // 개별 필드가 0이면 기본값 적용
        if (string.IsNullOrEmpty(data.weaponName)) data.weaponName = name;
        if (data.damage <= 0) data.damage = damage;
        if (data.range <= 0f) data.range = range;
        if (data.fireRate <= 0f) data.fireRate = fireRate;
        if (data.maxAmmo == 0) data.maxAmmo = maxAmmo;
        
        // 반동 값도 설정 (기존 데이터에 0으로 되어있을 수 있으니 Inspector 확인 필요하지만 기본값 설정)
        if (data.recoilX == 0f && type != WeaponType.Knife) data.recoilX = recoilX;
        // recoilY는 0일 수도 있으니 패스 (칼 등)
        if (data.kickbackForce == 0f && type != WeaponType.Knife) data.kickbackForce = kickbackForce;

        // 탄약 설정
        if (data.maxReserveAmmo == 0 && type != WeaponType.Knife) data.maxReserveAmmo = maxReserveAmmo;
        if (data.reloadTime == 0f && type != WeaponType.Knife) data.reloadTime = reloadTime;

        // 초기화 (게임 시작 시) - 이미 값이 있으면(저장된 데이터 등) 덮어쓰지 않도록 주의? 
        // 아니면 그냥 시작할 때마다 꽉 채워줌
        if (data.currentAmmo == 0) data.currentAmmo = data.maxAmmo;
        if (data.currentReserveAmmo == 0) data.currentReserveAmmo = data.maxReserveAmmo;
    }

    public void GetCurrentRecoil(out float x, out float y)
    {
        WeaponData weapon = GetCurrentWeapon();
        if (weapon != null)
        {
            x = weapon.recoilX;
            y = weapon.recoilY;
        }
        else
        {
            x = 0;
            y = 0;
        }
    }
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        // 팀 변경 이벤트 구독
        if (playerController != null)
        {
            playerController.teamId.OnValueChanged += OnTeamChanged;
        }
        
        // 약간의 딜레이 후 무기 장착 (팀 ID가 설정된 후)
        Invoke(nameof(DelayedEquip), 0.5f);
    }
    
    void DelayedEquip()
    {
        EquipSlot(currentSlot.Value);
    }
    
    void OnTeamChanged(int oldVal, int newVal)
    {
        // 팀이 바뀌면 무기 재장착 (새 팀의 WeaponHolder 사용)
        EquipSlot(currentSlot.Value);
    }

    void Update()
    {
        if (!IsOwner) return;

        // 반동(Kickback) 복구 - 매 프레임 부드럽게 원위치로
        currentKickbackPos = Vector3.Lerp(currentKickbackPos, Vector3.zero, Time.deltaTime * kickbackRecoverySpeed);
        
        // FPS 팔 전체(손+무기)를 뒤로 밈
        if (fpsArmsAnimator != null)
        {
            fpsArmsAnimator.transform.localPosition = defaultArmsPos + currentKickbackPos;
        }
        else if (currentFPSWeaponModel != null)
        {
            // 팔이 없으면 무기 모델만이라도 밈 (백업 로직)
            WeaponData weapon = GetCurrentWeapon();
            if (weapon != null)
            {
                currentFPSWeaponModel.transform.localPosition = weapon.fpsPositionOffset + currentKickbackPos;
            }
        }

        // R키로 재장전
        if (Input.GetKeyDown(KeyCode.R))
        {
            TryReload();
        }
        
        // 줌 처리 (토글 방식 - HandleZoom 함수 위임)
        HandleZoom();
        
        // FOV 애니메이션 (부드럽게)
        WeaponData currentWeapon = GetCurrentWeapon();
        float targetFOV = (isZoomed && currentWeapon != null) ? currentWeapon.zoomFOV : defaultFOV;
        playerCam.fieldOfView = Mathf.Lerp(playerCam.fieldOfView, targetFOV, Time.deltaTime * 15f);
        
        // 무기 슬롯 교체 (1, 2, 3 키)
        WeaponSlot? newSlot = null;
        if (Input.GetKeyDown(KeyCode.Alpha1)) newSlot = WeaponSlot.Primary;
        if (Input.GetKeyDown(KeyCode.Alpha2)) newSlot = WeaponSlot.Secondary;
        if (Input.GetKeyDown(KeyCode.Alpha3)) newSlot = WeaponSlot.Melee;
        
        // 스크롤로 무기 교체
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > SCROLL_DEADZONE)
        {
            int slotIndex = (int)currentSlot.Value + (scroll > 0 ? -1 : 1);
            slotIndex = Mathf.Clamp(slotIndex, 0, 2);
            newSlot = (WeaponSlot)slotIndex;
        }
        
        if (newSlot.HasValue && newSlot.Value != currentSlot.Value)
        {
            // Owner는 즉시 로컬 반영 (딜레이 없이 애니메이션 전환)
            EquipSlot(newSlot.Value);
            // 서버에 동기화 (다른 플레이어들에게 전파)
            SwitchSlotServerRpc(newSlot.Value);
        }
        
        // Q키로 주무기 타입 변경 (라이플 <-> 스나이퍼)
        if (Input.GetKeyDown(KeyCode.Q))
        {
            PrimaryWeaponType newType = selectedPrimaryType.Value == PrimaryWeaponType.Rifle 
                ? PrimaryWeaponType.Sniper 
                : PrimaryWeaponType.Rifle;
            ChangePrimaryTypeServerRpc(newType);
        }
    }

    void HandleZoom()
    {
        if (!IsOwner || playerCam == null) return;

        WeaponData currentWeapon = GetCurrentWeapon();
        bool canZoomWeapon = currentWeapon != null && currentWeapon.canZoom;

        // 우클릭 (토글)
        if (Input.GetButtonDown("Fire2") && canZoomWeapon)
        {
            isZoomed = !isZoomed;
            
            if (isZoomed)
            {
                // 줌 진입
                // 스코프 모드(UI) 진입 시 무기 모델 숨김
                if (currentWeapon.useScopeOverlay)
                {
                    if (scopeOverlay != null) scopeOverlay.SetActive(true);
                    ToggleWeaponVisibility(false);
                }

                if (playerController != null) 
                {
                    // 줌 감도 저하 (정밀 조준)
                    playerController.mouseSensitivity = defaultSensitivity * 0.3f;
                    // 이동 속도 2배 감소
                    playerController.speed = defaultSpeed * 0.5f; 
                }
            }
            else
            {
                // 줌 해제
                // 스코프 모드 해제
                if (scopeOverlay != null) scopeOverlay.SetActive(false);
                ToggleWeaponVisibility(true);

                if (playerController != null) 
                {
                    // 감도 복구
                    playerController.mouseSensitivity = defaultSensitivity;
                    // 이동 속도 복구
                    playerController.speed = defaultSpeed;
                }
            }
        }
        else if (!canZoomWeapon && isZoomed)
        {
            // 무기 바꿨는데 줌 상태면 강제 해제
            isZoomed = false;
            if (scopeOverlay != null) scopeOverlay.SetActive(false);
            ToggleWeaponVisibility(true);
            
            if (playerController != null) 
            {
                playerController.mouseSensitivity = defaultSensitivity;
                playerController.speed = defaultSpeed;
            }
        }

        // FOV 애니메이션 (부드럽게)
        float targetFOV = isZoomed ? currentWeapon.zoomFOV : defaultFOV;
        playerCam.fieldOfView = Mathf.Lerp(playerCam.fieldOfView, targetFOV, Time.deltaTime * 15f);
    }

    void ToggleWeaponVisibility(bool visible)
    {
        if (currentFPSWeaponModel != null)
        {
            Renderer[] renderers = currentFPSWeaponModel.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers) r.enabled = visible;
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
        // Owner는 Update에서 이미 즉시 반영했으므로 스킵
        if (IsOwner) return;
        EquipSlot(newVal);
    }
    
    void OnPrimaryTypeChanged(PrimaryWeaponType oldVal, PrimaryWeaponType newVal)
    {
        // 주무기 슬롯 사용 중이면 모델 갱신
        if (currentSlot.Value == WeaponSlot.Primary)
        {
            EquipSlot(WeaponSlot.Primary);
        }
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
        if (currentFPSWeaponModel != null)
        {
            Destroy(currentFPSWeaponModel);
        }
        
        // 현재 팀에 맞는 WeaponHolder 선택
        int teamId = playerController != null ? playerController.teamId.Value : 0;
        
        // 칼은 왼손, 나머지는 오른손
        Transform activeWeaponHolder;
        if (slot == WeaponSlot.Melee)
        {
            // 칼은 왼손 홀더
            activeWeaponHolder = (teamId == 0) ? redKnifeHolder : blueKnifeHolder;
        }
        else
        {
            // 총은 오른손 홀더
            activeWeaponHolder = (teamId == 0) ? redWeaponHolder : blueWeaponHolder;
        }
        
        // 3인칭 무기 모델 생성 (다른 플레이어가 볼 용도)
        if (weapon.model != null && activeWeaponHolder != null)
        {
            currentWeaponModel = Instantiate(weapon.model, activeWeaponHolder);
            currentWeaponModel.transform.localPosition = weapon.positionOffset;
            currentWeaponModel.transform.localRotation = Quaternion.Euler(weapon.rotationOffset);
            
            // 내 캐릭터의 3인칭 무기는 ThirdPerson 레이어로 설정 (내 카메라에서 안 보임)
            if (IsOwner)
            {
                SetLayerRecursively(currentWeaponModel, LayerMask.NameToLayer("ThirdPerson"));
            }
        }
        
        // 1인칭 무기 모델 생성 (Owner만 - FPSArms 레이어로 내 카메라에서만 보임)
        if (IsOwner && weapon.model != null && fpsWeaponHolder != null)
        {
            currentFPSWeaponModel = Instantiate(weapon.model, fpsWeaponHolder);
            currentFPSWeaponModel.transform.localPosition = weapon.fpsPositionOffset;
            currentFPSWeaponModel.transform.localRotation = Quaternion.Euler(weapon.fpsRotationOffset);
            currentFPSWeaponModel.transform.localScale = weapon.fpsScale;
            
            // FPSArms 레이어로 설정 (내 카메라에서만 보임)
            SetLayerRecursively(currentFPSWeaponModel, LayerMask.NameToLayer("FPSArms"));
        }
        
        // FPS Arms Animator - 무기 모드 전환 (0=Gun, 1=Sword)
        if (IsOwner && fpsArmsAnimator != null)
        {
            int weaponMode = (slot == WeaponSlot.Melee) ? 1 : 0;
            fpsArmsAnimator.SetInteger(ANIM_FPS_WEAPON_MODE, weaponMode);
            
            // Sword Layer 가중치 제어 (칼=1, 총=0)
            // Override 모드에서 빈 스테이트가 Base Layer를 덮어쓰지 않도록
            int swordLayerIdx = fpsArmsAnimator.GetLayerIndex("Sword Layer");
            if (swordLayerIdx >= 0)
            {
                fpsArmsAnimator.SetLayerWeight(swordLayerIdx, weaponMode);
            }
        }
        
        // 탄약 초기화
        currentAmmo = weapon.maxAmmo;
        
        // 상체 애니메이션 전환
        // WeaponSlot: 0=Primary, 1=Secondary, 2=Melee
        if (playerController != null && playerController.anim != null)
        {
            var animator = playerController.anim;
            
            // 무기 교체 시 남아있는 Attack 트리거 초기화
            animator.ResetTrigger(Animator.StringToHash("Attack"));
            
            // [임시 해결] 1. 하체는 무조건 라이플 모션(0)으로 고정하여 이동 보장
            animator.SetInteger(ANIM_WEAPON_SLOT, 0);
            
            // [임시 해결] 2. 상체(Layer 1)만 해당 무기 포즈로 강제 전환 (Pistol Idle, Knife Idle)
            int ubIdx = animator.GetLayerIndex("Upper Body");
            if (ubIdx >= 0)
            {
                animator.SetLayerWeight(ubIdx, 1f);
                
                if (slot == WeaponSlot.Secondary) 
                {
                    animator.CrossFade("Pistol Idle", 0.1f, ubIdx);
                }
                else if (slot == WeaponSlot.Melee) 
                {
                    animator.CrossFade("Knife Idle", 0.1f, ubIdx);
                }
                // 라이플(Primary)은 파라미터 0에 의해 자연스럽게 처리됨
            }
        }
    }
    
    /// <summary>
    /// 근접 공격 (칼) - FPS 팔(1인칭)만 처리
    /// 3인칭은 Upper Body 레이어에서 Attack 트리거로 자동 전환됨
    /// </summary>
    public void TriggerMeleeAttack()
    {
        // FPS Arms 공격 (1인칭만 - 3인칭과 완전 분리)
        if (fpsArmsAnimator != null)
        {
            fpsArmsAnimator.SetTrigger(ANIM_FPS_SWORD_ATTACK);
        }
        // 3인칭은 PlayerController.Shoot()에서 anim.SetTrigger("Attack")로 처리
        // Upper Body 레이어: Knife Idle → Knife Attack 자동 전환
        // 레이어 가중치 변경 없음 → 땅 꺼짐 방지
    }
    
    void SetLayerRecursively(GameObject obj, int layer)
    {
        if (obj == null || layer < 0 || layer > 31) return;
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
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
        
        // ★ 재장전 중이면 발사 불가
        if (isReloading) return false;
        
        WeaponData weapon = GetCurrentWeapon();
        if (weapon == null) return false;
        
        // 탄약 체크 (칼은 무한)
        if (weapon.maxAmmo > 0)
        {
             if (weapon.currentAmmo <= 0)
             {
                 // 총알 없으면 자동 재장전 시도
                 TryReload();
                 return false;
             }
             
             // 발사 성공 시 탄약 감소
             weapon.currentAmmo--;
        }
        
        nextFireTime = Time.time + weapon.fireRate;
        
        // 발사 성공! 사운드 재생 (서버를 통해 다른 클라이언트에 전파)
        PlayWeaponSoundServerRpc(currentSlot.Value, true);

        // ★ Owner는 즉시 로컬 재생 (GunScript 의존성 제거)
        if (audioSource != null && weapon.fireSound != null)
        {
            audioSource.PlayOneShot(weapon.fireSound);
        }
        
        // ★ 총기 후퇴 (Kickback) 적용 - 모델을 뒤로(-Z) 밈
        // 칼은 후퇴 효과 없음
        if (weapon.type != WeaponType.Knife)
        {
            currentKickbackPos.z -= weapon.kickbackForce;
            // 너무 심하게 밀리지 않도록 제한 (최대 0.5 유닛)
            currentKickbackPos.z = Mathf.Max(currentKickbackPos.z, -0.5f);
        }
        
        nextFireTime = Time.time + weapon.fireRate;
        if (weapon.maxAmmo > 0) currentAmmo--;
        
        return true;
    }
    
    [ServerRpc]
    void PlayWeaponSoundServerRpc(WeaponSlot slot, bool isFire)
    {
        PlayWeaponSoundClientRpc(slot, isFire);
    }
    
    public WeaponData GetCurrentWeapon()
    {
        return GetWeaponForSlot(currentSlot.Value);
    }
    
    public int GetCurrentDamage()
    {
        int dmg = GetCurrentWeapon()?.damage ?? 10;
        return dmg > 0 ? dmg : 10; // 최소 대미지 보장
    }
    
    public float GetCurrentRange()
    {
        float rng = GetCurrentWeapon()?.range ?? 100f;
        return rng > 0f ? rng : 100f; // 최소 사거리 보장
    }
    
    public WeaponSlot GetCurrentSlot()
    {
        return currentSlot.Value;
    }
    
    public PrimaryWeaponType GetPrimaryType()
    {
        return selectedPrimaryType.Value;
    }
    
    /// <summary>
    /// UI에서 주무기 선택 시 호출
    /// </summary>
    public void SelectPrimaryWeapon(PrimaryWeaponType weaponType)
    {
        if (!IsOwner) return;
        ChangePrimaryTypeServerRpc(weaponType);
    }
    
    /// <summary>
    /// 현재 장착된 무기 모델의 Transform 반환 (IK용)
    /// </summary>
    public Transform GetCurrentWeaponModel()
    {
        return currentWeaponModel != null ? currentWeaponModel.transform : null;
    }

    public Vector3 GetMuzzlePosition()
    {
        // 1인칭 (내 화면)
        if (IsOwner && currentFPSWeaponModel != null)
        {
            // 모델의 앞쪽 0.8m 정도를 총구로 가정 (모델마다 다를 수 있음)
            return currentFPSWeaponModel.transform.position + currentFPSWeaponModel.transform.forward * 0.8f;
        }
        // 3인칭 (다른 플레이어가 볼 때)
        else if (currentWeaponModel != null)
        {
             return currentWeaponModel.transform.position + currentWeaponModel.transform.forward * 0.8f;
        }
        
        // 무기가 없으면 대충 가슴 높이
        return transform.position + Vector3.up * 1.4f + transform.forward * 0.5f;
    }
}
