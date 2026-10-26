var p=UnityEngine.Object.FindFirstObjectByType<Cheon.Tests.PlayerReactions.Test_PlayerReactionPreview>();
p.enabled=false;p.ResetPreview();p.Play(Cheon.Tests.PlayerReactions.Test_PlayerReactionPreview.PreviewMode.HeavyThenGetUp);
for(int i=0;i<360&&!(p.AnimatorState=="IDLE"&&p.Phase==Cheon.Tests.PlayerReactions.Test_PlayerReactionPreview.PreviewPhase.Complete);i++)p.AdvancePlayback(1f/60f);
p.AdvancePlayback(p.Settings.returnToIdleSeconds*0.5f);p.SetPaused(true);p.enabled=true;
return new {p.CurrentFrame,p.IsBlending,hips=p.CharacterAnimator.GetBoneTransform(HumanBodyBones.Hips).position.ToString()};
