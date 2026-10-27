using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Cheon.Prototype.MountainPass;
public static class Test_BalancedNaturalPatterns
{
    const string Path="Assets/Temp/BossPatternBalance~/NaturalPatterns.json";
    public static object Main()
    {
        if(!EditorApplication.isPlaying)throw new Exception("Play mode required");
        var game=UnityEngine.Object.FindFirstObjectByType<PassGameFlow>();var boss=(PassBossPatternController)game.Boss;
        var settings=UnityEngine.Object.FindFirstObjectByType<PassBossPatternSettings>();
        game.BeginBossChallenge();game.Entrance.SkipPresentation();game.PlayerHealth.Invulnerable=true;game.Player.GetComponent<PassPlayerMovement>().InputEnabled=false;
        var births=new Dictionary<int,float>();var smokeObserved=new HashSet<int>();var normalKinds=new HashSet<string>();var events=new List<object>();var intervals=new List<int>();
        float began=Time.time,lastSave=began;int lastFrame=-1,starts=0,busyStarts=0,peakMinions=0,streak=0,maxStreak=0;
        bool linkedBefore=false,pausedForCapture=false,captured=false,finished=false;string lastPhase="",lastNormal="";
        var report=new Dictionary<string,object>{{"forcedPatternCalls",false},{"settingsJson",JsonUtility.ToJson(settings)}};
        Action save=()=>{
            report["elapsed"]=Time.time-began;report["finished"]=finished;report["pausedForCapture"]=pausedForCapture;
            report["normalCompleted"]=boss.NormalAttacksCompleted;report["normalKinds"]=normalKinds.ToArray();report["maxNormalStreak"]=maxStreak;
            report["jumps"]=boss.JumpsCompleted;report["falls"]=boss.FallsCompleted;report["cracks"]=boss.CracksCompleted;report["summons"]=boss.SummonsCompleted;
            report["linkedStarts"]=starts;report["linkedCompleted"]=boss.LinkedAttack.CompletedCount;report["busyReservationStarts"]=busyStarts;
            report["cancelled"]=boss.LinkedAttack.CancelledCount;report["cancelReason"]=boss.LinkedAttack.LastCancellationReason;
            report["normalIntervals"]=intervals;report["peakMinions"]=peakMinions;report["spawnSmokeActors"]=smokeObserved.Count;report["events"]=events;
            File.WriteAllText(Path,Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
        };
        EditorApplication.CallbackFunction tick=null;
        tick=()=>{
            try
            {
                if(!EditorApplication.isPlaying){EditorApplication.update-=tick;return;}
                if(Time.frameCount==lastFrame)return;lastFrame=Time.frameCount;
                if(pausedForCapture){if(Time.timeScale<=0)return;pausedForCapture=false;}
                float elapsed=Time.time-began;var minions=settings.Enemies.Where(enemy=>enemy!=null&&enemy.IsMinion&&enemy.IsAlive).ToArray();peakMinions=Mathf.Max(peakMinions,minions.Length);
                foreach(var minion in minions)
                {
                    int id=minion.GetInstanceID();if(!births.ContainsKey(id))births[id]=Time.time;
                    if(minion.TeleportVfx.SpawnSmokeBursts>0&&minion.TeleportVfx.SmokeParticleCount>0)smokeObserved.Add(id);
                    if(!captured&&smokeObserved.Count>=2&&Time.time-births[id]>=.18f)
                    {captured=true;pausedForCapture=true;Time.timeScale=0;save();return;}
                }
                bool linked=boss.LinkedAttack.IsActive;
                if(linked&&!linkedBefore)
                {
                    starts++;intervals.Add(boss.NormalAttacksSinceLinked);if(minions.Any(enemy=>enemy.Phase!=PassBossPatternController.PatternPhase.Idle))busyStarts++;
                    events.Add(new{at=elapsed,kind="LinkedStart",normalSincePrevious=boss.NormalAttacksSinceLinked,participants=minions.Select(enemy=>enemy.Phase.ToString()).ToArray()});
                }
                linkedBefore=linked;string phase=boss.Phase.ToString();
                if(phase!=lastPhase)
                {
                    if(!linked&&(phase=="JumpWindup"||phase=="FallWindup"||phase=="CrackWindup"))
                    {normalKinds.Add(phase);streak=lastNormal==phase?streak+1:1;maxStreak=Mathf.Max(maxStreak,streak);lastNormal=phase;}
                    events.Add(new{at=elapsed,kind=phase,normal=boss.NormalAttacksCompleted});lastPhase=phase;
                }
                if(Time.time-lastSave>=4){lastSave=Time.time;save();}
                if(elapsed>=120)
                {
                    if(boss.LinkedAttack.CompletedCount<2)throw new Exception("Natural cooperative attacks did not repeat");
                    if(normalKinds.Count!=3)throw new Exception("A normal attack was starved");
                    if(smokeObserved.Count<2||peakMinions>settings.summon.maximumAlive)throw new Exception("Spawn VFX or population failed");
                    if(maxStreak>settings.selection.maximumConsecutive)throw new Exception("Repeated normal attack limit failed");
                    finished=true;EditorApplication.update-=tick;save();
                }
                else if(!game.IsRunning)throw new Exception("Encounter stopped");
            }
            catch(Exception exception){report["error"]=exception.ToString();finished=true;Time.timeScale=1;EditorApplication.update-=tick;save();}
        };
        EditorApplication.update+=tick;save();return new{path=Path,durationSeconds=120,pauseForSpawnCapture=true};
    }
}
