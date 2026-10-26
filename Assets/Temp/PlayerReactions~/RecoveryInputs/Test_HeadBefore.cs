var p=UnityEngine.Object.FindFirstObjectByType<Cheon.Tests.PlayerReactions.Test_PlayerReactionPreview>();
p.ResetPreview();p.SetView(90f,3.7f);
p.Settings.getUpHeadBowDegrees=0f;p.Scrub(1,25f);
return new{p.Phase,p.CurrentFrame};
