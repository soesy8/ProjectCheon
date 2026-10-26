var p=UnityEngine.Object.FindFirstObjectByType<Cheon.Tests.PlayerReactions.Test_PlayerReactionPreview>();
p.Settings.getUpHeadBowDegrees=10f;p.Scrub(1,25f);
return new{p.Phase,p.CurrentFrame};
