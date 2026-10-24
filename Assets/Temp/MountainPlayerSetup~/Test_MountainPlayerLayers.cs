using System.Linq;
using Cheon.Testing.PlayerFan;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class Test_MountainPlayerLayers
{
    public static object Main()
    {
        const string folder = "Assets/Test/CheonMountainMap/Animations";
        const string controllerPath = folder + "/Test_MountainPlayer.controller";
        const string maskPath = folder + "/Test_MountainPlayerUpperBody.mask";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Test/CheonMountainMap", "Animations");
        var control = UnityEngine.Object.FindFirstObjectByType<Test_PlayerFanAnimationController>();
        if (Application.isPlaying || control == null) throw new System.InvalidOperationException("맵의 Edit 모드에서 실행하세요.");
        var animator = control.CharacterAnimator;
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) == null)
            AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(animator.runtimeAnimatorController), controllerPath);
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(maskPath);
        if (mask == null)
        {
            mask = new AvatarMask { name = "Test_MountainPlayerUpperBody" };
            AssetDatabase.CreateAsset(mask, maskPath);
        }
        for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++) mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
        foreach (var part in new[] { AvatarMaskBodyPart.Body, AvatarMaskBodyPart.Head, AvatarMaskBodyPart.LeftArm,
            AvatarMaskBodyPart.RightArm, AvatarMaskBodyPart.LeftFingers, AvatarMaskBodyPart.RightFingers })
            mask.SetHumanoidBodyPartActive(part, true);

        if (!controller.layers.Any(layer => layer.name == "EquipmentUpperBody"))
        {
            var machine = new AnimatorStateMachine { name = "Test_EquipmentUpperBody" };
            AssetDatabase.AddObjectToAsset(machine, controller);
            var empty = machine.AddState("EMPTY");
            empty.writeDefaultValues = false;
            machine.defaultState = empty;
            foreach (string stateName in new[] { "DRAW", "SHEATHE" })
            {
                var original = controller.layers[0].stateMachine.states.First(s => s.state.name == stateName).state;
                var state = machine.AddState(stateName);
                state.motion = original.motion;
                state.speed = original.speed;
                state.speedParameter = original.speedParameter;
                state.speedParameterActive = original.speedParameterActive;
                state.writeDefaultValues = false;
            }
            controller.AddLayer(new AnimatorControllerLayer
            {
                name = "EquipmentUpperBody", stateMachine = machine, avatarMask = mask,
                defaultWeight = 0f, blendingMode = AnimatorLayerBlendingMode.Override, syncedLayerIndex = -1
            });
        }
        animator.runtimeAnimatorController = controller;
        control.ConfigureMapPlayback(control.SavedTimings, true);
        animator.Rebind();
        animator.Play("SHEATHED_IDLE", 0, 0f);
        animator.SetLayerWeight(1, 0f);
        animator.Update(0f);
        EditorUtility.SetDirty(mask);
        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(animator);
        EditorUtility.SetDirty(control);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        return new { controller = controllerPath, mask = maskPath,
            layers = controller.layers.Select(l => new { l.name, weight = l.defaultWeight, states = l.stateMachine.states.Select(s => s.state.name).ToArray() }).ToArray() };
    }
}
