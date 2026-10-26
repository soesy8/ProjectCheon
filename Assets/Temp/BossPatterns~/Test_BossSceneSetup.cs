using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Cheon.Prototype.MountainPass;
using Cheon.Tests.BossPatterns;

public static class Test_BossSceneSetup
{
    public static object Main()
    {
        const string scenePath = "Assets/Test/BossPatterns/Scenes/Test_BossPatterns.unity";
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != scenePath || EditorApplication.isPlaying) throw new InvalidOperationException("대상 테스트 씬의 Edit Mode에서만 실행합니다.");
        if (scene.isDirty) throw new InvalidOperationException("저장되지 않은 변경이 있습니다.");
        if (UnityEngine.Object.FindFirstObjectByType<Test_BossPatternSettings>() != null) throw new InvalidOperationException("이미 설정된 씬입니다.");
        var oldBoss = UnityEngine.Object.FindFirstObjectByType<PassBossController>();
        var oldCombat = UnityEngine.Object.FindFirstObjectByType<PassPlayerCombat>();
        var ranges = UnityEngine.Object.FindFirstObjectByType<PassAttackRangeEffects>();
        var settings = new GameObject("Test_BossPatternSettings").AddComponent<Test_BossPatternSettings>();
        settings.summon.visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Bosses/JangseungBoss.prefab");
        settings.summon.modelScale = oldBoss.transform.lossyScale;

        EnsureFolder("Assets/Test/BossPatterns/Textures");
        EnsureFolder("Assets/Test/BossPatterns/Materials");
        const string texturePath = "Assets/Test/BossPatterns/Textures/Test_T_BossDust.png";
        const string materialPath = "Assets/Test/BossPatterns/Materials/Test_M_BossDust.mat";
        if (!AssetDatabase.CopyAsset("Assets/Test/FanAttackVfx/Textures/Test_T_GroundDust.png", texturePath)) throw new Exception("먼지 텍스처 복사 실패");
        var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
        importer.maxTextureSize = 1024;
        foreach (string platform in new[] { "DefaultTexturePlatform", "Standalone", "Android", "iPhone", "WebGL" })
        {
            TextureImporterPlatformSettings platformSettings = importer.GetPlatformTextureSettings(platform);
            platformSettings.maxTextureSize = 1024;
            importer.SetPlatformTextureSettings(platformSettings);
        }
        importer.SaveAndReimport();
        if (!AssetDatabase.CopyAsset("Assets/Test/FanAttackVfx/Materials/Test_M_GroundDust.mat", materialPath)) throw new Exception("먼지 머티리얼 복사 실패");
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        material.name = "Test_M_BossDust";
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
        settings.dust.material = material; EditorUtility.SetDirty(material);

        var boss = oldBoss.gameObject.AddComponent<Test_BossPatternController>();
        EditorUtility.CopySerializedManagedFieldsOnly(oldBoss, boss);
        boss.ConfigurePattern(settings, false);
        var combat = oldCombat.gameObject.AddComponent<Test_PlayerAreaCombat>();
        EditorUtility.CopySerializedManagedFieldsOnly(oldCombat, combat);
        var rangeData = new SerializedObject(ranges).FindProperty("ranges");
        var attackAreas = new PassAttackRangeEffects.AttackRange[rangeData.arraySize];
        for (int index = 0; index < rangeData.arraySize; index++)
        {
            var item = rangeData.GetArrayElementAtIndex(index);
            attackAreas[index] = new PassAttackRangeEffects.AttackRange
            {
                sector = (PassAttackSector)item.FindPropertyRelative("sector").objectReferenceValue,
                attack = item.FindPropertyRelative("attack").intValue,
                startFrame = item.FindPropertyRelative("startFrame").intValue,
                endFrame = item.FindPropertyRelative("endFrame").intValue
            };
        }
        combat.ConfigureAreaCombat(settings, attackAreas);
        var replacements = new Dictionary<UnityEngine.Object, UnityEngine.Object> { { oldBoss, boss }, { oldCombat, combat } };
        int referencesUpdated = 0;
        foreach (var root in scene.GetRootGameObjects())
            foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component == null || component == oldBoss || component == oldCombat) continue;
                var serialized = new SerializedObject(component);
                var property = serialized.GetIterator();
                while (property.Next(true))
                    if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue != null && replacements.TryGetValue(property.objectReferenceValue, out var replacement))
                    { property.objectReferenceValue = replacement; referencesUpdated++; }
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        UnityEngine.Object.DestroyImmediate(oldCombat);
        UnityEngine.Object.DestroyImmediate(oldBoss);
        ranges.enabled = false;
        EditorUtility.SetDirty(settings); EditorUtility.SetDirty(boss); EditorUtility.SetDirty(combat);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new Exception("씬 저장 실패");
        Selection.activeGameObject = settings.gameObject;
        return Newtonsoft.Json.JsonConvert.SerializeObject(new
        {
            scene = scene.path, referencesUpdated, bossType = boss.GetType().Name,
            combatType = combat.GetType().Name, settingsObject = settings.name,
            attackAreas = attackAreas.Length,
            textureSize = new[] { AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath).width, AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath).height }
        });
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent); AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }
}
