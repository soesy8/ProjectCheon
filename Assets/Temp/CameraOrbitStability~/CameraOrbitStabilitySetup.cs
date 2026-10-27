using Cheon.Prototype.MountainPass;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CameraOrbitStabilitySetup
{
    public static object Apply()
    {
        var camera = Object.FindFirstObjectByType<PassCamera>();
        Undo.RecordObject(camera, "기본 궤도 유지 카메라 조정");
        var settings = new SerializedObject(camera);
        settings.FindProperty("lockPitchAllowance").floatValue = 20f;
        settings.FindProperty("maximumFramingRetreat").floatValue = 12f;
        settings.FindProperty("maximumFramingSpeed").floatValue = 12f;
        settings.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(camera.gameObject.scene);
        return new { saved = EditorSceneManager.SaveScene(camera.gameObject.scene), settings = EditorJsonUtility.ToJson(camera) };
    }
}
