var veil = UnityEngine.Object.FindFirstObjectByType<Cheon.Prototype.MountainPass.PassStairCloudVeil>();
Undo.RecordObject(veil, "Adjust stair cloud clearing");
var serialized = new SerializedObject(veil);
serialized.FindProperty("clearingSeconds").floatValue = 2.8f;
serialized.FindProperty("openingDistance").floatValue = 0.5f;
serialized.ApplyModifiedProperties();
EditorUtility.SetDirty(veil);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(veil.gameObject.scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(veil.gameObject.scene);
return new { clearingSeconds = serialized.FindProperty("clearingSeconds").floatValue,
    openingDistance = serialized.FindProperty("openingDistance").floatValue };
