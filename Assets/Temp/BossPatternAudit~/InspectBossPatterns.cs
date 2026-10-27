using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Cheon.Prototype.MountainPass;
public static class InspectBossPatterns
{
    public static object Main()
    {
        var scene=SceneManager.GetActiveScene();
        var settings=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<PassBossPatternSettings>(true)).First();
        var boss=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<PassBossPatternController>(true)).First(b=>!b.IsMinion);
        // Unity 객체의 프로퍼티를 재귀 탐색하지 않고 Unity 직렬화기로 설정 값만 기록한다.
        var result=new JObject{["scene"]=scene.path,["dirty"]=scene.isDirty,["playing"]=EditorApplication.isPlaying,["time"]=Time.time,["phase"]=boss.Phase.ToString(),["settings"]=JObject.Parse(JsonUtility.ToJson(settings))};
        File.WriteAllText("Assets/Temp/BossPatternAudit~/CurrentSceneSettings.json",result.ToString());
        return new{scene=scene.path,scene.isDirty,playing=EditorApplication.isPlaying,phase=boss.Phase.ToString()};
    }
}
