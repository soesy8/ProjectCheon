if(!Application.isPlaying) throw new Exception("플레이 모드가 필요합니다.");
var movement=UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.MountainMap.Test_MountainPlayerMovement>();
var control=movement.AnimationControl;var animator=control.CharacterAnimator;var body=movement.GetComponent<CharacterController>();
var oldCapture=Time.captureDeltaTime;Time.captureDeltaTime=1f/120f;
var oldKeyboard=UnityEngine.InputSystem.Keyboard.current;
var keyboard=UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>("Test_DuplicateGuardKeyboard");
var oldBackground=UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior;
UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior=UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
var ground=GameObject.CreatePrimitive(PrimitiveType.Plane);ground.name="Test_TemporaryDuplicateGuardGround";
ground.hideFlags=HideFlags.DontSave;ground.transform.position=new Vector3(0,150,0);ground.transform.localScale=Vector3.one*10;
var held=Array.Empty<UnityEngine.InputSystem.Key>();
Action feed=()=>{keyboard.MakeCurrent();UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(held));};
UnityEngine.InputSystem.InputSystem.onBeforeUpdate+=feed;
var cases=new [] {"공격 1의 후반 재입력", "구르기 후반 Space 재입력", "다른 공격 예약 중 현재 공격 재입력", "공격 1→2→3 정상 콤보"};
int testCase=0,phase=0,stage=0,lastFrame=-1,phaseFrame=0,releaseFrame=-1,idleFrame=-1,restarts=0;
string previousState="";float previousProgress=0;
Vector3 start=Vector3.zero;
var states=new List<string>();var checks=new List<string>();var reports=new List<object>();var trace=new List<object>();
double began=EditorApplication.timeSinceStartup;
EditorApplication.CallbackFunction tick=null;
void Check(bool value,string reason){if(!value)throw new Exception(reason);checks.Add(reason);}
void Pulse(UnityEngine.InputSystem.Key key){held=new [] {key};releaseFrame=Time.frameCount+1;}
void Prepare()
{
    held=Array.Empty<UnityEngine.InputSystem.Key>();UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
    movement.ResetToSpawn();if(!control.IsCombatRequested)control.EnterCombat();
    body.enabled=false;movement.transform.SetPositionAndRotation(new Vector3(0,150.04f,0),Quaternion.identity);body.enabled=true;Physics.SyncTransforms();
    phase=0;stage=0;phaseFrame=Time.frameCount;idleFrame=-1;restarts=0;previousState="";previousProgress=0;
    states=new List<string>();trace=new List<object>();start=movement.transform.position;
}
void Finish(bool success,string reason)
{
    EditorApplication.update-=tick;UnityEngine.InputSystem.InputSystem.onBeforeUpdate-=feed;
    UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard);if(oldKeyboard!=null)oldKeyboard.MakeCurrent();
    UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior=oldBackground;Time.captureDeltaTime=oldCapture;
    movement.ResetToSpawn();UnityEngine.Object.Destroy(ground);
    System.IO.File.WriteAllText("Assets/Temp/PreventDuplicateActions~/Test_KeyboardGuardReport.json",Newtonsoft.Json.JsonConvert.SerializeObject(new {success,reason,checks,reports,trace,elapsed=EditorApplication.timeSinceStartup-began},Newtonsoft.Json.Formatting.Indented));
}
tick=()=>
{
    try
    {
        if(!Application.isPlaying||control==null){Finish(false,"재생 종료");return;}
        if(EditorApplication.timeSinceStartup-began>25d){Finish(false,"시간 초과 "+testCase+"/"+phase+"/"+stage);return;}
        if(lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
        if(Time.frameCount>=releaseFrame)held=Array.Empty<UnityEngine.InputSystem.Key>();
        string state=control.SelectedState;float progress=control.PlaybackProgress;
        if(phase==0)
        {
            if(Time.frameCount<phaseFrame+10||state!="DRAWN_IDLE"||animator.IsInTransition(0))return;
            start=movement.transform.position;Pulse(testCase==1?UnityEngine.InputSystem.Key.Space:UnityEngine.InputSystem.Key.J);
            phase=1;phaseFrame=Time.frameCount;return;
        }
        if(state!=previousState)
        {
            states.Add(state);trace.Add(new {frame=Time.frameCount,state,progress,buffer=movement.BufferedAction});
        }
        else if((state.StartsWith("ATTACK")||state=="ROLL")&&previousProgress-progress>0.3f)restarts++;
        previousState=state;previousProgress=progress;
        if(testCase==0)
        {
            if(stage==0&&state=="ATTACK1"&&progress>0.6f){Pulse(UnityEngine.InputSystem.Key.Digit1);stage=1;}
            else if(stage==1&&state=="ATTACK1"&&progress>0.95f){Pulse(UnityEngine.InputSystem.Key.Digit1);stage=2;}
            if(stage>=1&&state=="ATTACK1")Check(movement.BufferedAction=="없음","공격 중 같은 공격 입력은 예약되지 않음");
        }
        else if(testCase==1)
        {
            if(stage==0&&state=="ROLL"&&progress>0.8f){Pulse(UnityEngine.InputSystem.Key.Space);stage=1;}
            if(stage==1&&state=="ROLL")Check(movement.BufferedAction=="없음","구르기 중 Space 입력은 예약되지 않음");
        }
        else if(testCase==2)
        {
            if(stage==0&&state=="ATTACK1"&&progress>0.38f){Pulse(UnityEngine.InputSystem.Key.Digit2);stage=1;}
            else if(stage==1&&state=="ATTACK1"&&progress>0.47f){Pulse(UnityEngine.InputSystem.Key.Digit1);stage=2;}
            else if(stage==2&&state=="ATTACK1"&&progress>0.49f){Check(movement.BufferedAction=="공격 2","중복 공격 1 입력 후에도 공격 2 예약 유지");stage=3;}
        }
        else if(testCase==3)
        {
            if(stage==0&&state=="ATTACK1"&&progress>0.5f){Pulse(UnityEngine.InputSystem.Key.J);stage=1;}
            else if(stage==1&&state=="ATTACK2"&&progress>0.5f){Pulse(UnityEngine.InputSystem.Key.J);stage=2;}
        }
        if(stage==0||state!="DRAWN_IDLE"||Time.frameCount<phaseFrame+15)return;
        if(idleFrame<0)idleFrame=Time.frameCount;
        if(Time.frameCount<idleFrame+45)return;
        Check(restarts==0,cases[testCase]+"에서 진행도 초기화 없음");
        if(testCase==0)Check(states.Count(s=>s=="ATTACK1")==1,"공격 1 연타 후 추가 공격이 실행되지 않음");
        if(testCase==1)
        {
            Check(states.Count(s=>s=="ROLL")==1,"구르기 종료 후 추가 구르기가 실행되지 않음");
            Check(Mathf.Abs(Vector3.ProjectOnPlane(movement.transform.position-start,Vector3.up).magnitude-7.5f)<0.05f,"구르기 거리 7.5m 유지");
        }
        if(testCase==2)Check(states.Contains("ATTACK2"),"키 입력으로 예약한 다른 공격 정상 실행");
        if(testCase==3)Check(states.Contains("ATTACK1")&&states.Contains("ATTACK2")&&states.Contains("ATTACK3"),"키 입력으로 공격 1→2→3 정상 실행");
        reports.Add(new {name=cases[testCase],states,restarts,trace});testCase++;
        if(testCase==cases.Length)Finish(true,"실제 키 입력 중복 방지 및 정상 콤보 검증 완료");else Prepare();
    }
    catch(Exception error){Finish(false,error.ToString());}
};
Prepare();EditorApplication.update+=tick;
return new {started=true,cases};
