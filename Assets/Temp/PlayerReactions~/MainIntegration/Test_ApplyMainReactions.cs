using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Cheon.Prototype.MountainPass;
using Cheon.Tests.PlayerReactions;
public static class Test_ApplyMainReactions
{
    private static AnimationClip CopyMotion(string source,string destination,string name)
    {
        if(!System.IO.File.Exists(destination) && !AssetDatabase.CopyAsset(source,destination))throw new Exception("Copy failed "+destination);
        var importer=(ModelImporter)AssetImporter.GetAtPath(destination);
        var clips=importer.clipAnimations;clips[0].name=name;importer.clipAnimations=clips;importer.SaveAndReimport();
        return AssetDatabase.LoadAllAssetsAtPath(destination).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview__"));
    }
    private static void CopyRange(PassPlayerReactionSettings.MotionRange target,Test_PlayerReactionSettings.MotionRange source,AnimationClip clip)
    {target.clip=clip;target.startFrame=source.startFrame;target.endFrame=source.endFrame;target.speed=source.speed;}
    public static object Main()
    {
        if(EditorApplication.isPlaying)throw new Exception("Edit mode required");
        const string root="Assets/Arts/Characters/Player/Animations/MountainMap/";
        var heavy=CopyMotion("Assets/Test/PlayerReactions/Animations/Test_HeavyHit.fbx",root+"HeavyHit.fbx","HeavyHit");
        var getUp=CopyMotion("Assets/Test/PlayerReactions/Animations/Test_GetUp.fbx",root+"GetUp.fbx","GetUp");
        var test=AssetDatabase.LoadAssetAtPath<Test_PlayerReactionSettings>("Assets/Test/PlayerReactions/Settings/Test_PlayerReactionSettings.asset");
        const string settingsPath="Assets/Settings/MountainPass/MountainPlayerReactions.asset";
        var settings=AssetDatabase.LoadAssetAtPath<PassPlayerReactionSettings>(settingsPath);
        if(settings==null){settings=ScriptableObject.CreateInstance<PassPlayerReactionSettings>();AssetDatabase.CreateAsset(settings,settingsPath);}
        CopyRange(settings.heavyHit,test.heavyHit,heavy);CopyRange(settings.getUp,test.getUp,getUp);CopyRange(settings.death,test.death,heavy);
        settings.landingFrame=test.landingFrame;settings.landedRecoveryDelay=test.landedRecoveryDelay;settings.downHoldSeconds=test.downHoldSeconds;
        settings.getUpDodgeCancelProgress=0.45f;settings.getUpMoveCancelProgress=test.getUpMoveCancelProgress;
        settings.transitionSeconds=test.transitionSeconds;settings.returnToIdleSeconds=test.returnToIdleSeconds;settings.inputTransitionSeconds=test.inputTransitionSeconds;settings.getUpHeadBowDegrees=test.getUpHeadBowDegrees;
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(root+"Test_MountainPlayer.controller");
        var machine=controller.layers[0].stateMachine;
        settings.sheathedIdleClip=((BlendTree)machine.states.Single(s=>s.state.name=="SHEATHED_IDLE").state.motion).children[0].motion as AnimationClip;
        settings.drawnIdleClip=((BlendTree)machine.states.Single(s=>s.state.name=="DRAWN_IDLE").state.motion).children[0].motion as AnimationClip;
        string[] states={"HEAVY_HIT","GET_UP","DEATH"};string[] parameters={"HeavyTime","GetUpTime","DeathTime"};
        for(int i=0;i<states.Length;i++){
            if(!controller.parameters.Any(p=>p.name==parameters[i]))controller.AddParameter(parameters[i],AnimatorControllerParameterType.Float);
            var state=machine.states.Select(s=>s.state).FirstOrDefault(s=>s.name==states[i])??machine.AddState(states[i],new Vector3(800,150+i*90));
            state.motion=settings.GetRange(states[i]).clip;state.timeParameter=parameters[i];state.timeParameterActive=true;state.writeDefaultValues=false;state.iKOnFeet=false;
            EditorUtility.SetDirty(state);
        }
        const string mainPath="Assets/Scenes/MountainPass/MountainPass.unity";
        var scene=SceneManager.GetSceneByPath(mainPath);if(!scene.IsValid()||!scene.isLoaded)scene=EditorSceneManager.OpenScene(mainPath,OpenSceneMode.Additive);
        var movement=scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<PassPlayerMovement>(true)).Single();
        var animation=movement.AnimationControl;animation.ConfigureReactions(settings);
        var pose=animation.GetComponent<PassPlayerReactionPose>()??animation.gameObject.AddComponent<PassPlayerReactionPose>();
        pose.Configure(animation,movement.GetComponent<CharacterController>());
        EditorUtility.SetDirty(animation);EditorUtility.SetDirty(pose);EditorUtility.SetDirty(settings);EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssetIfDirty(settings);AssetDatabase.SaveAssetIfDirty(controller);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        SceneManager.SetActiveScene(scene);
        SessionState.SetString("PlayerReactions_PreviousPlayScene",EditorSceneManager.playModeStartScene!=null?AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene):"");
        EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(mainPath);
        return new{scene=scene.path,settings=settingsPath,heavy=heavy.name,getUp=getUp.name,settings.getUpDodgeCancelProgress,states=states,owner=animation.gameObject.name,body=movement.gameObject.name};
    }
}
