var s=AssetDatabase.LoadAssetAtPath<Cheon.Tests.PlayerReactions.Test_PlayerReactionSettings>(Cheon.Tests.PlayerReactions.Editor.Test_PlayerReactionSceneBuilder.SettingsPath);
s.heavyHit.endFrame=s.heavyHit.MaximumFrame; s.death.endFrame=s.heavyHit.MaximumFrame;
s.landingFrame=26; s.landedRecoveryDelay=0.5f; s.getUpDodgeCancelProgress=0.7f; s.getUpMoveCancelProgress=0.9f; s.getUpHeadBowDegrees=10f;
var result=Cheon.Tests.PlayerReactions.Editor.Test_PlayerReactionSceneBuilder.ApplyControllerToScene();
Application.runInBackground=true;
return new {result,s.heavyHit.endFrame,s.landingFrame,s.landedRecoveryDelay,dodge=s.dodge.clip.name,scenes=Enumerable.Range(0,UnityEngine.SceneManagement.SceneManager.sceneCount).Select(i=>new {path=UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).path,dirty=UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty})};
