using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

/// <summary>
/// Base Layer 정리: 이전에 추가했던 Attack 상태를 제거합니다.
/// 3인칭 공격은 Upper Body 레이어에서만 처리합니다.
/// </summary>
[InitializeOnLoad]
public class AutoSetupAnimator
{
    static AutoSetupAnimator()
    {
        if (EditorPrefs.GetBool("AutoSetupAnimator_Cleanup_V1", false)) return;
        
        CleanupBaseLayer();
        
        EditorPrefs.SetBool("AutoSetupAnimator_Cleanup_V1", true);
    }

    static void CleanupBaseLayer()
    {
        string controllerPath = "Assets/Animation/PlayerAnimator.controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);

        if (controller == null) return;

        // Base Layer에서 Attack 상태 제거 (있으면)
        var rootStateMachine = controller.layers[0].stateMachine;
        var states = rootStateMachine.states;
        
        for (int i = states.Length - 1; i >= 0; i--)
        {
            if (states[i].state.name == "Attack")
            {
                rootStateMachine.RemoveState(states[i].state);
                Debug.Log("[AutoSetup] ✅ Base Layer에서 Attack 상태 제거 완료 (Upper Body에서만 처리)");
            }
        }
        
        // AnyState -> Attack 트랜지션도 제거
        var anyTransitions = rootStateMachine.anyStateTransitions;
        for (int i = anyTransitions.Length - 1; i >= 0; i--)
        {
            if (anyTransitions[i].destinationState != null && 
                anyTransitions[i].destinationState.name == "Attack")
            {
                rootStateMachine.RemoveAnyStateTransition(anyTransitions[i]);
            }
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
    }
}
