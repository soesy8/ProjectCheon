var preview = UnityEngine.Object.FindFirstObjectByType<Cheon.Tests.PlayerReactions.Test_PlayerReactionPreview>();
preview.Scrub(2, preview.Settings.death.endFrame);
return new {phase=preview.Phase.ToString(),frame=preview.CurrentFrame, hips=preview.CharacterAnimator.GetBoneTransform(HumanBodyBones.Hips).position.ToString()};
