using System.Linq;
using UnityEngine;
using UnityEditor;

public static class Test_InspectReferenceGeometry
{
    public static object Inspect()
    {
        string folder="Assets/Test/Cheon/Arts/ReferenceEnvironment/";
        return new[]{"GroundedMountainBase","Sotdae","JadeRings","RuinedPavilion"}.Select(key=>new {
            key,
            source=Describe(AssetDatabase.LoadAssetAtPath<GameObject>(folder+"Models/Test_CheonRef_"+key+".fbx")),
            prefab=Describe(AssetDatabase.LoadAssetAtPath<GameObject>(folder+"Prefabs/Test_CheonRef_"+key+".prefab"))
        }).ToArray();
    }
    private static object Describe(GameObject root)
    {
        var filters=root.GetComponentsInChildren<MeshFilter>();
        var points=filters.SelectMany(f=>f.sharedMesh.vertices.Select(p=>f.transform.TransformPoint(p))).ToArray();
        return new{min=new Vector3(points.Min(p=>p.x),points.Min(p=>p.y),points.Min(p=>p.z)).ToString(),max=new Vector3(points.Max(p=>p.x),points.Max(p=>p.y),points.Max(p=>p.z)).ToString(),parts=filters.Take(6).Select(f=>new{f.name,position=f.transform.position.ToString(),rotation=f.transform.rotation.eulerAngles.ToString(),size=f.sharedMesh.bounds.size.ToString()}).ToArray()};
    }
}
