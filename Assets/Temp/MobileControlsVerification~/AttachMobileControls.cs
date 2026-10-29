using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Cheon.Prototype.MountainPass;

public static class AttachMobileControls
{
    public static object Main()
    {
        var hud = Object.FindFirstObjectByType<PassHud>();
        if (hud == null || EditorApplication.isPlaying) throw new System.InvalidOperationException("Expected scene HUD in Edit Mode.");
        var controls = hud.GetComponent<PassMobileControls>();
        if (controls == null) controls = Undo.AddComponent<PassMobileControls>(hud.gameObject);
        var serialized = new SerializedObject(controls);
        string[] fields = { "attackSprite", "dodgeSprite", "healSprite", "lockOnSprite", "pauseSprite", "interactSprite", "joystickBaseSprite", "joystickKnobSprite" };
        string[] names = { "Attack", "Dodge", "Heal", "LockOn", "Pause", "Interact", "JoystickBase", "JoystickKnob" };
        for (int index = 0; index < fields.Length; index++)
        {
            string path = "Assets/Arts/UI/MountainPass/MobileControls/T_Mobile" + names[index] + ".png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) throw new System.InvalidOperationException("Missing sprite: " + path);
            serialized.FindProperty(fields[index]).objectReferenceValue = sprite;
        }
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(controls);
        EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);
        EditorSceneManager.SaveScene(hud.gameObject.scene);
        return new { scene = hud.gameObject.scene.path, target = hud.name, spriteCount = fields.Length, mobileOnly = !PassMobileControls.SupportsPlatform };
    }
}
