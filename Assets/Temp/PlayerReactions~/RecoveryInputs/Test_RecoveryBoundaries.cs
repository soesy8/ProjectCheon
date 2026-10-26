using System;
using System.Collections.Generic;
using System.IO;
using Cheon.Tests.PlayerReactions;
using UnityEngine;
public static class Test_RecoveryBoundaries
{
    private static Test_PlayerReactionPreview p;
    private static readonly List<string> checks=new List<string>();
    private static void Check(bool condition,string text){if(!condition)throw new Exception("FAIL: "+text);checks.Add(text);}
    private static void Step(float seconds){for(float left=seconds;left>0.000001f;){float dt=Mathf.Min(0.01f,left);p.AdvancePlayback(dt);left-=dt;}}
    private static void ToProgress(float progress){float frame=p.Settings.getUp.startFrame+(p.Settings.getUp.endFrame-p.Settings.getUp.startFrame)*progress;Step(Mathf.Max(0f,(frame-p.CurrentFrame)/p.Settings.getUp.FrameRate/p.Settings.getUp.speed));}
    public static object Main()
    {
        p=UnityEngine.Object.FindFirstObjectByType<Test_PlayerReactionPreview>();
        var input=p.GetComponent<Test_PlayerReactionInput>();
        string original=JsonUtility.ToJson(p.Settings); bool wasEnabled=p.enabled;
        p.enabled=false; input.enabled=false; p.repeatSequence=false;
        try{
            Check(p.Settings.heavyHit.MaximumFrame==91 && p.Settings.heavyHit.endFrame==91 && p.Settings.death.endFrame==91,"새 강피격 전체 91프레임 및 사망 클립 갱신");
            foreach(float speed in new[]{0.5f,1f,2f}){
                p.ResetPreview(); p.Settings.heavyHit.speed=speed; p.Play(Test_PlayerReactionPreview.PreviewMode.HeavyThenGetUp);
                Step(26f/30f/speed);Check(!p.CanRecover,"착지 직후 잠김 / 배속 "+speed);
                Step(0.489f);p.SetInput(Vector2.zero,true);p.AdvancePlayback(0f);
                Check(p.Phase==Test_PlayerReactionPreview.PreviewPhase.HeavyHit && !p.CanRecover,"착지 +0.489초 회피 거부 / 배속 "+speed);
                p.SetInput(Vector2.zero,false);Step(0.012f);
                Check(p.Phase==Test_PlayerReactionPreview.PreviewPhase.HeavyHit && p.CanRecover,"실제 +0.501초 해금 및 이른 회피 예약 없음 / 배속 "+speed);
                p.SetInput(Vector2.zero,true);p.AdvancePlayback(0f);
                Check(p.Phase==Test_PlayerReactionPreview.PreviewPhase.GetUp && p.AnimatorState=="GET_UP","해금 후 회피 입력은 일어나기로 연결 / 배속 "+speed);
            }
            p.Settings.heavyHit.speed=1f;p.ResetPreview();p.Play(Test_PlayerReactionPreview.PreviewMode.HeavyThenGetUp);
            Step(26f/30f+0.49f);p.SetInput(Vector2.up,false);p.AdvancePlayback(0f);Check(p.Phase==Test_PlayerReactionPreview.PreviewPhase.HeavyHit,"0.5초 전 이동 차단");
            Step(0.011f);Check(p.Phase==Test_PlayerReactionPreview.PreviewPhase.GetUp,"이동을 유지하면 0.5초 이후 일어나기");
            foreach(bool trimmed in new[]{false,true}){
                p.ResetPreview();p.Settings.getUp.startFrame=trimmed?10:0;p.Settings.getUp.endFrame=trimmed?50:58;p.Settings.getUp.speed=trimmed?1.6f:1f;
                p.Play(Test_PlayerReactionPreview.PreviewMode.GetUpOnly);ToProgress(0.699f);p.SetInput(Vector2.zero,true);p.AdvancePlayback(0f);
                Check(p.Phase==Test_PlayerReactionPreview.PreviewPhase.GetUp && !p.CanDodge,"69.9% 회피 차단 / 트림 "+trimmed);
                ToProgress(0.701f);Check(p.Phase==Test_PlayerReactionPreview.PreviewPhase.GetUp && p.CanDodge,"70.1% 회피 해금 / 트림 "+trimmed);
                p.SetInput(Vector2.zero,true);p.AdvancePlayback(0f);Check(p.Phase==Test_PlayerReactionPreview.PreviewPhase.Dodging && p.AnimatorState=="DODGE","70% 이후 구르기로 실제 상태 전환 / 트림 "+trimmed);
                p.ResetPreview();p.Play(Test_PlayerReactionPreview.PreviewMode.GetUpOnly);p.SetInput(Vector2.up,false);ToProgress(0.899f);
                Check(p.Phase==Test_PlayerReactionPreview.PreviewPhase.GetUp && !p.CanMove,"89.9% 이동 차단 / 트림 "+trimmed);
                ToProgress(0.901f);Check(p.Phase==Test_PlayerReactionPreview.PreviewPhase.Moving && p.AnimatorState=="IDLE","90% 이후 이동 블렌드트리 진입 / 트림 "+trimmed);
            }
            JsonUtility.FromJsonOverwrite(original,p.Settings);p.ResetPreview();p.Play(Test_PlayerReactionPreview.PreviewMode.GetUpOnly);ToProgress(0.899f);
            Check(p.AnimatorState=="GET_UP","무입력 89.9%까지 일어나기 유지");ToProgress(0.901f);
            Check(p.AnimatorState=="IDLE" && p.IsBlending,"무입력 90%부터 대기로 블렌딩 시작");Step(0.3f);Check(p.IsBlending,"대기 전환 0.3초 후에도 부드러운 혼합 유지");Step(0.3f);Check(!p.IsBlending,"0.55초 대기 전환 완료");
            p.ResetPreview();p.Play(Test_PlayerReactionPreview.PreviewMode.Death);p.SetInput(Vector2.up,true);Step(4f);
            Check(p.Phase==Test_PlayerReactionPreview.PreviewPhase.Dead && p.AnimatorState=="DEATH" && !p.CanDodge && !p.CanMove && !p.CanRecover,"사망은 이동·회피 캔슬과 자동 회복 차단");
            p.Settings.getUpHeadBowDegrees=0f;p.Scrub(1,25f);var a=p.CharacterAnimator;var head=a.GetBoneTransform(HumanBodyBones.Head);var neck=a.GetBoneTransform(HumanBodyBones.Neck);Quaternion rawHead=head.rotation,rawNeck=neck.rotation;
            p.Settings.getUpHeadBowDegrees=10f;p.Scrub(1,25f);float headAngle=Quaternion.Angle(rawHead,head.rotation),neckAngle=Quaternion.Angle(rawNeck,neck.rotation);
            Check(Mathf.Abs(headAngle-10f)<0.05f && Mathf.Abs(neckAngle-2.5f)<0.05f,"머리 10도 숙임을 목 2.5도·머리 7.5도로 분산");
            Quaternion corrected=head.rotation;for(int i=0;i<100;i++)p.AdvancePlayback(0f);Check(Quaternion.Angle(corrected,head.rotation)<0.05f,"일시정지 100회 평가에도 머리 보정 누적 없음");
            var result=new {status="passed",checks,headAngle,neckAngle};string json=Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented);File.WriteAllText("Assets/Temp/PlayerReactions~/RecoveryInputs/Test_BoundariesReport.json",json);return result;
        }finally{JsonUtility.FromJsonOverwrite(original,p.Settings);p.ResetPreview();p.enabled=wasEnabled;input.enabled=true;}
    }
}
