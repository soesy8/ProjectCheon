using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class AssetOrganization
{
    private const string Work = "Assets/Temp/AssetOrganization~/";
    private static JObject ReadPlan() => JObject.Parse(File.ReadAllText(Work + "OrganizationPlan.json"));

    public static object Inspect()
    {
        var scenes = Enumerable.Range(0, SceneManager.sceneCount).Select(index =>
        {
            var scene = SceneManager.GetSceneAt(index);
            var objects = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
            return new
            {
                scene.path,
                scene.isDirty,
                objects = objects.Length,
                missingScripts = objects.Sum(item => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject)),
                animators = objects.SelectMany(item => item.GetComponents<Animator>()).Select(animator => new
                {
                    animator.name,
                    controller = AssetDatabase.GetAssetPath(animator.runtimeAnimatorController)
                }).ToArray()
            };
        }).ToArray();
        return new { compiling = EditorApplication.isCompiling, compilationFailed = EditorUtility.scriptCompilationFailed, scenes };
    }

    public static object Move()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            throw new InvalidOperationException("에디터가 편집 가능한 상태가 아닙니다.");

        var plan = ReadPlan();
        if (!File.Exists(Work + "BeforeEditor.json"))
            File.WriteAllText(Work + "BeforeEditor.json", JsonConvert.SerializeObject(Inspect(), Formatting.Indented));
        var moved = File.Exists(Work + "MoveProgress.json")
            ? JArray.Parse(File.ReadAllText(Work + "MoveProgress.json")).Cast<object>().ToList()
            : new List<object>();
        // AssetDatabase로 이동하여 에셋과 폴더의 기존 GUID를 함께 보존한다.
        // 새 부모 폴더가 데이터베이스에 반영된 뒤 다음 이동을 실행한다.
        foreach (var move in plan["moves"].Skip(moved.Count))
        {
            string from = (string)move["from"];
            string to = (string)move["to"];
            MovePath(from, to);
            moved.Add(new { from, to });
            File.WriteAllText(Work + "MoveProgress.json", JsonConvert.SerializeObject(moved, Formatting.Indented));
        }
        return new { operations = moved.Count, editor = Inspect() };
    }

    public static object Remap()
    {
        var plan = ReadPlan();
        var replacements = plan["duplicates"].ToDictionary(item => (string)item["guid"], item => (string)item["keepGuid"]);
        var referrers = plan["referrers"].Values<string>().ToArray();
        var changed = new List<object>();
        var backups = Work + "ReferenceBackups/";
        foreach (string assetPath in referrers)
        {
            if (!File.Exists(assetPath))
                throw new FileNotFoundException("참조를 교체할 에셋을 찾을 수 없습니다.", assetPath);
            string backup = backups + assetPath;
            Directory.CreateDirectory(Path.GetDirectoryName(backup));
            File.Copy(assetPath, backup, false);
            string extension = Path.GetExtension(assetPath);
            int count = 0;
            if (extension == ".unity")
            {
                var scene = SceneManager.GetSceneByPath(assetPath);
                bool opened = !scene.IsValid() || !scene.isLoaded;
                if (opened) scene = EditorSceneManager.OpenScene(assetPath, OpenSceneMode.Additive);
                try
                {
                    if (scene.isDirty)
                        throw new InvalidOperationException("저장되지 않은 씬은 자동으로 덮어쓰지 않습니다: " + assetPath);
                    foreach (var root in scene.GetRootGameObjects()) count += RemapHierarchy(root, replacements);
                    if (count > 0)
                    {
                        EditorSceneManager.MarkSceneDirty(scene);
                        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("씬 저장 실패: " + assetPath);
                    }
                }
                finally
                {
                    if (opened) EditorSceneManager.CloseScene(scene, true);
                }
            }
            else if (extension == ".prefab")
            {
                var root = PrefabUtility.LoadPrefabContents(assetPath);
                try
                {
                    count = RemapHierarchy(root, replacements);
                    if (count > 0) PrefabUtility.SaveAsPrefabAsset(root, assetPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            else
            {
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
                {
                    if (asset == null) continue;
                    int assetChanges = RemapObject(asset, replacements);
                    count += assetChanges;
                    if (assetChanges > 0) AssetDatabase.SaveAssetIfDirty(asset);
                }
            }
            changed.Add(new { path = assetPath, references = count });
            File.WriteAllText(Work + "ReferenceChanges.json", JsonConvert.SerializeObject(changed, Formatting.Indented));
        }
        return new { changed = changed.ToArray() };
    }

    public static object RemoveDuplicates()
    {
        var plan = ReadPlan();
        var duplicates = plan["duplicates"].ToArray();
        var removedPaths = new HashSet<string>(duplicates.Select(item => (string)item["path"]), StringComparer.OrdinalIgnoreCase);
        var removedGuids = new HashSet<string>(duplicates.Select(item => (string)item["guid"]));
        var extensions = new HashSet<string> { ".meta", ".mat", ".asset", ".unity", ".prefab", ".anim", ".controller", ".playable", ".json", ".cs", ".txt", ".shader", ".cginc", ".mask", ".terrainlayer", ".inputactions" };
        // 삭제 전에 문자열 GUID까지 검사하여 폰트 생성 정보 등의 숨은 참조도 확인한다.
        foreach (string file in Directory.GetFiles("Assets", "*", SearchOption.AllDirectories))
        {
            string assetPath = file.Replace('\\', '/');
            if (assetPath.StartsWith("Assets/Temp/") || removedPaths.Contains(assetPath) ||
                (assetPath.EndsWith(".meta") && removedPaths.Contains(assetPath.Substring(0, assetPath.Length - 5))) ||
                !extensions.Contains(Path.GetExtension(assetPath))) continue;
            byte[] bytes = File.ReadAllBytes(file);
            if (bytes.Contains((byte)0)) continue;
            string text = Encoding.UTF8.GetString(bytes);
            foreach (string guid in removedGuids)
                if (text.Contains(guid)) throw new InvalidOperationException("삭제 대상의 참조가 남아 있습니다: " + assetPath + " -> " + guid);
        }

        var removed = new List<string>();
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var item in duplicates)
            {
                string assetPath = (string)item["path"];
                string keepGuid = (string)item["keepGuid"];
                if (string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(keepGuid)))
                    throw new InvalidOperationException("남길 에셋을 찾을 수 없습니다: " + keepGuid);
                if (!AssetDatabase.DeleteAsset(assetPath)) throw new IOException("중복 에셋 삭제 실패: " + assetPath);
                removed.Add(assetPath);
                File.WriteAllText(Work + "RemovedDuplicates.json", JsonConvert.SerializeObject(removed, Formatting.Indented));
            }
            foreach (var relocation in plan["keepRelocations"])
                MovePath((string)relocation["from"], (string)relocation["to"]);
            foreach (string folder in plan["deleteFolders"].Values<string>())
            {
                if (Directory.GetFiles(folder, "*", SearchOption.AllDirectories).Any(file => !file.EndsWith(".meta")))
                    throw new InvalidOperationException("정리 대상 폴더 안에 이동하지 않은 파일이 있습니다: " + folder);
                if (!AssetDatabase.DeleteAsset(folder)) throw new IOException("빈 폴더 삭제 실패: " + folder);
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }
        AssetDatabase.Refresh();
        return new { removed = removed.Count, editor = Inspect() };
    }

    public static object CleanEmptyFolders()
    {
        string[] folders =
        {
            "Assets/Arts/Characters/Player/Animations/MountainPass",
            "Assets/Arts/Environment/Cheon/EnvironmentModels/01_Lantern/Textures",
            "Assets/Arts/Environment/Cheon/EnvironmentModels/04_Gate/Textures",
            "Assets/Arts/Environment/Cheon/EnvironmentModels/06_Bell_Pavilion/Textures"
        };
        foreach (string folder in folders)
        {
            // 이번 통합으로 비게 된 폴더만 제거한다.
            if (!Directory.Exists(folder) || Directory.GetFileSystemEntries(folder).Length != 0)
                throw new InvalidOperationException("비어 있는 폴더가 아닙니다: " + folder);
            if (!AssetDatabase.DeleteAsset(folder)) throw new IOException("빈 폴더 삭제 실패: " + folder);
        }
        return new { removedFolders = folders, editor = Inspect() };
    }

    private static int RemapHierarchy(GameObject root, Dictionary<string, string> replacements)
    {
        int count = 0;
        foreach (var transform in root.GetComponentsInChildren<Transform>(true))
        {
            count += RemapObject(transform.gameObject, replacements);
            foreach (var component in transform.GetComponents<Component>())
                if (component != null) count += RemapObject(component, replacements);
        }
        return count;
    }

    private static int RemapObject(UnityEngine.Object target, Dictionary<string, string> replacements)
    {
        var serialized = new SerializedObject(target);
        var property = serialized.GetIterator();
        int changes = 0;
        while (property.Next(true))
        {
            if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue != null)
            {
                var original = property.objectReferenceValue;
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(original, out string guid, out long localId) || !replacements.TryGetValue(guid, out string replacementGuid)) continue;
                string keepPath = AssetDatabase.GUIDToAssetPath(replacementGuid);
                var replacement = AssetDatabase.LoadAllAssetsAtPath(keepPath).FirstOrDefault(asset =>
                    asset != null && asset.GetType() == original.GetType() &&
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string unusedGuid, out long replacementId) && replacementId == localId);
                if (replacement == null) throw new InvalidOperationException("호환되는 서브 에셋을 찾지 못했습니다: " + keepPath + " #" + localId);
                property.objectReferenceValue = replacement;
                changes++;
            }
            else if (property.propertyType == SerializedPropertyType.String && replacements.TryGetValue(property.stringValue ?? "", out string replacementGuid))
            {
                property.stringValue = replacementGuid;
                changes++;
            }
        }
        if (changes > 0)
        {
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }
        return changes;
    }

    private static void MovePath(string from, string to)
    {
        if (!File.Exists(from) && !Directory.Exists(from)) throw new FileNotFoundException("이동할 경로가 없습니다.", from);
        if (File.Exists(to) || Directory.Exists(to)) throw new IOException("대상 경로에 파일이 이미 있습니다: " + to);
        EnsureFolder(Path.GetDirectoryName(to).Replace('\\', '/'));
        string error = AssetDatabase.MoveAsset(from, to);
        if (!string.IsNullOrEmpty(error)) throw new IOException(from + " -> " + to + ": " + error);
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        string guid = AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        if (string.IsNullOrEmpty(guid)) throw new IOException("폴더 생성 실패: " + folder);
    }
}
