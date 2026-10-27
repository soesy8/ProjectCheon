using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Cheon.Prototype.MountainPass;
using Cheon.Tests.CombatPresentation;

public static class Test_FootstepSyncSetup
{
    public static object Apply()
    {
        var scene=SceneManager.GetActiveScene();
        if(EditorApplication.isPlaying || scene.path!="Assets/Test/CombatPresentation/Scenes/Test_MountainPass_ArtFeedback.unity")
            throw new Exception("테스트 씬 편집 모드에서만 적용합니다.");
        var audio=UnityEngine.Object.FindFirstObjectByType<PassGameFlow>().Audio;
        Undo.RecordObject(audio,"바람 전용 타격음 제거");
        var data=new SerializedObject(audio);
        // 전용 바람 음원이 없으면 PassAudio.Strike가 기존 나뭇가지 타격음으로 처리한다.
        data.FindProperty("windHit").objectReferenceValue=null;
        data.ApplyModifiedProperties();
        var foley=UnityEngine.Object.FindFirstObjectByType<Test_PlayerFoley>();
        Undo.RecordObject(foley,"애니메이션 착지 프레임 연결");
        var feet=new SerializedObject(foley);
        feet.FindProperty("leftContactFrame").floatValue=8f;
        feet.FindProperty("rightContactFrame").floatValue=18.5f;
        feet.ApplyModifiedProperties();
        string root="Assets/Test/CombatPresentation/Audio/";
        for(int i=1;i<=4;i++) AssetDatabase.ImportAsset(root+"Test_FootstepStone_"+i.ToString("00")+".wav",ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
        if(!AssetDatabase.DeleteAsset(root+"Test_WindImpact.wav")) throw new Exception("바람 타격음 삭제 실패");
        EditorUtility.SetDirty(audio);EditorUtility.SetDirty(foley);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        return new {leftFrame=8,rightFrame=18.5,removedWindClip=true,saved=!scene.isDirty};
    }
}
