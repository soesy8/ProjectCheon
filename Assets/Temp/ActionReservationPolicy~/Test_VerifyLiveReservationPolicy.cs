if(!Application.isPlaying)throw new Exception("플레이 모드가 필요합니다.");
var movement=UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.MountainMap.Test_MountainPlayerMovement>();
var control=movement.AnimationControl;var animator=control.CharacterAnimator;var body=movement.GetComponent<CharacterController>();
float oldCapture=Time.captureDeltaTime;Time.captureDeltaTime=1f/120f;
var oldKeyboard=UnityEngine.InputSystem.Keyboard.current;var oldMouse=UnityEngine.InputSystem.Mouse.current;
var keyboard=UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>("Test_ReservationKeyboard");
var mouse=UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>("Test_ReservationMouse");
var oldBackground=UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior;
UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior=UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
var ground=GameObject.CreatePrimitive(PrimitiveType.Plane);ground.name="Test_TemporaryReservationGround";
ground.hideFlags=HideFlags.DontSave;ground.transform.position=new Vector3(0,150,0);ground.transform.localScale=Vector3.one*10f;
var held=Array.Empty<UnityEngine.InputSystem.Key>();bool mouseHeld=false;
Action feed=()=>{
    keyboard.MakeCurrent();mouse.MakeCurrent();
    UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(held));
    UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,new UnityEngine.InputSystem.LowLevel.MouseState{position=new Vector2(Screen.width*0.48f,Screen.height*0.5f),buttons=(ushort)(mouseHeld?1:0)});
};
UnityEngine.InputSystem.InputSystem.onBeforeUpdate+=feed;
var cases=new [] {"피격 > 회피·공격·발도납도", "회피 > 공격·발도납도·클릭", "공격 > 발도납도", "개별 공격과 콤보의 동시 입력", "달리면서 발도납도", "너무 이른 입력 무시", "0.15초 창의 정상 콤보", "캔슬 100%에서 예약 우선 실행", "예약 중 피격으로 취소", "같은 우선순위의 첫 예약 유지", "회피가 공격 예약 교체", "숫자 입력 중 예약 취소", "이동으로 현재 동작 중단 시 예약 취소"};
int testCase=0,phase=0,stage=0,lastFrame=-1,phaseFrame=0,releaseFrame=-1,idleFrame=-1,focusFrame=-1;
string previousState="",previousBuffer="";
var states=new List<string>();var trace=new List<object>();var checks=new List<string>();var reports=new List<object>();
TMPro.TMP_InputField focused=null;
double began=EditorApplication.timeSinceStartup;
EditorApplication.CallbackFunction tick=null;
void Check(bool value,string reason){if(!value)throw new Exception(reason);checks.Add(reason);}
void Pulse(params UnityEngine.InputSystem.Key[] keys){held=keys;releaseFrame=Time.frameCount+1;}
void Note(string message){trace.Add(new {frame=Time.frameCount,time=Time.time,message,state=control.SelectedState,progress=control.PlaybackProgress,buffer=movement.BufferedAction,sequence=control.PlaybackSequence});}
void Prepare()
{
    held=Array.Empty<UnityEngine.InputSystem.Key>();mouseHeld=false;
    if(focused!=null){focused.DeactivateInputField();focused=null;}
    UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
    movement.ResetToSpawn();if(!control.IsCombatRequested)control.EnterCombat();
    body.enabled=false;movement.transform.SetPositionAndRotation(new Vector3(0,150.04f,0),Quaternion.identity);body.enabled=true;Physics.SyncTransforms();
    phase=0;stage=0;phaseFrame=Time.frameCount;idleFrame=-1;previousState="";previousBuffer="";
    states=new List<string>();trace=new List<object>();
}
void Finish(bool success,string reason)
{
    EditorApplication.update-=tick;UnityEngine.InputSystem.InputSystem.onBeforeUpdate-=feed;
    UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard);UnityEngine.InputSystem.InputSystem.RemoveDevice(mouse);
    if(oldKeyboard!=null)oldKeyboard.MakeCurrent();if(oldMouse!=null)oldMouse.MakeCurrent();
    UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior=oldBackground;Time.captureDeltaTime=oldCapture;
    if(focused!=null)focused.DeactivateInputField();UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
    movement.ResetToSpawn();UnityEngine.Object.Destroy(ground);
    System.IO.File.WriteAllText("Assets/Temp/ActionReservationPolicy~/Test_LiveReservationReport.json",Newtonsoft.Json.JsonConvert.SerializeObject(new {success,reason,testCase,stage,checks,reports,trace,elapsed=EditorApplication.timeSinceStartup-began},Newtonsoft.Json.Formatting.Indented));
}
void Complete()
{
    reports.Add(new {name=cases[testCase],states,trace});testCase++;
    if(testCase==cases.Length)Finish(true,"동시 입력 우선순위·0.15초 예약·자동 복귀·중단 처리 검증 완료");else Prepare();
}
tick=()=>
{
    try
    {
        if(!Application.isPlaying||movement==null){Finish(false,"플레이 종료");return;}
        if(EditorApplication.timeSinceStartup-began>40d){Finish(false,"시간 초과 "+testCase+"/"+phase+"/"+stage);return;}
        if(lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
        if(Time.frameCount>=releaseFrame){held=Array.Empty<UnityEngine.InputSystem.Key>();mouseHeld=false;}
        string state=control.SelectedState;float progress=control.PlaybackProgress;
        if(phase==0)
        {
            if(Time.frameCount<phaseFrame+10||state!="DRAWN_IDLE"||animator.IsInTransition(0))return;
            if(testCase==0){Pulse(UnityEngine.InputSystem.Key.Digit4,UnityEngine.InputSystem.Key.Space,UnityEngine.InputSystem.Key.Digit1,UnityEngine.InputSystem.Key.F);mouseHeld=true;}
            else if(testCase==1){Pulse(UnityEngine.InputSystem.Key.Space,UnityEngine.InputSystem.Key.Digit1,UnityEngine.InputSystem.Key.F);mouseHeld=true;}
            else if(testCase==2){Pulse(UnityEngine.InputSystem.Key.Digit1,UnityEngine.InputSystem.Key.F);mouseHeld=true;}
            else if(testCase==3){Pulse(UnityEngine.InputSystem.Key.Digit2,UnityEngine.InputSystem.Key.J);mouseHeld=true;}
            else if(testCase==4)Pulse(UnityEngine.InputSystem.Key.W,UnityEngine.InputSystem.Key.LeftShift,UnityEngine.InputSystem.Key.F);
            else if(testCase==7)Pulse(UnityEngine.InputSystem.Key.Space);
            else Pulse(UnityEngine.InputSystem.Key.J);
            phase=1;phaseFrame=Time.frameCount;return;
        }
        if(state!=previousState){states.Add(state);Note("상태 변경");}
        if(movement.BufferedAction!=previousBuffer)Note("예약 변경");
        previousState=state;previousBuffer=movement.BufferedAction;
        if(testCase<=4&&Time.frameCount==phaseFrame+1)
        {
            string expected=testCase==0?"HIT":testCase==1?"ROLL":testCase==2?"ATTACK1":testCase==3?"ATTACK2":"SHEATHE";
            Check(state==expected,cases[testCase]+" → "+state);
            Check(!UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject(),"동시 클릭 시험은 UI 바깥에서 수행");
            if(testCase==4)Check(movement.IsMoving,"발도·납도 중 하체 달리기 유지");
            stage=1;
        }
        else if(testCase==5)
        {
            if(stage==0&&state=="ATTACK1"&&control.SecondsUntilAttackCancel(2)<=0.2f&&control.SecondsUntilAttackCancel(2)>0.17f)
            {Pulse(UnityEngine.InputSystem.Key.J);stage=1;Note("캔슬 약 0.2초 전 입력");}
            else if(stage==1&&state=="ATTACK1"&&control.SecondsUntilAttackCancel(2)<0.17f)
            {Check(movement.BufferedAction=="없음","0.15초보다 이른 입력은 예약되지 않음");stage=2;}
        }
        else if(testCase==6)
        {
            if(stage==0&&state=="ATTACK1"&&control.SecondsUntilAttackCancel(2)<=0.1f){Pulse(UnityEngine.InputSystem.Key.J);stage=1;}
            else if(stage==1&&state=="ATTACK2"&&control.SecondsUntilAttackCancel(3)<=0.1f){Pulse(UnityEngine.InputSystem.Key.J);stage=2;}
        }
        else if(testCase==7)
        {
            if(stage==0&&state=="ROLL"&&progress>0.88f){Pulse(UnityEngine.InputSystem.Key.Digit1);stage=1;Note("구르기 종료 직전 공격 예약");}
        }
        else if(testCase==8)
        {
            if(stage==0&&state=="ATTACK1"&&control.SecondsUntilAttackCancel(2)<=0.12f){Pulse(UnityEngine.InputSystem.Key.J);stage=1;}
            else if(stage==1&&movement.BufferedAction=="공격 2"){Pulse(UnityEngine.InputSystem.Key.Digit4);stage=2;}
        }
        else if(testCase==9)
        {
            if(stage==0&&state=="ATTACK1"&&control.SecondsUntilAttackCancel(2)<=0.12f){Pulse(UnityEngine.InputSystem.Key.Digit2);stage=1;}
            else if(stage==1&&movement.BufferedAction=="공격 2"){Pulse(UnityEngine.InputSystem.Key.Digit3);stage=2;}
            else if(stage==2&&state=="ATTACK1"){Check(movement.BufferedAction=="공격 2","공격 3 추가 입력에도 첫 공격 2 예약 유지");stage=3;}
        }
        else if(testCase==10)
        {
            if(stage==0&&state=="ATTACK1"&&control.SecondsUntilAttackCancel(2)<=0.04f){Pulse(UnityEngine.InputSystem.Key.J);stage=1;}
            else if(stage==1&&movement.BufferedAction=="공격 2"){Pulse(UnityEngine.InputSystem.Key.Space);stage=2;}
            else if(stage==2&&movement.BufferedAction=="회피"){Check(true,"회피 입력이 대기 중 공격 예약을 교체");stage=3;}
        }
        else if(testCase==11)
        {
            if(stage==0&&state=="ATTACK1"&&control.SecondsUntilAttackCancel(2)<=0.12f){Pulse(UnityEngine.InputSystem.Key.J);stage=1;}
            else if(stage==1&&movement.BufferedAction=="공격 2")
            {
                focused=GameObject.Find("Test_PlaybackStart_Run").GetComponent<TMPro.TMP_InputField>();
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(focused.gameObject);focused.ActivateInputField();
                focusFrame=Time.frameCount;stage=2;
            }
            else if(stage==2&&Time.frameCount>=focusFrame+5)
            {
                Check(movement.BufferedAction=="없음","숫자 입력란 포커스 중 기존 예약 취소");
                focused.DeactivateInputField();focused=null;UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);stage=3;
            }
        }
        else if(testCase==12)
        {
            if(stage==0&&state=="ATTACK1"&&control.SecondsUntilEquipmentCancel()<=0.12f){Pulse(UnityEngine.InputSystem.Key.F);stage=1;}
            else if(stage==1&&movement.BufferedAction=="발도 / 납도")
            {held=new [] {UnityEngine.InputSystem.Key.W};releaseFrame=Time.frameCount+8;stage=2;}
        }
        if(stage==0||control.IsActionPlaying||animator.IsInTransition(0)||Time.frameCount<phaseFrame+15)return;
        if(idleFrame<0)idleFrame=Time.frameCount;
        if(Time.frameCount<idleFrame+45)return;
        Check(movement.BufferedAction=="없음",cases[testCase]+" 종료 후 남은 예약 없음");
        if(testCase<=4)Check(states.Count(s=>s=="HIT"||s=="ROLL"||s.StartsWith("ATTACK")||s=="SHEATHE")==1,"동시 입력에서 선택한 동작 하나만 실행");
        if(testCase==5)Check(!states.Contains("ATTACK2"),"이른 입력이 나중에 자동 실행되지 않음");
        if(testCase==6)Check(states.Contains("ATTACK2")&&states.Contains("ATTACK3"),"0.15초 예약 창으로 공격 1→2→3 콤보 실행");
        if(testCase==7)
        {
            int roll=states.IndexOf("ROLL"),attack=states.IndexOf("ATTACK1");
            Check(roll>=0&&attack==roll+1,"캔슬 100%에서 대기를 거치지 않고 구르기→공격 예약 실행");
        }
        if(testCase==8)Check(states.Contains("HIT")&&!states.Contains("ATTACK2"),"피격 후 이전 공격 예약이 재실행되지 않음");
        if(testCase==9)Check(states.Contains("ATTACK2")&&!states.Contains("ATTACK3"),"첫 예약만 한 번 실행");
        if(testCase==10)Check(states.Contains("ROLL")&&!states.Contains("ATTACK2"),"공격 예약 대신 회피만 실행");
        if(testCase==11)Check(!states.Contains("ATTACK2"),"숫자 편집을 마쳐도 과거 예약이 실행되지 않음");
        if(testCase==12)Check(!states.Contains("SHEATHE"),"이동으로 원래 공격을 중단하면 발도·납도 예약 취소");
        Complete();
    }
    catch(Exception error){Finish(false,error.ToString());}
};
Prepare();EditorApplication.update+=tick;
return new {started=true,cases};
