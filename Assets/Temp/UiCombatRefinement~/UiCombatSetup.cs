using System;
using System.IO;
using System.Linq;
using Cheon.Prototype.MountainPass;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class UiCombatSetup
{
    public static object Apply()
    {
        if (EditorApplication.isPlaying) throw new Exception("Edit Mode에서 실행하세요.");
        Scene active = SceneManager.GetActiveScene();
        // 현재 Editor에 남아 있던 사용자 변경까지 복사해 보존한다.
        if (!File.Exists("Assets/Temp/UiCombatRefinement~/Before/LoadedScene.unity"))
            EditorSceneManager.SaveScene(active, "Assets/Temp/UiCombatRefinement~/Before/LoadedScene.unity", true);
        AssetDatabase.Refresh();
        string art = "Assets/Arts/UI/MountainPass/";
        foreach (string path in Directory.GetFiles(art, "*.png"))
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.maxTextureSize = 1024; importer.mipmapEnabled = false; importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            var spriteSettings = new TextureImporterSettings(); importer.ReadTextureSettings(spriteSettings);
            spriteSettings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(spriteSettings);
            importer.maxTextureSize = 1024;
            foreach (string platform in new[] {"Standalone", "Android", "iPhone", "WebGL"})
            {
                var settings = importer.GetPlatformTextureSettings(platform); settings.name = platform;
                settings.maxTextureSize = 1024; settings.overridden = true; importer.SetPlatformTextureSettings(settings);
            }
            importer.SaveAndReimport();
        }
        var brush = AssetDatabase.LoadAssetAtPath<Sprite>(art + "T_InkBrush.png");
        var gourd = AssetDatabase.LoadAssetAtPath<Sprite>(art + "T_HealingGourd.png");
        foreach (string path in Directory.GetFiles("Assets/Arts/Audio/MountainPass", "*.wav"))
        {
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            var sample = importer.defaultSampleSettings;
            sample.preloadAudioData = true;
            sample.loadType = path.Contains("MountainWind") ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            sample.compressionFormat = path.Contains("MountainWind") ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.PCM;
            sample.quality = 0.7f; importer.defaultSampleSettings = sample;
            importer.loadInBackground = false; importer.forceToMono = true; importer.SaveAndReimport();
        }
        string materialPath = "Assets/Arts/Effects/HitImpact/M_HitImpact.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null) { material = new Material(Shader.Find("Cheon/HitImpact")); AssetDatabase.CreateAsset(material, materialPath); }
        ApplyScene(active, brush, gourd, material);
        foreach (string path in new[] {"Assets/Scenes/MountainPass/MountainPass.unity", "Assets/Scenes/MountainPass/MountainPass_Optimized.unity"})
        {
            if (path == active.path) continue;
            var scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            ApplyScene(scene, brush, gourd, material);
            if (opened) EditorSceneManager.CloseScene(scene, true);
        }
        SceneManager.SetActiveScene(active);
        AssetDatabase.SaveAssets();
        return new {scene = active.path, bossHealth = 1000, direct = new[] {10, 15, 40}, wind = new[] {5, 10}, maxTexture = 1024};
    }
    private static void ApplyScene(Scene scene, Sprite brush, Sprite gourd, Material hitMaterial)
    {
        var roots = scene.GetRootGameObjects();
        var flow = roots.SelectMany(x => x.GetComponentsInChildren<PassGameFlow>(true)).First();
        var flowData = new SerializedObject(flow);
        var player = (PassPlayerMovement)flowData.FindProperty("player").objectReferenceValue;
        var audio = (PassAudio)flowData.FindProperty("audioControl").objectReferenceValue;
        var hud = (PassHud)flowData.FindProperty("hud").objectReferenceValue;
        var effects = (PassEffects)flowData.FindProperty("effects").objectReferenceValue;
        var energy = player.GetComponent<PassPlayerEnergy>(); if (energy == null) energy = Undo.AddComponent<PassPlayerEnergy>(player.gameObject);
        energy.Configure(flow); EditorUtility.SetDirty(energy);
        var playerAudio = player.GetComponent<PassPlayerAudio>(); if (playerAudio == null) playerAudio = Undo.AddComponent<PassPlayerAudio>(player.gameObject);
        playerAudio.Configure(flow, audio); EditorUtility.SetDirty(playerAudio);
        var hit = effects.GetComponent<PassHitEffects>(); if (hit == null) hit = Undo.AddComponent<PassHitEffects>(effects.gameObject);
        hit.Configure(hitMaterial); EditorUtility.SetDirty(hit);
        Undo.RecordObject(hud, "천지인 UI 적용"); hud.ConfigureArt(brush, gourd); EditorUtility.SetDirty(hud);
        hud.ConfigureTitle(AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Arts/UI/MountainPass/T_TitleBackground.png"),
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Arts/UI/MountainPass/T_TitleLogo.png"));
        if (hud.GetComponent<PassScreenFade>() == null) Undo.AddComponent<PassScreenFade>(hud.gameObject);
        var sound = new SerializedObject(audio);
        string[] fields = {"fanWhoosh", "strike", "windHit", "hurt", "slam", "bell", "wind", "footstep", "dodge", "drink", "confirm", "spirit", "victory", "defeat", "equipment"};
        string[] clips = {"FanWhoosh", "WoodStrike", "WindHit", "PlayerHurt", "GuardianSlam", "ShrineBell", "MountainWind", "Footstep", "Dodge", "GourdDrink", "MenuConfirm", "SpiritAwaken", "Victory", "Defeat", "FanEquip"};
        for (int i = 0; i < fields.Length; i++) sound.FindProperty(fields[i]).objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Arts/Audio/MountainPass/" + clips[i] + ".wav");
        sound.ApplyModifiedProperties();
        foreach (var damage in roots.SelectMany(x => x.GetComponentsInChildren<PassFanAttackDamageSettings>(true)))
        {
            Undo.RecordObject(damage, "공격 피해 수치 적용");
            damage.attack1.baseDamage = 15; damage.attack1.directWeight = 2f / 3f; damage.attack1.windWeight = 1f / 3f;
            damage.attack2.baseDamage = 15; damage.attack2.directWeight = 1;
            damage.attack3.baseDamage = 50; damage.attack3.directWeight = .8f; damage.attack3.windWeight = .2f;
            EditorUtility.SetDirty(damage);
        }
        foreach (var boss in roots.SelectMany(x => x.GetComponentsInChildren<PassBossPatternController>(true)))
        {
            var data = new SerializedObject(boss);
            var settings = (PassBossPatternSettings)data.FindProperty("settings").objectReferenceValue;
            Undo.RecordObject(settings, "전투 체력 수치 적용"); settings.combat.bossHealth = 1000;
            settings.combat.playerDamage = new Vector3(10, 15, 40); EditorUtility.SetDirty(settings);
            var health = new SerializedObject(boss.GetComponent<PassHealth>());
            health.FindProperty("maximum").floatValue = boss.IsMinion ? settings.combat.minionHealth : 1000;
            health.ApplyModifiedProperties();
        }
        var drinking = player.GetComponent<PassGourdDrinking>();
        if (drinking != null) { var data = new SerializedObject(drinking); data.FindProperty("healAmount").floatValue = 40; data.ApplyModifiedProperties(); }
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
    }
}
