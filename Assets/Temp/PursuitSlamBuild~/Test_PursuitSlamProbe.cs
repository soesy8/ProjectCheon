using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Cheon.Prototype.MountainPass;
using UnityEngine;

public static class Test_PursuitSlamProbe
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static PassBossPatternController boss;
    private static PassBossPursuitSlam slam;
    private static PassPlayerMovement player;
    private static PassHealth health;
    private static PassBossPatternSettings settings;
    private static readonly List<float> damage = new List<float>();
    private static readonly List<bool> heavy = new List<bool>();

    public static async Task<object> Run()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Play mode required.");
        int firstFrame = Time.frameCount;
        await Task.Delay(150);
        Require(Time.frameCount > firstFrame, "Player loop must advance.");
        settings = UnityEngine.Object.FindFirstObjectByType<PassBossPatternSettings>();
        boss = settings.MainBoss; slam = boss.PursuitSlam;
        player = UnityEngine.Object.FindFirstObjectByType<PassPlayerMovement>();
        health = player.GetComponent<PassHealth>();
        var flow = UnityEngine.Object.FindFirstObjectByType<PassGameFlow>();
        flow.BeginJourney(); typeof(PassGameFlow).GetProperty("InEncounter").SetValue(flow, true);
        boss.WakeUp(); SetBossField("openingReadyAt", float.PositiveInfinity);
        player.InputEnabled = false; health.Configure(1000f, 0f); health.Restore();
        health.Damaged += OnDamage;
        try
        {
            PlacePlayer(Ground(settings.ArenaCenter + boss.transform.right * 3f + boss.transform.forward * 2f));
            InvokeBoss("BeginPursuitSlam"); float begun = Time.time;
            float jump1 = await Stage(PassBossPursuitSlam.SlamStage.TrackingFlight);
            Vector3 lock1 = slam.LandingPosition;
            PlacePlayer(Ground(lock1 + boss.transform.right * 2f));
            await Task.Delay(150);
            Require(Vector3.Distance(lock1, slam.LandingPosition) < .001f, "First target moved during flight.");
            float land1 = await Stage(PassBossPursuitSlam.SlamStage.TrackingRecovery);
            Require(damage.Count == 1 && !heavy[0], "First landing must hit once with a normal reaction.");
            PlacePlayer(Ground(lock1 + boss.transform.right * 3f + boss.transform.forward * 2f));
            float jump2 = await Stage(PassBossPursuitSlam.SlamStage.TrackingFlight);
            Vector3 lock2 = slam.LandingPosition;
            Require(Vector3.Distance(lock2, player.transform.position) < .05f, "Second jump must lock the latest player position.");
            PlacePlayer(Ground(lock2 + boss.transform.right * 2f));
            float land2 = await Stage(PassBossPursuitSlam.SlamStage.TrackingRecovery);
            Require(damage.Count == 2 && !heavy[1] && slam.TrackingLandings == 2, "Second landing must be normal.");
            float rise = await Stage(PassBossPursuitSlam.SlamStage.HeavyRise);
            Vector3 heavyTarget = slam.LandingPosition;
            // 회전하는 몸체의 옆으로 피하되 충격파 반경 안에는 남는다.
            PlacePlayer(Ground(heavyTarget + boss.transform.right * 3.5f));
            float fall = await Stage(PassBossPursuitSlam.SlamStage.HeavyFall);
            Require(Vector3.Distance(heavyTarget, slam.LandingPosition) < .001f, "Heavy target moved after takeoff.");
            float landed = await Stage(PassBossPursuitSlam.SlamStage.Recovery);
            Require(damage.Count == 3 && heavy[2] && !slam.FinalHitWasBody, "Shockwave must be one heavy reaction.");
            Near(damage[0], 20f, "Tracking damage 1", .01f);
            Near(damage[1], 20f, "Tracking damage 2", .01f);
            Near(damage[2], 28f, "Shockwave damage", .01f);
            Require(Vector3.Dot(boss.Body.transform.forward, Vector3.up) > .99f, "The back must face the ground.");
            Require(boss.Body.bounds.min.y >= slam.LandingPosition.y - .03f, "The body penetrated the ground.");
            float finished = await Wait(() => !slam.IsActive);
            Require(damage.Count == 3, "Recovery dealt extra damage.");

            Near(jump1 - begun, 1f, "Windup"); Near(land1 - jump1, 1f, "Tracking flight 1");
            Near(jump2 - land1, .65f, "Landing pause 1"); Near(land2 - jump2, 1f, "Tracking flight 2");
            Near(rise - land2, .65f, "Landing pause 2"); Near(fall - rise, 1.5f, "Heavy rise");
            Near(landed - fall, .75f, "Heavy fall"); Near(finished - landed, 7f, "Recovery");

            damage.Clear(); heavy.Clear();
            ForceHeavyFall(settings.ArenaCenter + boss.transform.right * 5f, 0f);
            await Stage(PassBossPursuitSlam.SlamStage.Recovery);
            await Task.Delay(100);
            Require(damage.Count == 1 && heavy[0] && slam.FinalHitWasBody, "Body contact must hit once with heavy reaction.");
            Near(damage[0], 42f, "Body multiplier", .01f);
            float bodyDamage = damage[0];

            damage.Clear(); heavy.Clear();
            ForceHeavyFall(settings.ArenaCenter, 7f);
            await Stage(PassBossPursuitSlam.SlamStage.Recovery);
            Require(damage.Count == 0, "Outside the 6m shockwave should not be hit.");

            boss.ResetEncounter(); boss.WakeUp();
            var history = (float[])typeof(PassBossPatternController).GetField("lastNormalUse", PrivateInstance).GetValue(boss);
            for (int index = 0; index < history.Length - 1; index++) history[index] = Time.time;
            SetBossField("openingReadyAt", 0f); health.Invulnerable = true;
            PlacePlayer(Ground(settings.ArenaCenter + boss.transform.right * 4f));
            await Wait(() => boss.Phase == PassBossPatternController.PatternPhase.PursuitSlam);
            bool automaticSelection = slam.IsActive;
            boss.ResetEncounter();
            Require(!slam.IsActive && boss.Phase == PassBossPatternController.PatternPhase.Dormant, "Retry did not clear the new pattern.");
            return new
            {
                passed = true, framesAdvanced = Time.frameCount - firstFrame,
                windup = jump1 - begun, trackingFlight1 = land1 - jump1, pause1 = jump2 - land1,
                trackingFlight2 = land2 - jump2, pause2 = rise - land2,
                heavyRise = fall - rise, heavyFall = landed - fall, recovery = finished - landed,
                trackingDamage = 20, shockwaveDamage = 28, bodyDamage,
                targetLockPassed = true, automaticSelection, resetPassed = true
            };
        }
        finally { health.Damaged -= OnDamage; }
    }

    private static void ForceHeavyFall(Vector3 desired, float lateralDistance)
    {
        Vector3 target = Ground(desired);
        SetBossField("openingReadyAt", float.PositiveInfinity);
        typeof(PassBossPatternController).GetMethod("SetPhase", PrivateInstance).Invoke(boss,
            new object[] { PassBossPatternController.PatternPhase.PursuitSlam });
        typeof(PassBossPursuitSlam).GetProperty("LandingPosition").SetValue(slam, target);
        typeof(PassBossPursuitSlam).GetField("finalHitPlayer", PrivateInstance).SetValue(slam, false);
        typeof(PassBossPursuitSlam).GetProperty("FinalHitWasBody").SetValue(slam, false);
        boss.transform.position = target + Vector3.up * settings.pursuitSlam.heavyHeight;
        typeof(PassBossPursuitSlam).GetMethod("SetPose", PrivateInstance).Invoke(slam, new object[] { 0f });
        typeof(PassBossPursuitSlam).GetMethod("SetStage", PrivateInstance).Invoke(slam,
            new object[] { PassBossPursuitSlam.SlamStage.HeavyFall });
        health.Restore(); PlacePlayer(Ground(target + boss.transform.right * lateralDistance));
    }

    private static void OnDamage(float amount) { damage.Add(amount); heavy.Add(health.LastDamageWasHeavy); }
    private static void InvokeBoss(string method) => typeof(PassBossPatternController).GetMethod(method, PrivateInstance).Invoke(boss, null);
    private static void SetBossField(string name, object value) => typeof(PassBossPatternController).GetField(name, PrivateInstance).SetValue(boss, value);
    private static void PlacePlayer(Vector3 position) { player.Teleport(position, Quaternion.identity); Physics.SyncTransforms(); }
    private static Vector3 Ground(Vector3 point)
    {
        return Physics.RaycastAll(new Vector3(point.x, settings.ArenaCenter.y + 3f, point.z), Vector3.down, 8f)
            .Where(hit => hit.normal.y > .65f && hit.collider.GetComponentInParent<PassHealth>() == null)
            .OrderBy(hit => hit.distance).First().point;
    }
    private static Task<float> Stage(PassBossPursuitSlam.SlamStage stage) => Wait(() => slam.Stage == stage);
    private static async Task<float> Wait(Func<bool> condition)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (!condition())
        {
            if (!Application.isPlaying || watch.Elapsed.TotalSeconds > 15) throw new InvalidOperationException("Phase timeout: " + slam.Stage);
            await Task.Delay(5);
        }
        return Time.time;
    }
    private static void Require(bool passed, string message) { if (!passed) throw new InvalidOperationException(message); }
    private static void Near(float actual, float expected, string label, float tolerance = .12f)
    { Require(Mathf.Abs(actual - expected) <= tolerance, label + ": " + actual + " != " + expected); }
}
