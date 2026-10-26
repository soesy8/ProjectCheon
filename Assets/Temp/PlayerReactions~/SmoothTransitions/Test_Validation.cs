var preview=UnityEngine.Object.FindFirstObjectByType<Cheon.Tests.PlayerReactions.Test_PlayerReactionPreview>();
preview.enabled=false; preview.repeatSequence=false; preview.ResetPreview();
var results=new List<string>();
Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception("FAIL: "+label);results.Add(label);};
var settings=preview.Settings; var json=JsonUtility.ToJson(settings);
var asset=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(Cheon.Tests.PlayerReactions.Editor.Test_PlayerReactionSceneBuilder.ControllerPath);
var tree=asset.layers[0].stateMachine.states.Single(s=>s.state.name=="IDLE").state.motion as UnityEditor.Animations.BlendTree;
check(tree!=null && tree.children.Length==2 && tree.blendParameter=="MoveAmount", "기존 1D 대기/이동 블렌드트리 복제");
preview.Play(Cheon.Tests.PlayerReactions.Test_PlayerReactionPreview.PreviewMode.HeavyThenGetUp);
bool hitBlend=false,recoveryBlend=false,idleBlend=false,outgoingAdvanced=false;
int getUpBlendFrames=0,idleBlendFrames=0; float maxIdleStep=0f,maxGetUpStep=0f, idleStartFrame=0f;
Vector3 previousHips=preview.CharacterAnimator.GetBoneTransform(HumanBodyBones.Hips).position;
for(int i=0;i<360;i++){
 var phase=preview.Phase; float previousFrame=preview.CurrentFrame;
 preview.AdvancePlayback(1f/60f);
 var hips=preview.CharacterAnimator.GetBoneTransform(HumanBodyBones.Hips).position;
 if(preview.IsBlending && preview.Phase==Cheon.Tests.PlayerReactions.Test_PlayerReactionPreview.PreviewPhase.HeavyHit)hitBlend=true;
 if(preview.IsBlending && preview.Phase==Cheon.Tests.PlayerReactions.Test_PlayerReactionPreview.PreviewPhase.GetUp){recoveryBlend=true;getUpBlendFrames++;maxGetUpStep=Mathf.Max(maxGetUpStep,Vector3.Distance(hips,previousHips));}
 if(preview.IsBlending && preview.AnimatorState=="IDLE"){
  if(!idleBlend)idleStartFrame=preview.CurrentFrame;
  idleBlend=true;idleBlendFrames++;maxIdleStep=Mathf.Max(maxIdleStep,Vector3.Distance(hips,previousHips));
  if(phase==Cheon.Tests.PlayerReactions.Test_PlayerReactionPreview.PreviewPhase.Complete && preview.CurrentFrame>previousFrame)outgoingAdvanced=true;
 }
 previousHips=hips;
}
check(hitBlend&&recoveryBlend&&idleBlend,"대기→강피격→일어나기→대기 Animator 크로스페이드");
check(idleStartFrame<settings.getUp.endFrame && outgoingAdvanced,"일어나기 종료 전에 대기와 겹쳐 재생");
check(preview.CurrentFrame==settings.getUp.endFrame,"블렌딩 중에도 일어나기 종료 프레임까지 진행");
float idleBefore=preview.IdleNormalizedTime;preview.AdvancePlayback(0.4f);
check(preview.AnimatorState=="IDLE" && preview.IdleNormalizedTime>idleBefore,"회복 후 대기 모션 계속 재생");
check(maxGetUpStep<0.12f && maxIdleStep<0.12f,"모션 연결 시 골반 위치 연속성");
preview.SetPaused(true); float pausedIdle=preview.IdleNormalizedTime;preview.AdvancePlayback(1f);check(preview.IdleNormalizedTime==pausedIdle,"대기 블렌드트리 일시정지");
settings.heavyHit.startFrame=3; settings.heavyHit.endFrame=20;settings.heavyHit.speed=0.5f;
preview.Play(Cheon.Tests.PlayerReactions.Test_PlayerReactionPreview.PreviewMode.HeavyOnly);preview.AdvancePlayback(0.2f);
check(Mathf.Abs(preview.CurrentFrame-6f)<0.01f,"시작 프레임과 0.5배속 유지");
preview.Scrub(0,9);preview.StepFrame(0,1);check(preview.CurrentFrame==10,"수동 프레임 탐색 유지");
JsonUtility.FromJsonOverwrite(json,settings);
preview.Play(Cheon.Tests.PlayerReactions.Test_PlayerReactionPreview.PreviewMode.Death);
for(int i=0;i<240;i++)preview.AdvancePlayback(1f/60f);
check(preview.Phase==Cheon.Tests.PlayerReactions.Test_PlayerReactionPreview.PreviewPhase.Dead&&preview.IsPaused&&preview.AnimatorState=="DEATH","사망 자세 유지, 대기 복귀 없음");
preview.Play(Cheon.Tests.PlayerReactions.Test_PlayerReactionPreview.PreviewMode.GetUpOnly);
for(int i=0;i<240;i++)preview.AdvancePlayback(1f/60f);
check(preview.AnimatorState=="IDLE"&&preview.Phase==Cheon.Tests.PlayerReactions.Test_PlayerReactionPreview.PreviewPhase.Complete,"일어나기 단독 재생 후 대기로 연결");
preview.ResetPreview();preview.enabled=true;
return new {checks=results,getUpBlendFrames,idleBlendFrames,idleStartFrame,maxGetUpStep,maxIdleStep};
