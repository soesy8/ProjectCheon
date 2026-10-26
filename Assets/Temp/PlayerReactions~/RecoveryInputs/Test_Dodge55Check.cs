var p=UnityEngine.Object.FindFirstObjectByType<Cheon.Tests.PlayerReactions.Test_PlayerReactionPreview>();
if(p==null) throw new System.Exception("테스트 씬이 열려 있지 않습니다.");
var original=JsonUtility.ToJson(p.Settings);
var wasEnabled=p.enabled;
p.enabled=false;
try {
 p.ResetPreview();p.Play(Cheon.Tests.PlayerReactions.Test_PlayerReactionPreview.PreviewMode.GetUpOnly);
 p.AdvancePlayback(p.Settings.getUp.Duration*0.549f);
 p.SetInput(Vector2.zero,true);p.AdvancePlayback(0f);
 bool blocked=p.Phase==Cheon.Tests.PlayerReactions.Test_PlayerReactionPreview.PreviewPhase.GetUp && !p.CanDodge;
 p.AdvancePlayback(p.Settings.getUp.Duration*0.001f);
 bool unlocked=p.CanDodge;
 p.SetInput(Vector2.zero,true);p.AdvancePlayback(0f);
 bool dodged=p.Phase==Cheon.Tests.PlayerReactions.Test_PlayerReactionPreview.PreviewPhase.Dodging && p.AnimatorState=="DODGE";
 if(!blocked || !unlocked || !dodged) throw new System.Exception("55% 경계 검증 실패");
 return new {savedThreshold=p.Settings.getUpDodgeCancelProgress,blockedAt54point9=blocked,unlockedAt55=unlocked,transitionedToDodge=dodged,moveThreshold=p.Settings.getUpMoveCancelProgress};
} finally {JsonUtility.FromJsonOverwrite(original,p.Settings);p.ResetPreview();p.enabled=wasEnabled;}
