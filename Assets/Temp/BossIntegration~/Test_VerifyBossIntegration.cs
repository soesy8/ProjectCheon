using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Cheon.Prototype.MountainPass;

public static class Test_VerifyBossIntegration
{
    public static object Main()
    {
        var setup = EditorSceneManager.GetSceneManagerSetup();
        var results = new List<object>();
        foreach (string path in new[] { "Assets/Scenes/MountainPass/MountainPass.unity", "Assets/Test/FanAttackVfx/Scenes/Test_FanAttackVfx.unity" })
        {
            var scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            var components = scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
            if (components.Any(c => c == null)) throw new Exception("Missing scripts: " + path);
            var settings = components.OfType<PassBossPatternSettings>().Single();
            var boss = components.OfType<PassBossController>().Single();
            var combat = components.OfType<PassPlayerCombat>().Single();
            var flow = components.OfType<PassGameFlow>().Single();
            var animation = components.OfType<PassAnimationController>().Single();
            if (!(boss is PassBossPatternController) || !(combat is PassPlayerAreaCombat) || flow.Boss != boss) throw new Exception("Incorrect combat references");
            if (settings.linkedAttack.cooldown != 6f) throw new Exception("Incorrect cooldown");
            var serialized = new SerializedObject(boss);
            foreach (string field in new[] { "player", "playerHealth", "game", "effects", "view", "audioControl", "visual", "settings" })
                if (serialized.FindProperty(field).objectReferenceValue == null) throw new Exception("Missing boss reference: " + field);
            var controller = (AnimatorController)animation.CharacterAnimator.runtimeAnimatorController;
            string[] states = controller.layers[0].stateMachine.states.Select(s => s.state.name).ToArray();
            if (new[] { "HEAVY_HIT", "GET_UP", "DEATH" }.Any(s => !states.Contains(s))) throw new Exception("Missing animation states");
            if (animation.ReactionSettings == null || animation.GetComponent<PassPlayerReactionPose>() == null) throw new Exception("Missing reactions");
            foreach (var material in new[] { settings.dust.material, settings.crack.material, settings.death.material, settings.death.powderMaterial })
                if (material == null || material.shader == null || ShaderUtil.ShaderHasError(material.shader) || AssetDatabase.GetAssetPath(material).StartsWith("Assets/Test/")) throw new Exception("Invalid shared VFX");
            var texture = (Texture2D)settings.dust.material.GetTexture("_BaseMap");
            if (texture.width > 1024 || texture.height > 1024) throw new Exception("Texture too large");
            if (Resources.Load<Shader>("S_BossWhiteSmoke") == null) throw new Exception("Missing teleport shader");
            results.Add(new { scene = path, boss = boss.GetType().Name, combat = combat.GetType().Name, cooldown = settings.linkedAttack.cooldown, reaction = AssetDatabase.GetAssetPath(animation.ReactionSettings), states = states.Where(s => s == "HEAVY_HIT" || s == "GET_UP" || s == "DEATH").ToArray(), texture = new[] { texture.width, texture.height }, fanVfx = components.Count(c => c.GetType().Name == "Test_FanAttackVfx"), isDirty = scene.isDirty });
            if (opened) EditorSceneManager.CloseScene(scene, true);
        }
        EditorSceneManager.RestoreSceneManagerSetup(setup);
        return results;
    }
}
