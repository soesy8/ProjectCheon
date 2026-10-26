var preview = UnityEngine.Object.FindFirstObjectByType<Cheon.Tests.PlayerReactions.Test_PlayerReactionPreview>();
preview.Scrub(1, 25);
return new {phase=preview.Phase.ToString(),frame=preview.CurrentFrame, hips=preview.CharacterAnimator.GetBoneTransform(HumanBodyBones.Hips).position.ToString()};
