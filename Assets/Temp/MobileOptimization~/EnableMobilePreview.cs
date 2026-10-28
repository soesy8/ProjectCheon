var settings = new UnityEditor.SerializedObject(UnityEditor.AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
var excluded = settings.FindProperty("m_QualitySettings").GetArrayElementAtIndex(0).FindPropertyRelative("excludedTargetPlatforms");
for (int i = excluded.arraySize - 1; i >= 0; i--)
    if (excluded.GetArrayElementAtIndex(i).stringValue == "Standalone") excluded.DeleteArrayElementAtIndex(i);
settings.ApplyModifiedPropertiesWithoutUndo();
UnityEditor.AssetDatabase.SaveAssets();
UnityEngine.QualitySettings.SetQualityLevel(0, true);
return UnityEngine.QualitySettings.names;
