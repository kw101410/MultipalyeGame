using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

/// <summary>
/// FPS Arms Animator에 무기별 포즈를 설정하는 에디터 도구
/// GunAnimator.controller에 weaponMode 파라미터와 Sword 스테이트들을 추가합니다.
/// </summary>
public class FPSAnimatorSetup : EditorWindow
{
    [MenuItem("Tools/FPS Animator Setup - 무기별 포즈 추가")]
    static void ShowWindow()
    {
        GetWindow<FPSAnimatorSetup>("FPS Animator Setup");
    }

    void OnGUI()
    {
        GUILayout.Label("FPS Arms Animator에 무기별 포즈 추가", EditorStyles.boldLabel);
        GUILayout.Space(10);
        GUILayout.Label("GunAnimator.controller에 다음을 추가합니다:", EditorStyles.wordWrappedLabel);
        GUILayout.Label("• weaponMode 파라미터 (int: 0=Gun, 1=Sword)");
        GUILayout.Label("• Sword 레이어 (Sword Idle/Walk/Run/Attack 스테이트)");
        GUILayout.Space(10);

        if (GUILayout.Button("설정 실행", GUILayout.Height(40)))
        {
            SetupAnimator();
        }
    }

    static void SetupAnimator()
    {
        // GunAnimator.controller 찾기
        string[] guids = AssetDatabase.FindAssets("GunAnimator t:AnimatorController");
        if (guids.Length == 0)
        {
            Debug.LogError("[FPSAnimatorSetup] GunAnimator.controller를 찾을 수 없습니다!");
            return;
        }

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (controller == null)
        {
            Debug.LogError("[FPSAnimatorSetup] AnimatorController 로드 실패!");
            return;
        }

        // FPS_Character.fbx에서 애니메이션 클립 찾기
        string fbxPath = "Assets/Easy FPS/Model_Animations_Textures/main character/FPS_Character.fbx";
        Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        
        AnimationClip swordIdle = null, swordWalk = null, swordRun = null;
        AnimationClip swordAttack1 = null, swordAttack2 = null, swordAttack3 = null;

        foreach (var asset in allAssets)
        {
            if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
            {
                switch (clip.name)
                {
                    case "Character_Sword_idle": swordIdle = clip; break;
                    case "Character_Sword_Walk": swordWalk = clip; break;
                    case "Character_Sword_Run": swordRun = clip; break;
                    case "Character_Sword_Attack1": swordAttack1 = clip; break;
                    case "Character_Sword_Attack2": swordAttack2 = clip; break;
                    case "Character_Sword_Attack3": swordAttack3 = clip; break;
                }
            }
        }

        if (swordIdle == null)
        {
            Debug.LogError("[FPSAnimatorSetup] Sword 애니메이션 클립을 찾을 수 없습니다!");
            return;
        }

        // 1. weaponMode 파라미터 추가 (이미 있으면 스킵)
        bool hasWeaponMode = false;
        foreach (var param in controller.parameters)
        {
            if (param.name == "weaponMode")
            {
                hasWeaponMode = true;
                break;
            }
        }
        if (!hasWeaponMode)
        {
            controller.AddParameter("weaponMode", AnimatorControllerParameterType.Int);
            Debug.Log("[FPSAnimatorSetup] weaponMode 파라미터 추가됨");
        }
        
        // swordAttack 트리거도 추가
        bool hasSwordAttack = false;
        foreach (var param in controller.parameters)
        {
            if (param.name == "swordAttack")
            {
                hasSwordAttack = true;
                break;
            }
        }
        if (!hasSwordAttack)
        {
            controller.AddParameter("swordAttack", AnimatorControllerParameterType.Trigger);
            Debug.Log("[FPSAnimatorSetup] swordAttack 트리거 추가됨");
        }

        // 2. 기존 Base Layer에 weaponMode == 0 조건 추가하는 대신
        //    새로운 Sword 레이어를 만들어서 weaponMode로 제어

        // 기존 Sword 레이어가 있으면 제거
        for (int i = controller.layers.Length - 1; i >= 0; i--)
        {
            if (controller.layers[i].name == "Sword Layer")
            {
                controller.RemoveLayer(i);
                Debug.Log("[FPSAnimatorSetup] 기존 Sword Layer 제거됨");
            }
        }

        // Sword 레이어 추가
        controller.AddLayer("Sword Layer");
        int swordLayerIndex = controller.layers.Length - 1;
        
        // 레이어 설정
        var layers = controller.layers;
        layers[swordLayerIndex].defaultWeight = 0f;  // 코드에서 칼 장착 시에만 1로 변경
        layers[swordLayerIndex].blendingMode = AnimatorLayerBlendingMode.Override;
        controller.layers = layers;
        
        AnimatorStateMachine swordSM = controller.layers[swordLayerIndex].stateMachine;

        // 빈 스테이트 (weaponMode == 0일 때 = 총 모드 → Base Layer가 재생됨)
        var emptyState = swordSM.AddState("Empty (Gun Mode)", new Vector3(300, 0, 0));
        emptyState.motion = null; // 모션 없음
        swordSM.defaultState = emptyState;

        // Sword Idle 스테이트
        var sIdle = swordSM.AddState("Sword_Idle", new Vector3(300, 120, 0));
        sIdle.motion = swordIdle;

        // Sword Walk 스테이트
        AnimatorState sWalk = null;
        if (swordWalk != null)
        {
            sWalk = swordSM.AddState("Sword_Walk", new Vector3(540, 120, 0));
            sWalk.motion = swordWalk;
            sWalk.speed = 2f;
        }

        // Sword Run 스테이트
        AnimatorState sRun = null;
        if (swordRun != null)
        {
            sRun = swordSM.AddState("Sword_Run", new Vector3(780, 120, 0));
            sRun.motion = swordRun;
        }

        // Sword Attack 스테이트
        AnimatorState sAttack = null;
        if (swordAttack1 != null)
        {
            sAttack = swordSM.AddState("Sword_Attack", new Vector3(540, 240, 0));
            sAttack.motion = swordAttack1;
        }

        // === 트랜지션 설정 ===

        // Empty → Sword Idle (weaponMode == 1)
        var toSword = emptyState.AddTransition(sIdle);
        toSword.AddCondition(AnimatorConditionMode.Equals, 1, "weaponMode");
        toSword.duration = 0.15f;
        toSword.hasExitTime = false;

        // Sword Idle → Empty (weaponMode == 0)
        var toGun = sIdle.AddTransition(emptyState);
        toGun.AddCondition(AnimatorConditionMode.NotEqual, 1, "weaponMode");
        toGun.duration = 0.15f;
        toGun.hasExitTime = false;

        // Sword Idle ↔ Sword Walk
        if (sWalk != null)
        {
            var idleToWalk = sIdle.AddTransition(sWalk);
            idleToWalk.AddCondition(AnimatorConditionMode.Greater, 1, "walkSpeed");
            idleToWalk.AddCondition(AnimatorConditionMode.Less, 5, "maxSpeed");
            idleToWalk.duration = 0.2f;
            idleToWalk.hasExitTime = false;

            var walkToIdle = sWalk.AddTransition(sIdle);
            walkToIdle.AddCondition(AnimatorConditionMode.Less, 1, "walkSpeed");
            walkToIdle.duration = 0.2f;
            walkToIdle.hasExitTime = false;
            
            // Walk → Empty (weaponMode != 1)
            var walkToGun = sWalk.AddTransition(emptyState);
            walkToGun.AddCondition(AnimatorConditionMode.NotEqual, 1, "weaponMode");
            walkToGun.duration = 0.15f;
            walkToGun.hasExitTime = false;
        }

        // Sword Walk ↔ Sword Run
        if (sWalk != null && sRun != null)
        {
            var walkToRun = sWalk.AddTransition(sRun);
            walkToRun.AddCondition(AnimatorConditionMode.Greater, 4, "maxSpeed");
            walkToRun.duration = 0.2f;
            walkToRun.hasExitTime = false;

            var runToWalk = sRun.AddTransition(sWalk);
            runToWalk.AddCondition(AnimatorConditionMode.Less, 5, "maxSpeed");
            runToWalk.duration = 0.2f;
            runToWalk.hasExitTime = false;
            
            // Run → Empty (weaponMode != 1)
            var runToGun = sRun.AddTransition(emptyState);
            runToGun.AddCondition(AnimatorConditionMode.NotEqual, 1, "weaponMode");
            runToGun.duration = 0.15f;
            runToGun.hasExitTime = false;
        }

        // Sword Idle → Sword Attack (swordAttack 트리거)
        if (sAttack != null)
        {
            var idleToAttack = sIdle.AddTransition(sAttack);
            idleToAttack.AddCondition(AnimatorConditionMode.If, 0, "swordAttack");
            idleToAttack.duration = 0.1f;
            idleToAttack.hasExitTime = false;

            // Attack → Sword Idle (애니메이션 끝나면)
            var attackToIdle = sAttack.AddTransition(sIdle);
            attackToIdle.hasExitTime = true;
            attackToIdle.exitTime = 0.9f;
            attackToIdle.duration = 0.15f;
            
            // Attack → Empty (weaponMode != 1)
            var attackToGun = sAttack.AddTransition(emptyState);
            attackToGun.AddCondition(AnimatorConditionMode.NotEqual, 1, "weaponMode");
            attackToGun.duration = 0.15f;
            attackToGun.hasExitTime = false;
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        
        Debug.Log("[FPSAnimatorSetup] ✅ 완료! Sword Layer가 GunAnimator에 추가되었습니다.");
        Debug.Log("[FPSAnimatorSetup] weaponMode: 0=Gun, 1=Sword");
    }
}
