UnityEditor.SessionState.SetBool("Cheon.VerifyMobileQuality", false);
string bootstrap = "Assets/Temp/MobileOptimization/Test_MobileQualityBootstrap.cs";
if (System.IO.File.Exists(bootstrap))
{
    System.IO.File.Copy(bootstrap, "Assets/Temp/MobileOptimization~/Test_MobileQualityBootstrap.cs", true);
    UnityEditor.AssetDatabase.DeleteAsset(bootstrap);
}
var settings = new UnityEditor.SerializedObject(UnityEditor.AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
var excluded = settings.FindProperty("m_QualitySettings").GetArrayElementAtIndex(0).FindPropertyRelative("excludedTargetPlatforms");
bool hasStandalone = false;
for (int i = 0; i < excluded.arraySize; i++) if (excluded.GetArrayElementAtIndex(i).stringValue == "Standalone") hasStandalone = true;
if (!hasStandalone) { int index = excluded.arraySize; excluded.InsertArrayElementAtIndex(index); excluded.GetArrayElementAtIndex(index).stringValue = "Standalone"; }
int previous = UnityEditor.EditorPrefs.GetInt("Cheon.MobileOptimization.PreviousQuality", 1);
settings.FindProperty("m_CurrentQuality").intValue = previous;
settings.ApplyModifiedPropertiesWithoutUndo();
UnityEngine.QualitySettings.SetQualityLevel(previous, true);
UnityEditor.AssetDatabase.SaveAssets();
return new { quality = UnityEngine.QualitySettings.GetQualityLevel(), target = UnityEditor.EditorUserBuildSettings.activeBuildTarget.ToString() };
