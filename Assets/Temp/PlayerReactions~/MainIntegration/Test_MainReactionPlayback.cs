using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Cheon.Prototype.MountainPass;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
[DefaultExecutionOrder(100)] public class Test_MainReactionPlayback : MonoBehaviour
{
    private TaskCompletionSource<string> done=new TaskCompletionSource<string>();
    private PassGameFlow game;private PassPlayerMovement player;private new PassAnimationController animation;private PassHealth health;
    private Keyboard keyboard;private Key[] heldKeys=new Key[0];private InputSettings originalInputSettings,testInputSettings;
    private Vector3 spawn;private Quaternion facing;private float started,stageStarted,lastGetUpProgress,lastHitProgress;private int stage;
    private bool earlySpace,released,lateSpace,downRecorded;private Vector3 movementOrigin;
    private List<string> checks=new List<string>();private List<object> samples=new List<object>();
    public static Task<string> Main()
    {
        var r=new GameObject("Test_MainReactionPlayback").AddComponent<Test_MainReactionPlayback>();
        r.game=UnityEngine.Object.FindFirstObjectByType<PassGameFlow>();r.player=r.game.Player.GetComponent<PassPlayerMovement>();r.animation=r.player.AnimationControl;r.health=r.game.PlayerHealth;
        r.game.BeginJourney();r.game.Boss.enabled=false;var arena=(Vector3)typeof(PassGameFlow).GetField("arenaCheckpoint",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(r.game);r.player.Teleport(arena,Quaternion.LookRotation(Vector3.ProjectOnPlane(r.game.Boss.transform.position-arena,Vector3.up)));r.game.View.SnapBehind();r.health.Configure(10000f,0f);r.health.Restore();r.spawn=r.player.transform.position;r.facing=r.player.transform.rotation;
        r.originalInputSettings=InputSystem.settings;r.testInputSettings=UnityEngine.Object.Instantiate(InputSystem.settings);r.testInputSettings.hideFlags=HideFlags.DontSave;
        InputSystem.settings=r.testInputSettings;r.testInputSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;r.testInputSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
        r.keyboard=InputSystem.AddDevice<Keyboard>();r.keyboard.MakeCurrent();EventSystem.current?.SetSelectedGameObject(null);
        r.started=Time.realtimeSinceStartup;r.Check(Mathf.Abs(r.animation.ReactionSettings.getUpDodgeCancelProgress-0.45f)<0.0001f,"메인 일어나기 회피 기준 45%");
        var timing=r.animation.RuntimeTimings.Get("HIT");
        r.Check(Mathf.Abs(timing.moveCancel-0.3f)<0.0001f && Mathf.Abs(timing.dodgeCancel-0.1f)<0.0001f,"일반 피격 이동 30%·회피 10%");
        r.Check(timing.attack1Cancel==0.85f && timing.attack2Cancel==0.85f && timing.attack3Cancel==0.85f && timing.equipmentCancel==0.9f,"일반 피격의 공격·장비 캔슬 기존 값 유지");
        r.health.TakeDamage(1f);
        r.Check(r.animation.SelectedState=="HIT","일반 피해는 기존 일반 피격 재생");
        r.animation.CharacterAnimator.Play("HIT",0,0.099f);r.animation.CharacterAnimator.Update(0f);r.Check(!r.animation.CanDodge,"일반 피격 9.9% 회피 차단");
        r.animation.CharacterAnimator.Play("HIT",0,0.101f);r.animation.CharacterAnimator.Update(0f);r.Check(r.animation.CanDodge,"일반 피격 10.1% 회피 허용");
        r.animation.CharacterAnimator.Play("HIT",0,0.299f);r.animation.CharacterAnimator.Update(0f);r.Check(!r.animation.CanMove,"일반 피격 29.9% 이동 차단");
        r.animation.CharacterAnimator.Play("HIT",0,0.301f);r.animation.CharacterAnimator.Update(0f);r.Check(r.animation.CanMove,"일반 피격 30.1% 이동 허용");
        r.Keys(Key.Space);r.stageStarted=Time.realtimeSinceStartup;return r.done.Task;
    }
    private void Check(bool value,string label){if(!value)throw new Exception("FAIL stage "+stage+": "+label);checks.Add(label);}
    private void Keys(params Key[] keys){heldKeys=keys;InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));}
    private void Record(string label){var a=animation.CharacterAnimator;var h=a.GetBoneTransform(HumanBodyBones.Hips).position;var head=a.GetBoneTransform(HumanBodyBones.Head).position;var root=player.transform.position;samples.Add(new{label,frame=Time.frameCount,state=animation.SelectedState,reactionFrame=animation.ReactionFrame,progress=animation.PlaybackProgress,landed=animation.LandedSeconds,hipAboveRoot=h.y-root.y,headAboveRoot=head.y-root.y,root=new[]{root.x,root.y,root.z},visualOffset=new[]{a.transform.localPosition.x,a.transform.localPosition.y,a.transform.localPosition.z}});}
    private void ResetPlayer(){Keys();player.Teleport(spawn,facing);health.Restore();player.InputEnabled=true;earlySpace=released=lateSpace=downRecorded=false;stageStarted=Time.realtimeSinceStartup;}
    private bool Idle=>animation.SelectedState=="SHEATHED_IDLE"||animation.SelectedState=="DRAWN_IDLE";
    private void Finish(Exception error=null)
    {
        Record(error==null?"Finished":"Failed: "+error.Message);
        string json=Newtonsoft.Json.JsonConvert.SerializeObject(new{status=error==null?"passed":"failed",stage,checks,samples},Newtonsoft.Json.Formatting.Indented);
        File.WriteAllText("Assets/Temp/PlayerReactions~/MainIntegration/Test_MainPlaybackReport.json",json);
        Keys();if(keyboard!=null)InputSystem.RemoveDevice(keyboard);InputSystem.settings=originalInputSettings;Destroy(testInputSettings);
        if(error==null)done.TrySetResult(json);else done.TrySetException(error);Destroy(gameObject);
    }
    private void LateUpdate()
    {
        try{
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(heldKeys));
            if(Time.realtimeSinceStartup-started>40f)throw new Exception("Timeout state="+animation.SelectedState+" frame="+animation.ReactionFrame+" progress="+animation.PlaybackProgress);
            if(stage==0 && animation.SelectedState=="ROLL") {Check(true,"일반 피격에서 실제 Space 입력으로 구르기");Record("Normal hit dodge");Keys();stage=1;}
            else if(stage==1 && Idle && !animation.CharacterAnimator.IsInTransition(0)) {ResetPlayer();health.TakeDamage(1f);Keys(Key.W);lastHitProgress=0f;stage=2;}
            else if(stage==2){if(animation.SelectedState=="HIT")lastHitProgress=animation.PlaybackProgress;if(Idle){Check(lastHitProgress>=0.25f && lastHitProgress<0.31f,"일반 피격 이동 캔슬 실제 W 입력");movementOrigin=player.transform.position;stageStarted=Time.realtimeSinceStartup;stage=3;}}
            else if(stage==3 && Time.realtimeSinceStartup-stageStarted>0.2f){Check(Vector3.Distance(movementOrigin,player.transform.position)>0.2f,"일반 피격 캔슬 뒤 실제 이동");ResetPlayer();var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;typeof(PassBossController).GetField("waveAttack",flags).SetValue(game.Boss,false);typeof(PassBossController).GetField("slamPosition",flags).SetValue(game.Boss,player.transform.position);typeof(PassBossController).GetMethod("ResolveAttack",flags).Invoke(game.Boss,null);Check(health.LastDamageWasHeavy && animation.SelectedState=="HEAVY_HIT","실제 보스 내려찍기 명중 시 메인 강피격 재생");Keys(Key.W);stage=4;}
            else if(stage==4 && animation.SelectedState=="GET_UP") {Check(animation.LandedSeconds>=0.5f && animation.LandedSeconds<0.65f,"강피격 착지 +0.5초 후 W 입력으로 일어나기");Check(Vector3.ProjectOnPlane(player.transform.position-spawn,Vector3.up).magnitude>0.5f,"강피격 수평 이동을 실제 캐릭터 몸체에 전달");Record("Main heavy to get up");lastGetUpProgress=0f;stage=5;}
            else if(stage==5){if(animation.SelectedState=="GET_UP")lastGetUpProgress=animation.PlaybackProgress;if(Idle){Check(lastGetUpProgress>=0.86f && lastGetUpProgress<0.91f,"일어나기 이동 캔슬 90% 유지");Record("Get up move cancel");stageStarted=Time.realtimeSinceStartup;stage=6;}}
            else if(stage==6 && Time.realtimeSinceStartup-stageStarted>0.2f){ResetPlayer();health.TakeDamage(1f,true);stage=7;}
            else if(stage==7){
                if(!earlySpace && animation.LandedSeconds>0.3f){Keys(Key.Space);earlySpace=true;}
                else if(earlySpace && !released){Check(animation.SelectedState=="HEAVY_HIT","착지 +0.5초 전 실제 Space 차단");Keys();released=true;}
                if(released && !lateSpace && animation.LandedSeconds>0.53f){Keys(Key.Space);lateSpace=true;}
                if(animation.SelectedState=="GET_UP"){Check(lateSpace,"강피격 후 Space는 일어나기에 소비");Record("Main Space recovery");Keys();earlySpace=released=lateSpace=false;stage=8;}
            }
            else if(stage==8){
                if(animation.SelectedState=="GET_UP"){
                    lastGetUpProgress=animation.PlaybackProgress;
                    if(!earlySpace && lastGetUpProgress>0.35f){Keys(Key.Space);earlySpace=true;}
                    else if(earlySpace && !released){Check(animation.SelectedState=="GET_UP","일어나기 45% 이전 실제 Space 차단");Keys();released=true;}
                    if(released && !lateSpace && lastGetUpProgress>0.46f){Keys(Key.Space);lateSpace=true;}
                }
                if(animation.SelectedState=="ROLL"){Check(lateSpace && lastGetUpProgress>=0.45f && lastGetUpProgress<0.55f,"일어나기 45% 이후 실제 Space 회피 캔슬");Record("Main get up dodge");Keys();stage=9;}
            }
            else if(stage==9 && Idle && !animation.CharacterAnimator.IsInTransition(0)){ResetPlayer();health.TakeDamage(1f,true);stage=10;}
            else if(stage==10){
                if(animation.SelectedState=="HEAVY_HIT" && animation.ReactionFrame>=91f && !downRecorded){var hips=animation.CharacterAnimator.GetBoneTransform(HumanBodyBones.Hips);Check(hips.position.y-player.transform.position.y<0.4f,"메인 실제 프레임에서 강피격 91프레임 쓰러진 자세");Record("Main full heavy hit down");downRecorded=true;}
                if(animation.SelectedState=="GET_UP")lastGetUpProgress=animation.PlaybackProgress;
                if(Idle && !animation.CharacterAnimator.IsInTransition(0)){Check(downRecorded && lastGetUpProgress>=0.86f,"무입력 전체 피격 후 90%에서 대기 전환");var hip=animation.CharacterAnimator.GetBoneTransform(HumanBodyBones.Hips);Check(hip.position.y-player.transform.position.y>0.8f,"메인 실제 대기 자세 복귀");Record("Main natural idle");stageStarted=Time.realtimeSinceStartup;stage=11;}
            }
            else if(stage==11 && Time.realtimeSinceStartup-stageStarted>0.2f){ResetPlayer();health.TakeDamage(health.Current+1f);Check(game.State==PassGameFlow.RunState.Defeat && animation.SelectedState=="DEATH","일반 공격도 치명타이면 강피격 사망 재생");Keys(Key.W,Key.Space);stage=12;}
            else if(stage==12 && animation.ReactionFrame>=91f){Record("Main death full clip");Check(animation.SelectedState=="DEATH" && !animation.CanMove && !animation.CanDodge,"사망 마지막 프레임에서 이동·회피 차단");stageStarted=Time.realtimeSinceStartup;Keys();stage=13;}
            else if(stage==13 && Time.realtimeSinceStartup-stageStarted>0.4f){Keys(Key.W,Key.Space);stageStarted=Time.realtimeSinceStartup;stage=14;}
            else if(stage==14 && Time.realtimeSinceStartup-stageStarted>0.3f){Check(animation.SelectedState=="DEATH" && animation.ReactionFrame==91f,"사망 후 새 입력에도 마지막 자세 유지");Record("Main death held");Keys();game.RetryCheckpoint();game.Boss.enabled=false;stageStarted=Time.realtimeSinceStartup;stage=15;}
            else if(stage==15 && Time.realtimeSinceStartup-stageStarted>0.25f){Check(!health.IsDead && player.InputEnabled && Idle,"재시작 시 사망·위치 보정 해제 및 입력 복구");Check(animation.CharacterAnimator.transform.localPosition.sqrMagnitude<0.00001f,"재시작 뒤 모델 원점 복구");Finish();}
        }catch(Exception error){Finish(error);}
    }
}



