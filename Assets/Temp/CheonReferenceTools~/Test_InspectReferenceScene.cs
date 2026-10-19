using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public static class Test_InspectReferenceScene
{
    public static object Inspect()
    {
        Scene previous = SceneManager.GetActiveScene();
        Scene map = EditorSceneManager.OpenScene("Assets/Test/Cheon/Scenes/Test_CheonEnvironmentMap.unity", OpenSceneMode.Additive);
        try
        {
            var all = map.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
            var info = new {
                models = all.Where(t => PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)).GroupBy(t => t.name).Select(g => new {
                    name = g.Key, count = g.Count(), placements = g.Where(t => !t.name.Contains("Grass") && !t.name.Contains("Flowers")).Select(t => new { position = t.position.ToString("F3"), rotation = t.eulerAngles.ToString("F2"), scale = t.localScale.ToString("F3") }).ToArray()
                }).ToArray(),
                terrain = all.Where(t => t.name == "Test_MossTerrace" || t.name == "Test_SpiritTerrace").Select(t => new { name = t.name, position = t.position.ToString("F3"), size = t.GetComponent<Renderer>().bounds.size.ToString("F3"), top = t.GetComponent<Renderer>().bounds.max.y }).ToArray(),
                prefabMaterials = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Test/Cheon/Arts/EnvironmentModels/06_Bell_Pavilion/Test_Cheon_BellPavilion.fbx").GetComponentsInChildren<MeshFilter>().Select(f => new { name = f.name, position = f.transform.position.ToString("F3") }).ToArray()
            };
            File.WriteAllText("Assets/Temp/Test_CheonReferenceSceneBefore.json", Newtonsoft.Json.JsonConvert.SerializeObject(info, Newtonsoft.Json.Formatting.Indented));
            return new { info.terrain, types = info.models.Select(m => new {m.name,m.count}).ToArray() };
        }
        finally { SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(map,true); }
    }
}
