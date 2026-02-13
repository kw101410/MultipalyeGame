using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

/// <summary>
/// GunAnimator(FPS Arms)에 Sword Layer와 swordAttack 트리거를 자동 추가
/// 1인칭 칼 공격 애니메이션 설정
/// </summary>
[InitializeOnLoad]
public class FPSArmsSwordSetup : Editor
{
    static FPSArmsSwordSetup()
    {
        if (EditorPrefs.GetBool("FPSArmsSwordSetup_Done_V5", false)) return;
        
        Setup();
        
        EditorPrefs.SetBool("FPSArmsSwordSetup_Done_V5", true);
    }

    [MenuItem("Tools/Animation/Setup FPS Arms Sword Layer")]
    static void ForceSetup()
    {
        EditorPrefs.DeleteKey("FPSArmsSwordSetup_Done_V5");
        Setup();
        EditorPrefs.SetBool("FPSArmsSwordSetup_Done_V5", true);
    }

    static void Setup()
    {
        string controllerPath = "Assets/Easy FPS/GunAnimator.controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);

        if (controller == null)
        {
            Debug.LogError("[FPSSword] GunAnimator.controller를 찾을 수 없습니다!");
            return;
        }

        // 1. swordAttack 트리거 파라미터 추가
        bool hasTrigger = false;
        foreach (var p in controller.parameters)
        {
            if (p.name == "swordAttack") { hasTrigger = true; break; }
        }
        if (!hasTrigger)
        {
            controller.AddParameter("swordAttack", AnimatorControllerParameterType.Trigger);
            Debug.Log("[FPSSword] swordAttack 트리거 추가됨");
        }

        // 2. Sword Layer 찾기 또는 생성
        int swordLayerIdx = -1;
        var layers = controller.layers;
        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i].name == "Sword Layer")
            {
                swordLayerIdx = i;
                break;
            }
        }

        // 기존 Sword Layer가 있으면 삭제 후 재생성
        if (swordLayerIdx >= 0)
        {
            controller.RemoveLayer(swordLayerIdx);
        }

        // 3. 칼 공격 및 Idle 애니메이션 찾기
        AnimationClip swordClip = null;
        AnimationClip swordIdleClip = null;
        
        // FPS_Character.fbx에서 Character_Malee 또는 공격 관련 클립 찾기
        string fbxPath = "Assets/Easy FPS/Model_Animations_Textures/main character/FPS_Character.fbx";
        Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        
        if (allAssets != null)
        {
            foreach (var asset in allAssets)
            {
                if (asset is AnimationClip clip && !clip.name.Contains("__preview__"))
                {
                    Debug.Log("[FPSSword] FBX 클립 발견: " + clip.name);
                    
                    string lower = clip.name.ToLower();
                    
                    // Attack 찾기
                    if (swordClip == null)
                    {
                        // 사용자가 지정한 'Character_Sword_Attack2' 우선
                        if (clip.name == "Character_Sword_Attack2" || 
                            clip.name == "Character_Sword_Attack1" || 
                            clip.name == "Character_Malee" || 
                            lower.Contains("malee") || 
                            lower.Contains("melee") || lower.Contains("sword") || 
                            lower.Contains("stab") || lower.Contains("slash"))
                        {
                            swordClip = clip;
                            Debug.Log("[FPSSword] 칼 공격 클립 선택: " + clip.name);
                        }
                    }
                    
                    // Idle 찾기
                    if (swordIdleClip == null)
                    {
                        if (clip.name == "Character_Sword_Idle" || 
                            (lower.Contains("sword") && lower.Contains("idle")))
                        {
                            swordIdleClip = clip;
                            Debug.Log("[FPSSword] 칼 Idle 클립 선택: " + clip.name);
                        }
                    }
                }
            }
        }
        
        // FPS_Character.fbx에서 못 찾으면 Stabbing.fbx 시도
        if (swordClip == null)
        {
            // ... (기존 Stabbing 로직 생략, 필요하면 추가)
        }

        if (swordClip == null)
        {
            Debug.LogError("[FPSSword] 칼 공격 애니메이션을 찾을 수 없습니다!");
            return;
        }

        // 4. Sword Layer 생성
        AnimatorControllerLayer swordLayer = new AnimatorControllerLayer();
        swordLayer.name = "Sword Layer";
        swordLayer.defaultWeight = 0f; // 기본은 꺼져있음 (칼 장착 시 코드에서 1로 변경)
        swordLayer.blendingMode = AnimatorLayerBlendingMode.Override;
        
        swordLayer.stateMachine = new AnimatorStateMachine();
        swordLayer.stateMachine.name = "Sword Layer";
        swordLayer.stateMachine.hideFlags = HideFlags.HideInHierarchy;
        
        AssetDatabase.AddObjectToAsset(swordLayer.stateMachine, controller);
        controller.AddLayer(swordLayer);

        layers = controller.layers;
        swordLayerIdx = layers.Length - 1;
        var stateMachine = layers[swordLayerIdx].stateMachine;

        // 5. 스테이트 생성
        AnimatorState idleState = stateMachine.AddState("Sword Idle", new Vector3(250, 0, 0));
        
        if (swordIdleClip != null)
        {
            idleState.motion = swordIdleClip;
            Debug.Log("[FPSSword] Sword Idle 상태에 모션 할당됨: " + swordIdleClip.name);
        }
        else
        {
            // Idle 모션이 없으면 Attack 모션의 첫 프레임을 Idle로 사용 (멈춤)
            // 안 그러면 Base Layer의 총 든 자세가 보일 수 있음
            idleState.motion = swordClip;
            idleState.speed = 0f; 
            Debug.Log("[FPSSword] Sword Idle 모션 못 찾음 -> Attack 모션(Speed 0)으로 대체");
        }
        
        AnimatorState attackState = stateMachine.AddState("Sword Attack", new Vector3(250, 80, 0));
        attackState.motion = swordClip;
        
        stateMachine.defaultState = idleState;

        // 6. 트랜지션: Idle → Attack (swordAttack 트리거)
        var attackTrans = idleState.AddTransition(attackState);
        attackTrans.hasExitTime = false;
        attackTrans.duration = 0.05f;
        attackTrans.AddCondition(AnimatorConditionMode.If, 0, "swordAttack");

        // Attack → Idle (애니메이션 끝나면 자동 복귀)
        var returnTrans = attackState.AddTransition(idleState);
        returnTrans.hasExitTime = true;
        returnTrans.exitTime = 0.9f;
        returnTrans.duration = 0.1f;

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        Debug.Log("[FPSSword] ✅ GunAnimator에 Sword Layer 추가 완료!");
    }
}
