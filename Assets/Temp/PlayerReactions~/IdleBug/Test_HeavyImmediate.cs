var p=UnityEngine.Object.FindFirstObjectByType<Cheon.Tests.PlayerReactions.Test_PlayerReactionPreview>();
p.enabled=false;p.ResetPreview();p.Play(Cheon.Tests.PlayerReactions.Test_PlayerReactionPreview.PreviewMode.HeavyOnly);
for(int i=0;i<100;i++)p.AdvancePlayback(1f/60f);
var playable=(UnityEngine.Animations.AnimatorControllerPlayable)typeof(Cheon.Tests.PlayerReactions.Test_PlayerReactionPreview).GetField("controller",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(p);
return new {frame=Time.frameCount,phase=p.Phase.ToString(),scriptState=p.AnimatorState,playableHeavy=playable.GetCurrentAnimatorStateInfo(0).IsName("HEAVY_HIT"),nativeIdle=p.CharacterAnimator.GetCurrentAnimatorStateInfo(0).IsName("IDLE"),nativeController=p.CharacterAnimator.runtimeAnimatorController==null?"null":p.CharacterAnimator.runtimeAnimatorController.name,hip=p.CharacterAnimator.GetBoneTransform(HumanBodyBones.Hips).position.ToString()};
