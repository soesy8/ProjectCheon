var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, "Assets/Temp/MobileOptimization~/Main.EditorBefore.unity", true);
UnityEditor.EditorPrefs.SetInt("Cheon.MobileOptimization.PreviousQuality", UnityEngine.QualitySettings.GetQualityLevel());
UnityEngine.QualitySettings.SetQualityLevel(0, true);
return new { scene = scene.path, playing = UnityEditor.EditorApplication.isPlaying, quality = UnityEngine.QualitySettings.GetQualityLevel() };
