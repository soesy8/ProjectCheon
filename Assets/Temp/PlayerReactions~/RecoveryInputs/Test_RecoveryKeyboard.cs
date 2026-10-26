using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Cheon.Tests.PlayerReactions;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
public class Test_RecoveryKeyboard : MonoBehaviour
{
    [Serializable] public class Sample {public string label,phase,state;public int frame;public float clipFrame,landed,hipHeight,headHeight,x,z;}
    [Serializable] public class Report {public string status;public int startFrame,endFrame;public List<string> checks=new List<string>();public List<Sample> samples=new List<Sample>();}
    private InputSettings originalInputSettings, testInputSettings; private UnityEditor.EditorWindow previousWindow; private TaskCompletionSource<string> done=new TaskCompletionSource<string>();
    private Test_PlayerReactionPreview p;private Keyboard keyboard;private Report report=new Report();
    private int stage;private float started,stageStarted,previousGetUpFrame,moveStarted;private bool earlySpace,sentRelease,lateSpace;
    private Vector3 moveOrigin,dodgeOrigin;private float idleTime; private Key[] heldKeys=new Key[0];
    public static Task<string> Main(){var r=new GameObject("Test_RecoveryKeyboard").AddComponent<Test_RecoveryKeyboard>();r.p=UnityEngine.Object.FindFirstObjectByType<Test_PlayerReactionPreview>();r.p.ResetPreview();r.p.repeatSequence=false;r.originalInputSettings=InputSystem.settings;r.testInputSettings=UnityEngine.Object.Instantiate(InputSystem.settings);r.testInputSettings.hideFlags=HideFlags.DontSave;InputSystem.settings=r.testInputSettings;r.testInputSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;r.testInputSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;r.previousWindow=UnityEditor.EditorWindow.focusedWindow;UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();r.keyboard=InputSystem.AddDevice<Keyboard>();r.keyboard.MakeCurrent();r.started=Time.realtimeSinceStartup;r.report.startFrame=Time.frameCount;r.Click("HeavyPattern");r.Keys(Key.W);return r.done.Task;}
    private void Keys(params Key[] keys){heldKeys=keys;InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));}
    private void Check(bool value,string label){if(!value)throw new Exception("FAIL stage "+stage+": "+label);report.checks.Add(label);}
    private void Click(string name){var b=UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None).Single(b=>b.name==name && b.gameObject.scene==p.gameObject.scene);EventSystem.current.SetSelectedGameObject(null);ExecuteEvents.Execute(b.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);stageStarted=Time.realtimeSinceStartup;}
    private Sample Record(string label){var a=p.CharacterAnimator;var hip=a.GetBoneTransform(HumanBodyBones.Hips).position;var s=new Sample{label=label,phase=p.Phase.ToString(),state=p.AnimatorState,frame=Time.frameCount,clipFrame=p.CurrentFrame,landed=p.LandedSeconds,hipHeight=hip.y,headHeight=a.GetBoneTransform(HumanBodyBones.Head).position.y,x=hip.x,z=hip.z};report.samples.Add(s);return s;}
    private void Finish(Exception error=null){Record("Finish stage "+stage+" / "+p.LastInputResult+" / W="+keyboard.wKey.isPressed+" / current="+(Keyboard.current==keyboard)); File.WriteAllText("Assets/Temp/PlayerReactions~/RecoveryInputs/Test_KeyboardLastAttempt.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));Keys();if(keyboard!=null)InputSystem.RemoveDevice(keyboard);InputSystem.settings=originalInputSettings;Destroy(testInputSettings);if(previousWindow!=null)previousWindow.Focus();p.ResetPreview();if(error==null){report.status="passed";report.endFrame=Time.frameCount;string json=Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented);File.WriteAllText("Assets/Temp/PlayerReactions~/RecoveryInputs/Test_KeyboardReport.json",json);done.TrySetResult(json);}else done.TrySetException(error);Destroy(gameObject);}
    private void LateUpdate(){try{ InputSystem.QueueStateEvent(keyboard,new KeyboardState(heldKeys));
        if(Time.realtimeSinceStartup-started>27f)throw new Exception("Timeout stage "+stage+" phase="+p.Phase+" state="+p.AnimatorState+" frame="+p.CurrentFrame+" prog="+p.GetUpProgress+" canMove="+p.CanMove+" W="+keyboard.wKey.isPressed+" current="+(Keyboard.current==keyboard)+" inputEnabled="+p.GetComponent<Test_PlayerReactionInput>().enabled);
        if(stage==0){
            if(p.Phase==Test_PlayerReactionPreview.PreviewPhase.HeavyHit && p.CurrentFrame>28f && !earlySpace){var pose=Record("New heavy hit after landing");Check(pose.hipHeight<0.4f && pose.headHeight<0.6f,"새 강피격이 실제 프레임에서 바닥에 쓰러짐");earlySpace=true;}
            if(p.Phase==Test_PlayerReactionPreview.PreviewPhase.GetUp){Record("Held W cancels hit into get up");Check(p.LandedSeconds>=0.5f && p.LandedSeconds<0.6f && p.CurrentFrame<3f,"실제 W 유지 시 착지 후 0.5초부터 일어나기");stage=1;}
        }
        else if(stage==1){if(p.Phase==Test_PlayerReactionPreview.PreviewPhase.GetUp)previousGetUpFrame=p.CurrentFrame;if(p.Phase==Test_PlayerReactionPreview.PreviewPhase.Moving){Record("W cancels get up to movement");Check(previousGetUpFrame>=51f && p.AnimatorState=="IDLE","실제 W 입력은 일어나기 90%에서 이동 트리로 연결");moveOrigin=p.CharacterAnimator.transform.position;moveStarted=Time.realtimeSinceStartup;stage=2;}}
        else if(stage==2 && Time.realtimeSinceStartup-moveStarted>0.35f){Check(Vector3.Distance(moveOrigin,p.CharacterAnimator.transform.position)>0.6f,"이동 캔슬 후 캐릭터가 실제 이동");Record("Rendered locomotion");Keys();p.ResetPreview();Click("HeavyPattern");earlySpace=false;sentRelease=false;lateSpace=false;stage=3;}
        else if(stage==3){
            if(!earlySpace && p.LandedSeconds>0.3f){Keys(Key.Space);earlySpace=true;}
            else if(earlySpace && !sentRelease){Check(p.Phase==Test_PlayerReactionPreview.PreviewPhase.HeavyHit,"실제 이른 Space 입력 거부");Keys();sentRelease=true;}
            if(sentRelease && !lateSpace && p.LandedSeconds>0.53f){Keys(Key.Space);lateSpace=true;}
            if(p.Phase==Test_PlayerReactionPreview.PreviewPhase.GetUp){Check(lateSpace && p.CurrentFrame<3f,"실제 Space 입력으로 피격을 캔슬하면 구르기 대신 일어나기");Record("Space recovers after landing");Keys();earlySpace=false;sentRelease=false;lateSpace=false;stage=4;}
        }
        else if(stage==4){
            if(p.Phase==Test_PlayerReactionPreview.PreviewPhase.GetUp){previousGetUpFrame=p.CurrentFrame;
                if(!earlySpace && p.GetUpProgress>0.6f){Keys(Key.Space);earlySpace=true;}
                else if(earlySpace && !sentRelease){Check(p.AnimatorState=="GET_UP","실제 Space 70% 이전 회피 차단");Keys();sentRelease=true;}
                if(sentRelease && !lateSpace && p.GetUpProgress>0.73f){Keys(Key.Space);lateSpace=true;}
            }
            if(p.Phase==Test_PlayerReactionPreview.PreviewPhase.Dodging){Check(lateSpace && previousGetUpFrame>=40.6f,"실제 Space 70% 이후 구르기로 캔슬");Record("Rendered get up to dodge");dodgeOrigin=p.CharacterAnimator.transform.position;Keys();stage=5;}
        }
        else if(stage==5 && p.AnimatorState=="IDLE" && !p.IsBlending){Check(Vector3.Distance(dodgeOrigin,p.CharacterAnimator.transform.position)>1f,"구르기 애니메이션과 실제 회피 이동 재생");Record("Dodge returned to idle");p.ResetPreview();Click("HeavyPattern");earlySpace=false;sentRelease=false;stage=6;}
        else if(stage==6){
            if(p.Phase==Test_PlayerReactionPreview.PreviewPhase.DownHold && !earlySpace){var pose=Record("No input holds full hit");Check(pose.clipFrame>=91f && pose.hipHeight<0.4f,"무입력에서 새 강피격 전체 91프레임 재생");earlySpace=true;}
            if(p.Phase==Test_PlayerReactionPreview.PreviewPhase.GetUp){previousGetUpFrame=p.CurrentFrame;if(!sentRelease && p.CurrentFrame>24f){Record("Natural get up corrected head");sentRelease=true;}}
            if(p.Phase==Test_PlayerReactionPreview.PreviewPhase.Complete && p.AnimatorState=="IDLE" && !p.IsBlending){var pose=Record("No input natural idle return");Check(earlySpace && sentRelease && previousGetUpFrame>=51f && pose.hipHeight>0.8f,"무입력 90%부터 블렌딩 후 실제 대기 자세 복귀");idleTime=p.IdleNormalizedTime;stageStarted=Time.realtimeSinceStartup;stage=7;}
        }
        else if(stage==7 && Time.realtimeSinceStartup-stageStarted>0.2f){Check(p.IdleNormalizedTime>idleTime,"대기 모션이 실제 프레임에서 계속 진행");p.ResetPreview();Click("LethalAttack");Keys(Key.W,Key.Space);stage=8;}
        else if(stage==8 && p.Phase==Test_PlayerReactionPreview.PreviewPhase.Dead){var pose=Record("Lethal hit held");Check(pose.hipHeight<0.4f && p.AnimatorState=="DEATH","이동·회피 입력 중에도 사망 자세 유지");stageStarted=Time.realtimeSinceStartup;Keys();stage=9;}
        else if(stage==9 && Time.realtimeSinceStartup-stageStarted>0.3f){Keys(Key.W,Key.Space);stage=10;stageStarted=Time.realtimeSinceStartup;}
        else if(stage==10 && Time.realtimeSinceStartup-stageStarted>0.3f){Check(p.Phase==Test_PlayerReactionPreview.PreviewPhase.Dead && p.IsPaused,"사망 후 새로운 이동·회피 입력으로도 회복 불가");Finish();}
    }catch(Exception error){Finish(error);}}
}



