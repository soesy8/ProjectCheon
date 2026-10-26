var p=UnityEngine.Object.FindFirstObjectByType<Cheon.Tests.PlayerReactions.Test_PlayerReactionPreview>();
typeof(Cheon.Tests.PlayerReactions.Test_PlayerReactionPreview).GetField("cameraHeight",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(p,0.3f);
p.SetView(90f,3.7f);p.Scrub(1,25f);
return true;
