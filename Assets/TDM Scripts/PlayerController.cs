using Unity.Netcode;
using UnityEngine;

// 최적화: Animator 파라미터 해시 캐싱 (문자열 비교 제거)

public class PlayerController : NetworkBehaviour
{
    [Header("Base Components")]
    public CharacterController controller;
    public Animator anim;

    [Header("Move Settings")]
    public float speed = 5f;
    public float jumpHeight = 1.5f;
    public float gravity = -9.81f;

    private Vector3 velocity;

    // 최적화: Animator 파라미터 해시 캐싱
    private static readonly int ANIM_INPUT_X = Animator.StringToHash("InputX");
    private static readonly int ANIM_INPUT_Y = Animator.StringToHash("InputY");
    private static readonly int ANIM_ATTACK = Animator.StringToHash("Attack");


    // 최적화: 발소리 RPC 호출 빈도 줄이기
    private int stepCounter = 0;
    private const int STEP_SYNC_INTERVAL = 4; // 4걸음에 1번만 네트워크 동기화

    [Header("Look Settings")]
    public float mouseSensitivity = 100f;
    private float xRotation = 0f;

    [Header("Combat Settings")]
    public NetworkVariable<int> hp = new NetworkVariable<int>(100);
    // 초기값 -1: 의미 없음. 서버값 덮어씌워질 예정.
    public NetworkVariable<int> teamId = new NetworkVariable<int>(-1);
    public Camera myCam;

    private bool isGrounded;

    [Header("Visuals")]
    public GameObject redModel;
    public GameObject blueModel;
    
    [Header("Network Animation")]
    public Unity.Netcode.Components.NetworkAnimator networkAnimator;
    private AudioListener listener;

    [Header("Audio Settings")]
    public AudioSource audioSource;
    public AudioClip walkSound;
    public AudioClip runSound;
    public AudioClip jumpSound;
    public AudioClip hitSound;
    public AudioClip deathSound;
    
    [Header("Effects")]
    public GameObject bulletTrailPrefab; // 총알 궤적 프리팹 (LineRenderer)
    public Vector3 muzzleOffset = new Vector3(0.3f, -0.2f, 0.5f); // 1인칭 궤적 시작 위치 오프셋 (우, 하, 전)
    public Transform muzzlePoint; // ★ 1인칭용 총구 위치 (Inspector에서 빈 오브젝트 할당)
    public GameObject bulletHolePrefab; // ★ 총알 자국 프리팹 (Quad + Material)

    private float nextStepTime = 0f;
    private float stepInterval = 0.5f; // 발소리 간격

    private RagdollController ragdoll;
    private WeaponController weaponController;
    private ThirdPersonSpineSync spineSync;
    private PlayerController playerController;
    private Camera playerCam;
    


    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        weaponController = GetComponent<WeaponController>();
        listener = GetComponentInChildren<AudioListener>();
        ragdoll = GetComponent<RagdollController>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (networkAnimator == null) networkAnimator = GetComponent<Unity.Netcode.Components.NetworkAnimator>();
        
        // 시작 시 기본 모델 설정 (Red 모델을 기본으로, 나중에 팀 배정되면 변경됨)
        if (redModel != null)
        {
            redModel.SetActive(true);
            anim = redModel.GetComponent<Animator>();
            if (anim == null) anim = redModel.GetComponentInChildren<Animator>();
            
            /*
            if (anim != null)
            {
                anim.applyRootMotion = false; // In Place 애니메이션
            }
            */
            
            if (networkAnimator != null && anim != null)
            {
                networkAnimator.Animator = anim;
            }
        }
        if (blueModel != null) blueModel.SetActive(false);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // 1. [서버] 접속자 팀 배정 (접속 순서대로 0, 1)
        if (IsServer)
        {
            teamId.Value = (int)OwnerClientId % 2;
        }

        // 2. [공통] 값이 '바뀔 때' 실행될 로직 등록
        teamId.OnValueChanged += OnTeamChanged;

        // ★ 핵심: 이미 값이 들어와 있는 상태면 이벤트가 안 터짐. 수동 호출.
        ApplyTeamModel(teamId.Value);

        // 3. [내 캐릭터] 초기 설정
        if (IsOwner)
        {
            // 마우스 가두기
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // 스폰 위치로 이동 (팀 배정이 유효한 경우만)
            if (teamId.Value != -1) MoveToSpawnPoints(teamId.Value);

            // UI 초기화
            if (UIManager.Instance != null)
                UIManager.Instance.UpdateHP(hp.Value, teamId.Value);

            // HP  
            hp.OnValueChanged += OnHpChanged;
        }
        else
        {
            // 남의 캐릭터면 카메라랑 리스너 끄기
            if (myCam != null) myCam.enabled = false;
            if (listener != null) listener.enabled = false; 
            
            // Non-owner also needs to listen to HP for ragdoll
            hp.OnValueChanged += OnHpChanged;
        }
    }

    private void OnHpChanged(int oldVal, int newVal)
    {
        if (IsOwner && UIManager.Instance != null)
            UIManager.Instance.UpdateHP(newVal, teamId.Value);

        // Ragdoll Logic Restored
        if (ragdoll != null)
        {
            if (newVal <= 0)
            {
                ragdoll.EnableRagdoll();
                // 아주 약하게 밀려나는 효과
                ragdoll.ApplyForce(-transform.forward, 25f);
            }
            else if (newVal > 0 && oldVal <= 0)
            {
                ragdoll.DisableRagdoll();
                ApplyTeamModel(teamId.Value); 
            }
        }
    }

    private void OnTeamChanged(int oldVal, int newVal)
    {
        ApplyTeamModel(newVal);
        if (IsOwner) MoveToSpawnPoints(newVal);
    }

    private void ApplyTeamModel(int team)
    {
        if (redModel == null || blueModel == null) return;

        redModel.SetActive(false);
        blueModel.SetActive(false);

        GameObject activeModel = null;
        if (team == 0) 
        {
            redModel.SetActive(true);
            activeModel = redModel;
        }
        else if (team == 1) 
        {
            blueModel.SetActive(true);
            activeModel = blueModel;
        }

        // 활성화된 모델에서 Animator 가져와서 연결
        if (activeModel != null)
        {
            // Root Motion Handler 추가 (땅 꺼짐 해결)
            if (activeModel.GetComponent<RootMotionHandler>() == null)
            {
                activeModel.AddComponent<RootMotionHandler>();
            }

            anim = activeModel.GetComponent<Animator>();
            if (anim == null)
            {
                anim = activeModel.GetComponentInChildren<Animator>();
            }
            
            /*
            if (anim != null)
            {
                anim.applyRootMotion = false; // In Place 애니메이션
            }
            */
            
            // NetworkAnimator에도 새 Animator 연결
            if (networkAnimator != null && anim != null)
            {
                networkAnimator.Animator = anim;
            }
        }

        // ★ 랙돌 초기화 (새 모델의 Animator 전달)
        // ★ 랙돌 초기화 (새 모델의 Animator 전달)
        if (ragdoll != null) ragdoll.Init(anim);

        if (anim != null)
        {
            anim.Rebind();
            // anim.applyRootMotion = false;
        }
    }

    private bool inputEnabled = false; // 기본값 false로 변경 (라운드 시작 전 활동 방지)

    public void SetInputActive(bool active)
    {
        inputEnabled = active;
    }
    
    // ...

    void Update()
    {
        if (!IsSpawned || !IsOwner) return;

        // 게임 화면 클릭하면 마우스 다시 잡기 (에디터 문제 해결)
        // 최적화: 이미 잠겨있으면 스킵
        if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (hp.Value <= 0 || !inputEnabled) return; // ★ 입력 비활성화 체크 추가

        Look();
        Move();

        if (weaponController != null)
        {
            // 라이플인지 확인 (주무기 슬롯 & 라이플 타입)
            bool isRifle = weaponController.GetCurrentSlot() == WeaponSlot.Primary && 
                           weaponController.GetPrimaryType() == PrimaryWeaponType.Rifle;
            
            // 라이플은 누르고 있으면 연사(GetButton), 나머지는 클릭당 1발(GetButtonDown)
            if (isRifle ? Input.GetButton("Fire1") : Input.GetButtonDown("Fire1"))
            {
                Shoot();
            }
        }
        else if (Input.GetButtonDown("Fire1"))
        {
            Shoot();
        }
    }

    void Look()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        myCam.transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
    }

    void Move()
    {
        if (controller == null || !controller.enabled) return;
        isGrounded = controller.isGrounded;
        if (isGrounded && velocity.y < 0) velocity.y = -2f;

        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        Vector3 move = transform.right * x + transform.forward * z;
        
        // 이동 중이면 발소리 재생
        // 최적화: sqrMagnitude 사용 (sqrt 연산 제거)
        if (isGrounded && move.sqrMagnitude > 0.01f)
        {
            if (Time.time >= nextStepTime)
            {
                PlayFootstepSound(true); 
                nextStepTime = Time.time + stepInterval;
            }
        }

        controller.Move(move * speed * Time.deltaTime);

        // 최적화: 해시로 Animator 파라미터 접근
        if (anim != null)
        {
            anim.SetFloat(ANIM_INPUT_X, x);
            anim.SetFloat(ANIM_INPUT_Y, z);
        }

        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            PlayJumpSoundServerRpc();
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);

        /* Removed Layer Weight Hack */
        

    }

    void PlayFootstepSound(bool isWalk)
    {
        // 최적화: 로컬에서 즉시 재생 (지연 없음) + 간헐적으로만 네트워크 동기화
        PlayStepSoundLocal(isWalk);
        
        stepCounter++;
        if (stepCounter >= STEP_SYNC_INTERVAL)
        {
            stepCounter = 0;
            PlayStepSoundServerRpc(isWalk);
        }
    }

    void PlayStepSoundLocal(bool isWalk)
    {
        if (audioSource == null) return;
        AudioClip clip = isWalk ? walkSound : runSound;
        if (clip != null)
        {
            audioSource.pitch = Random.Range(0.9f, 1.1f);
            audioSource.PlayOneShot(clip, 0.6f);
        }
    }

    [ServerRpc]
    void PlayStepSoundServerRpc(bool isWalk)
    {
        PlayStepSoundClientRpc(isWalk);
    }

    [ClientRpc]
    void PlayStepSoundClientRpc(bool isWalk)
    {
        // 최적화: Owner는 이미 로컬에서 재생했으므로 스킵
        if (IsOwner) return;
        
        if (audioSource == null) return;
        AudioClip clip = isWalk ? walkSound : runSound;
        if (clip != null)
        {
            audioSource.pitch = Random.Range(0.9f, 1.1f);
            audioSource.PlayOneShot(clip, 0.6f);
        }
    }

    [ServerRpc]
    void PlayJumpSoundServerRpc()
    {
        PlayJumpSoundClientRpc();
    }

    [ClientRpc]
    void PlayJumpSoundClientRpc()
    {
        if (audioSource != null && jumpSound != null)
        {
            audioSource.pitch = 1.0f;
            audioSource.PlayOneShot(jumpSound);
        }
    }

    void Shoot()
    {
        if (weaponController != null && !weaponController.TryShoot()) return;

        // 칼 공격일 때 처리 (궤적 없음)
        if (weaponController != null && weaponController.GetCurrentSlot() == WeaponSlot.Melee)
        {
            // 3인칭 공격 애니메이션 (상체만 재생하여 하체 움직임 유지)
            if (anim != null)
            {
                int ubIdx = anim.GetLayerIndex("Upper Body");
                if (ubIdx >= 0)
                {
                    // 상체 레이어에서만 Knife Attack 재생 (하체 방해 X)
                    // (이동 중 공격 가능)
                    anim.CrossFade("Knife Attack", 0.1f, ubIdx);
                }
                else
                {
                    anim.SetTrigger(ANIM_ATTACK); // 백업
                }
            }
            // FPS Arms + 전신 공격 모션
            weaponController.TriggerMeleeAttack();
            return; 
        }
        
        float range = weaponController != null ? weaponController.GetCurrentRange() : 100f;
        int damage = weaponController != null ? weaponController.GetCurrentDamage() : 10;
        
        Vector3 endPoint = myCam.transform.position + myCam.transform.forward * range;
        
        // Raycast로 적 감지
        RaycastHit hit;
        if (Physics.Raycast(myCam.transform.position, myCam.transform.forward, out hit, range))
        {
            endPoint = hit.point;

            // GetComponentInParent로 부모까지 검색 (자식 Collider 히트 대응)
            var targetScript = hit.transform.GetComponentInParent<PlayerController>();
            
            if (targetScript != null)
            {
                // 자기 자신을 맞춘 경우 무시
                if (targetScript.NetworkObjectId == NetworkObjectId) return;
                
                // 같은 팀이면 무시
                if (targetScript.teamId.Value == teamId.Value) return;
                
                SubmitHitServerRpc(targetScript.NetworkObjectId, damage);
            }
            else
            {
                // 플레이어가 아니면 벽/바닥으로 간주 -> 총알 자국 생성 (서버 중계)
                Debug.Log($"[Shoot] 벽/바닥 적중: {hit.collider.name}"); // 디버그 로그 추가
                SpawnBulletHoleServerRpc(hit.point, hit.normal);
            }
        }
        
        // 총알 궤적 생성 (서버 중계)
        SpawnBulletTrailServerRpc(endPoint);

        // ★ 반동 적용 (총알 발사 후 마지막에 적용해야 정확도가 유지됨)
        if (weaponController != null)
        {
            float rx, ry;
            weaponController.GetCurrentRecoil(out rx, out ry);
            ApplyRecoil(rx, ry);
        }
    }

    void ApplyRecoil(float vertical, float horizontal)
    {
        // ... (생략: 기존 코드 유지)
        xRotation -= vertical;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);
        float randomY = Random.Range(-horizontal, horizontal);
        transform.Rotate(Vector3.up * randomY);
        if (myCam != null) myCam.transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
    }
    
    [ServerRpc]
    void SpawnBulletTrailServerRpc(Vector3 endPoint)
    {
        SpawnBulletTrailClientRpc(endPoint);
    }

    [ClientRpc]
    void SpawnBulletTrailClientRpc(Vector3 endPoint)
    {
        if (bulletTrailPrefab == null) return;
        
        Vector3 startPoint = transform.position + Vector3.up * 1.5f; 
        
        if (IsOwner)
        {
            // 1. 빈 오브젝트(Transform)가 할당되어 있으면 그 위치를 사용 (가장 우선)
            if (muzzlePoint != null)
            {
                startPoint = muzzlePoint.position;
            }
            // 2. 없으면 카메라 기준으로 내가 설정한 오프셋 사용
            else if (myCam != null)
            {
                startPoint = myCam.transform.TransformPoint(muzzleOffset);
            }
        }
        else if (weaponController != null)
        {
            // 3. 다른 사람 화면(3인칭)에서는 무기 모델 총구 위치 사용
            startPoint = weaponController.GetMuzzlePosition();
        }
            
        GameObject trail = Instantiate(bulletTrailPrefab, startPoint, Quaternion.identity);
        LineRenderer line = trail.GetComponent<LineRenderer>();
        if (line != null)
        {
            // ★ 중요: 월드 좌표계 사용 강제 설정
            line.useWorldSpace = true;
            line.positionCount = 2; // 점 2개
            
            line.SetPosition(0, startPoint);
            line.SetPosition(1, endPoint);
        }
    }

    [ServerRpc]
    void SpawnBulletHoleServerRpc(Vector3 pos, Vector3 normal)
    {
        SpawnBulletHoleClientRpc(pos, normal);
    }

    [ClientRpc]
    void SpawnBulletHoleClientRpc(Vector3 pos, Vector3 normal)
    {
        if (bulletHolePrefab != null)
        {
            // Z-Fighting 방지 (면에서 아주 살짝 띄움 - 너무 멀면 붕 떠보임)
            Vector3 spawnPos = pos + normal * 0.02f;
            Quaternion rot = Quaternion.LookRotation(normal);
            
            // Quad가 반대 방향을 보고 있다면 180도 회전 (프리팹에 따라 다를 수 있음)
            rot *= Quaternion.Euler(0, 180f, 0);
            
            Instantiate(bulletHolePrefab, spawnPos, rot);
        }
    }

    [ServerRpc]
    void SubmitHitServerRpc(ulong targetId, int damage)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetId, out var targetObj))
        {
            var targetScript = targetObj.GetComponent<PlayerController>();
            if (targetScript != null && targetScript.hp.Value > 0)
                targetScript.TakeDamage(damage);
        }
    }

    public void TakeDamage(int damage)
    {
        if (hp.Value <= 0) return;
        
        // 피격 소리 (RPC)
        PlayHitSoundClientRpc();

        hp.Value -= damage;
        if (hp.Value <= 0 && IsServer)
        {
            RoundGameManager.Instance?.OnPlayerDied(teamId.Value);
            PlayDeathSoundClientRpc();
        }
    }

    [ClientRpc]
    void PlayHitSoundClientRpc()
    {
        if (audioSource != null && hitSound != null)
        {
            audioSource.pitch = Random.Range(0.9f, 1.1f);
            audioSource.PlayOneShot(hitSound);
        }
    }

    [ClientRpc]
    void PlayDeathSoundClientRpc()
    {
        if (audioSource != null && deathSound != null)
        {
            audioSource.pitch = 1.0f;
            audioSource.PlayOneShot(deathSound);
        }
    }

    public void Respawn(int spawnIndex)
    {
        if (!IsSpawned) return;
        hp.Value = 100;
        RespawnClientRpc(teamId.Value, spawnIndex);
    }

    [ClientRpc]
    private void RespawnClientRpc(int team, int spawnIndex)
    {
        if (!IsOwner) return;
        MoveToSpawnPoints(team, spawnIndex);
        if (UIManager.Instance != null) UIManager.Instance.UpdateHP(100, team);
        SetPlayerState(true);
    }




    void MoveToSpawnPoints(int team, int spawnIndex = -1)
    {
        if (RoundGameManager.Instance == null) return;
        Transform targetSpawn = (team == 0) ? RoundGameManager.Instance.spawnPointA : RoundGameManager.Instance.spawnPointB;

        if (targetSpawn == null) return;

        if (controller != null) controller.enabled = false;
        if (spawnIndex == -1) spawnIndex = (int)(OwnerClientId % 4);
        transform.position = targetSpawn.position + targetSpawn.right * (spawnIndex * 2.0f);
        transform.rotation = targetSpawn.rotation;
        if (controller != null) controller.enabled = true;
    }

    void SetPlayerState(bool isActive)
    {
        if (isActive)
        {
            if (ragdoll != null) ragdoll.DisableRagdoll();
            ApplyTeamModel(teamId.Value);
        }
        else
        {
            if (redModel) redModel.SetActive(false);
            if (blueModel) blueModel.SetActive(false);
        }
        if (controller != null) controller.enabled = isActive;
        if (IsOwner) this.enabled = isActive;
    }
}
