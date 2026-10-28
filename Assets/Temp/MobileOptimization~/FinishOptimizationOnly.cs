if (UnityEditor.BuildPipeline.isBuildingPlayer) throw new System.InvalidOperationException("The cancelled build has not finished unwinding.");
UnityEngine.QualitySettings.SetQualityLevel(1, true);
var qualityAsset = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0];
var quality = new UnityEditor.SerializedObject(qualityAsset);
quality.FindProperty("m_CurrentQuality").intValue = 1;
quality.ApplyModifiedPropertiesWithoutUndo();
UnityEditor.AssetDatabase.SaveAssetIfDirty(qualityAsset);
var playerAsset = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0];
var player = new UnityEditor.SerializedObject(playerAsset);
var batching = player.FindProperty("m_BuildTargetBatching");
if (batching.arraySize == 1 && batching.GetArrayElementAtIndex(0).FindPropertyRelative("m_BuildTarget").stringValue == "Android")
{
    batching.ClearArray(); player.ApplyModifiedPropertiesWithoutUndo();
    UnityEditor.AssetDatabase.SaveAssetIfDirty(playerAsset);
}
var globalAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets/Settings/UniversalRenderPipelineGlobalSettings.asset");
var globalSettings = new UnityEditor.SerializedObject(globalAsset);
globalSettings.FindProperty("m_Settings.m_RuntimeSettings.m_List").ClearArray();
globalSettings.ApplyModifiedPropertiesWithoutUndo();
UnityEditor.AssetDatabase.SaveAssetIfDirty(globalAsset);
System.IO.Directory.CreateDirectory("Assets/Temp/MobileOptimization~/BuildMetadata");
foreach (string filename in new[] { "PerformanceTestRunInfo.json", "PerformanceTestRunSettings.json" })
foreach (string suffix in new[] { "", ".meta" })
{
    string source = "Assets/Resources/" + filename + suffix;
    string destination = "Assets/Temp/MobileOptimization~/BuildMetadata/" + filename + suffix;
    if (System.IO.File.Exists(source) && !System.IO.File.Exists(destination)) System.IO.File.Move(source, destination);
}
if (System.IO.Directory.Exists("Assets/Resources") && System.IO.Directory.GetFileSystemEntries("Assets/Resources").Length == 0)
    UnityEditor.AssetDatabase.DeleteAsset("Assets/Resources");
if (System.IO.Directory.Exists("Assets/Temp/MobileOptimization") && System.Array.TrueForAll(System.IO.Directory.GetFiles("Assets/Temp/MobileOptimization", "*", System.IO.SearchOption.AllDirectories), p => p.EndsWith(".meta")))
    UnityEditor.AssetDatabase.DeleteAsset("Assets/Temp/MobileOptimization");
UnityEditor.AssetDatabase.Refresh();
return new { building = UnityEditor.BuildPipeline.isBuildingPlayer, quality = UnityEngine.QualitySettings.GetQualityLevel(), target = UnityEditor.EditorUserBuildSettings.activeBuildTarget.ToString(), playing = UnityEditor.EditorApplication.isPlaying };
