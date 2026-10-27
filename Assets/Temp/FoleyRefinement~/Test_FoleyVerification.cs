using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cheon.Prototype.MountainPass;
using Cheon.Tests.CombatPresentation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

public static class Test_FoleyVerification
{
    static async Task Until(Func<bool> check, float limit=8)
    {
        float end = Time.realtimeSinceStartup + limit;
        while (!check()) { if (Time.realtimeSinceStartup > end) throw new Exception("실행 조건 시간 초과"); await Task.Delay(1); }
    }
    static async Task Seconds(float duration)
    {
        float end=Time.realtimeSinceStartup+duration;
        await Until(() => Time.realtimeSinceStartup >= end, duration+2);
    }
    static float Peak(AudioSource source)
    {
        float[] samples=new float[1024]; source.GetOutputData(samples,0); return samples.Max(Mathf.Abs);
    }
    public static async Task<object> Run()
    {
        if (!Application.isPlaying) throw new Exception("Play Mode required");
        var result = new Dictionary<string,object>();
        var originalKeyboard=Keyboard.current;
        var keyboard=InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent();
        try
        {
            var flow=Object.FindFirstObjectByType<PassGameFlow>();
            var player=Object.FindFirstObjectByType<PassPlayerMovement>();
            var foley=player.GetComponent<Test_PlayerFoley>();
            var anim=player.AnimationControl;
            var sources=flow.Audio.GetComponents<AudioSource>();
            var pause=Object.FindFirstObjectByType<Test_PausePresentation>();
            result["originalAudioDisabled"]=!player.GetComponent<PassPlayerAudio>().enabled;
            int frame=Time.frameCount;
            flow.BeginJourney(); await Until(()=>flow.IsRunning);
            flow.PlayerHealth.Invulnerable=true;
            UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
            var floor=Object.FindObjectsByType<Collider>(FindObjectsSortMode.None).First(c=>c.name=="Floor");
            if (!floor.Raycast(new Ray(floor.bounds.center+Vector3.up*5,Vector3.down),out var stoneHit,10)) throw new Exception("Stone floor ray missed");
            player.Teleport(stoneHit.point+Vector3.up*.05f,Quaternion.identity);
            flow.View.LockBoss(false); flow.View.SnapBehind();
            await Seconds(.25f);
            result["stoneWalk"]=await Walk(keyboard, player,foley,sources[1],false,Test_FootstepSurface.GroundKind.Stone);
            result["stoneClip"]=foley.LastStepClip != null ? foley.LastStepClip.name : "none";
            flow.BeginBossChallenge(); await Until(()=>!flow.IsTransitioning);
            flow.Entrance.SkipPresentation(); await Until(()=>flow.IsRunning);
            var boss=(PassBossPatternController)flow.Boss; boss.enabled=false;
            flow.PlayerHealth.Invulnerable=true;
            Vector3 desired=boss.transform.position+boss.transform.forward*9;
            var ground=Physics.RaycastAll(desired+Vector3.up*4,Vector3.down,10,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)
                .Where(h=>h.normal.y>.65f && h.collider.GetComponentInParent<PassHealth>()==null).OrderBy(h=>h.distance).First();
            player.Teleport(ground.point+Vector3.up*.04f,Quaternion.LookRotation(-boss.transform.right));
            flow.View.LockBoss(false); flow.View.SnapBehind(); await Seconds(.25f);
            result["dirtWalk"]=await Walk(keyboard,player,foley,sources[1],false,Test_FootstepSurface.GroundKind.Dirt);
            result["dirtSprint"]=await Walk(keyboard,player,foley,sources[1],true,Test_FootstepSurface.GroundKind.Dirt);
            result["dirtClip"]=foley.LastStepClip != null ? foley.LastStepClip.name : "none";
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Space));
            await Until(()=>anim.IsDodging);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());
            int rollSteps=foley.PlayedStepCount;
            float rollPeak=0;
            for(int i=0;i<8;i++){await Seconds(.035f);rollPeak=Mathf.Max(rollPeak,Peak(sources[0]));}
            result["rollAudibleWithoutFootsteps"]=rollPeak>.0001f && foley.PlayedStepCount==rollSteps;
            await Until(()=>!anim.IsActionPlaying);
            Vector3 position=boss.transform.position+boss.transform.forward*2.3f+Vector3.up*.04f;
            player.Teleport(position,Quaternion.LookRotation(Vector3.ProjectOnPlane(boss.transform.position-position,Vector3.up)));
            boss.Health.Invulnerable=false;
            if (!anim.IsCombatRequested) anim.TryAttackInput(1,false,false);
            await Until(()=>!anim.IsEquipmentTransition && !anim.IsActionPlaying);
            var combat=player.GetComponent<PassFanAttackCombat>();
            int hits=combat.DirectHitsLanded, swings=foley.PlayedSwingCount;
            float health=boss.Health.Current;
            result["attack2Accepted"]=anim.TryAttackInput(2,false,false);
            await Until(()=>anim.SelectedState=="ATTACK2" && anim.NormalizedTime*anim.GetAttackMaximumFrame(2)>23);
            pause.TogglePauseImmediately();
            await Seconds(.3f);
            result["pauseDelaysPendingSwing"]=foley.PlayedSwingCount==swings && !sources[0].isPlaying;
            pause.TogglePauseImmediately();
            await Until(()=>anim.SelectedState!="ATTACK2" || anim.NormalizedTime*anim.GetAttackMaximumFrame(2)>43);
            result["swingFrame"]=foley.LastSwingFrame;
            result["hitFrame"]=combat.LastHitFrame;
            result["damage"]=health-boss.Health.Current;
            result["swingBeforeContact"]=foley.LastSwingAttack==2 && foley.LastSwingFrame>=27 && foley.LastSwingFrame<combat.LastHitFrame;
            result["singleSwingAndHit"]=foley.PlayedSwingCount==swings+1 && combat.DirectHitsLanded==hits+1 && health>boss.Health.Current;
            result["hitInsideVisualSweep"]=combat.LastHitFrame>=28 && combat.LastHitFrame<=34;
            await Until(()=>!anim.IsActionPlaying);
            // 전환으로 공격이 취소되면 예약된 소리가 뒤늦게 나오지 않아야 한다.
            swings=foley.PlayedSwingCount;
            anim.TryAttackInput(2,false,false); await Seconds(.05f); anim.PlayIdle(); await Seconds(.5f);
            result["cancelClearsPendingSound"]=foley.PlayedSwingCount==swings;
            var levels=new List<float>();
            foreach (Action action in new Action[]{flow.Audio.Slam,flow.Audio.Spirit,()=>flow.Audio.Strike(),()=>flow.Audio.Strike(true)})
            {
                sources[0].Stop();action();float peak=0;
                for(int i=0;i<10;i++){await Seconds(.02f);peak=Mathf.Max(peak,Peak(sources[0]));}
                levels.Add(peak);
            }
            result["newEffectsOutput"]=levels.All(p=>p>.0001f && p<.98f);
            result["effectPeaks"]=levels;
            sources[0].Stop();flow.Audio.Bell();await Seconds(1.4f);
            result["jingStillPlayingHalfway"]=sources[0].isPlaying;
            await Seconds(1.5f);result["jingStopsWithin2_9Seconds"]=!sources[0].isPlaying;
            int cueCount=Object.FindFirstObjectByType<Test_BossAudioCues>().PlayedCueCount;
            boss.enabled=true;
            var cues=Object.FindFirstObjectByType<Test_BossAudioCues>();
            await Until(()=>cues.PlayedCueCount>cueCount,15);
            result["naturalBossCue"]=cues.LastCue.name;
            var cueSource=GameObject.Find("Test_BossWarningAudio").GetComponent<AudioSource>();
            float cuePeak=0;
            for(int i=0;i<10;i++){await Seconds(.02f);cuePeak=Mathf.Max(cuePeak,Peak(cueSource));}
            result["bossCueAudible"]=cuePeak>.0001f;
            pause.TogglePauseImmediately();await Seconds(.1f);
            result["pauseStopsCueAndFoley"]=!cueSource.isPlaying && !sources[0].isPlaying && !sources[1].isPlaying;
            pause.TogglePauseImmediately();
            result["framesAdvance"]=Time.frameCount>frame+100;
        }
        finally
        {
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.RemoveDevice(keyboard);
            if(originalKeyboard!=null)originalKeyboard.MakeCurrent();
            var failures=result.Where(x=>x.Value is bool && !(bool)x.Value).Select(x=>x.Key).ToArray();
            result["failures"]=failures;result["passed"]=failures.Length==0;
            File.WriteAllText("Assets/Temp/FoleyRefinement~/PlayVerification.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
        }
        return result;
    }
    static async Task<bool> Walk(Keyboard keyboard,PassPlayerMovement player,Test_PlayerFoley foley,AudioSource source,bool sprint,Test_FootstepSurface.GroundKind surface)
    {
        Vector3 start=player.transform.position;
        int count=foley.PlayedStepCount;
        bool allCorrect=true,mode=false;float peak=0;
        var clips=new List<string>();int previous=count;
        InputSystem.QueueStateEvent(keyboard,sprint?new KeyboardState(Key.W,Key.LeftShift):new KeyboardState(Key.W));
        float end=Time.realtimeSinceStartup+1.3f;
        while(Time.realtimeSinceStartup<end)
        {
            await Seconds(.02f);mode|=player.IsMoving && player.IsSprinting==sprint;
            peak=Mathf.Max(peak,Peak(source));
            if(foley.PlayedStepCount!=previous)
            {
                allCorrect &= foley.LastSurface==surface;
                if(clips.Count>0)allCorrect &= clips.Last()!=foley.LastStepClip.name;
                clips.Add(foley.LastStepClip.name);previous=foley.PlayedStepCount;
            }
        }
        InputSystem.QueueStateEvent(keyboard,new KeyboardState());await Seconds(.05f);
        bool success=mode && allCorrect && foley.PlayedStepCount-count>=2 && peak>.0001f && Vector3.Distance(start,player.transform.position)>.5f;
        File.AppendAllText("Assets/Temp/FoleyRefinement~/WalkDetails.txt",$"{surface} sprint={sprint} success={success} mode={mode} correct={allCorrect} count={foley.PlayedStepCount-count} peak={peak} distance={Vector3.Distance(start,player.transform.position)} clips={string.Join(",",clips)}\n");
        return success;
    }
}
