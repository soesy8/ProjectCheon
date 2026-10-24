using System;
using System.IO;
using System.Linq;
using Cheon.Testing.MountainMap;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

public static class Test_MountainPlayerFont
{
    public static object Main()
    {
        const string root = "Assets/Test/CheonMountainMap";
        const string path = root + "/Arts/Fonts/Test_MountainPlayerKorean.asset";
        var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>("Assets/Test/PlayerFanAnimation/Fonts/Test_TMPSettings.asset");
        typeof(TMP_Settings).GetField("s_Instance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).SetValue(null, settings);
        if (!AssetDatabase.IsValidFolder(root + "/Arts")) AssetDatabase.CreateFolder(root, "Arts");
        if (!AssetDatabase.IsValidFolder(root + "/Arts/Fonts")) AssetDatabase.CreateFolder(root + "/Arts", "Fonts");
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (font == null)
        {
            var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Test/Cheon/Arts/Fonts/Test_CheonKorean.ttf");
            font = TMP_FontAsset.CreateFontAsset(source, 48, 5, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, false);
            font.name = "Test_MountainPlayerKorean";
            AssetDatabase.CreateAsset(font, path);
            font.material.name = "Test_M_MountainPlayerFont";
            AssetDatabase.AddObjectToAsset(font.material, font);
            font.atlasTextures[0].name = "Test_T_MountainPlayerGlyphs";
            AssetDatabase.AddObjectToAsset(font.atlasTextures[0], font);
        }
        string sourceText = string.Join("", Directory.GetFiles(root + "/Scripts", "*.cs").Select(File.ReadAllText)) +
            File.ReadAllText("Assets/Test/PlayerFanAnimation/Scripts/Test_PlayerActionTimings.cs") +
            new string(Enumerable.Range(32, 95).Select(c => (char)c).ToArray());
        uint[] characters = sourceText.Where(c => !char.IsControl(c) && c != '\uFEFF').Select(c => (uint)c).Distinct().Where(c => !font.HasCharacter((int)c)).ToArray();
        font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
        if (characters.Length > 0) font.TryAddCharacters(characters, out var missing);
        uint[] unavailable = characters.Where(c => !font.HasCharacter((int)c)).ToArray();
        if (unavailable.Length > 0) throw new InvalidOperationException("등록하지 못한 글자: " + string.Join(",", unavailable));
        font.atlasPopulationMode = AtlasPopulationMode.Static;
        EditorUtility.SetDirty(font);
        foreach (var texture in font.atlasTextures) EditorUtility.SetDirty(texture);
        foreach (var component in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Where(c => c is Test_PlayerControlGuide || c is Test_PlayerActionTimingUI))
        {
            var serialized = new SerializedObject(component);
            serialized.FindProperty("koreanFont").objectReferenceValue = font;
            serialized.ApplyModifiedProperties();
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        return new { path, added = characters.Length, total = font.characterTable.Count };
    }
}
