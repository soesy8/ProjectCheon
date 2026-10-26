var oldStart = UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene;
UnityEditor.SessionState.SetString("Test_PlayerReactions_PreviousStartScene", oldStart == null ? "" : UnityEditor.AssetDatabase.GetAssetPath(oldStart));
UnityEditor.SessionState.SetBool("Test_PlayerReactions_Background", Application.runInBackground);
UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(Cheon.Tests.PlayerReactions.Editor.Test_PlayerReactionSceneBuilder.ScenePath);
Application.runInBackground = true;
return new { startScene=UnityEditor.AssetDatabase.GetAssetPath(UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene), oldStart=oldStart == null ? "" : oldStart.name };

