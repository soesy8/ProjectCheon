if (!Application.isPlaying) throw new Exception("마운틴 씬의 플레이 모드가 필요합니다.");
var movement = UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.MountainMap.Test_MountainPlayerMovement>();
var control = movement.AnimationControl;
var body = movement.GetComponent<CharacterController>();
var cameraControl = UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.MountainMap.Test_MountainPlayerCamera>();
var view = Camera.main.transform;
var viewPosition=view.position;
var viewRotation=view.rotation;
var cameraWasEnabled=cameraControl.enabled;
cameraControl.enabled=false;
view.rotation=Quaternion.identity;
var oldCaptureDelta=Time.captureDeltaTime;
var oldKeyboard=UnityEngine.InputSystem.Keyboard.current;
var keyboard=UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>("Test_RollMovementKeyboard");
var oldBackground=UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior;
UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior=UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
var progressField=movement.GetType().GetField("dodgeProgress",flags);
var steeringField=movement.GetType().GetField("dodgeSteering",flags);
float Progress() => (float)progressField.GetValue(movement);
var ground=GameObject.CreatePrimitive(PrimitiveType.Plane);
ground.name="Test_TemporaryRollMovementGround";
ground.hideFlags=HideFlags.DontSave;
ground.transform.SetPositionAndRotation(new Vector3(0f,150f,0f),Quaternion.identity);
ground.transform.localScale=Vector3.one*10f;
UnityEngine.InputSystem.Key[] held=Array.Empty<UnityEngine.InputSystem.Key>();
Action feed=()=> { keyboard.MakeCurrent(); UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(held)); };
UnityEngine.InputSystem.InputSystem.onBeforeUpdate+=feed;
UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
var cases=new [] {"기본 구르기 60fps", "구간 8~32 / 1.5배속 30fps", "구르는 중 방향 조작 35%", "방향 조작 0% 비교", "방향 조작 100% 비교", "일시정지 후 구르기 재개"};
var reports=new List<object>();
var checks=new List<string>();
void Check(bool condition,string reason) { if(!condition) throw new Exception(reason); checks.Add(reason); }
int testCase=0,phase=0,lastFrame=-1,phaseFrame=Time.frameCount,pauseUntil=-1;
bool pausedOnce=false;
Vector3 start=Vector3.zero,previous=Vector3.zero,pausePosition=Vector3.zero;
float distanceSum=0f,lastYaw=0f,pauseProgress=0f;
float[] binsDistance=new float[4],binsTime=new float[4];
var samples=new List<object>();
double began=EditorApplication.timeSinceStartup;
EditorApplication.CallbackFunction tick=null;
void Next(int value) { phase=value; phaseFrame=Time.frameCount; }
void Prepare()
{
    held=Array.Empty<UnityEngine.InputSystem.Key>();
    if(control.IsPaused) control.TogglePause();
    movement.ResetToSpawn();
    control.ReloadSavedPlaybackSettings();
    if(testCase==1) { control.SetPlaybackFrameRange(4,8,32); control.rollSpeed=1.5f; }
    steeringField.SetValue(movement,testCase==3?0f:testCase==4?1f:0.35f);
    Time.captureDeltaTime=testCase==1?1f/30f:1f/60f;
    body.enabled=false;
    movement.transform.SetPositionAndRotation(new Vector3(0f,150.04f,0f),Quaternion.identity);
    body.enabled=true;
    Physics.SyncTransforms();
    start=previous=movement.transform.position;
    distanceSum=0f; lastYaw=0f; pausedOnce=false;
    binsDistance=new float[4]; binsTime=new float[4]; samples=new List<object>();
    Next(0);
}
void Record()
{
    var now=movement.transform.position;
    float distance=Vector3.ProjectOnPlane(now-previous,Vector3.up).magnitude;
    float progress=Progress();
    distanceSum+=distance;
    if(!control.IsPaused)
    {
        int bin=Mathf.Min(3,Mathf.FloorToInt(progress*4f));
        binsDistance[bin]+=distance; binsTime[bin]+=Time.deltaTime;
    }
    if(control.IsDodging) lastYaw=Vector3.Angle(Vector3.forward,movement.transform.forward);
    samples.Add(new {frame=Time.frameCount,progress,distance,delta=Time.deltaTime,paused=control.IsPaused,yaw=lastYaw});
    previous=now;
}
void Finish(bool success,string reason)
{
    EditorApplication.update-=tick;
    UnityEngine.InputSystem.InputSystem.onBeforeUpdate-=feed;
    UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard);
    if(oldKeyboard!=null) oldKeyboard.MakeCurrent();
    UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior=oldBackground;
    Time.captureDeltaTime=oldCaptureDelta;
    steeringField.SetValue(movement,0.35f);
    if(control.IsPaused) control.TogglePause();
    control.ReloadSavedPlaybackSettings(); movement.ResetToSpawn();
    view.SetPositionAndRotation(viewPosition,viewRotation); cameraControl.enabled=cameraWasEnabled;
    UnityEngine.Object.Destroy(ground);
    System.IO.File.WriteAllText("Assets/Temp/RollMovementTuning~/Test_RollMovementReport.json",Newtonsoft.Json.JsonConvert.SerializeObject(new {success,reason,checks,reports,elapsed=EditorApplication.timeSinceStartup-began},Newtonsoft.Json.Formatting.Indented));
}
tick=()=>
{
    try
    {
        if(!Application.isPlaying || movement==null) { Finish(false,"검증 도중 플레이 종료"); return; }
        if(EditorApplication.timeSinceStartup-began>35d) { Finish(false,"검증 시간 초과: "+testCase+" / "+phase); return; }
        if(lastFrame==Time.frameCount) return;
        lastFrame=Time.frameCount;
        if(phase==0)
        {
            if(Time.frameCount<phaseFrame+6) return;
            start=previous=movement.transform.position;
            held=testCase>=2&&testCase<=4 ? new [] {UnityEngine.InputSystem.Key.W,UnityEngine.InputSystem.Key.Space} : new [] {UnityEngine.InputSystem.Key.Space};
            Next(1);
        }
        else if(phase==1)
        {
            if(!control.IsDodging) return;
            Record(); held=Array.Empty<UnityEngine.InputSystem.Key>(); Next(2);
        }
        else if(phase==2)
        {
            Record();
            if(testCase>=2&&testCase<=4&&control.IsDodging&&Progress()>0.12f)
                held=new [] {UnityEngine.InputSystem.Key.D};
            if(testCase==5&&control.IsDodging)
            {
                if(!pausedOnce&&Progress()>0.3f)
                {
                    pausedOnce=true; pausePosition=movement.transform.position; pauseProgress=Progress();
                    pauseUntil=Time.frameCount+10; control.TogglePause();
                }
                else if(control.IsPaused&&Time.frameCount>=pauseUntil)
                {
                    Check(Vector3.ProjectOnPlane(movement.transform.position-pausePosition,Vector3.up).magnitude<0.0001f&&Mathf.Abs(Progress()-pauseProgress)<0.0001f,"일시정지 중 이동과 구르기 진행도 유지");
                    control.TogglePause();
                }
            }
            if(control.IsDodging) return;
            held=Array.Empty<UnityEngine.InputSystem.Key>();
            var average=binsDistance.Select((d,index)=>binsTime[index]>0f?d/binsTime[index]:0f).ToArray();
            Check(Mathf.Abs(distanceSum-7.5f)<0.08f,cases[testCase]+" 총 이동 거리: "+distanceSum.ToString("F4"));
            Check(average[0]>average[1]&&average[1]>average[2]&&average[2]>average[3]&&average[3]<average[0]*0.25f,cases[testCase]+" 초반부터 마지막 구간까지 점진적으로 감속");
            if(testCase==2) Check(Mathf.Abs(lastYaw-28.3f)<2f,"구르는 중 새 입력을 35% 반영한 방향 전환: "+lastYaw.ToString("F2")+"도");
            if(testCase==3) Check(lastYaw<0.1f,"조작 비율 0%에서 방향 고정");
            if(testCase==4) Check(lastYaw>88f,"조작 비율 100%에서 입력 방향까지 전환");
            reports.Add(new {name=cases[testCase],distance=distanceSum,quarterSpeeds=average,lastYaw,samples});
            testCase++;
            if(testCase>=cases.Length) Finish(true,"감속, 거리 유지, 방향 입력, 일시정지 검증 완료");
            else Prepare();
        }
    }
    catch(Exception error) { Finish(false,error.ToString()); }
};
Prepare();
EditorApplication.update+=tick;
return new {started=true,cases=cases};
