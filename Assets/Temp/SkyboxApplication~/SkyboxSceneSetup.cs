using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;

public static class SkyboxSceneSetup
{
    private const string MaterialPath = "Assets/Arts/Environment/Cheon/Skyboxes/M_CheonMountainClouds_Skybox.mat";
    private const string WorkFolder = "Assets/Temp/SkyboxApplication~";

    public static object InspectRuntime()
    {
        var scene = SceneManager.GetActiveScene();
        var skybox = RenderSettings.skybox;
        return SaveReport("RuntimeState", new {
            playing = EditorApplication.isPlaying, paused = EditorApplication.isPaused,
            frame = Time.frameCount, scene = scene.path,
            skyboxName = skybox != null ? skybox.name : null,
            texture = skybox != null && skybox.HasProperty("_MainTex") ? AssetDatabase.GetAssetPath(skybox.GetTexture("_MainTex")) : null,
            skyTint = skybox != null && skybox.HasProperty("_Tint") ? skybox.GetColor("_Tint").ToString() : null,
            cameras = FindCameras(scene).Select(camera => new { camera.name, camera.enabled, clearFlags = camera.clearFlags.ToString() }).ToArray()
        });
    }

    public static object InspectScenes()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("편집 모드에서 검사합니다.");
        var originalActive = SceneManager.GetActiveScene();
        var reports = new List<object>();
        try
        {
            foreach (string path in FindScenePaths())
            {
                Scene scene = SceneManager.GetSceneByPath(path);
                bool wasLoaded = scene.IsValid() && scene.isLoaded;
                if (!wasLoaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    SceneManager.SetActiveScene(scene);
                    reports.Add(new {
                        path, wasLoaded, scene.isDirty,
                        skybox = RenderSettings.skybox != null ? AssetDatabase.GetAssetPath(RenderSettings.skybox) : null,
                        cameras = FindCameras(scene).Select(camera => new {
                            path = ObjectPath(camera.transform), camera.enabled,
                            camera.gameObject.activeInHierarchy,
                            clearFlags = camera.clearFlags.ToString(),
                            renderType = camera.TryGetComponent<UniversalAdditionalCameraData>(out var data) ? data.renderType.ToString() : "Base",
                            overrideMaterial = camera.TryGetComponent<Skybox>(out var skybox) && skybox.material != null ? AssetDatabase.GetAssetPath(skybox.material) : null,
                            position = Vector(camera.transform.position), rotation = Vector(camera.transform.eulerAngles)
                        }).ToArray()
                    });
                }
                finally
                {
                    if (originalActive.IsValid() && originalActive.isLoaded) SceneManager.SetActiveScene(originalActive);
                    if (!wasLoaded) EditorSceneManager.CloseScene(scene, true);
                }
            }
        }
        finally
        {
            if (originalActive.IsValid() && originalActive.isLoaded) SceneManager.SetActiveScene(originalActive);
        }
        return SaveReport("SceneInspection", new { activeScene = originalActive.path, scenes = reports });
    }

    public static object ApplyAllScenes()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("편집 모드에서 적용합니다.");
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null || material.shader.name != "Skybox/Panoramic")
            throw new InvalidOperationException("공용 스카이박스 머티리얼을 확인할 수 없습니다.");
        var originalActive = SceneManager.GetActiveScene();
        var reports = new List<object>();
        try
        {
            foreach (string path in FindScenePaths())
            {
                Scene scene = SceneManager.GetSceneByPath(path);
                bool wasLoaded = scene.IsValid() && scene.isLoaded;
                if (!wasLoaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    SceneManager.SetActiveScene(scene);
                    BackupScene(scene);
                    RenderSettings.skybox = material;
                    var changedCameras = new List<string>();
                    foreach (var camera in FindCameras(scene))
                    {
                        // 겹쳐 그리는 UI 카메라는 기존 깊이 버퍼 동작을 유지한다.
                        bool isOverlay = camera.TryGetComponent<UniversalAdditionalCameraData>(out var data) &&
                            data.renderType == CameraRenderType.Overlay;
                        if (isOverlay) continue;
                        Undo.RecordObject(camera, "공용 스카이박스 적용");
                        camera.clearFlags = CameraClearFlags.Skybox;
                        EditorUtility.SetDirty(camera);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(camera);
                        if (camera.TryGetComponent<Skybox>(out var skybox) && skybox.enabled)
                        {
                            Undo.RecordObject(skybox, "공용 스카이박스 적용");
                            skybox.material = material;
                            EditorUtility.SetDirty(skybox);
                            PrefabUtility.RecordPrefabInstancePropertyModifications(skybox);
                        }
                        changedCameras.Add(ObjectPath(camera.transform));
                    }
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene)) throw new IOException("씬 저장 실패: " + path);
                    reports.Add(new { path, material = AssetDatabase.GetAssetPath(RenderSettings.skybox), cameras = changedCameras.ToArray(), saved = !scene.isDirty });
                }
                finally
                {
                    if (originalActive.IsValid() && originalActive.isLoaded) SceneManager.SetActiveScene(originalActive);
                    if (!wasLoaded) EditorSceneManager.CloseScene(scene, true);
                }
            }
        }
        finally
        {
            if (originalActive.IsValid() && originalActive.isLoaded) SceneManager.SetActiveScene(originalActive);
            SceneView.RepaintAll();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }
        return SaveReport("Application", new { material = MaterialPath, scenes = reports });
    }

    public static object VerifyAllScenes()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("편집 모드에서 검사합니다.");
        var originalActive = SceneManager.GetActiveScene();
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        var reports = new List<object>();
        try
        {
            foreach (string path in FindScenePaths())
            {
                Scene scene = SceneManager.GetSceneByPath(path);
                bool wasLoaded = scene.IsValid() && scene.isLoaded;
                if (!wasLoaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    SceneManager.SetActiveScene(scene);
                    if (RenderSettings.skybox != material) throw new InvalidOperationException("스카이박스 불일치: " + path);
                    var cameras = FindCameras(scene);
                    foreach (var camera in cameras)
                    {
                        if (camera.TryGetComponent<UniversalAdditionalCameraData>(out var data) && data.renderType == CameraRenderType.Overlay) continue;
                        if (camera.clearFlags != CameraClearFlags.Skybox) throw new InvalidOperationException("카메라 배경 불일치: " + path + "/" + camera.name);
                        if (camera.TryGetComponent<Skybox>(out var skybox) && skybox.enabled && skybox.material != null && skybox.material != material)
                            throw new InvalidOperationException("카메라 전용 스카이박스 불일치: " + path + "/" + camera.name);
                    }
                    string preview = RenderScenePreview(scene, cameras.First(camera => camera.enabled));
                    reports.Add(new { path, skybox = MaterialPath, cameraCount = cameras.Length, scene.isDirty, preview });
                }
                finally
                {
                    if (originalActive.IsValid() && originalActive.isLoaded) SceneManager.SetActiveScene(originalActive);
                    if (!wasLoaded) EditorSceneManager.CloseScene(scene, true);
                }
            }
        }
        finally
        {
            if (originalActive.IsValid() && originalActive.isLoaded) SceneManager.SetActiveScene(originalActive);
        }
        return SaveReport("Verification", new { sceneCount = reports.Count, scenes = reports });
    }

    private static string RenderScenePreview(Scene scene, Camera source)
    {
        var previewScene = EditorSceneManager.NewPreviewScene();
        var previewObject = new GameObject("SkyboxScenePreview");
        SceneManager.MoveGameObjectToScene(previewObject, previewScene);
        var camera = previewObject.AddComponent<Camera>();
        camera.CopyFrom(source);
        camera.enabled = false;
        camera.scene = scene;
        camera.aspect = 1.6f;
        camera.allowHDR = false;
        camera.allowMSAA = false;
        var sourceData = source.GetComponent<UniversalAdditionalCameraData>();
        if (sourceData != null)
        {
            var data = previewObject.AddComponent<UniversalAdditionalCameraData>();
            EditorUtility.CopySerialized(sourceData, data);
        }
        // 게임 카메라는 그대로 두고 사당 진입부에서 배경과 지형의 조화를 확인한다.
        camera.transform.position = new Vector3(-2.5f, 31f, 43f);
        camera.transform.rotation = Quaternion.LookRotation(new Vector3(0.77f, -0.13f, -0.64f));
        var renderTexture = new RenderTexture(1024, 640, 24, RenderTextureFormat.ARGB32);
        var previous = RenderTexture.active;
        var image = new Texture2D(1024, 640, TextureFormat.RGB24, false);
        string outputPath = WorkFolder + "/T_" + scene.name + "_Skybox.png";
        try
        {
            camera.targetTexture = renderTexture;
            camera.Render();
            RenderTexture.active = renderTexture;
            image.ReadPixels(new Rect(0, 0, 1024, 640), 0, 0);
            image.Apply();
            File.WriteAllBytes(outputPath, image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            camera.targetTexture = null;
            renderTexture.Release();
            UnityEngine.Object.DestroyImmediate(renderTexture);
            UnityEngine.Object.DestroyImmediate(image);
            EditorSceneManager.ClosePreviewScene(previewScene);
        }
        return outputPath;
    }

    public static object SetSpiritWorldForPreview(bool active)
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("재생 모드에서 검사합니다.");
        var transition = UnityEngine.Object.FindFirstObjectByType<Cheon.Prototype.MountainPass.PassWorldTransition>();
        if (transition == null) throw new InvalidOperationException("영계 전환 컴포넌트가 없습니다.");
        transition.SetSpiritWorld(active);
        return new { spiritWorld = active, frame = Time.frameCount };
    }

    private static string[] FindScenePaths() => AssetDatabase.FindAssets("t:Scene", new[] { "Assets" })
        .Select(AssetDatabase.GUIDToAssetPath).Where(path => path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
        .Distinct().OrderBy(path => path).ToArray();

    private static Camera[] FindCameras(Scene scene) => scene.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<Camera>(true)).ToArray();

    private static void BackupScene(Scene scene)
    {
        string backupPath = WorkFolder + "/Backups/" + scene.path;
        Directory.CreateDirectory(Path.GetDirectoryName(backupPath));
        if (!File.Exists(backupPath))
        {
            File.Copy(scene.path, backupPath);
            if (File.Exists(scene.path + ".meta")) File.Copy(scene.path + ".meta", backupPath + ".meta");
            if (scene.isDirty && !EditorSceneManager.SaveScene(scene, backupPath + ".unsaved.unity", true))
                throw new IOException("편집 중인 씬 백업 실패: " + scene.path);
        }
    }

    private static object SaveReport(string name, object report)
    {
        File.WriteAllText(WorkFolder + "/" + name + ".json", JsonConvert.SerializeObject(report, Formatting.Indented));
        return JObject.FromObject(report);
    }

    private static float[] Vector(Vector3 value) => new[] { value.x, value.y, value.z };
    private static string ObjectPath(Transform transform) => transform.parent == null ? transform.name : ObjectPath(transform.parent) + "/" + transform.name;
}
