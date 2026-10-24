if(!Application.isPlaying)throw new Exception("플레이 모드가 필요합니다.");
var movement=UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.MountainMap.Test_MountainPlayerMovement>();
var control=movement.AnimationControl;var animator=control.CharacterAnimator;
var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
var queue=movement.GetType().GetMethod("QueueAction",flags);
var consume=movement.GetType().GetMethod("TryBufferedAction",flags);
var validate=movement.GetType().GetMethod("ValidatePendingAction",flags);
var executed=movement.GetType().GetField("actionExecutedFrame",flags);
var wasMovementEnabled=movement.enabled;var wasControlEnabled=control.enabled;
var beforeSpeed=control.attack1Speed;var beforeStart=control.attack1StartFrame;var beforeEnd=control.attack1EndFrame;
var timing=control.RuntimeTimings.Get("ATTACK1");var beforeEquipment=timing.equipmentCancel;
movement.enabled=false;control.enabled=false;
var clip=new SerializedObject(control).FindProperty("attack1Clip").objectReferenceValue as AnimationClip;
var checks=new List<string>();var measurements=new List<object>();
void Check(bool condition,string reason){if(!condition)throw new Exception(reason);checks.Add(reason);}
void Queue(int action)=>queue.Invoke(movement,new object[]{action});
void Idle()
{
    if(control.IsPaused)control.TogglePause();movement.ResetToSpawn();
    if(!control.IsCombatRequested)control.EnterCombat();control.PlayIdle();
    animator.Play("DRAWN_IDLE",0,0f);animator.Update(0f);executed.SetValue(movement,-1);
}
float Duration()=>(control.GetAttackEndFrame(1)-control.GetAttackStartFrame(1))/clip.frameRate/control.attack1Speed;
void AtProgress(float progress)
{
    Idle();control.PlayAttack1();
    float frame=Mathf.Lerp(control.GetAttackStartFrame(1),control.GetAttackEndFrame(1),progress);
    animator.Play("ATTACK1",0,frame/(clip.length*clip.frameRate));animator.Update(0f);
}
void AtSeconds(float seconds)=>AtProgress(timing.attack2Cancel-seconds/Duration());
bool success=false;string reason="";
try
{
    Check(Mathf.Abs(new SerializedObject(movement).FindProperty("inputBufferSeconds").floatValue-0.15f)<0.00001f,"씬의 공통 예약 시간 0.15초 저장");
    foreach(bool trimmed in new [] {false,true})
    {
        if(trimmed){control.attack1Speed=2.4f;control.SetAttackFrameRange(1,8,32);}
        else{control.attack1Speed=beforeSpeed;control.SetAttackFrameRange(1,beforeStart,beforeEnd);}
        AtSeconds(0.151f);float outside=control.SecondsUntilAttackCancel(2);Queue(2);
        Check(Mathf.Abs(outside-0.151f)<0.0001f&&movement.BufferedAction=="없음",(trimmed?"잘라낸 구간·배속":"기존 구간·배속")+"에서 캔슬 0.151초 전 입력 거부");
        AtSeconds(0.149f);float inside=control.SecondsUntilAttackCancel(2);Queue(2);
        Check(Mathf.Abs(inside-0.149f)<0.0001f&&movement.BufferedAction=="공격 2",(trimmed?"잘라낸 구간·배속":"기존 구간·배속")+"에서 캔슬 0.149초 전 예약 수락");
        consume.Invoke(movement,new object[]{Vector3.zero});
        Check(control.SelectedState=="ATTACK1","예약 창 안에서도 캔슬 전에는 실행하지 않음");
        float target=Mathf.Lerp(control.GetAttackStartFrame(1),control.GetAttackEndFrame(1),timing.attack2Cancel+0.001f);
        animator.Play("ATTACK1",0,target/(clip.length*clip.frameRate));animator.Update(0f);
        consume.Invoke(movement,new object[]{Vector3.zero});
        Check(control.SelectedState=="ATTACK2"&&movement.BufferedAction=="없음","캔슬 도달 후 예약을 한 번 실행하고 삭제");
        measurements.Add(new {trimmed,outside,inside,duration=Duration()});
    }
    control.attack1Speed=beforeSpeed;control.SetAttackFrameRange(1,beforeStart,beforeEnd);

    AtProgress(0.52f);Queue(2);Queue(3);
    Check(movement.BufferedAction=="공격 2","같은 우선순위는 먼저 받은 공격 2 예약 유지");
    Queue(1);Check(movement.BufferedAction=="공격 2","현재 공격 중복 입력은 다른 예약을 덮어쓰지 않음");
    timing.equipmentCancel=0.65f;
    AtProgress(0.53f);Queue(-1);Check(movement.BufferedAction=="발도 / 납도","낮은 우선순위 예약 수락");
    Queue(2);Check(movement.BufferedAction=="공격 2","공격이 발도·납도 예약을 교체");
    Queue(-2);Check(movement.BufferedAction=="회피","회피가 공격 예약을 교체");
    Queue(3);Queue(-1);Check(movement.BufferedAction=="회피","공격과 발도·납도는 회피 예약을 덮어쓰지 않음");
    Queue(-3);consume.Invoke(movement,new object[]{Vector3.zero});
    Check(control.SelectedState=="HIT"&&movement.BufferedAction=="없음","피격이 회피 예약을 취소하고 즉시 실행");
    timing.equipmentCancel=beforeEquipment;

    AtProgress(0.52f);Queue(2);uint sequence=control.PlaybackSequence;control.PlayHit();validate.Invoke(movement,null);
    Check(control.PlaybackSequence!=sequence&&movement.BufferedAction=="없음","피격으로 원래 동작이 중단되면 예약 삭제");
    AtProgress(0.52f);Queue(2);control.PlayIdle();validate.Invoke(movement,null);
    Check(movement.BufferedAction=="없음","다른 상태로 전환되면 예약이 다음 상태로 넘어가지 않음");
    AtProgress(0.52f);Queue(2);control.TogglePause();validate.Invoke(movement,null);
    Check(movement.BufferedAction=="없음","일시정지 시 예약 삭제");control.TogglePause();
    AtProgress(0.52f);Queue(2);movement.ResetToSpawn();Check(movement.BufferedAction=="없음","리셋 시 예약 삭제");
    AtProgress(0.52f);movement.enabled=true;Queue(2);movement.enabled=false;
    Check(movement.BufferedAction=="없음","이동 컴포넌트 비활성화 시 예약 삭제");
    success=true;reason="0.15초 경계, 배속·구간, 우선순위 교체, 중복·중단 처리 검증 완료";
}
catch(Exception error){reason=error.ToString();}
finally
{
    timing.equipmentCancel=beforeEquipment;control.attack1Speed=beforeSpeed;control.SetAttackFrameRange(1,beforeStart,beforeEnd);
    Idle();movement.enabled=wasMovementEnabled;control.enabled=wasControlEnabled;
}
var report=new {success,reason,checks,measurements};
System.IO.File.WriteAllText("Assets/Temp/ActionReservationPolicy~/Test_ReservationWindowReport.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
return report;
