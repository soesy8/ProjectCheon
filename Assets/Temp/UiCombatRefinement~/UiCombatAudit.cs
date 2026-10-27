using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Cheon.Prototype.MountainPass;

public static class UiCombatAudit
{
    public static object Inspect()
    {
        var textures = Directory.GetFiles("Assets/Arts/UI/MountainPass", "*.png").Select(path =>
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            var platforms = new[] {"Standalone", "Android", "iPhone", "WebGL"}.Select(name => new {name, max = importer.GetPlatformTextureSettings(name).maxTextureSize}).ToArray();
            if (width > 1024 || height > 1024 || importer.maxTextureSize != 1024 || platforms.Any(x => x.max != 1024)) throw new Exception("Texture limit: " + path);
            return new {path, width, height, importedWidth = texture.width, importedHeight = texture.height, max = importer.maxTextureSize, platforms};
        }).ToArray();
        var scene = SceneManager.GetActiveScene();
        var flow = UnityEngine.Object.FindFirstObjectByType<PassGameFlow>();
        var player = new SerializedObject(flow).FindProperty("player").objectReferenceValue as PassPlayerMovement;
        var energy = new SerializedObject(player.GetComponent<PassPlayerEnergy>());
        var result = new {scene = scene.path, dirty = scene.isDirty, playing = EditorApplication.isPlaying,
            textures, fonts = UnityEngine.Object.FindObjectsByType<TMPro.TMP_FontAsset>(FindObjectsSortMode.None).Select(x => new {x.name, x.atlasWidth, x.atlasHeight}).ToArray(),
            energy = new {maximum = energy.FindProperty("maximum").floatValue, dodge = energy.FindProperty("dodgeCost").floatValue,
                sprint = energy.FindProperty("sprintCostPerSecond").floatValue, retreat = energy.FindProperty("retreatCostPerSecond").floatValue,
                regen = energy.FindProperty("regenerationPerSecond").floatValue},
            audioClips = Directory.GetFiles("Assets/Arts/Audio/MountainPass", "*.wav").Length,
            shaderErrors = ShaderUtil.GetShaderMessages(Shader.Find("Cheon/HitImpact")).Select(x => x.message).ToArray()};
        File.WriteAllText("Assets/Temp/UiCombatRefinement~/FinalAudit.json", Newtonsoft.Json.JsonConvert.SerializeObject(result, Newtonsoft.Json.Formatting.Indented));
        return result;
    }
}
