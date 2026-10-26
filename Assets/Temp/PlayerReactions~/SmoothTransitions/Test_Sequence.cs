var preview=UnityEngine.Object.FindFirstObjectByType<Cheon.Tests.PlayerReactions.Test_PlayerReactionPreview>();
preview.enabled=false;
preview.ResetPreview();
preview.Play(Cheon.Tests.PlayerReactions.Test_PlayerReactionPreview.PreviewMode.HeavyThenGetUp);
var rows = new List<object>();
string last="";
for(int i=0;i<400;i++) {
 preview.AdvancePlayback(1f/60f);
 string tag=preview.Phase.ToString()+"/"+preview.AnimatorState+"/"+preview.IsBlending;
 if(tag!=last) {var hips=preview.CharacterAnimator.GetBoneTransform(HumanBodyBones.Hips); rows.Add(new {step=i,tag,frame=preview.CurrentFrame,idle=preview.IdleNormalizedTime,hips=hips.position.ToString()});last=tag;}
}
float idleBefore=preview.IdleNormalizedTime;
preview.AdvancePlayback(0.5f);
return new {rows,idleBefore,idleAfter=preview.IdleNormalizedTime, phase=preview.Phase.ToString(),frame=preview.CurrentFrame};
