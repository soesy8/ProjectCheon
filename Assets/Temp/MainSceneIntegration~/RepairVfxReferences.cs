using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Cheon.Prototype.MountainPass;
using Cheon.Tests.FanAttackVfx;
using Object=UnityEngine.Object;

public static class RepairVfxReferences
{
    public static object Main()
    {
        var main=SceneManager.GetActiveScene();
        var source=EditorSceneManager.OpenScene("Assets/Test/FanAttackVfx/Scenes/Test_FanAttackVfx.unity",OpenSceneMode.Additive);
        var map=new Dictionary<Object,Object>();
        var sourceVfx=All<Test_FanAttackVfx>(source).Single(); var targetVfx=All<PassFanAttackVfx>(main).Single();
        var sourceRoll=All<Test_RollDust>(source).Single(); var targetRoll=All<PassRollDust>(main).Single();
        map[sourceVfx.Profile]=targetVfx.Profile;
        // 프로필 필드는 타입이 바뀌어 JSON 복사 때 비워졌으므로 메인 에셋을 직접 연결한다.
        map[sourceVfx.Profile]=AssetDatabase.LoadAssetAtPath<PassFanAttackVfxProfile>("Assets/Settings/MountainPass/MountainFanAttackVfx.asset");
        map[sourceRoll.Profile]=AssetDatabase.LoadAssetAtPath<PassRollDustProfile>("Assets/Settings/MountainPass/MountainRollDust.asset");
        map[All<PassAnimationController>(source).Single()]=All<PassAnimationController>(main).Single();
        map[All<PassPlayerMovement>(source).Single().transform]=All<PassPlayerMovement>(main).Single().transform;
        map[All<PassAttackRangeEffects>(source).Single()]=All<PassAttackRangeEffects>(main).Single();
        foreach(var pair in new[]{new[]{sourceVfx.transform,targetVfx.transform},new[]{sourceRoll.transform,targetRoll.transform}})
            foreach(var component in pair[0].GetComponentsInChildren<Component>(true))
            {
                string path=AnimationUtility.CalculateTransformPath(component.transform,pair[0]).Replace("Test_","");
                Transform target=path.Length==0?pair[1]:pair[1].Find(path);
                if(target==null)throw new Exception("Missing target "+path);
                Type type=component.GetType();
                if(type.Namespace=="Cheon.Tests.FanAttackVfx")type=typeof(PassFanAttackVfx).Assembly.GetType("Cheon.Prototype.MountainPass.Pass"+type.Name.Substring(5),true);
                map[component]=target.GetComponent(type);
                map[component.gameObject]=target.gameObject;
            }
        int count=0;
        foreach(var pair in map.Where(p=>p.Key is MonoBehaviour && p.Key.GetType().Namespace=="Cheon.Tests.FanAttackVfx").ToArray())
        {
            var original=new SerializedObject(pair.Key); var target=new SerializedObject(pair.Value); var prop=original.GetIterator();
            while(prop.Next(true))
            {
                if(prop.propertyType!=SerializedPropertyType.ObjectReference || prop.name=="m_Script")continue;
                var field=target.FindProperty(prop.propertyPath);
                if(field==null || field.propertyType!=SerializedPropertyType.ObjectReference)continue;
                Object value=prop.objectReferenceValue;
                if(value!=null && map.TryGetValue(value,out var replacement)) { field.objectReferenceValue=replacement;count++; }
            }
            target.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(pair.Value);
        }
        PrefabUtility.SaveAsPrefabAssetAndConnect(targetVfx.gameObject,"Assets/Prefabs/MountainPass/FanAttackVfx.prefab",InteractionMode.AutomatedAction);
        PrefabUtility.SaveAsPrefabAssetAndConnect(targetRoll.gameObject,"Assets/Prefabs/MountainPass/RollDust.prefab",InteractionMode.AutomatedAction);
        EditorSceneManager.MarkSceneDirty(main);EditorSceneManager.SaveScene(main);
        EditorSceneManager.CloseScene(source,true);SceneManager.SetActiveScene(main);
        return new { count, vfx=targetVfx.Profile!=null, roll=targetRoll.Profile!=null };
    }
    private static T[] All<T>(Scene s) where T:Component=>s.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).ToArray();
}
