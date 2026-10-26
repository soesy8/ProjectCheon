using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Cheon.Prototype.MountainPass;
using Cheon.Tests.BossPatterns;

public static class Test_ApplyBossIntegration
{
    private static readonly Dictionary<UnityEngine.Object, UnityEngine.Object> artCopies = new Dictionary<UnityEngine.Object, UnityEngine.Object>();
    private const string ArtRoot = "Assets/Arts/Effects/BossPatterns";

    public static object Main()
    {
        if (EditorApplication.isPlaying) throw new Exception("Edit Mode required");
        var setup = EditorSceneManager.GetSceneManagerSetup();
        foreach (var item in setup)
            if (SceneManager.GetSceneByPath(item.path).isDirty) throw new Exception("Unsaved scene: " + item.path);

        var sourceScene = LoadScene("Assets/Test/BossPatterns/Scenes/Test_BossPatterns.unity");
        var source = Components<Test_BossPatternSettings>(sourceScene).Single();
        source.linkedAttack.cooldown = 6f;
        EditorUtility.SetDirty(source);
        EditorSceneManager.MarkSceneDirty(sourceScene);
        if (!EditorSceneManager.SaveScene(sourceScene)) throw new Exception("Could not save boss test scene");

        var smoke = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Test/BossPatterns/Shaders/Resources/Test_S_WhiteSmoke.shader");
        CopyArt(smoke, "S_BossWhiteSmoke");
        var results = new List<object>();
        foreach (string scenePath in new[] { "Assets/Scenes/MountainPass/MountainPass.unity", "Assets/Test/FanAttackVfx/Scenes/Test_FanAttackVfx.unity" })
        {
            var scene = LoadScene(scenePath);
            string backup = "Assets/Temp/BossIntegration~/" + Path.GetFileName(scenePath) + ".before";
            if (!File.Exists(backup)) File.Copy(scenePath, backup);
            var oldBoss = Components<PassBossController>(scene).Single();
            var oldCombat = Components<PassPlayerCombat>(scene).Single();
            var ranges = Components<PassAttackRangeEffects>(scene).Single();
            var settings = Components<PassBossPatternSettings>(scene).SingleOrDefault();
            if (settings == null)
            {
                var root = new GameObject("BossPatternSettings");
                SceneManager.MoveGameObjectToScene(root, scene);
                settings = root.AddComponent<PassBossPatternSettings>();
            }
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source), settings);
            settings.linkedAttack.cooldown = 6f;
            settings.dust.material = (Material)CopyArt(source.dust.material);
            settings.crack.material = (Material)CopyArt(source.crack.material);
            settings.death.material = (Material)CopyArt(source.death.material);
            settings.death.powderMaterial = (Material)CopyArt(source.death.powderMaterial);

            var boss = oldBoss as PassBossPatternController;
            if (boss == null)
            {
                boss = oldBoss.gameObject.AddComponent<PassBossPatternController>();
                EditorUtility.CopySerializedManagedFieldsOnly(oldBoss, boss);
            }
            boss.ConfigurePattern(settings, false);
            var combat = oldCombat as PassPlayerAreaCombat;
            if (combat == null)
            {
                combat = oldCombat.gameObject.AddComponent<PassPlayerAreaCombat>();
                EditorUtility.CopySerializedManagedFieldsOnly(oldCombat, combat);
            }
            var rangeArray = new SerializedObject(ranges).FindProperty("ranges");
            var areas = new PassAttackRangeEffects.AttackRange[rangeArray.arraySize];
            for (int index = 0; index < areas.Length; index++)
            {
                var entry = rangeArray.GetArrayElementAtIndex(index);
                areas[index] = new PassAttackRangeEffects.AttackRange
                {
                    sector = (PassAttackSector)entry.FindPropertyRelative("sector").objectReferenceValue,
                    attack = entry.FindPropertyRelative("attack").intValue,
                    startFrame = entry.FindPropertyRelative("startFrame").intValue,
                    endFrame = entry.FindPropertyRelative("endFrame").intValue
                };
            }
            if (areas.Length == 0 || areas.Any(a => a.sector == null)) throw new Exception("Missing attack sectors in " + scenePath);
            combat.ConfigureAreaCombat(settings, areas);
            var replacements = new Dictionary<UnityEngine.Object, UnityEngine.Object>();
            if (oldBoss != boss) replacements.Add(oldBoss, boss);
            if (oldCombat != combat) replacements.Add(oldCombat, combat);
            int references = ReplaceReferences(scene, replacements, oldBoss != boss ? oldBoss : null, oldCombat != combat ? oldCombat : null);
            if (oldCombat != combat) UnityEngine.Object.DestroyImmediate(oldCombat);
            if (oldBoss != boss) UnityEngine.Object.DestroyImmediate(oldBoss);

            var movement = Components<PassPlayerMovement>(scene).Single();
            var animation = movement.AnimationControl;
            var reactions = AssetDatabase.LoadAssetAtPath<PassPlayerReactionSettings>("Assets/Settings/MountainPass/MountainPlayerReactions.asset");
            if (reactions == null || reactions.heavyHit.clip == null || reactions.getUp.clip == null) throw new Exception("Missing shared reactions");
            animation.ConfigureReactions(reactions);
            animation.CharacterAnimator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Arts/Characters/Player/Animations/MountainMap/Test_MountainPlayer.controller");
            var pose = animation.GetComponent<PassPlayerReactionPose>() ?? animation.gameObject.AddComponent<PassPlayerReactionPose>();
            pose.Configure(animation, movement.GetComponent<CharacterController>());
            foreach (var component in new Component[] { settings, boss, combat, animation, animation.CharacterAnimator, pose }) EditorUtility.SetDirty(component);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Could not save " + scenePath);
            results.Add(new { scene = scenePath, boss = boss.GetType().Name, combat = combat.GetType().Name, areas = areas.Length, references, cooldown = settings.linkedAttack.cooldown, reaction = AssetDatabase.GetAssetPath(animation.ReactionSettings), dust = AssetDatabase.GetAssetPath(settings.dust.material) });
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.RestoreSceneManagerSetup(setup);
        return new { scenes = results, art = artCopies.Values.Select(AssetDatabase.GetAssetPath).ToArray() };
    }

    private static Scene LoadScene(string path)
    {
        var scene = SceneManager.GetSceneByPath(path);
        return scene.IsValid() && scene.isLoaded ? scene : EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
    }

    private static T[] Components<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

    private static int ReplaceReferences(Scene scene, Dictionary<UnityEngine.Object, UnityEngine.Object> replacements, params Component[] removed)
    {
        int count = 0;
        foreach (var component in Components<MonoBehaviour>(scene))
        {
            if (component == null || removed.Contains(component)) continue;
            var serialized = new SerializedObject(component);
            var property = serialized.GetIterator();
            while (property.Next(true))
                if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue != null && replacements.TryGetValue(property.objectReferenceValue, out var replacement))
                { property.objectReferenceValue = replacement; count++; }
            if (serialized.ApplyModifiedPropertiesWithoutUndo()) EditorUtility.SetDirty(component);
        }
        return count;
    }

    private static UnityEngine.Object CopyArt(UnityEngine.Object source, string nameOverride = null)
    {
        if (source == null) throw new Exception("Missing VFX asset");
        if (artCopies.TryGetValue(source, out var existing)) return existing;
        string sourcePath = AssetDatabase.GetAssetPath(source);
        if (!sourcePath.StartsWith("Assets/Test/")) return source;
        string category = source is Shader ? (sourcePath.Contains("/Resources/") ? "Shaders/Resources" : "Shaders") : source is Material ? "Materials" : "Textures";
        string name = nameOverride ?? Path.GetFileNameWithoutExtension(sourcePath).Replace("Test_", "");
        string destination = ArtRoot + "/" + category + "/" + name + Path.GetExtension(sourcePath);
        EnsureFolder(Path.GetDirectoryName(destination).Replace('\\', '/'));
        if (!File.Exists(destination) && !AssetDatabase.CopyAsset(sourcePath, destination)) throw new Exception("Copy failed: " + destination);
        if (source is Shader)
        {
            string text = File.ReadAllText(destination).Replace("Cheon/Tests/", "Cheon/Effects/");
            File.WriteAllText(destination, text, new System.Text.UTF8Encoding(false));
            AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceSynchronousImport);
        }
        var copy = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(destination);
        artCopies.Add(source, copy);
        if (source is Material material)
        {
            var result = (Material)copy;
            result.name = name;
            result.shader = (Shader)CopyArt(material.shader);
            foreach (string property in material.GetTexturePropertyNames())
                if (material.GetTexture(property) != null) result.SetTexture(property, (Texture)CopyArt(material.GetTexture(property)));
            EditorUtility.SetDirty(result);
        }
        else if (source is Texture2D)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(destination);
            importer.maxTextureSize = 1024;
            foreach (string platform in new[] { "DefaultTexturePlatform", "Standalone", "Android", "iPhone", "WebGL" })
            {
                var options = importer.GetPlatformTextureSettings(platform);
                options.maxTextureSize = 1024; importer.SetPlatformTextureSettings(options);
            }
            importer.SaveAndReimport();
        }
        return copy;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
