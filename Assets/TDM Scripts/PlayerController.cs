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

    [Header("Audio Settings")]
    public AudioSource audioSource;
    public AudioClip walkSound;
    public AudioClip runSound;
    public AudioClip jumpSound;
    public AudioClip hitSound;
    public AudioClip deathSound;

    private float nextStepTime = 0f;
    private float stepInterval = 0.5f; // 발소리 간격

    private RagdollController ragdoll;
    private WeaponController weaponController;

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
            anim.applyRootMotion = false;
        }
    }

    void Update()
    {
        if (!IsSpawned || !IsOwner) return;

        // 게임 화면 클릭하면 마우스 다시 잡기 (에디터 문제 해결)
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
        if (isGrounded && move.magnitude > 0.1f)
        {
            if (Time.time >= nextStepTime)
            {
                PlayFootstepSound(true); 
                nextStepTime = Time.time + stepInterval;
            }
        }

        controller.Move(move * speed * Time.deltaTime);

        if (anim != null)
        {
            anim.SetFloat("InputX", x);
            anim.SetFloat("InputY", z);
        }

        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            PlayJumpSoundServerRpc();
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    void PlayFootstepSound(bool isWalk)
    {
        PlayStepSoundServerRpc(isWalk);
    }

    [ServerRpc]
    void PlayStepSoundServerRpc(bool isWalk)
    {
        PlayStepSoundClientRpc(isWalk);
    }

    [ClientRpc]
    void PlayStepSoundClientRpc(bool isWalk)
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
