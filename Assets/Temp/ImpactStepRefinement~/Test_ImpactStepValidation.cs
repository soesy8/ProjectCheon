using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using Cheon.Prototype.MountainPass;
using Cheon.Tests.CombatPresentation;

public static class Test_ImpactStepValidation
{
    public static object Check()
    {
        if(EditorApplication.isPlaying) throw new Exception("Edit Mode required");
        var flow=UnityEngine.Object.FindFirstObjectByType<PassGameFlow>();
        var audio=new SerializedObject(flow.Audio);
        var wood=(AudioClip)audio.FindProperty("strike").objectReferenceValue;
        var wind=(AudioClip)audio.FindProperty("windHit").objectReferenceValue;
        if(wood==null || wood!=wind || wood.name!="Test_WoodImpact") throw new Exception("나뭇가지 타격음 연결 오류");
        var foley=UnityEngine.Object.FindFirstObjectByType<Test_PlayerFoley>();
        var data=new SerializedObject(foley);
        var stones=data.FindProperty("stoneSteps");
        var stoneChecks=new System.Collections.Generic.List<object>();
        for(int i=0;i<stones.arraySize;i++)
        {
            var clip=(AudioClip)stones.GetArrayElementAtIndex(i).objectReferenceValue;
            if(clip==null || !clip.name.StartsWith("Test_FootstepStone_")) throw new Exception("돌 발걸음 연결 오류");
            clip.LoadAudioData();
            var samples=new float[clip.samples*clip.channels];
            if(!clip.GetData(samples,0) || samples.Max(v=>Mathf.Abs(v))<=0) throw new Exception("돌 발걸음 디코드 오류");
            if(Mathf.Abs(clip.length-.24f)>.001f) throw new Exception("발걸음 길이 오류");
            stoneChecks.Add(new {clip=clip.name,seconds=clip.length,channels=clip.channels,peak=samples.Max(v=>Mathf.Abs(v))});
        }
        bool oldDisabled=!foley.GetComponent<PassPlayerAudio>().enabled;
        if(!oldDisabled || !foley.enabled) throw new Exception("발걸음 컴포넌트 중복 또는 비활성");
        var scene=SceneManager.GetActiveScene();
        var deps=AssetDatabase.GetDependencies(scene.path,true);
        var report=new {passed=true,woodClip=wood.name,windHitSameAsWood=wood==wind,stoneChecks,oldPlayerAudioDisabled=oldDisabled,
            archiveReferenced=deps.Any(p=>p.Contains("Dokkaebi")),saved=!scene.isDirty};
        System.IO.File.WriteAllText("Assets/Temp/ImpactStepRefinement~/Test_ValidationReport.json",
            Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
        return report;
    }
}
