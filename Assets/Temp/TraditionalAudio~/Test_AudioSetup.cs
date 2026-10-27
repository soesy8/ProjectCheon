using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cheon.Prototype.MountainPass;
using Cheon.Tests.CombatPresentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class Test_AudioSetup
{
    const string Root = "Assets/Test/CombatPresentation/Audio/";
    const string ScenePath = "Assets/Test/CombatPresentation/Scenes/Test_MountainPass_ArtFeedback.unity";
    public static object Apply()
    {
        if (EditorApplication.isPlaying || SceneManager.GetActiveScene().path != ScenePath)
            throw new Exception("테스트 씬 Edit Mode에서만 적용합니다.");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach (string file in Directory.GetFiles(Root, "*.wav"))
        {
            var importer = (AudioImporter)AssetImporter.GetAtPath(file.Replace('\\', '/'));
            bool music = file.Contains("Exploration_") || file.Contains("Combat_");
            importer.forceToMono = !music;
            var settings = importer.defaultSampleSettings;
            settings.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = music ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.PCM;
            settings.quality = .85f;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            settings.preloadAudioData = !music;
            importer.defaultSampleSettings = settings;
            importer.loadInBackground = false;
            importer.SaveAndReimport();
        }
        var flow = Object.FindFirstObjectByType<PassGameFlow>();
        var audio = flow.Audio;
        var paths = new Dictionary<string, string> {
            {"fanWhoosh","Test_FanSwing"}, {"strike","Test_WoodImpact"}, {"windHit","Test_WindImpact"},
            {"hurt","Test_PlayerHurt"}, {"slam","Test_GuardianSlam"}, {"bell","Test_ShrineJing"},
            {"wind","Test_MountainAir"}, {"footstep","Test_Footstep"}, {"dodge","Test_DodgeCloth"},
            {"drink","Test_GourdRestore"}, {"confirm","Test_MenuGayageum"}, {"spirit","Test_SpiritAwaken"},
            {"victory","Test_Victory"}, {"defeat","Test_Defeat"}, {"equipment","Test_FanEquip"}
        };
        Undo.RecordObject(audio, "테스트 전통 악기 효과음 적용");
        var data = new SerializedObject(audio);
        foreach (var pair in paths) data.FindProperty(pair.Key).objectReferenceValue = Clip(pair.Value);
        data.ApplyModifiedProperties();
        var mix = audio.GetComponent<Test_TraditionalSoundscape>();
        if (mix == null) mix = Undo.AddComponent<Test_TraditionalSoundscape>(audio.gameObject);
        Undo.RecordObject(mix, "테스트 음악 연결");
        mix.Configure(flow, audio, Clip("Test_Exploration_GayageumDaegeum"), Clip("Test_Combat_JangguJwago"), Clip("Test_MenuGayageum"));
        var cues = flow.Boss.GetComponent<Test_BossAudioCues>();
        if (cues == null) cues = Undo.AddComponent<Test_BossAudioCues>(flow.Boss.gameObject);
        Undo.RecordObject(cues, "테스트 보스 경고음 연결");
        cues.Configure(flow, (PassBossPatternController)flow.Boss, mix, Clip("Test_CueJump"), Clip("Test_CueHeavy"), Clip("Test_CueCrack"));
        EditorUtility.SetDirty(mix); EditorUtility.SetDirty(cues);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        var report = new { scene = ScenePath, effects = paths.Count, music = 2, warnings = 3,
            files = Directory.GetFiles(Root, "*.wav").Length };
        File.WriteAllText("Assets/Temp/TraditionalAudio~/scene_setup.json", Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented));
        return report;
    }
    static AudioClip Clip(string name)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + name + ".wav");
        if (clip == null) throw new Exception("음원 누락: " + name);
        return clip;
    }
}
