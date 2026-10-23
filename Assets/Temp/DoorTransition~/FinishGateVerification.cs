if (SessionState.GetBool("Cheon.GateTransition.Restored", false) == false)
{
    Application.runInBackground = SessionState.GetBool("Cheon.GateTransition.RunInBackground", false);
    SessionState.EraseBool("Cheon.GateTransition.RunInBackground");
    SessionState.SetBool("Cheon.GateTransition.Restored", true);
}
var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
var effect = UnityEngine.Object.FindFirstObjectByType<Cheon.World.GateEnvironmentTransition>();
Selection.activeGameObject = effect.gameObject;
AssetDatabase.Refresh();
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
return new { scene = scene.path, scene.isDirty, Application.runInBackground, effect.enabled, effectObject = effect.name, scriptGuid = AssetDatabase.AssetPathToGUID("Assets/Scripts/World/GateEnvironmentTransition.cs"), sunIntensity = RenderSettings.sun.intensity, background = Camera.main.backgroundColor.ToString() };
