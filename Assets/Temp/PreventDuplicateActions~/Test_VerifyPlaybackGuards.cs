if(!Application.isPlaying) throw new Exception("플레이 모드가 필요합니다.");
var movement=UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.MountainMap.Test_MountainPlayerMovement>();
var control=movement.AnimationControl;
var animator=control.CharacterAnimator;
var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
var queue=movement.GetType().GetMethod("QueueAction",flags);
var consume=movement.GetType().GetMethod("TryBufferedAction",flags);
var late=control.GetType().GetMethod("LateUpdate",flags);
var timingsField=control.GetType().GetField("runtimeTimings",flags);
var savedTimings=timingsField.GetValue(control);
var wasMovementEnabled=movement.enabled;
var wasControlEnabled=control.enabled;
var wasRepeat=control.RepeatSelectedAnimation;
var runStart=control.run2StartFrame;
var runEnd=control.run2EndFrame;
movement.enabled=false;control.enabled=false;
var checks=new List<string>();
var metrics=new Dictionary<string,float>();
void Check(bool value,string reason) { if(!value) throw new Exception(reason);checks.Add(reason); }
void Idle()
{
    movement.ResetToSpawn();
    if(!control.IsCombatRequested) control.EnterCombat();
    control.PlayIdle();animator.Play("DRAWN_IDLE",0,0f);animator.Update(0f);
}
void Queue(int action) => queue.Invoke(movement,new object[]{action});
void Consume() => consume.Invoke(movement,new object[]{Vector3.zero});
void Advance(float seconds)
{
    animator.Update(0f);
    for(float remaining=seconds;remaining>0.00001f;remaining-=1f/120f)
        animator.Update(Mathf.Min(remaining,1f/120f));
}
bool success=false;string reason="";
try
{
    Idle();
    Check(control.TryPlayAttack(1),"대기 중 공격 1 최초 입력 허용");
    Check(!control.TryPlayAttack(1),"Animator 평가 전 같은 공격 요청 중복 차단");
    Advance(0.72f);
    float progress=control.PlaybackProgress;
    metrics["attack1BeforeDuplicate"]=progress;
    Check(progress>0.55f,"공격 1 캔슬 허용 시점까지 진행");
    Check(!control.CanPlayAttack(1)&&!control.TryPlayAttack(1),"캔슬 시점 이후에도 동일 공격 직접 API 재시작 차단");
    control.PlayAttack1();animator.Update(0f);
    Check(Mathf.Abs(control.PlaybackProgress-progress)<0.0001f,"같은 공격 버튼·직접 호출이 진행도를 초기화하지 않음");
    Queue(2);Check(movement.BufferedAction=="공격 2","다른 공격 예약 허용");
    Queue(1);Check(movement.BufferedAction=="공격 2","중복 공격 입력이 기존의 다른 공격 예약을 보존");
    Consume();Check(control.SelectedState=="ATTACK2"&&movement.BufferedAction=="없음","공격 1에서 공격 2로 정상 전환 및 예약 소비");
    Advance(1.3f);
    Check(!control.TryPlayAttack(2),"공격 2 중복 재시작 차단");
    Check(control.TryPlayAttack(3),"공격 2에서 공격 3으로 정상 전환");
    Advance(1.2f);
    Check(!control.TryPlayAttack(3),"공격 3 중복 재시작 차단");

    Idle();control.PlayAttack1();Advance(1.3f);late.Invoke(control,null);
    Check(control.SelectedState=="DRAWN_IDLE"&&control.IsStatePlaying("ATTACK1"),"자동 복귀 시 블렌드에 남아 있는 공격 감지");
    Queue(1);Check(movement.BufferedAction=="없음"&&!control.TryPlayAttack(1),"블렌드 종료 전 동일 공격 입력도 예약 없이 차단");
    Advance(0.5f);
    Check(!control.IsStatePlaying("ATTACK1")&&control.TryPlayAttack(1),"동작과 블렌드가 끝난 뒤 새 입력으로 같은 공격 재사용 가능");

    Idle();Check(control.TryPlayDodge(),"대기 중 구르기 최초 실행");Advance(0.4f);
    progress=control.PlaybackProgress;
    Check(!control.CanDodge&&!control.TryPlayDodge(),"구르기 중 같은 구르기 API 요청 차단");
    control.PlayRoll();animator.Update(0f);
    Check(Mathf.Abs(control.PlaybackProgress-progress)<0.0001f,"구르기 직접 호출이 진행도를 초기화하지 않음");
    Queue(-2);Check(movement.BufferedAction=="없음","구르기 중 Space 입력이 다음 구르기 예약으로 남지 않음");

    Idle();control.PlayHit();Advance(0.25f);progress=control.PlaybackProgress;
    control.PlayHit();animator.Update(0f);
    Check(Mathf.Abs(control.PlaybackProgress-progress)<0.0001f,"피격 동작 연타가 재시작되지 않음");
    Idle();Advance(0.3f);float idleTime=control.NormalizedTime;
    control.PlayIdle();animator.Update(0f);
    Check(Mathf.Abs(control.NormalizedTime-idleTime)<0.0001f,"같은 대기 요청이 재생 주기를 초기화하지 않음");
    control.PlayRun2();Advance(0.35f);float runTime=control.NormalizedTime;
    control.PlayRun2();animator.Update(0f);
    Check(Mathf.Abs(control.NormalizedTime-runTime)<0.0001f,"같은 달리기 요청이 재생 주기를 초기화하지 않음");

    // 애니메이션 테스트 씬의 명시적 반복 옵션과 잘라낸 달리기 루프는 유지되어야 한다.
    Idle();timingsField.SetValue(control,null);control.RepeatSelectedAnimation=true;
    control.PlayAttack1();float previous=0f;int repeats=0;
    for(int index=0;index<250;index++)
    {
        animator.Update(1f/60f);late.Invoke(control,null);
        float value=control.PlaybackProgress;if(previous-value>0.3f) repeats++;previous=value;
    }
    Check(repeats>=2&&control.SelectedState=="ATTACK1","미리보기의 명시적 동작 반복 옵션 유지");
    metrics["previewAttackRepeats"]=repeats;
    control.RepeatSelectedAnimation=false;Idle();control.SetPlaybackFrameRange(5,4,12);
    control.PlayRun2();previous=0f;repeats=0;
    for(int index=0;index<120;index++)
    {
        animator.Update(1f/60f);late.Invoke(control,null);
        float value=control.PlaybackProgress;if(previous-value>0.3f) repeats++;previous=value;
    }
    Check(repeats>=2&&control.SelectedState=="RUN2","시작·종료 프레임을 자른 달리기의 자동 루프 유지");
    metrics["trimmedRunLoops"]=repeats;
    success=true;reason="중복 차단, 다른 동작 전환, 종료 후 재사용, 명시적 반복 검증 완료";
}
catch(Exception error) { reason=error.ToString(); }
finally
{
    timingsField.SetValue(control,savedTimings);control.RepeatSelectedAnimation=wasRepeat;
    control.SetPlaybackFrameRange(5,runStart,runEnd);
    Idle();movement.enabled=wasMovementEnabled;control.enabled=wasControlEnabled;
}
var report=new {success,reason,checks,metrics};
System.IO.File.WriteAllText("Assets/Temp/PreventDuplicateActions~/Test_PlaybackGuardReport.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
return report;
