using System;
using System.Linq;
using Cheon.Prototype.MountainPass;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FogAdjustment
{
    public static object Apply()
    {
        if (Application.isPlaying) throw new InvalidOperationException("편집 모드에서 씬을 수정합니다.");
        var scene = SceneManager.GetActiveScene();
        var fog = UnityEngine.Object.FindObjectsByType<PassHeightFog>(FindObjectsSortMode.None)
            .Single(component => component.name == "RearMistCurtain");
        var player = UnityEngine.Object.FindFirstObjectByType<PassPlayerMovement>();
        var renderers = player.GetComponentsInChildren<SkinnedMeshRenderer>()
            .Where(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy).ToArray();
        float headHeight = renderers.Max(renderer => renderer.bounds.max.y);
        var serialized = new SerializedObject(fog);
        Vector2 oldArea = serialized.FindProperty("areaSize").vector2Value;
        float oldDepth = serialized.FindProperty("volumeDepth").floatValue;
        Vector3 oldPosition = fog.transform.position;
        var boundary = serialized.FindProperty("densityBoundary").objectReferenceValue as Transform;
        System.IO.File.Copy(scene.path, "Assets/Temp/FogAdjustment~/BeforeScene.unity", false);

        Undo.RegisterFullObjectHierarchyUndo(fog.gameObject, "짙은 안개 크기와 높이 조정");
        // Transform 스케일은 컴포넌트가 매번 덮어쓰므로 실제 안개 영역 설정을 변경한다.
        serialized.FindProperty("areaSize").vector2Value = oldArea * 2f;
        serialized.FindProperty("volumeDepth").floatValue = oldDepth * 2f;
        serialized.ApplyModifiedProperties();
        Vector3 position = oldPosition;
        position.y = headHeight + 1f;
        fog.transform.position = position;
        fog.ApplySettings();
        EditorUtility.SetDirty(fog);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        if (!UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("씬을 저장하지 못했습니다.");
        Selection.activeGameObject = fog.gameObject;
        return new
        {
            fog.name,
            before = new { area = new[] { oldArea.x, oldArea.y }, depth = oldDepth, position = Vector(oldPosition) },
            after = new { area = new[] { oldArea.x * 2f, oldArea.y * 2f }, depth = oldDepth * 2f, position = Vector(position) },
            playerHeadHeight = headHeight,
            topAboveHead = position.y - headHeight,
            bounds = Box(fog.GetComponentInChildren<MeshRenderer>().bounds),
            boundary = boundary == null ? null : new { boundary.name, position = Vector(boundary.position), forward = Vector(boundary.forward) },
            scene.isDirty
        };
    }

    public static object Inspect()
    {
        var scene = SceneManager.GetActiveScene();
        var fogs = UnityEngine.Object.FindObjectsByType<PassHeightFog>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var controllers = UnityEngine.Object.FindObjectsByType<PassMountainMist>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var player = UnityEngine.Object.FindFirstObjectByType<PassPlayerMovement>();
        return new
        {
            playing = Application.isPlaying,
            scene.path,
            scene.isDirty,
            selected = Selection.activeGameObject != null ? Selection.activeGameObject.name : null,
            fogs = fogs.Select(fog => new
            {
                fog.name,
                fog.enabled,
                position = Vector(fog.transform.position),
                scale = Vector(fog.transform.localScale),
                fields = GetFields(fog),
                renderers = fog.GetComponentsInChildren<Renderer>(true).Select(renderer => new
                {
                    renderer.name,
                    renderer.enabled,
                    bounds = Box(renderer.bounds),
                    materials = renderer.sharedMaterials.Select(material => material != null ? AssetDatabase.GetAssetPath(material) : null).ToArray()
                }).ToArray()
            }).ToArray(),
            controllers = controllers.Select(controller => new { controller.name, fields = GetFields(controller) }).ToArray(),
            player = player == null ? null : new
            {
                player.name,
                position = Vector(player.transform.position),
                scale = Vector(player.transform.lossyScale),
                controllers = player.GetComponentsInChildren<CharacterController>(true).Select(controller => new { controller.name, controller.height, center = Vector(controller.center), bounds = Box(controller.bounds) }).ToArray(),
                renderers = player.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(renderer => new { renderer.name, renderer.enabled, active = renderer.gameObject.activeInHierarchy, bounds = Box(renderer.bounds) }).ToArray()
            }
        };
    }

    private static object[] GetFields(UnityEngine.Object target)
    {
        var serialized = new SerializedObject(target);
        var iterator = serialized.GetIterator();
        var fields = new System.Collections.Generic.List<object>();
        bool enterChildren = true;
        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;
            object value;
            switch (iterator.propertyType)
            {
                case SerializedPropertyType.Float: value = iterator.floatValue; break;
                case SerializedPropertyType.Vector2: value = new[] { iterator.vector2Value.x, iterator.vector2Value.y }; break;
                case SerializedPropertyType.Color: value = new[] { iterator.colorValue.r, iterator.colorValue.g, iterator.colorValue.b, iterator.colorValue.a }; break;
                case SerializedPropertyType.ObjectReference: value = iterator.objectReferenceValue != null ? iterator.objectReferenceValue.name : null; break;
                default: continue;
            }
            fields.Add(new { name = iterator.name, value });
        }
        return fields.ToArray();
    }

    private static float[] Vector(Vector3 value) => new[] { value.x, value.y, value.z };
    private static object Box(Bounds value) => new { min = Vector(value.min), max = Vector(value.max), size = Vector(value.size) };
}
