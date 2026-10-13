UnityEditor.AssetDatabase.Refresh();
if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/Proto"))
    UnityEditor.AssetDatabase.CreateFolder("Assets", "Proto");
if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/Proto/Scripts"))
    UnityEditor.AssetDatabase.CreateFolder("Assets/Proto", "Scripts");
foreach (var folder in new[] { "Player", "Combat", "World" })
{
    string error = UnityEditor.AssetDatabase.MoveAsset(
        "Assets/Scripts/" + folder, "Assets/Proto/Scripts/" + folder);
    if (!string.IsNullOrEmpty(error))
        throw new System.Exception(error);
}
return "Moved prototype scripts to Assets/Proto/Scripts with their meta files.";
