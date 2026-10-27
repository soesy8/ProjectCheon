using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Cheon.Prototype.MountainPass;

public static class Test_BossNaturalPatterns
{
    const string ReportPath="Assets/Temp/BossPatternAudit~/NaturalPatterns.json";
    public static object Main()
    {
        if(!EditorApplication.isPlaying)throw new Exception("Play mode required");
        var game=UnityEngine.Object.FindFirstObjectByType<PassGameFlow>();
        var settings=UnityEngine.Object.FindFirstObjectByType<PassBossPatternSettings>();
        var boss=(PassBossPatternController)game.Boss;
        game.BeginBossChallenge();game.Entrance.SkipPresentation();
        game.PlayerHealth.Invulnerable=true;game.Player.GetComponent<PassPlayerMovement>().InputEnabled=false;
        var canJoin=typeof(PassBossPatternController).GetProperty("CanJoinLinkedAttack",BindingFlags.NonPublic|BindingFlags.Instance);
        var phases=new Dictionary<int,string>();
        var events=new List<object>();
        var reasons=new Dictionary<string,int>();
        float began=Time.time,lastSave=began;int lastFrame=-1,linkedStarts=0,linkedCancellations=0;
        bool linkedBefore=false,done=false;int completedBefore=0;
        var report=new Dictionary<string,object>{{"scene",boss.gameObject.scene.path},{"mode","Natural AI, no forced pattern calls"},{"settingsJson",JsonUtility.ToJson(settings)}};
        Action<string> count=reason=>{if(!reasons.ContainsKey(reason))reasons[reason]=0;reasons[reason]++;};
        Action save=()=>{
            report["elapsed"]=Time.time-began;report["finished"]=done;report["bossJumps"]=boss.JumpsCompleted;report["bossFalls"]=boss.FallsCompleted;
            report["bossCracks"]=boss.CracksCompleted;report["summons"]=boss.SummonsCompleted;report["livingMinions"]=boss.LivingMinions;
            report["linkedStarts"]=linkedStarts;report["linkedCompleted"]=boss.LinkedAttack.CompletedCount;report["linkedCancellations"]=linkedCancellations;
            report["idleOpportunitySamples"]=reasons;report["events"]=events;
            // 기록 대상은 문자열·숫자·리스트뿐이며 Unity 객체를 일반 JSON에 넘기지 않는다.
            File.WriteAllText(ReportPath,Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
        };
        EditorApplication.CallbackFunction tick=null;
        tick=()=>{
            try
            {
                if(!EditorApplication.isPlaying){EditorApplication.update-=tick;return;}
                if(Time.frameCount==lastFrame)return;lastFrame=Time.frameCount;
                float elapsed=Time.time-began;
                foreach(var enemy in settings.Enemies.Where(enemy=>enemy!=null))
                {
                    string phase=enemy.Phase.ToString();int id=enemy.GetInstanceID();
                    if(!phases.TryGetValue(id,out var previous)||previous!=phase)
                    {
                        events.Add(new{at=elapsed,actor=enemy.IsMinion?enemy.name:"Boss",phase,
                            jumpCooldown=enemy.JumpReadyAt-Time.time,fallCooldown=enemy.FallReadyAt-Time.time});phases[id]=phase;
                    }
                }
                bool linked=boss.LinkedAttack.IsActive;
                if(linked&&!linkedBefore)linkedStarts++;
                if(!linked&&linkedBefore&&boss.LinkedAttack.CompletedCount==completedBefore)linkedCancellations++;
                linkedBefore=linked;completedBefore=boss.LinkedAttack.CompletedCount;
                if(boss.Phase==PassBossPatternController.PatternPhase.Idle&&!linked)
                {
                    var minions=settings.Enemies.Where(enemy=>enemy!=null&&enemy.IsMinion&&enemy.IsAlive).ToArray();
                    Vector3 direction=Vector3.ProjectOnPlane(game.Player.position-boss.transform.position,Vector3.up);
                    if(minions.Length<settings.linkedAttack.participants)count("not_enough_living_minions");
                    else if(boss.ConsecutiveTurns>=settings.turn.maximumConsecutive)count("forced_normal_attack_after_turns");
                    else if(direction.sqrMagnitude>.001f&&Vector3.Angle(boss.transform.forward,direction)>settings.turn.triggerAngle)count("turning_has_priority");
                    else if(Time.time<boss.LinkedAttack.ReadyAt)count("linked_cooldown");
                    else if(Time.time<boss.FallReadyAt)count("boss_fall_cooldown");
                    else
                    {
                        int idle=minions.Count(enemy=>enemy.Phase==PassBossPatternController.PatternPhase.Idle);
                        int eligible=minions.Count(enemy=>(bool)canJoin.GetValue(enemy));
                        count("boss_ready_idle_minions_"+idle+"_eligible_"+eligible);
                    }
                }
                if(Time.time-lastSave>=4f){lastSave=Time.time;save();}
                if(elapsed>=120f||!game.IsRunning){done=true;EditorApplication.update-=tick;save();}
            }
            catch(Exception exception)
            {report["error"]=exception.ToString();done=true;EditorApplication.update-=tick;save();}
        };
        EditorApplication.update+=tick;save();return new{path=ReportPath,durationSeconds=120,forcedPatternCalls=false};
    }
}
