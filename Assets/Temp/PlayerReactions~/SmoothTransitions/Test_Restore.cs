string old=UnityEditor.SessionState.GetString("Test_SmoothReactions_OldStart","");
UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(old)?null:UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(old);
Application.runInBackground=UnityEditor.SessionState.GetBool("Test_SmoothReactions_OldBackground",true);
UnityEditor.EditorApplication.isPaused=UnityEditor.SessionState.GetBool("Test_SmoothReactions_OldPaused",false);
UnityEditor.SessionState.EraseString("Test_SmoothReactions_OldStart");
UnityEditor.SessionState.EraseBool("Test_SmoothReactions_OldBackground");
UnityEditor.SessionState.EraseBool("Test_SmoothReactions_OldPaused");
UnityEditor.Selection.activeObject=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(Cheon.Tests.PlayerReactions.Editor.Test_PlayerReactionSceneBuilder.ControllerPath);
return new {controller=UnityEditor.AssetDatabase.GetAssetPath(UnityEditor.Selection.activeObject), playing=UnityEditor.EditorApplication.isPlaying};
