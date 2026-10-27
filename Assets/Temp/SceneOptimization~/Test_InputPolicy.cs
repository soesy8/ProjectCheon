using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
public static class Test_InputPolicy
{
    private const string Key="CheonOptimization.OriginalInputSettings";
    public static object Enable()
    {
        if(SessionState.GetInt(Key,0)!=0)return "Already enabled";
        SessionState.SetInt(Key,InputSystem.settings.GetInstanceID());
        var copy=Object.Instantiate(InputSystem.settings);copy.hideFlags=HideFlags.HideAndDontSave;
        copy.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        copy.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings=copy;
        return new{behavior=InputSystem.settings.editorInputBehaviorInPlayMode.ToString(),background=InputSystem.settings.backgroundBehavior.ToString()};
    }
    public static object Restore()
    {
        int id=SessionState.GetInt(Key,0);if(id==0)return "No override";
        var original=EditorUtility.InstanceIDToObject(id) as InputSettings;
        var copy=InputSystem.settings;if(original!=null)InputSystem.settings=original;
        if(copy!=original && copy.hideFlags==HideFlags.HideAndDontSave)Object.DestroyImmediate(copy);
        SessionState.EraseInt(Key);return "Restored";
    }
}
