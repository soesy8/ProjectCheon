using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class Test_SplitCheonEnvironmentScene
{
    private const string ModelFolder = "Assets/Test/Cheon/Arts/EnvironmentModels/";
    private static readonly HashSet<string> TerrainNames = new HashSet<string> {
        "Test_MossTerrace", "Test_SpiritTerrace", "Test_WalkableGround",
        "Test_SpiritWalkableGround", "Test_CliffSkirt", "Test_Cliff", "Test_ExtraCliff"
    };

    public static object VerifySaved()
    {
        var report = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText("Assets/Temp/Test_CheonSceneSplitVerification.json"));
        Scene original = SceneManager.GetActiveScene();
        bool wasDirty = original.isDirty;
        Scene saved = default;
        try
        {
            saved = EditorSceneManager.OpenScene((string)report["newScene"], OpenSceneMode.Additive);
            var all = AllTransforms(saved);
            var models = ImportedModels(saved);
            var terrain = all.Where(t => TerrainNames.Contains(t.name)).ToArray();
            int unwantedRenderers = all.Where(t => t.GetComponent<Renderer>() != null && !TerrainNames.Contains(t.name) &&
                !PrefabUtility.IsPartOfPrefabInstance(t.gameObject)).Count();
            int scripts = all.Sum(t => t.GetComponents<MonoBehaviour>().Length + GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
            int cameras = all.Sum(t => t.GetComponents<Camera>().Length);
            int lights = all.Sum(t => t.GetComponents<Light>().Length);
            bool unchanged = FileHash(original.path) == (string)report["originalHash"] && original.isDirty == wasDirty;
            if (models.Length != (int)report["importedModels"] || terrain.Length != 95 || unwantedRenderers != 0 || scripts != 0 || cameras != 0 || lights != 0 || !unchanged)
                throw new InvalidOperationException("저장 후 씬 재로드 검증 실패");
            return new { savedScene = saved.path, models = models.Length, modelTypes = models.Select(t => PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject)).Distinct().Count(),
                terrainObjects = terrain.Length, unwantedRenderers, scripts, cameras, lights, originalUnchanged = unchanged, originalUnsavedChangesPreserved = wasDirty };
        }
        finally
        {
            SceneManager.SetActiveScene(original);
            if (saved.IsValid() && saved.isLoaded) EditorSceneManager.CloseScene(saved, true);
        }
    }

    public static object Split()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Edit Mode에서 실행하세요.");
        Scene source = SceneManager.GetActiveScene();
        if (source.path != Test_CheonSceneBuilder.ScenePath) throw new InvalidOperationException("기존 천지인 테스트 씬을 활성화하세요.");
        bool sourceWasDirty = source.isDirty;
        string originalHash = FileHash(source.path);
        int[] originalIds = AllTransforms(source).Select(t => t.GetInstanceID()).OrderBy(i => i).ToArray();
        var originalModels = ImportedModels(source);
        if (originalModels.Length == 0) throw new InvalidOperationException("복사할 새 환경 모델이 없습니다.");
        var originalTerrain = AllTransforms(source).Where(t => TerrainNames.Contains(t.name)).ToArray();
        if (!originalTerrain.Any(t => t.name == "Test_MossTerrace")) throw new InvalidOperationException("맵 지형이 없습니다.");

        string destination = AssetDatabase.GenerateUniqueAssetPath("Assets/Test/Cheon/Scenes/Test_CheonEnvironmentMap.unity");
        // 현재 메모리의 수정 사항을 복사본에 포함하고 원본 씬 파일은 저장하지 않는다.
        if (!EditorSceneManager.SaveScene(source, destination, true)) throw new IOException("씬 복사본 저장 실패");
        if (sourceWasDirty && !source.isDirty) EditorSceneManager.MarkSceneDirty(source);
        Scene copy = default;
        try
        {
            copy = EditorSceneManager.OpenScene(destination, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(copy);
            var oldRoots = copy.GetRootGameObjects();
            var models = ImportedModels(copy);
            var terrain = AllTransforms(copy).Where(t => TerrainNames.Contains(t.name)).ToArray();
            var snapshots = models.Concat(terrain).ToDictionary(t => t, t => t.localToWorldMatrix);
            var map = new GameObject("Test_EnvironmentMap").transform;
            var terrainGroup = new GameObject("Test_MapTerrain").transform; terrainGroup.SetParent(map, false);
            var modelGroup = new GameObject("Test_ImportedModels").transform; modelGroup.SetParent(map, false);

            foreach (var group in models.GroupBy(t => PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject)))
            {
                var category = new GameObject(Path.GetFileNameWithoutExtension(group.Key)).transform;
                category.SetParent(modelGroup, false);
                foreach (var model in group)
                {
                    model.SetParent(category, true);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(model);
                }
            }
            foreach (var item in terrain) item.SetParent(terrainGroup, true);

            // 옮겨 둔 모델과 지형 외에는 복사본에서만 제거한다.
            foreach (var root in oldRoots) Object.DestroyImmediate(root);
            var remaining = AllTransforms(copy);
            foreach (var item in remaining)
                foreach (var script in item.GetComponents<MonoBehaviour>()) Object.DestroyImmediate(script);
            foreach (var pair in snapshots)
                if (MatrixDifference(pair.Key.localToWorldMatrix, pair.Value) > .0001f)
                    throw new InvalidOperationException("배치가 달라진 오브젝트: " + pair.Key.name);

            int missingScripts = remaining.Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
            int forbiddenComponents = remaining.Sum(t => t.GetComponents<MonoBehaviour>().Length + t.GetComponents<Camera>().Length +
                t.GetComponents<Light>().Length + t.GetComponents<AudioSource>().Length + t.GetComponents<Canvas>().Length + t.GetComponents<LineRenderer>().Length);
            int[] currentIds = AllTransforms(source).Select(t => t.GetInstanceID()).OrderBy(i => i).ToArray();
            bool originalUnchanged = source.path == Test_CheonSceneBuilder.ScenePath && FileHash(source.path) == originalHash &&
                originalIds.SequenceEqual(currentIds) && source.isDirty == sourceWasDirty;
            if (!originalUnchanged || models.Length != originalModels.Length || terrain.Length != originalTerrain.Length || missingScripts != 0 || forbiddenComponents != 0)
                throw new InvalidOperationException("씬 분리 무결성 검증 실패");
            EditorSceneManager.MarkSceneDirty(copy);
            if (!EditorSceneManager.SaveScene(copy)) throw new IOException("정리한 씬 저장 실패");
            var result = new {
                originalScene = source.path, newScene = destination, originalUnchanged, originalUnsavedChangesPreserved = sourceWasDirty,
                importedModels = models.Length,
                modelTypes = models.GroupBy(t => PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject))
                    .Select(g => new { prefab = g.Key, count = g.Count() }).ToArray(),
                terrain = terrain.GroupBy(t => t.name).Select(g => new { name = g.Key, count = g.Count() }).ToArray(),
                missingScripts, forbiddenComponents, originalHash, placementPreserved = true
            };
            File.WriteAllText("Assets/Temp/Test_CheonSceneSplitVerification.json", Newtonsoft.Json.JsonConvert.SerializeObject(result, Newtonsoft.Json.Formatting.Indented));
            return result;
        }
        finally
        {
            // 원본의 저장되지 않은 변경을 잃지 않도록 열린 원본 씬으로 돌아온다.
            SceneManager.SetActiveScene(source);
            if (copy.IsValid() && copy.isLoaded) EditorSceneManager.CloseScene(copy, true);
        }
    }

    private static Transform[] AllTransforms(Scene scene) => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();

    private static Transform[] ImportedModels(Scene scene) => AllTransforms(scene).Where(t => PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject) &&
        PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject).StartsWith(ModelFolder, StringComparison.Ordinal)).ToArray();

    private static string FileHash(string path)
    {
        using (var algorithm = SHA256.Create()) return BitConverter.ToString(algorithm.ComputeHash(File.ReadAllBytes(path))).Replace("-", "");
    }

    private static float MatrixDifference(Matrix4x4 first, Matrix4x4 second)
    {
        float difference = 0;
        for (int index = 0; index < 16; index++) difference = Mathf.Max(difference, Mathf.Abs(first[index] - second[index]));
        return difference;
    }
}
