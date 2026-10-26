var preview=UnityEngine.Object.FindFirstObjectByType<Cheon.Tests.PlayerReactions.Test_PlayerReactionPreview>();
return new {frame=Time.frameCount, phase=preview == null ? "missing":preview.Phase.ToString(),state=preview==null?"":preview.AnimatorState,blending=preview!=null&&preview.IsBlending,idleTime=preview==null?0f:preview.IdleNormalizedTime};
