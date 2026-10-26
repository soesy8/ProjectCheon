using System;
using System.IO;
using System.Threading.Tasks;
using Cheon.Prototype.MountainPass;
using UnityEngine;
[DefaultExecutionOrder(150)]
public class Test_MainRenderedPose : MonoBehaviour
{
    private TaskCompletionSource<object> done=new TaskCompletionSource<object>();
    private PassGameFlow game;private PassAnimationController control;private PassPlayerMovement player;
    private float started,idleStarted=-1f;private int stage;private bool down;private float idleHip,idleHead,idleProgress;
    public static Task<object> Main(){var r=new GameObject("Test_MainRenderedPose").AddComponent<Test_MainRenderedPose>();r.game=UnityEngine.Object.FindFirstObjectByType<PassGameFlow>();r.player=r.game.Player.GetComponent<PassPlayerMovement>();r.control=r.player.AnimationControl;r.game.BeginJourney();r.game.Boss.enabled=false;r.game.PlayerHealth.Configure(10000f,0f);r.game.PlayerHealth.Restore();var arena=(Vector3)typeof(PassGameFlow).GetField("arenaCheckpoint",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(r.game);r.player.Teleport(arena,Quaternion.identity);r.game.View.SnapBehind();r.player.InputEnabled=false;r.started=Time.realtimeSinceStartup;r.game.PlayerHealth.TakeDamage(1f,true);return r.done.Task;}
    private void LateUpdate(){try{
        if(Time.realtimeSinceStartup-started>15f)throw new Exception("Rendered test timeout "+control.SelectedState);
        if(stage==0){
            if(control.SelectedState=="HEAVY_HIT" && control.ReactionFrame>=91f){down=true;}
            if(control.SelectedState=="SHEATHED_IDLE"){
                if(idleStarted<0f)idleStarted=Time.realtimeSinceStartup;
                if(Time.realtimeSinceStartup-idleStarted>0.7f && !control.CharacterAnimator.IsInTransition(0)){
                    idleHip=control.CharacterAnimator.GetBoneTransform(HumanBodyBones.Hips).position.y-player.transform.position.y;
                    idleHead=control.CharacterAnimator.GetBoneTransform(HumanBodyBones.Head).position.y-player.transform.position.y;
                    idleProgress=control.CharacterAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                    if(!down||idleHip<0.87f||idleHead<1.48f||idleProgress<=0f)throw new Exception("Idle skin pose invalid "+idleHip+" / "+idleHead);
                    game.PlayerHealth.TakeDamage(1f,true);stage=1;
                }
            }
        }
        else if(stage==1 && control.CanRecoverFromHeavy){control.TryRecoverFromHeavy(true,false);stage=2;}
        else if(stage==2 && control.SelectedState=="GET_UP" && control.ReactionFrame>=25f){control.TogglePause();stage=3;idleStarted=Time.realtimeSinceStartup;}
        else if(stage==3 && Time.realtimeSinceStartup-idleStarted>0.1f){
            var pose=control.GetComponent<PassPlayerReactionPose>();var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var neck=control.CharacterAnimator.GetBoneTransform(HumanBodyBones.Neck);var head=control.CharacterAnimator.GetBoneTransform(HumanBodyBones.Head);
            var rawNeck=(Quaternion)typeof(PassPlayerReactionPose).GetField("neckBeforeBow",flags).GetValue(pose);var rawHead=(Quaternion)typeof(PassPlayerReactionPose).GetField("headBeforeBow",flags).GetValue(pose);
            float bow=Quaternion.Angle(neck.parent.rotation*rawNeck*rawHead,head.rotation);
            if(Mathf.Abs(bow-10f)>0.1f)throw new Exception("Rendered head bow incorrect "+bow);
            var sampler=(GameObject)typeof(PassPlayerReactionPose).GetField("samplingRoot",flags).GetValue(pose);
            var result=new{status="passed",fullHeavyClip=down,idleHip,idleHead,idleProgress,headBowDegrees=bow,pausedGetUpFrame=control.ReactionFrame,samplerColliders=sampler.GetComponentsInChildren<Collider>(true).Length};
            File.WriteAllText("Assets/Temp/PlayerReactions~/MainIntegration/Test_RenderedPoseReport.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
            done.TrySetResult(result);Destroy(gameObject);
        }
    }catch(Exception error){done.TrySetException(error);Destroy(gameObject);}}
}
