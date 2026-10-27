using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Cheon.Tests.CombatPresentation;

public static class Test_MusicSetup
{
    public static object Apply()
    {
        var scene=SceneManager.GetActiveScene();
        if(EditorApplication.isPlaying || scene.path!="Assets/Test/CombatPresentation/Scenes/Test_MountainPass_ArtFeedback.unity")
            throw new Exception("Test scene Edit Mode required");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach(string name in new[]{"Test_Exploration_GayageumDaegeum","Test_Combat_JangguJwago"})
        {
            string path="Assets/Test/CombatPresentation/Audio/"+name+".wav";
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
            var importer=(AudioImporter)AssetImporter.GetAtPath(path);
            importer.forceToMono=false;
            var settings=importer.defaultSampleSettings;
            settings.loadType=AudioClipLoadType.Streaming;
            settings.compressionFormat=AudioCompressionFormat.Vorbis;
            settings.quality=.85f;
            settings.sampleRateSetting=AudioSampleRateSetting.PreserveSampleRate;
            settings.preloadAudioData=false;
            importer.defaultSampleSettings=settings;
            importer.SaveAndReimport();
        }
        var mix=UnityEngine.Object.FindFirstObjectByType<Test_TraditionalSoundscape>();
        Undo.RecordObject(mix,"BGM 단독 전환 적용");
        var serialized=new SerializedObject(mix);
        serialized.FindProperty("musicFadeOutSeconds").floatValue=.22f;
        serialized.FindProperty("musicFadeInSeconds").floatValue=.12f;
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(mix);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        var calm=(AudioClip)serialized.FindProperty("explorationMusic").objectReferenceValue;
        var battle=(AudioClip)serialized.FindProperty("combatMusic").objectReferenceValue;
        return new {explorationSeconds=calm.length,combatSeconds=battle.length,calmSamples=calm.samples,battleSamples=battle.samples,
            fadeOut=.22f,fadeIn=.12f,saved=!scene.isDirty};
    }
}
