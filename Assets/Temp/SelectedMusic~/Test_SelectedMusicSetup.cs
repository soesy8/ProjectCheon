using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Cheon.Prototype.MountainPass;
using Cheon.Tests.CombatPresentation;

public static class Test_SelectedMusicSetup
{
    const string Root = "Assets/Test/CombatPresentation/Audio/";
    public static object Apply()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.path != "Assets/Test/CombatPresentation/Scenes/Test_MountainPass_ArtFeedback.unity")
            throw new Exception("테스트 씬 Edit Mode에서만 적용합니다.");
        foreach (string file in new[] { "Test_Bgm_Samulnori.mp3", "Test_Bgm_SeventhNight.mp3", "Test_Bgm_OurSounds.mp3", "Test_Bgm_Dokkaebi.mp3" })
        {
            var importer = (AudioImporter)AssetImporter.GetAtPath(Root + file);
            if (importer == null) throw new Exception("임포트 누락: " + file);
            importer.forceToMono = false;
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.Streaming;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = .85f;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            settings.preloadAudioData = false;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }
        var flow = UnityEngine.Object.FindFirstObjectByType<PassGameFlow>();
        var mix = UnityEngine.Object.FindFirstObjectByType<Test_TraditionalSoundscape>();
        Undo.RecordObject(mix, "선택한 공유마당 BGM 적용");
        var audio = new SerializedObject(mix);
        audio.FindProperty("titleVictoryMusic").objectReferenceValue = Clip("Test_Bgm_OurSounds.mp3");
        audio.FindProperty("explorationMusic").objectReferenceValue = Clip("Test_Bgm_SeventhNight.mp3");
        audio.FindProperty("combatMusic").objectReferenceValue = Clip("Test_Bgm_Samulnori.mp3");
        audio.ApplyModifiedProperties();
        var hud = UnityEngine.Object.FindFirstObjectByType<PassHud>();
        var credits = hud.GetComponent<Test_AudioCreditsPanel>();
        if (credits == null) credits = Undo.AddComponent<Test_AudioCreditsPanel>(hud.gameObject);
        Undo.RecordObject(credits, "타이틀 음악·효과음 크레딧 연결");
        var ui = new SerializedObject(credits);
        ui.FindProperty("game").objectReferenceValue = flow;
        ui.FindProperty("musicCredits").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "Test_MusicCredits.txt");
        ui.FindProperty("effectCredits").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "Test_SoundEffectCredits.txt");
        ui.ApplyModifiedProperties();
        EditorUtility.SetDirty(mix);
        EditorUtility.SetDirty(credits);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        var dependencies = AssetDatabase.GetDependencies(scene.path, true);
        if (dependencies.Contains(Root + "Test_Bgm_Dokkaebi.mp3")) throw new Exception("도깨비가 씬에 연결되어 있습니다.");
        return new { saved = !scene.isDirty, titleVictory = Clip("Test_Bgm_OurSounds.mp3").length,
            field = Clip("Test_Bgm_SeventhNight.mp3").length, boss = Clip("Test_Bgm_Samulnori.mp3").length,
            archive = Clip("Test_Bgm_Dokkaebi.mp3").length, archiveInScene = false,
            sceneMusic = dependencies.Where(p => p.Contains("Test_Bgm_")).ToArray() };
    }
    static AudioClip Clip(string file) => AssetDatabase.LoadAssetAtPath<AudioClip>(Root + file) ?? throw new Exception(file);
}
