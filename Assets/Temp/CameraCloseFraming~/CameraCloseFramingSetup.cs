using Cheon.Prototype.MountainPass;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CameraCloseFramingSetup
{
    public static object Apply()
    {
        var camera = Object.FindFirstObjectByType<PassCamera>();
        var settings = new SerializedObject(camera);
        Undo.RecordObject(camera, "가까운 보스 주시 구도 적용");
        settings.FindProperty("lockDeadZone").rectValue = new Rect(.44f, .45f, .12f, .2f);
        settings.FindProperty("lockFollowSpeed").floatValue = 10f;
        settings.FindProperty("framingMargin").floatValue = .05f;
        settings.FindProperty("framingReturnSpeed").floatValue = 5f;
        settings.FindProperty("maximumFramingLift").floatValue = 4f;
        settings.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(camera.gameObject.scene);
        bool saved = EditorSceneManager.SaveScene(camera.gameObject.scene);
        return new { saved, settings = EditorJsonUtility.ToJson(camera) };
    }
}
