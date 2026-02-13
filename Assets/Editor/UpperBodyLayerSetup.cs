using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

/// <summary>
/// Upper Body 레이어 자동 설정 (컴파일 시 자동 실행)
/// Avatar Mask 없이 Full Body Override로 무기별 상체 애니메이션 전환
/// </summary>
[InitializeOnLoad]
public class UpperBodyLayerSetup : Editor
{
    static UpperBodyLayerSetup()
    {
        if (EditorPrefs.GetBool("UpperBodyLayerSetup_Done_V3", false)) return;
        
        SetupUpperBodyLayer();
        
        EditorPrefs.SetBool("UpperBodyLayerSetup_Done_V3", true);
    }

    [MenuItem("Tools/Animation/Setup Upper Body Layer")]
    static void SetupUpperBodyLayer()
    {
        string controllerPath = "Assets/Animation/PlayerAnimator.controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        
        if (controller == null)
        {
            Debug.LogError("[UpperBodySetup] PlayerAnimator.controller를 찾을 수 없습니다!");
            return;
        }
        
        // 파라미터 추가
        AddParameterIfNotExists(controller, "WeaponSlot", AnimatorControllerParameterType.Int);
        AddParameterIfNotExists(controller, "Attack", AnimatorControllerParameterType.Trigger);
        
        // 기존 Upper Body 레이어 삭제
        var layers = controller.layers;
        for (int i = layers.Length - 1; i >= 0; i--)
        {
            if (layers[i].name == "Upper Body")
            {
                controller.RemoveLayer(i);
            }
        }
        
        // 새 레이어 추가 (마스크 없음 = Full Body Override)
        AnimatorControllerLayer upperBodyLayer = new AnimatorControllerLayer();
        upperBodyLayer.name = "Upper Body";
        upperBodyLayer.defaultWeight = 1f;
        upperBodyLayer.avatarMask = null; // ★ 마스크 없음! Generic Rig 호환
        upperBodyLayer.blendingMode = AnimatorLayerBlendingMode.Override;
        
        upperBodyLayer.stateMachine = new AnimatorStateMachine();
        upperBodyLayer.stateMachine.name = "Upper Body";
        upperBodyLayer.stateMachine.hideFlags = HideFlags.HideInHierarchy;
        
        AssetDatabase.AddObjectToAsset(upperBodyLayer.stateMachine, controller);
        controller.AddLayer(upperBodyLayer);
        
        layers = controller.layers;
        int upperBodyLayerIndex = layers.Length - 1;
        var stateMachine = layers[upperBodyLayerIndex].stateMachine;
        
        // 스테이트 생성
        AnimationClip rifleIdle = FindAnimationClip("Assets/Animation/rifel/Rifle Idle.fbx");
        AnimatorState rifleState = stateMachine.AddState("Rifle Idle", new Vector3(250, 0, 0));
        if (rifleIdle != null) rifleState.motion = rifleIdle;
        
        AnimationClip pistolIdle = FindAnimationClip("Assets/Animation/pistol/Pistol Idle.fbx");
        AnimatorState pistolState = stateMachine.AddState("Pistol Idle", new Vector3(250, 80, 0));
        if (pistolIdle != null) pistolState.motion = pistolIdle;
        
        AnimationClip knifeIdle = FindAnimationClip("Assets/Animation/knife/Knife Idle.fbx");
        AnimatorState knifeState = stateMachine.AddState("Knife Idle", new Vector3(250, 160, 0));
        if (knifeIdle != null) knifeState.motion = knifeIdle;
        
        AnimationClip knifeAttack = FindAnimationClip("Assets/Animation/knife/Stabbing.fbx");
        AnimatorState knifeAttackState = stateMachine.AddState("Knife Attack", new Vector3(500, 160, 0));
        if (knifeAttack != null) knifeAttackState.motion = knifeAttack;
        
        stateMachine.defaultState = rifleState;
        
        // 무기 전환 트랜지션 (WeaponSlot: 0=Primary, 1=Secondary, 2=Melee)
        AddWeaponTransition(pistolState, rifleState, 0);
        AddWeaponTransition(knifeState, rifleState, 0);
        AddWeaponTransition(knifeAttackState, rifleState, 0);
        
        AddWeaponTransition(rifleState, pistolState, 1);
        AddWeaponTransition(knifeState, pistolState, 1);
        AddWeaponTransition(knifeAttackState, pistolState, 1);
        
        AddWeaponTransition(rifleState, knifeState, 2);
        AddWeaponTransition(pistolState, knifeState, 2);
        
        // 칼 공격 트랜지션
        var attackTrans = knifeState.AddTransition(knifeAttackState);
        attackTrans.hasExitTime = false;
        attackTrans.duration = 0.05f;
        attackTrans.AddCondition(AnimatorConditionMode.If, 0, "Attack");
        
        var returnTrans = knifeAttackState.AddTransition(knifeState);
        returnTrans.hasExitTime = true;
        returnTrans.exitTime = 0.9f;
        returnTrans.duration = 0.1f;
        
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        
        Debug.Log("[UpperBodySetup] ✅ Upper Body Layer 설정 완료! (마스크 없음, Full Body Override)");
    }
    
    static AnimationClip FindAnimationClip(string path)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        if (assets == null) return null;
        foreach (var asset in assets)
        {
            if (asset is AnimationClip clip && !clip.name.Contains("__preview__"))
            {
                return clip;
            }
        }
        return null;
    }
    
    static void AddWeaponTransition(AnimatorState from, AnimatorState to, int weaponSlotValue)
    {
        var transition = from.AddTransition(to);
        transition.hasExitTime = false;
        transition.duration = 0.1f;
        transition.AddCondition(AnimatorConditionMode.Equals, weaponSlotValue, "WeaponSlot");
    }
    
    static void AddParameterIfNotExists(AnimatorController controller, string name, AnimatorControllerParameterType type)
    {
        foreach (var param in controller.parameters)
        {
            if (param.name == name) return;
        }
        controller.AddParameter(name, type);
    }
}
