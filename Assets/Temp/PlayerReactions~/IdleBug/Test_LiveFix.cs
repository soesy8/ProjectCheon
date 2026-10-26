var p=UnityEngine.Object.FindFirstObjectByType<Cheon.Tests.PlayerReactions.Test_PlayerReactionPreview>();
p.CharacterAnimator.runtimeAnimatorController=null;
p.Scrub(0,46);p.enabled=true;
return new {frame=Time.frameCount,hip=p.CharacterAnimator.GetBoneTransform(HumanBodyBones.Hips).position.ToString(),controller=p.CharacterAnimator.runtimeAnimatorController==null?"null":"assigned"};
