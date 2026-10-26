UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene=null;
Application.runInBackground=UnityEditor.SessionState.GetBool("Test_PlayerReactions_Background",true);
UnityEditor.SessionState.EraseString("Test_PlayerReactions_PreviousStartScene");
UnityEditor.SessionState.EraseBool("Test_PlayerReactions_Background");
UnityEditor.Selection.activeObject=UnityEditor.AssetDatabase.LoadAssetAtPath<Cheon.Tests.PlayerReactions.Test_PlayerReactionSettings>(Cheon.Tests.PlayerReactions.Editor.Test_PlayerReactionSceneBuilder.SettingsPath);
var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(Cheon.Tests.PlayerReactions.Editor.Test_PlayerReactionSceneBuilder.ScenePath);
return new {playing=UnityEditor.EditorApplication.isPlaying,scene=scene.path,roots=scene.IsValid()?scene.GetRootGameObjects().Select(o=>o.name).ToArray():new string[0],settings=UnityEditor.Selection.activeObject.name};
