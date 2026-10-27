using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Cheon.Prototype.MountainPass;

public static class Test_ImpactStepSetup
{
    public static object Apply()
    {
        var scene=SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.path!="Assets/Test/CombatPresentation/Scenes/Test_MountainPass_ArtFeedback.unity")
            throw new Exception("테스트 씬 편집 모드가 필요합니다.");
        string root="Assets/Test/CombatPresentation/Audio/";
        for(int i=1;i<=4;i++)
            AssetDatabase.ImportAsset(root+"Test_FootstepStone_"+i.ToString("00")+".wav", ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
        var audio=UnityEngine.Object.FindFirstObjectByType<PassGameFlow>().Audio;
        var wood=AssetDatabase.LoadAssetAtPath<AudioClip>(root+"Test_WoodImpact.wav");
        if(wood==null) throw new Exception("기존 나뭇가지 타격음이 없습니다.");
        Undo.RecordObject(audio,"타격음을 기존 나뭇가지 소리로 통일");
        var data=new SerializedObject(audio);
        data.FindProperty("strike").objectReferenceValue=wood;
        data.FindProperty("windHit").objectReferenceValue=wood;
        data.ApplyModifiedProperties();
        EditorUtility.SetDirty(audio);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        return new {directHit=wood.name,windHit=wood.name,woodSeconds=wood.length,stoneFiles=4,saved=!scene.isDirty};
    }
}
