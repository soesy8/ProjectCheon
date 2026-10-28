using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json;

public static class ExportMobileGeometry
{
    public static object Main()
    {
        var records = new List<object>();
        foreach (string source in new[] {
            "Assets/Arts/Environment/SelfModeling/Models/Wall_Middle.fbx",
            "Assets/Arts/Environment/SelfModeling/Models/Wall_End.fbx",
            "Assets/Arts/Environment/CheonMountainMap/Models/Test_CheonMountainMap.fbx" })
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(source))
        {
            if (!(asset is Mesh mesh) || mesh.triangles.Length < 1536) continue;
            if (source.Contains("/CheonMountainMap/") && !(mesh.name.StartsWith("Pine") || mesh.name.StartsWith("Mountain") || mesh.name.StartsWith("Granite") || mesh.name.StartsWith("Rocky") || mesh.name.StartsWith("Summit_Rim"))) continue;
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh, out string guid, out long id);
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var uv = mesh.uv;
            var positions = new float[vertices.Length * 3];
            var normalData = new float[vertices.Length * 3];
            var uvData = new float[vertices.Length * 2];
            for (int i = 0; i < vertices.Length; i++)
            {
                positions[i * 3] = vertices[i].x; positions[i * 3 + 1] = vertices[i].y; positions[i * 3 + 2] = vertices[i].z;
                if (normals.Length == vertices.Length) { normalData[i * 3] = normals[i].x; normalData[i * 3 + 1] = normals[i].y; normalData[i * 3 + 2] = normals[i].z; }
                if (uv.Length == vertices.Length) { uvData[i * 2] = uv[i].x; uvData[i * 2 + 1] = uv[i].y; }
            }
            var submeshes = new int[mesh.subMeshCount][];
            for (int i = 0; i < mesh.subMeshCount; i++) submeshes[i] = mesh.GetTriangles(i);
            records.Add(new { key = guid + "_" + id, source, name = mesh.name, positions, normals = normalData, uv = uvData, submeshes });
        }
        File.WriteAllText("Assets/Temp/MobileOptimization~/GeometryInput.json", JsonConvert.SerializeObject(records));
        return new { meshes = records.Count };
    }
}
