if(!Application.isPlaying)throw new Exception("플레이 모드가 필요합니다.");
var movement=UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.MountainMap.Test_MountainPlayerMovement>();
var control=movement.AnimationControl;var body=movement.GetComponent<CharacterController>();
var cameraControl=UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.MountainMap.Test_MountainPlayerCamera>();
var view=Camera.main.transform;var cameraPosition=view.position;var cameraRotation=view.rotation;bool cameraEnabled=cameraControl.enabled;
cameraControl.enabled=false;view.rotation=Quaternion.identity;
float oldCapture=Time.captureDeltaTime;Time.captureDeltaTime=1f/120f;
float originalSpeed=control.rollSpeed;int originalStart=control.rollStartFrame,originalEnd=control.rollEndFrame;
var oldKeyboard=UnityEngine.InputSystem.Keyboard.current;
var keyboard=UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>("Test_RecoverySteeringKeyboard");
var background=UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior;
UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior=UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
var ground=GameObject.CreatePrimitive(PrimitiveType.Plane);ground.name="Test_TemporaryRecoverySteeringGround";ground.hideFlags=HideFlags.DontSave;
ground.transform.position=new Vector3(0,150,0);ground.transform.localScale=Vector3.one*10;
var held=Array.Empty<UnityEngine.InputSystem.Key>();
Action feed=()=>{keyboard.MakeCurrent();UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(held));};
UnityEngine.InputSystem.InputSystem.onBeforeUpdate+=feed;
var directionField=movement.GetType().GetField("dodgeDirection",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
var cases=new [] {"기본 구간 0~28F", "구간 8~32F / 1.5배속", "회복 구간 26~40F", "회복 전 0~20F", "회복 중 반대 방향 입력", "방향 입력 없이 직진"};
var checks=new List<string>();var reports=new List<object>();var samples=new List<object>();
int testCase=0,phase=0,lastFrame=-1,phaseFrame=0;
float distance=0,earlyYaw=0,lastYaw=0,maxVertical=0;
Vector3 previous=Vector3.zero;
double began=EditorApplication.timeSinceStartup;
EditorApplication.CallbackFunction tick=null;
void Check(bool value,string reason){if(!value)throw new Exception(reason);checks.Add(reason);}
void Prepare()
{
    held=Array.Empty<UnityEngine.InputSystem.Key>();movement.ResetToSpawn();
    control.rollSpeed=testCase==1?1.5f:originalSpeed;
    control.SetPlaybackFrameRange(4,testCase==1?8:testCase==2?26:0,testCase==1?32:testCase==2?40:testCase==3?20:28);
    body.enabled=false;movement.transform.SetPositionAndRotation(new Vector3(0,150.04f,0),Quaternion.identity);body.enabled=true;Physics.SyncTransforms();
    UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
    phase=0;phaseFrame=Time.frameCount;distance=0;earlyYaw=0;lastYaw=0;maxVertical=0;samples=new List<object>();previous=movement.transform.position;
}
void Record()
{
    var position=movement.transform.position;distance+=Vector3.ProjectOnPlane(position-previous,Vector3.up).magnitude;previous=position;
    float frame=control.NormalizedTime*control.GetPlaybackMaximumFrame(4);
    float yaw=Vector3.Angle(Vector3.forward,movement.transform.forward);
    if(control.IsDodging)
    {
        lastYaw=yaw;
        if(frame<22f)earlyYaw=Mathf.Max(earlyYaw,yaw);
        maxVertical=Mathf.Max(maxVertical,Mathf.Abs(((Vector3)directionField.GetValue(movement)).y),1f-Vector3.Dot(movement.transform.up,Vector3.up));
    }
    samples.Add(new {frame,yaw,distance,dodging=control.IsDodging});
}
void Finish(bool success,string message)
{
    EditorApplication.update-=tick;UnityEngine.InputSystem.InputSystem.onBeforeUpdate-=feed;
    UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard);if(oldKeyboard!=null)oldKeyboard.MakeCurrent();
    UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior=background;Time.captureDeltaTime=oldCapture;
    control.rollSpeed=originalSpeed;control.SetPlaybackFrameRange(4,originalStart,originalEnd);movement.ResetToSpawn();
    view.SetPositionAndRotation(cameraPosition,cameraRotation);cameraControl.enabled=cameraEnabled;UnityEngine.Object.Destroy(ground);
    System.IO.File.WriteAllText("Assets/Temp/RollRecoverySteering~/Test_LiveSteeringReport.json",Newtonsoft.Json.JsonConvert.SerializeObject(new {success,message,testCase,checks,reports,samples,elapsed=EditorApplication.timeSinceStartup-began},Newtonsoft.Json.Formatting.Indented));
}
tick=()=>
{
    try
    {
        if(!Application.isPlaying||control==null){Finish(false,"재생 종료");return;}
        if(EditorApplication.timeSinceStartup-began>25d){Finish(false,"시간 초과 "+testCase+"/"+phase);return;}
        if(lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
        if(phase==0)
        {
            if(Time.frameCount<phaseFrame+8||control.IsActionPlaying||control.CharacterAnimator.IsInTransition(0)||control.IsStatePlaying("ROLL"))return;
            previous=movement.transform.position;held=new [] {UnityEngine.InputSystem.Key.W,UnityEngine.InputSystem.Key.Space};phase=1;
        }
        else if(phase==1)
        {
            if(!control.IsDodging)return;Record();
            held=testCase==5?Array.Empty<UnityEngine.InputSystem.Key>():new [] {testCase==4?UnityEngine.InputSystem.Key.S:UnityEngine.InputSystem.Key.D};phase=2;
        }
        else if(phase==2)
        {
            Record();if(control.IsDodging)return;held=Array.Empty<UnityEngine.InputSystem.Key>();
            Check(Mathf.Abs(distance-7.5f)<0.04f,cases[testCase]+"에서 구르기 총 거리 7.5m 유지");
            Check(maxVertical<0.0001f,cases[testCase]+"에서 수평 회전과 이동 유지");
            if(testCase<=1)
            {
                Check(earlyYaw>37f&&earlyYaw<40f,cases[testCase]+"에서 기본 45% 방향 조작 확인");
                Check(lastYaw>60f,cases[testCase]+"에서 회복 75%로 방향 전환 증가");
            }
            if(testCase==2)Check(lastYaw>68f,"회복 구간부터 재생한 경우 75% 조작 적용");
            if(testCase==3)Check(lastYaw>37f&&lastYaw<40f,"회복 전에 끝나는 구간은 기본 45%만 적용");
            if(testCase==4)Check(earlyYaw<0.1f&&lastYaw>130f,"기본 구간은 역방향을 제한하고 회복 구간에서는 역방향 전환 허용");
            if(testCase==5)Check(lastYaw<0.1f,"입력이 없으면 처음 방향으로 직진 유지");
            reports.Add(new {name=cases[testCase],distance,earlyYaw,lastYaw,maxVertical,samples});testCase++;
            if(testCase==cases.Length)Finish(true,"실제 방향 입력, 회복 조작, 재생 구간·배속, 거리 유지 검증 완료");else Prepare();
        }
    }
    catch(Exception error){Finish(false,error.ToString());}
};
Prepare();EditorApplication.update+=tick;
return new {started=true,cases};
