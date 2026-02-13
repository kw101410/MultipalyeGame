using UnityEngine;
using Unity.Netcode;

/// <summary>
/// [임시 비활성화됨]
/// 업계 표준 FPS 3인칭 상체 동기화
/// 필요시 enabled = true로 활성화
/// </summary>
public class ThirdPersonSpineSync : NetworkBehaviour
{
    [Header("기능 활성화")]
    public bool enableSync = false;  // 기본 비활성화!
    
    [Header("동기화할 Spine 본들")]
    public Transform[] spineBones;
    
    [Header("회전 설정")]
    [Range(0f, 1f)]
    public float totalWeight = 0.7f;
    
    [Range(-90f, 90f)]
    public float minPitch = -40f;
    
    [Range(-90f, 90f)]
    public float maxPitch = 40f;
    
    [Header("부드러움")]
    public float smoothSpeed = 10f;
    
    private NetworkVariable<float> syncedPitch = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );
    
    private float currentPitch = 0f;
    private PlayerController playerController;
    private Animator currentAnimator;
    
    // 최적화: spineBones.Length 캐싱
    private int spineBoneCount = 0;
    
    void Awake()
    {
        playerController = GetComponent<PlayerController>();
        // 최적화: enableSync가 false이면 컴포넌트 자체를 비활성화 (Update/LateUpdate 호출 자체를 차단)
        if (!enableSync) enabled = false;
    }
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (enableSync) RefreshSpineBones();
    }
    
    public void RefreshSpineBones()
    {
        if (!enableSync) return;
        
        GameObject activeModel = GetActiveModel();
        if (activeModel == null) return;
        
        Animator animator = activeModel.GetComponent<Animator>();
        if (animator == null) animator = activeModel.GetComponentInChildren<Animator>();
        if (animator == null) return;
        
        currentAnimator = animator;
        
        var bones = new System.Collections.Generic.List<Transform>();
        
        Transform spine = animator.GetBoneTransform(HumanBodyBones.Spine);
        Transform chest = animator.GetBoneTransform(HumanBodyBones.Chest);
        Transform upperChest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
        
        if (spine != null) bones.Add(spine);
        if (chest != null) bones.Add(chest);
        if (upperChest != null) bones.Add(upperChest);
        
        spineBones = bones.ToArray();
        spineBoneCount = spineBones.Length;
    }
    
    GameObject GetActiveModel()
    {
        if (playerController == null) return null;
        
        if (playerController.redModel != null && playerController.redModel.activeSelf)
            return playerController.redModel;
        if (playerController.blueModel != null && playerController.blueModel.activeSelf)
            return playerController.blueModel;
            
        return null;
    }
    
    void Update()
    {
        // 최적화: enableSync가 false이면 Awake에서 enabled=false로 설정했으므로 여기까지 오지 않음
        if (!IsSpawned) return;
        
        if (IsOwner)
        {
            UpdatePitchFromCamera();
        }
    }
    
    void LateUpdate()
    {
        if (!IsSpawned) return;
        
        ApplySpineRotation();
    }
    
    void UpdatePitchFromCamera()
    {
        if (playerController == null || playerController.myCam == null) return;
        
        float cameraPitch = playerController.myCam.transform.localEulerAngles.x;
        if (cameraPitch > 180f) cameraPitch -= 360f;
        cameraPitch = Mathf.Clamp(cameraPitch, minPitch, maxPitch);
        
        if (Mathf.Abs(syncedPitch.Value - cameraPitch) > 0.5f)
        {
            syncedPitch.Value = cameraPitch;
        }
    }
    
    void ApplySpineRotation()
    {
        if (spineBoneCount == 0) return;
        
        currentPitch = Mathf.Lerp(currentPitch, syncedPitch.Value, smoothSpeed * Time.deltaTime);
        float rotationPerBone = currentPitch * totalWeight / spineBoneCount;
        
        for (int i = 0; i < spineBoneCount; i++)
        {
            if (spineBones[i] == null) continue;
            spineBones[i].localRotation = spineBones[i].localRotation * 
                                          Quaternion.Euler(rotationPerBone, 0f, 0f);
        }
    }
}
