using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Cheon.Prototype.MountainPass;
public static class Test_BalanceBoundaries
{
    const BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Instance;
    static PassGameFlow Game=>UnityEngine.Object.FindFirstObjectByType<PassGameFlow>();
    static PassBossPatternSettings Settings=>UnityEngine.Object.FindFirstObjectByType<PassBossPatternSettings>();
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static void Set(PassBossPatternController boss,string field,object value)=>typeof(PassBossPatternController).GetField(field,Flags).SetValue(boss,value);
    static object Get(PassBossPatternController boss,string field)=>typeof(PassBossPatternController).GetField(field,Flags).GetValue(boss);
    static async Task Until(Func<bool> condition,float timeout=40)
    {
        double deadline=EditorApplication.timeSinceStartup+timeout;
        while(!condition()){if(EditorApplication.timeSinceStartup>deadline)throw new Exception("State timed out");await Task.Delay(10);}
    }
    public static async Task<object> CancelAndRefill()
    {
        var boss=(PassBossPatternController)Game.Boss;var settings=Settings;
        await Until(()=>boss.LinkedAttack.IsActive);
        var before=settings.Enemies.Where(enemy=>enemy.IsMinion&&enemy.IsAlive).ToArray();Check(before.Length==2,"Two participants required");
        int cancelled=boss.LinkedAttack.CancelledCount;var survivor=before[1];int survivorId=survivor.GetInstanceID();
        before[0].Health.Kill();float diedAt=Time.time;
        await Until(()=>boss.LinkedAttack.CancelledCount>cancelled&&!boss.LinkedAttack.IsActive,5);
        Check(Get(survivor,"reservedBy")==null&&survivor.Body.enabled,"Cancellation must release the surviving participant");
        Check(boss.LivingMinions==1,"Exactly one minion should survive");
        Check(boss.SummonReadyAt>=diedAt+settings.summon.replenishCooldown-.1f,"Refill cooldown must start on the death");
        await Until(()=>boss.LivingMinions==2,40);
        var replacement=settings.Enemies.First(enemy=>enemy.IsMinion&&enemy.IsAlive&&enemy.GetInstanceID()!=survivorId);
        await Until(()=>replacement.TeleportVfx!=null&&replacement.TeleportVfx.SpawnSmokeBursts==1,3);
        Check(settings.Enemies.Any(enemy=>enemy.GetInstanceID()==survivorId&&enemy.IsAlive),"Refill must preserve the surviving minion");
        Check(replacement.TeleportVfx.SmokeParticleCount>0,"Replacement spawn must emit white smoke");
        var result=new{cancelledOnParticipantDeath=true,reservationReleased=true,onlyMissingMinionReplaced=true,spawnSmoke=true,
            refillDelay=Time.time-diedAt,minions=boss.LivingMinions,cancellationReason=boss.LinkedAttack.LastCancellationReason};
        File.WriteAllText("Assets/Temp/BossPatternBalance~/CancelAndRefill.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));return result;
    }
    public static async Task<object> TimeoutAndTurning()
    {
        var boss=(PassBossPatternController)Game.Boss;var settings=Settings;
        await Until(()=>!boss.LinkedAttack.IsActive&&boss.Phase==PassBossPatternController.PatternPhase.Idle&&Time.time>=boss.LinkedAttack.ReadyAt,45);
        boss.enabled=false;float timeout=settings.linkedAttack.preparationTimeout;float fall=(float)Get(boss,"fallReadyAt");
        bool released=false;string reason="";
        try
        {
            // 준비 완료가 불가능한 조건을 짧게 만들어 시간 초과 해제 경로만 검증한다.
            settings.linkedAttack.preparationTimeout=.05f;Set(boss,"fallReadyAt",Time.time+60f);
            int cancellations=boss.LinkedAttack.CancelledCount;
            Check(boss.LinkedAttack.TryBegin(),"Busy minions must be reservable");boss.LinkedAttack.Tick(.1f);
            Check(!boss.LinkedAttack.IsActive&&boss.LinkedAttack.CancelledCount==cancellations+1,"Preparation timeout must cancel");
            released=settings.Enemies.Where(enemy=>enemy.IsMinion&&enemy.IsAlive).All(enemy=>Get(enemy,"reservedBy")==null);
            Check(released,"Timeout must release all reservations");reason=boss.LinkedAttack.LastCancellationReason;
        }
        finally{settings.linkedAttack.preparationTimeout=timeout;Set(boss,"fallReadyAt",fall);boss.enabled=true;}
        var movement=Game.Player.GetComponent<PassPlayerMovement>();Vector3 candidate=boss.transform.position-boss.transform.forward*6f;
        var hit=Physics.RaycastAll(candidate+Vector3.up*8f,Vector3.down,20f,~0,QueryTriggerInteraction.Ignore)
            .Where(value=>value.normal.y>.65f&&value.collider.GetComponentInParent<PassHealth>()==null).OrderBy(value=>value.distance).First();
        movement.Teleport(hit.point,Quaternion.LookRotation(boss.transform.forward));
        Set(boss,"openingReadyAt",0f);Set(boss,"turnReadyAt",Time.time+100f);Set(boss,"jumpReadyAt",0f);Set(boss,"fallReadyAt",0f);Set(boss,"crackReadyAt",0f);
        typeof(PassBossPatternController).GetMethod("ChoosePattern",Flags).Invoke(boss,null);
        Check(boss.Phase==PassBossPatternController.PatternPhase.JumpWindup||boss.Phase==PassBossPatternController.PatternPhase.FallWindup||boss.Phase==PassBossPatternController.PatternPhase.CrackWindup,"Turn cooldown must not block an available attack");
        var result=new{preparationTimeoutCancelled=true,allReservationsReleased=released,reason,turnCooldownDoesNotBlockAttack=true,selected=boss.Phase.ToString()};
        File.WriteAllText("Assets/Temp/BossPatternBalance~/TimeoutAndTurning.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));return result;
    }
}
