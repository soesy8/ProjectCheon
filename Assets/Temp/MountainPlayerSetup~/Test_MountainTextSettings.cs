using System.Linq;
using Cheon.Testing.MountainMap;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class Test_MountainTextSettings
{
    public static object Main()
    {
        const string path = "Assets/Test/CheonMountainMap/Settings/Test_MountainTextSettings.asset";
        Cheon.Testing.Test_CheonTextSettings.Ensure();
        if (AssetDatabase.LoadAssetAtPath<TMP_Settings>(path) == null)
            AssetDatabase.CopyAsset("Assets/Test/PlayerFanAnimation/Fonts/Test_TMPSettings.asset", path);
        var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(path);
        var serialized = new SerializedObject(settings);
        serialized.FindProperty("assetVersion").stringValue = (string)typeof(TMP_Settings).GetField("s_CurrentAssetVersion", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).GetValue(null);
        serialized.FindProperty("m_defaultFontAsset").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/Test/CheonMountainMap/Arts/Fonts/Test_MountainPlayerKorean.asset");
        serialized.ApplyModifiedProperties();
        typeof(TMP_Settings).GetField("s_Instance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).SetValue(null, settings);
        foreach (var component in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Where(c => c is Test_PlayerControlGuide || c is Test_PlayerActionTimingUI))
        {
            var ui = new SerializedObject(component);
            ui.FindProperty("textSettings").objectReferenceValue = settings;
            ui.ApplyModifiedProperties();
        }
        AssetDatabase.SaveAssetIfDirty(settings);
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        return new { settings = path };
    }
}
