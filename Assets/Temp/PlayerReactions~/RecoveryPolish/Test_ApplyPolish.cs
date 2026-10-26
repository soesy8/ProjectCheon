var result=Cheon.Tests.PlayerReactions.Editor.Test_PlayerReactionSceneBuilder.RepairReactionMotionAvatars();
var settings=UnityEditor.AssetDatabase.LoadAssetAtPath<Cheon.Tests.PlayerReactions.Test_PlayerReactionSettings>(Cheon.Tests.PlayerReactions.Editor.Test_PlayerReactionSceneBuilder.SettingsPath);
UnityEditor.Undo.RecordObject(settings,"피격 회복 전환 시간 조정");
settings.transitionSeconds=0.35f;settings.returnToIdleSeconds=0.55f;
UnityEditor.EditorUtility.SetDirty(settings);UnityEditor.AssetDatabase.SaveAssetIfDirty(settings);
return new {result,settings.transitionSeconds,settings.returnToIdleSeconds};
