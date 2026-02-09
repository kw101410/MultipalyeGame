using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

/// <summary>
/// 상체/하체 애니메이션 분리 설정 도구
/// 무기별로 다른 상체 애니메이션 적용
/// 메뉴: Tools > Animation > Setup Upper Body Layer
/// </summary>
public class UpperBodyLayerSetup : Editor
{
    [MenuItem("Tools/Animation/Setup Upper Body Layer")]
    static void SetupUpperBodyLayer()
    {
        // 1. Avatar Mask 생성
        string maskPath = "Assets/Animation/UpperBodyMask.mask";
        AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(maskPath);
        
        if (mask == null)
        {
            mask = new AvatarMask();
            
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Root, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFootIK, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFootIK, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftHandIK, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightHandIK, true);
            
            AssetDatabase.CreateAsset(mask, maskPath);
            Debug.Log("UpperBodyMask 생성됨!");
        }
        
        // 2. Animator Controller 찾기
        string controllerPath = "Assets/Animation/PlayerAnimator.controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        
        if (controller == null)
        {
            Debug.LogError("PlayerAnimator.controller를 찾을 수 없습니다!");
            return;
        }
        
        // 3. WeaponSlot 파라미터 추가 (없으면)
        bool hasWeaponSlot = false;
        foreach (var param in controller.parameters)
        {
            if (param.name == "WeaponSlot")
            {
                hasWeaponSlot = true;
                break;
            }
        }
        if (!hasWeaponSlot)
        {
            controller.AddParameter("WeaponSlot", AnimatorControllerParameterType.Int);
            Debug.Log("WeaponSlot 파라미터 추가됨!");
        }
        
        // 4. Upper Body 레이어 찾기 또는 생성
        int upperBodyLayerIndex = -1;
        var layers = controller.layers;
        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i].name == "Upper Body")
            {
                upperBodyLayerIndex = i;
                break;
            }
        }
        
        AnimatorStateMachine stateMachine;
        
        if (upperBodyLayerIndex == -1)
        {
            // 새 레이어 추가
            AnimatorControllerLayer upperBodyLayer = new AnimatorControllerLayer();
            upperBodyLayer.name = "Upper Body";
            upperBodyLayer.defaultWeight = 1f;
            upperBodyLayer.avatarMask = mask;
            upperBodyLayer.blendingMode = AnimatorLayerBlendingMode.Override;
            
            upperBodyLayer.stateMachine = new AnimatorStateMachine();
            upperBodyLayer.stateMachine.name = "Upper Body";
            upperBodyLayer.stateMachine.hideFlags = HideFlags.HideInHierarchy;
            
            AssetDatabase.AddObjectToAsset(upperBodyLayer.stateMachine, controller);
            controller.AddLayer(upperBodyLayer);
            
            layers = controller.layers;
            upperBodyLayerIndex = layers.Length - 1;
            Debug.Log("Upper Body 레이어 추가됨!");
        }
        
        stateMachine = layers[upperBodyLayerIndex].stateMachine;
        
        // 5. 스테이트들 생성 (Empty, Pistol, Rifle, Knife)
        AnimatorState emptyState = FindOrCreateState(stateMachine, "Empty", null);
        
        // Pistol Idle 애니메이션 찾기
        AnimationClip pistolIdle = FindAnimationClip("Assets/Animation/pistol/Pistol Idle.fbx");
        AnimatorState pistolState = FindOrCreateState(stateMachine, "Pistol Idle", pistolIdle);
        
        // Rifle Idle 애니메이션 찾기 (있으면)
        AnimationClip rifleIdle = FindAnimationClip("Assets/Animation/rifel/Rifle Idle.fbx");
        AnimatorState rifleState = FindOrCreateState(stateMachine, "Rifle Idle", rifleIdle);
        
        // 기본 스테이트 설정
        stateMachine.defaultState = emptyState;
        
        // 6. 트랜지션 설정
        // Empty -> Pistol (WeaponSlot == 1)
        AddTransitionIfNotExists(emptyState, pistolState, "WeaponSlot", 1);
        // Empty -> Rifle (WeaponSlot == 0)  
        AddTransitionIfNotExists(emptyState, rifleState, "WeaponSlot", 0);
        
        // Pistol -> Empty (WeaponSlot != 1)
        AddExitTransition(pistolState, emptyState, "WeaponSlot", 1);
        // Rifle -> Empty (WeaponSlot != 0)
        AddExitTransition(rifleState, emptyState, "WeaponSlot", 0);
        
        // Pistol <-> Rifle
        AddTransitionIfNotExists(pistolState, rifleState, "WeaponSlot", 0);
        AddTransitionIfNotExists(rifleState, pistolState, "WeaponSlot", 1);
        
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        
        Debug.Log("✅ Upper Body Layer 설정 완료!");
        Debug.Log("WeaponSlot 값: 0=Primary(Rifle), 1=Secondary(Pistol), 2=Melee(Knife)");
    }
    
    static AnimationClip FindAnimationClip(string path)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        foreach (var asset in assets)
        {
            if (asset is AnimationClip clip && !clip.name.Contains("__preview__"))
            {
                return clip;
            }
        }
        return null;
    }
    
    static AnimatorState FindOrCreateState(AnimatorStateMachine stateMachine, string name, AnimationClip clip)
    {
        foreach (var state in stateMachine.states)
        {
            if (state.state.name == name)
            {
                return state.state;
            }
        }
        
        var newState = stateMachine.AddState(name);
        if (clip != null)
        {
            newState.motion = clip;
        }
        return newState;
    }
    
    static void AddTransitionIfNotExists(AnimatorState from, AnimatorState to, string param, int value)
    {
        foreach (var t in from.transitions)
        {
            if (t.destinationState == to) return;
        }
        
        var transition = from.AddTransition(to);
        transition.hasExitTime = false;
        transition.duration = 0.1f;
        transition.AddCondition(AnimatorConditionMode.Equals, value, param);
    }
    
    static void AddExitTransition(AnimatorState from, AnimatorState to, string param, int notValue)
    {
        foreach (var t in from.transitions)
        {
            if (t.destinationState == to) return;
        }
        
        var transition = from.AddTransition(to);
        transition.hasExitTime = false;
        transition.duration = 0.1f;
        transition.AddCondition(AnimatorConditionMode.NotEqual, notValue, param);
    }
}
