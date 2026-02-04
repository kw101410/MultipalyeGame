using UnityEngine;
using Unity.Netcode;

/// <summary>
/// FPS 뷰 컨트롤러
/// - 내 캐릭터: 1인칭 팔만 보임, 3인칭 모델 숨김
/// - 다른 플레이어: 3인칭 모델만 보임
/// </summary>
public class FPSViewController : NetworkBehaviour
{
    [Header("1인칭 팔 (FPS Arms)")]
    public GameObject fpsArms;  // FPS_Character_prefab
    
    [Header("3인칭 모델")]
    public GameObject redModel;
    public GameObject blueModel;
    
    [Header("카메라")]
    public Camera playerCamera;
    
    [Header("레이어 설정")]
    public LayerMask fpsArmsLayer;      // FPSArms 레이어
    public LayerMask thirdPersonLayer;  // ThirdPerson 레이어
    
    private PlayerController playerController;
    
    void Start()
    {
        playerController = GetComponent<PlayerController>();
        
        // FPS Arms 레이어 설정
        if (fpsArms != null)
        {
            SetLayerRecursively(fpsArms, LayerMask.NameToLayer("FPSArms"));
        }
        
        // 3인칭 모델 레이어는 OnNetworkSpawn에서 설정
    }
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        SetupView();
    }
    
    void SetupView()
    {
        if (IsOwner)
        {
            // 내 캐릭터: FPS Arms 보이기
            if (fpsArms != null) fpsArms.SetActive(true);
            
            // 내 3인칭 모델만 ThirdPerson 레이어로 설정 (내 카메라에서 안 보이게)
            if (redModel != null) SetLayerRecursively(redModel, LayerMask.NameToLayer("ThirdPerson"));
            if (blueModel != null) SetLayerRecursively(blueModel, LayerMask.NameToLayer("ThirdPerson"));
            
            // 카메라 설정: ThirdPerson 안 보이게, FPSArms 보이게
            if (playerCamera != null)
            {
                playerCamera.cullingMask &= ~(1 << LayerMask.NameToLayer("ThirdPerson"));
                playerCamera.cullingMask |= (1 << LayerMask.NameToLayer("FPSArms"));
            }
        }
        else
        {
            // 다른 플레이어: FPS Arms 숨기기
            if (fpsArms != null) fpsArms.SetActive(false);
            
            // 다른 플레이어의 3인칭 모델은 Default 레이어로 유지! (모든 카메라에서 보임)
            // 레이어 변경하지 않음 - 기본 Default 레이어 사용
        }
    }
    
    /// <summary>
    /// 팀 변경 시 호출 - 3인칭 모델 업데이트
    /// </summary>
    public void UpdateThirdPersonModel(int teamId)
    {
        // 3인칭 모델은 PlayerController에서 관리하므로 
        // 여기서는 레이어만 다시 설정
        if (redModel != null && redModel.activeSelf)
        {
            SetLayerRecursively(redModel, LayerMask.NameToLayer("ThirdPerson"));
        }
        if (blueModel != null && blueModel.activeSelf)
        {
            SetLayerRecursively(blueModel, LayerMask.NameToLayer("ThirdPerson"));
        }
    }
    
    /// <summary>
    /// 게임오브젝트와 모든 자식의 레이어 설정
    /// </summary>
    void SetLayerRecursively(GameObject obj, int layer)
    {
        if (obj == null) return;
        
        // 레이어가 유효하지 않으면 스킵 (레이어가 없으면 -1 반환)
        if (layer < 0 || layer > 31)
        {
            Debug.LogWarning($"레이어가 유효하지 않습니다. Edit → Project Settings → Tags and Layers에서 'FPSArms'와 'ThirdPerson' 레이어를 추가하세요!");
            return;
        }
        
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }
}
