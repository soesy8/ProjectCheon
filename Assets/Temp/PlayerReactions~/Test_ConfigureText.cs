var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(Cheon.Tests.PlayerReactions.Editor.Test_PlayerReactionSceneBuilder.ScenePath);
var ui = scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<Cheon.Tests.PlayerReactions.Test_PlayerReactionUI>()).Single();
var preview = ui.GetComponent<Cheon.Tests.PlayerReactions.Test_PlayerReactionPreview>();
Cheon.Tests.PlayerReactions.Editor.Test_PlayerReactionSceneBuilder.ConfigureText(ui, preview);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
return true;
