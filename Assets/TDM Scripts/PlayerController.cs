using Unity.Netcode;
using UnityEngine;

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

    private RagdollController ragdoll;
    private WeaponController weaponController;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        weaponController = GetComponent<WeaponController>();
        listener = GetComponentInChildren<AudioListener>();
        ragdoll = GetComponent<RagdollController>();
        if (networkAnimator == null) networkAnimator = GetComponent<Unity.Netcode.Components.NetworkAnimator>();
        
        // 시작 시 기본 모델 설정 (Red 모델을 기본으로, 나중에 팀 배정되면 변경됨)
        // NetworkAnimator 초기화 전에 Animator가 있어야 에러 안 남
        if (redModel != null)
        {
            redModel.SetActive(true);
            anim = redModel.GetComponent<Animator>();
            if (anim == null) anim = redModel.GetComponentInChildren<Animator>();
            
            if (anim != null)
            {
                anim.applyRootMotion = false; // In Place 애니메이션
            }
            
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

        // ★ 핵심 1: 이미 값이 들어와 있는 상태면 이벤트가 안 터짐. 
        // 그래서 수동으로 한 번 "내 팀에 맞는 옷 입어라"고 명령해야 함.
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
            // listener already assigned in Awake, just disable
            if (listener != null) listener.enabled = false; 
            
            // Non-owner also needs to listen to HP for ragdoll
            hp.OnValueChanged += OnHpChanged;
        }
    }

    private void OnHpChanged(int oldVal, int newVal)
    {
        if (IsOwner && UIManager.Instance != null)
            UIManager.Instance.UpdateHP(newVal, teamId.Value);

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
                // Ensure correct model is active/rebound
                ApplyTeamModel(teamId.Value); 
            }
        }
    }

    // 팀 변경 시 실행될 함수 (이벤트 연결용)
    private void OnTeamChanged(int oldVal, int newVal)
    {
        ApplyTeamModel(newVal);

        // 내 거면 스폰 포인트로 이동도 시켜줌
        if (IsOwner) MoveToSpawnPoints(newVal);
    }

    private void ApplyTeamModel(int team)
    {
        // 모델 연결 안 되어있으면 에러 띄워서 알려줌(개발 인스펙터 확인 좀)
        if (redModel == null || blueModel == null)
        {
            Debug.LogError($"[{gameObject.name}] 모델 연결 안됨! Inspector 확인해라.");
            return;
        }

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
        // -1이면 둘 다 꺼진 상태 유지

        // 활성화된 모델에서 Animator 가져와서 연결
        if (activeModel != null)
        {
            anim = activeModel.GetComponent<Animator>();
            if (anim == null)
            {
                anim = activeModel.GetComponentInChildren<Animator>();
            }
            
            if (anim != null)
            {
                anim.applyRootMotion = false; // In Place 애니메이션
            }
            
            // NetworkAnimator에도 새 Animator 연결
            if (networkAnimator != null && anim != null)
            {
                networkAnimator.Animator = anim;
            }
        }

        if (ragdoll != null) ragdoll.Init();

        if (anim != null)
        {
            anim.Rebind();
            anim.applyRootMotion = false; // Rebind 후에 꺼야 초기화되지 않음
        }
    }

    // ... (이하 Respawn, Update, Move, Look 등 기존과 동일) ...
    // ... (중략 하지 말고 아까 코드 그대로 쓰되 Update에 이거 하나만 추가) ...

    void Update()
    {
        if (!IsSpawned || !IsOwner) return;

        // ★ 핵심 2: 게임 화면 클릭하면 마우스 다시 잡기 (에디터 문제 해결)
        if (Input.GetMouseButtonDown(0))
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (hp.Value <= 0) return;

        Look();
        Move();

        if (Input.GetButtonDown("Fire1")) Shoot();
    }

    // ... (Move, Look, Shoot, TakeDamage, Respawn 등 나머지 함수들은 아까랑 똑같이 유지) ...
    // (복붙 편하게 하라고 아래에 핵심 함수들 다시 넣어줌)

    public void Respawn(int spawnIndex)
    {
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
        controller.Move(move * speed * Time.deltaTime);

        if (anim != null)
        {
            anim.SetFloat("InputX", x);
            anim.SetFloat("InputY", z);
        }

        if (Input.GetButtonDown("Jump") && isGrounded)
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    void Shoot()
    {
        // 무기 발사 체크 (발사속도, 탄약)
        if (weaponController != null && !weaponController.TryShoot()) return;
        
        float range = weaponController != null ? weaponController.GetCurrentRange() : 100f;
        
        RaycastHit hit;
        if (Physics.Raycast(myCam.transform.position, myCam.transform.forward, out hit, range))
        {
            if (hit.transform.CompareTag("Player"))
            {
                var targetScript = hit.transform.GetComponent<PlayerController>();
                if (targetScript != null)
                {
                    if (targetScript.teamId.Value == teamId.Value) return;
                    int damage = weaponController != null ? weaponController.GetCurrentDamage() : 10;
                    SubmitHitServerRpc(targetScript.NetworkObjectId, damage);
                }
            }
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
        hp.Value -= damage;
        if (hp.Value <= 0 && IsServer) RoundGameManager.Instance?.OnPlayerDied(teamId.Value);
    }

    void SetPlayerState(bool isActive)
    {
        if (isActive)
        {
            // 래그돌 먼저 끄기 (뼈 위치 복원)
            if (ragdoll != null) ragdoll.DisableRagdoll();
            
            // 모델 다시 표시
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

