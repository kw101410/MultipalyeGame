using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

/// <summary>
/// 무기 선택 UI 컨트롤러
/// - B키로 패널 열기/닫기
/// - Rifel, SNiperRifel 버튼으로 무기 선택
/// </summary>
public class WeaponSelectUI : MonoBehaviour
{
    [Header("UI References")]
    public GameObject weaponSelectPanel;  // 무기 선택 패널
    public Button rifleButton;            // Rifel 버튼
    public Button sniperRifleButton;      // SNiperRifel 버튼
    
    [Header("Settings")]
    public KeyCode openKey = KeyCode.B;   // 패널 여는 키
    
    private WeaponController localPlayerWeapon;
    private bool isPanelOpen = false;
    
    void Start()
    {
        // 패널 시작 시 닫기
        if (weaponSelectPanel != null)
        {
            weaponSelectPanel.SetActive(false);
        }
        
        // 버튼 이벤트 연결
        if (rifleButton != null)
        {
            rifleButton.onClick.AddListener(OnRifleSelected);
        }
        
        if (sniperRifleButton != null)
        {
            sniperRifleButton.onClick.AddListener(OnSniperRifleSelected);
        }
    }
    
    void Update()
    {
        // B키로 패널 열기/닫기
        if (Input.GetKeyDown(openKey))
        {
            TogglePanel();
        }
        
        // ESC키로 패널 닫기
        if (isPanelOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            ClosePanel();
        }
    }
    
    /// <summary>
    /// 패널 열기/닫기 토글
    /// </summary>
    public void TogglePanel()
    {
        if (isPanelOpen)
        {
            ClosePanel();
        }
        else
        {
            OpenPanel();
        }
    }
    
    /// <summary>
    /// 패널 열기
    /// </summary>
    public void OpenPanel()
    {
        // 로컬 플레이어의 WeaponController 찾기
        FindLocalPlayerWeapon();
        
        if (localPlayerWeapon == null)
        {
            Debug.LogWarning("로컬 플레이어의 WeaponController를 찾을 수 없습니다.");
            return;
        }
        
        if (weaponSelectPanel != null)
        {
            weaponSelectPanel.SetActive(true);
            isPanelOpen = true;
            
            // 마우스 커서 보이기
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
    
    /// <summary>
    /// 패널 닫기
    /// </summary>
    public void ClosePanel()
    {
        if (weaponSelectPanel != null)
        {
            weaponSelectPanel.SetActive(false);
            isPanelOpen = false;
            
            // 마우스 커서 다시 숨기기
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
    
    /// <summary>
    /// 라이플 선택
    /// </summary>
    public void OnRifleSelected()
    {
        if (localPlayerWeapon != null)
        {
            localPlayerWeapon.SelectPrimaryWeapon(PrimaryWeaponType.Rifle);
            Debug.Log("라이플 선택됨!");
        }
        ClosePanel();
    }
    
    /// <summary>
    /// 스나이퍼 라이플 선택
    /// </summary>
    public void OnSniperRifleSelected()
    {
        if (localPlayerWeapon != null)
        {
            localPlayerWeapon.SelectPrimaryWeapon(PrimaryWeaponType.Sniper);
            Debug.Log("스나이퍼 라이플 선택됨!");
        }
        ClosePanel();
    }
    
    /// <summary>
    /// 로컬 플레이어의 WeaponController 찾기
    /// </summary>
    private void FindLocalPlayerWeapon()
    {
        if (localPlayerWeapon != null) return;
        
        // 모든 플레이어 중에서 로컬 플레이어 찾기
        foreach (var player in FindObjectsOfType<PlayerController>())
        {
            if (player.IsOwner)
            {
                localPlayerWeapon = player.GetComponent<WeaponController>();
                break;
            }
        }
    }
    
    /// <summary>
    /// 패널이 열려있는지 확인
    /// </summary>
    public bool IsPanelOpen()
    {
        return isPanelOpen;
    }
}
