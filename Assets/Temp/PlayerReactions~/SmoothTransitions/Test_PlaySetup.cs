var old=UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene;
UnityEditor.SessionState.SetString("Test_SmoothReactions_OldStart",old==null?"":UnityEditor.AssetDatabase.GetAssetPath(old));
UnityEditor.SessionState.SetBool("Test_SmoothReactions_OldBackground",Application.runInBackground);
UnityEditor.SessionState.SetBool("Test_SmoothReactions_OldPaused",UnityEditor.EditorApplication.isPaused);
UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(Cheon.Tests.PlayerReactions.Editor.Test_PlayerReactionSceneBuilder.ScenePath);
UnityEditor.EditorApplication.isPaused=false;
Application.runInBackground=true;
return true;
