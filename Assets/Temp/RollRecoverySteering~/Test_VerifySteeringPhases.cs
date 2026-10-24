if(!Application.isPlaying)throw new Exception("플레이 모드가 필요합니다.");
var movement=UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.MountainMap.Test_MountainPlayerMovement>();
var control=movement.AnimationControl;var animator=control.CharacterAnimator;
var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
var displace=movement.GetType().GetMethod("GetDodgeDisplacement",flags);
var directionField=movement.GetType().GetField("dodgeDirection",flags);
var startField=movement.GetType().GetField("dodgeStartDirection",flags);
var progressField=movement.GetType().GetField("dodgeProgress",flags);
bool movementEnabled=movement.enabled,controlEnabled=control.enabled;
float originalSpeed=control.rollSpeed;int originalStart=control.rollStartFrame,originalEnd=control.rollEndFrame;
movement.enabled=false;control.enabled=false;
var checks=new List<string>();var samples=new List<object>();
void Check(bool value,string reason){if(!value)throw new Exception(reason);checks.Add(reason);}
void Begin(float? frame)
{
    movement.ResetToSpawn();
    animator.Play(control.IsCombatRequested?"DRAWN_IDLE":"SHEATHED_IDLE",0,0f);animator.Update(0f);
    control.PlayRoll();
    if(frame.HasValue){animator.Play("ROLL",0,frame.Value/control.GetPlaybackMaximumFrame(4));animator.Update(0f);}
    startField.SetValue(movement,Vector3.forward);directionField.SetValue(movement,Vector3.forward);progressField.SetValue(movement,0f);
}
void CheckSteering(float frame,float weight,string reason)
{
    Begin(frame);displace.Invoke(movement,new object[]{Vector3.right,1f});
    var actual=(Vector3)directionField.GetValue(movement);
    var expected=Vector3.Lerp(Vector3.forward,Vector3.right,weight).normalized;
    Check(Vector3.Angle(actual,expected)<0.05f,reason);
    samples.Add(new {frame,weight,yaw=Vector3.Angle(Vector3.forward,actual),actual=actual.ToString("F4")});
}
bool success=false;string message="";
try
{
    var so=new SerializedObject(movement);
    Check(Mathf.Approximately(so.FindProperty("dodgeSteering").floatValue,0.45f)&&Mathf.Approximately(so.FindProperty("dodgeRecoverySteering").floatValue,0.75f)&&so.FindProperty("dodgeRecoveryStartFrame").intValue==22,"기본 45%, 회복 75%, 시작 22프레임 설정");
    control.SetPlaybackFrameRange(4,0,40);control.rollSpeed=1.25f;
    CheckSteering(12,0.45f,"구르는 중 45% 입력 반영");
    CheckSteering(21,0.45f,"21프레임까지 기본 45% 유지");
    CheckSteering(22,0.75f,"22프레임부터 회복 75% 적용");
    CheckSteering(28,0.75f,"일어나는 후반 구간 75% 유지");
    control.SetPlaybackFrameRange(4,8,32);control.rollSpeed=2f;
    CheckSteering(21,0.45f,"배속과 시작·종료 구간을 바꿔도 21프레임은 기본 조작");
    CheckSteering(22,0.75f,"배속과 시작·종료 구간을 바꿔도 22프레임부터 회복 조작");
    control.SetPlaybackFrameRange(4,0,20);
    CheckSteering(20,0.45f,"회복 전에 잘라낸 구간에는 75%를 조기 적용하지 않음");
    control.SetPlaybackFrameRange(4,26,40);Begin(null);
    displace.Invoke(movement,new object[]{Vector3.right,1f});
    Check(Vector3.Angle((Vector3)directionField.GetValue(movement),Vector3.Lerp(Vector3.forward,Vector3.right,0.75f).normalized)<0.05f,"회복 구간에서 재생을 시작하면 첫 이동부터 75% 적용");
    Begin(26);
    for(int index=0;index<30;index++)
    {
        var delta=(Vector3)displace.Invoke(movement,new object[]{Vector3.back,1f/60f});
        var direction=(Vector3)directionField.GetValue(movement);
        if(Mathf.Abs(delta.y)>0.00001f||Mathf.Abs(direction.y)>0.00001f||Vector3.Dot(movement.transform.up,Vector3.up)<0.9999f)
            throw new Exception("반대 방향 조작 중 수평면 이탈");
    }
    Check(Vector3.Angle(Vector3.forward,(Vector3)directionField.GetValue(movement))>175f,"회복 구간의 반대 방향 입력도 수평 회전으로 처리");
    success=true;message="45%·75% 구간, 배속·프레임 범위, 역방향 수평 회전 검증 완료";
}
catch(Exception error){message=error.ToString();}
finally
{
    control.rollSpeed=originalSpeed;control.SetPlaybackFrameRange(4,originalStart,originalEnd);
    movement.ResetToSpawn();movement.enabled=movementEnabled;control.enabled=controlEnabled;
}
var report=new {success,message,checks,samples};
System.IO.File.WriteAllText("Assets/Temp/RollRecoverySteering~/Test_SteeringPhaseReport.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
return report;
