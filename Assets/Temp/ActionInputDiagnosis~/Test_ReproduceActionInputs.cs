if(!Application.isPlaying) throw new Exception("플레이 모드가 필요합니다.");
var movement=UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.MountainMap.Test_MountainPlayerMovement>();
var control=movement.AnimationControl;
var animator=control.CharacterAnimator;
var body=movement.GetComponent<CharacterController>();
var oldCapture=Time.captureDeltaTime;
Time.captureDeltaTime=1f/120f;
var oldKeyboard=UnityEngine.InputSystem.Keyboard.current;
var oldMouse=UnityEngine.InputSystem.Mouse.current;
var keyboard=UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>("Test_ActionDiagnosisKeyboard");
var mouse=UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>("Test_ActionDiagnosisMouse");
var oldBackground=UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior;
UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior=UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
var ground=GameObject.CreatePrimitive(PrimitiveType.Plane);
ground.name="Test_TemporaryActionDiagnosisGround";
ground.hideFlags=HideFlags.DontSave;
ground.transform.position=new Vector3(0,150,0);
ground.transform.localScale=Vector3.one*10f;
var held=Array.Empty<UnityEngine.InputSystem.Key>();
bool mouseHeld=false;
Action feed=()=> {
    keyboard.MakeCurrent(); mouse.MakeCurrent();
    UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(held));
    UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,new UnityEngine.InputSystem.LowLevel.MouseState {position=new Vector2(Screen.width*0.48f,Screen.height*0.5f),buttons=(ushort)(mouseHeld?1:0)});
};
UnityEngine.InputSystem.InputSystem.onBeforeUpdate+=feed;
var cases=new [] {"공격 초반의 추가 J 입력 만료", "공격 2 예약을 공격 3이 덮어씀", "같은 공격 1 재입력으로 재시작", "구르기 후반 입력의 다음 구르기 예약", "Space와 클릭 동시 입력", "예약 중 J 연타의 콤보 소실", "납도 중 추가 F 입력의 지연 반전"};
int testCase=0,phase=0,stage=0,lastFrame=-1,phaseFrame=Time.frameCount,releaseFrame=-1,restarts=0;
double began=EditorApplication.timeSinceStartup;
float previousProgress=0f;
string previousState="",previousBuffer="";
var trace=new List<object>();
var frames=new List<object>();
var reports=new List<object>();
EditorApplication.CallbackFunction tick=null;
void Pulse(UnityEngine.InputSystem.Key key) { held=new [] {key}; releaseFrame=Time.frameCount+1; }
void Note(string message) { trace.Add(new {time=Time.time,frame=Time.frameCount,message,state=control.SelectedState,progress=control.PlaybackProgress,buffer=movement.BufferedAction}); }
void Prepare()
{
    held=Array.Empty<UnityEngine.InputSystem.Key>(); mouseHeld=false;
    UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
    movement.ResetToSpawn();
    body.enabled=false;
    movement.transform.SetPositionAndRotation(new Vector3(0,150.04f,0),Quaternion.identity);
    body.enabled=true; Physics.SyncTransforms();
    if(!control.IsCombatRequested) control.EnterCombat();
    phase=0;stage=0;phaseFrame=Time.frameCount;restarts=0;
    previousState="";previousBuffer="";previousProgress=0;
    trace=new List<object>();frames=new List<object>();
}
void Finish(bool success,string reason)
{
    EditorApplication.update-=tick;
    UnityEngine.InputSystem.InputSystem.onBeforeUpdate-=feed;
    UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard); UnityEngine.InputSystem.InputSystem.RemoveDevice(mouse);
    if(oldKeyboard!=null) oldKeyboard.MakeCurrent(); if(oldMouse!=null) oldMouse.MakeCurrent();
    UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior=oldBackground;
    Time.captureDeltaTime=oldCapture;
    movement.ResetToSpawn(); UnityEngine.Object.Destroy(ground);
    System.IO.File.WriteAllText("Assets/Temp/ActionInputDiagnosis~/Test_ActionInputReport.json",Newtonsoft.Json.JsonConvert.SerializeObject(new {success,reason,reports,activeCase=testCase,trace,frames,elapsed=EditorApplication.timeSinceStartup-began},Newtonsoft.Json.Formatting.Indented));
}
void Complete(string observation)
{
    Note(observation);
    reports.Add(new {name=cases[testCase],observation,restarts,trace,frames});
    testCase++;
    if(testCase==cases.Length) Finish(true,"연속 입력 사례 관측 완료");
    else Prepare();
}
tick=()=>
{
    try
    {
        if(!Application.isPlaying||control==null) { Finish(false,"플레이 종료");return; }
        if(EditorApplication.timeSinceStartup-began>40d) { Finish(false,"시간 초과: "+testCase+"/"+phase+"/"+stage);return; }
        if(lastFrame==Time.frameCount) return;
        lastFrame=Time.frameCount;
        if(Time.frameCount>=releaseFrame) { held=Array.Empty<UnityEngine.InputSystem.Key>();mouseHeld=false; }
        var state=control.SelectedState;
        var progress=control.PlaybackProgress;
        var buffer=movement.BufferedAction;
        if(phase==0)
        {
            if(Time.frameCount<phaseFrame+8||state!="DRAWN_IDLE"||animator.IsInTransition(0)) return;
            if(testCase==3) Pulse(UnityEngine.InputSystem.Key.Space);
            else if(testCase==4) { Pulse(UnityEngine.InputSystem.Key.Space); mouseHeld=true; }
            else if(testCase==6) Pulse(UnityEngine.InputSystem.Key.F);
            else Pulse(UnityEngine.InputSystem.Key.J);
            phase=1;phaseFrame=Time.frameCount; Note("첫 입력");
            return;
        }
        if(state!=previousState||buffer!=previousBuffer) Note("상태 또는 예약 변경");
        if(state==previousState&&(state=="ATTACK1"||state=="ROLL")&&previousProgress-progress>0.3f)
        { restarts++;Note("동일 상태 진행도 초기화"); }
        frames.Add(new {time=Time.time,frame=Time.frameCount,state,progress,buffer,transition=animator.IsInTransition(0),upperWeight=animator.GetLayerWeight(1)});
        previousState=state;previousBuffer=buffer;previousProgress=progress;

        if(testCase==0)
        {
            if(stage==0&&state=="ATTACK1"&&progress>0.08f) { Pulse(UnityEngine.InputSystem.Key.J); stage=1;Note("공격 1 약 8%에서 J 추가"); }
            if(stage==1&&state=="DRAWN_IDLE"&&Time.frameCount>phaseFrame+10) Complete("초반 추가 입력 후 실행된 상태와 예약 만료 시점 기록");
        }
        else if(testCase==1)
        {
            if(stage==0&&state=="ATTACK1"&&progress>0.36f) { Pulse(UnityEngine.InputSystem.Key.Digit2);stage=1;Note("공격 2 예약"); }
            else if(stage==1&&state=="ATTACK1"&&progress>0.43f) { Pulse(UnityEngine.InputSystem.Key.Digit3);stage=2;Note("곧이어 공격 3 예약"); }
            if(stage==2&&state=="DRAWN_IDLE") Complete("공격 2와 3 중 실제 재생된 상태 기록");
        }
        else if(testCase==2)
        {
            if(stage==0&&state=="ATTACK1"&&progress>0.6f) { Pulse(UnityEngine.InputSystem.Key.Digit1);stage=1;Note("공격 1 60%에서 같은 공격 재입력"); }
            if(stage==1&&state=="DRAWN_IDLE") Complete("같은 공격 재시작 횟수: "+restarts);
        }
        else if(testCase==3)
        {
            if(stage==0&&state=="ROLL"&&progress>0.8f) { Pulse(UnityEngine.InputSystem.Key.Space);stage=1;Note("구르기 80%에서 Space 추가"); }
            if(stage==1&&state=="ROLL"&&progress<0.2f) { stage=2;Note("두 번째 구르기 시작"); }
            if(stage==2&&state=="DRAWN_IDLE") Complete("구르기 끝부분 입력 후 다음 구르기 예약 실행 기록");
        }
        else if(testCase==4)
        {
            if(Time.frameCount==phaseFrame+1) Note("동시 입력 다음 프레임 / UI 위 여부: "+UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject());
            if(state=="DRAWN_IDLE"&&Time.frameCount>phaseFrame+12) Complete("키보드 Space와 좌클릭 동시 입력 시 최종 동작 기록");
        }
        else if(testCase==5)
        {
            if(stage==0&&state=="ATTACK1"&&progress>0.36f) { Pulse(UnityEngine.InputSystem.Key.J);stage=1;Note("J 두 번째 입력"); }
            else if(stage==1&&state=="ATTACK1"&&progress>0.43f) { Pulse(UnityEngine.InputSystem.Key.J);stage=2;Note("J 세 번째 입력 / 아직 공격 1"); }
            if(stage==2&&state=="DRAWN_IDLE") Complete("J 세 번 입력에 대응한 실제 공격 순서 기록");
        }
        else if(testCase==6)
        {
            if(stage==0&&state=="SHEATHE"&&progress>0.3f) { Pulse(UnityEngine.InputSystem.Key.F);stage=1;Note("납도 30%에서 F 추가"); }
            if(stage==1&&state=="DRAWN_IDLE"&&Time.frameCount>phaseFrame+12) Complete("예약된 F가 허용 시점에 납도를 발도로 반전시키는 과정 기록");
        }
    }
    catch(Exception error) { Finish(false,error.ToString()); }
};
Prepare(); EditorApplication.update+=tick;
return new {started=true,cases=cases};
