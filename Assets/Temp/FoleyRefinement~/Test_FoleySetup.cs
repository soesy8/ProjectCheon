using System;
using System.IO;
using System.Linq;
using Cheon.Prototype.MountainPass;
using Cheon.Tests.CombatPresentation;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class Test_FoleySetup
{
    const string Root = "Assets/Test/CombatPresentation/Audio/";
    public static object Apply()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.path != "Assets/Test/CombatPresentation/Scenes/Test_MountainPass_ArtFeedback.unity")
            throw new Exception("테스트 씬 편집 모드에서만 적용합니다.");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach (string file in Directory.GetFiles(Root, "*.wav"))
        {
            if (file.Contains("Exploration_") || file.Contains("Combat_")) continue;
            var importer = (AudioImporter)AssetImporter.GetAtPath(file.Replace('\\','/'));
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.preloadAudioData = true;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = true;
            importer.loadInBackground = false;
            importer.SaveAndReimport();
        }
        var flow = Object.FindFirstObjectByType<PassGameFlow>();
        var player = Object.FindFirstObjectByType<PassPlayerMovement>();
        var old = player.GetComponent<PassPlayerAudio>();
        Undo.RecordObject(old, "테스트 플레이어 재질음 교체");
        old.enabled = false;
        var foley = player.GetComponent<Test_PlayerFoley>();
        if (foley == null) foley = Undo.AddComponent<Test_PlayerFoley>(player.gameObject);
        var combat = new SerializedObject(player.GetComponent<PassFanAttackCombat>());
        var profile = (PassFanAttackVfxProfile)combat.FindProperty("profile").objectReferenceValue;
        Undo.RecordObject(foley, "재질별 발걸음 연결");
        foley.Configure(flow, flow.Audio, profile,
            Enumerable.Range(1,4).Select(i => Clip("Test_FootstepDirt_"+i.ToString("00"))).ToArray(),
            Enumerable.Range(1,4).Select(i => Clip("Test_FootstepStone_"+i.ToString("00"))).ToArray());
        EditorUtility.SetDirty(foley);
        int stones = 0;
        foreach (var collider in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
        {
            if (!collider.enabled || collider.isTrigger) continue;
            string name = collider.name;
            bool stone = name.Contains("Stair") || name == "Floor" || name.StartsWith("Gate_Threshold") ||
                name.StartsWith("Granite_Cliff") || name.StartsWith("Summit_Rim_Outcrops");
            if (!stone) continue;
            if (collider.GetComponent<Test_FootstepSurface>() == null) Undo.AddComponent<Test_FootstepSurface>(collider.gameObject);
            stones++;
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        var info = new { stones, oldPlayerAudioDisabled=!old.enabled, jingSeconds=Clip("Test_ShrineJing").length,
            attack2AudioFrame=profile.GetSweep(2).startFrame-1, attack2DamageStart=profile.GetSweep(2).startFrame,
            attack2DamageEnd=profile.GetSweep(2).endFrame };
        File.WriteAllText("Assets/Temp/FoleyRefinement~/SetupReport.json", Newtonsoft.Json.JsonConvert.SerializeObject(info, Newtonsoft.Json.Formatting.Indented));
        return info;
    }
    static AudioClip Clip(string name)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + name + ".wav");
        if (clip == null) throw new Exception(name);
        return clip;
    }
}
