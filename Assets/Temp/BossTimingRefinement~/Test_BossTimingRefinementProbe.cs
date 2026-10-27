using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Cheon.Prototype.MountainPass;
using UnityEngine;

public static class Test_BossTimingRefinementProbe
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    public static async Task<object> Run()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Play mode is required.");
        int initialFrame = Time.frameCount;
        await Task.Delay(100);
        if (Time.frameCount <= initialFrame) throw new InvalidOperationException("Player loop is not advancing.");

        var flow = UnityEngine.Object.FindFirstObjectByType<PassGameFlow>();
        var settings = UnityEngine.Object.FindFirstObjectByType<PassBossPatternSettings>();
        var boss = settings.MainBoss;
        var player = UnityEngine.Object.FindFirstObjectByType<PassPlayerMovement>();
        flow.BeginJourney();
        // 등장 연출과 자동 패턴 선택을 제외하고 실제 공격·자세 전환 시간을 확인한다.
        typeof(PassGameFlow).GetProperty("InEncounter").SetValue(flow, true);
        player.InputEnabled = false;
        player.GetComponent<PassHealth>().Invulnerable = true;
        boss.WakeUp();
        var opening = typeof(PassBossPatternController).GetField("openingReadyAt", PrivateInstance);
        opening.SetValue(boss, float.PositiveInfinity);
        var pivot = (Transform)typeof(PassBossPatternController).GetField("bodyPivot", PrivateInstance).GetValue(boss);

        typeof(PassBossPatternController).GetMethod("BeginCrack", PrivateInstance).Invoke(boss, null);
        float crackStarted = Time.time;
        float attackStarted = await WaitUntil(() => boss.Phase == PassBossPatternController.PatternPhase.CrackActive);
        if (boss.GroundCrack.StepsEmitted < 1)
            throw new InvalidOperationException("The first explosion did not start with recoil.");
        CheckAngle(pivot, settings.crack.windupTilt);

        float recoilFinished = await WaitUntil(() => boss.PhaseElapsed >= settings.crack.recoilDuration);
        CheckAngle(pivot, -settings.crack.strikeTilt);
        float holdFinished = await WaitUntil(() => boss.Phase == PassBossPatternController.PatternPhase.CrackReturn);
        CheckAngle(pivot, -settings.crack.strikeTilt);
        if (!boss.GroundCrack.AttackFinished)
            throw new InvalidOperationException("The boss returned while explosions were active.");
        float returnFinished = await WaitUntil(() => boss.Phase == PassBossPatternController.PatternPhase.CrackRecovery);
        CheckAngle(pivot, 0f);
        float crackFinished = await WaitUntil(() => boss.Phase == PassBossPatternController.PatternPhase.Idle);

        typeof(PassBossPatternController).GetMethod("BeginFall", PrivateInstance).Invoke(boss, new object[] { false });
        float fallStarted = Time.time;
        float fallActive = await WaitUntil(() => boss.Phase == PassBossPatternController.PatternPhase.Falling);
        await WaitUntil(() => boss.Phase == PassBossPatternController.PatternPhase.Idle);

        // 실제로 두 잡몹을 소환해 순간이동 종료 후 첫 점프까지의 대기를 측정한다.
        typeof(PassBossPatternController).GetField("fallReadyAt", PrivateInstance).SetValue(boss, 0f);
        typeof(PassBossPatternController).GetMethod("SpawnMinions", PrivateInstance).Invoke(boss, null);
        await WaitUntil(() => settings.Enemies.Count(enemy => enemy.IsMinion &&
            enemy.Phase == PassBossPatternController.PatternPhase.Idle) >= settings.linkedAttack.participants);
        foreach (var enemy in settings.Enemies.Where(enemy => enemy.IsMinion))
            opening.SetValue(enemy, float.PositiveInfinity);
        if (!boss.LinkedAttack.TryBegin()) throw new InvalidOperationException("Linked attack could not begin.");
        var stage = typeof(PassBossLinkedAttack).GetField("stage", PrivateInstance);
        float leadInStarted = await WaitUntil(() => stage.GetValue(boss.LinkedAttack).ToString() == "LeadIn");
        float firstJumpOrdered = await WaitUntil(() => boss.LinkedAttack.JumpsOrdered > 0);

        CheckTiming(attackStarted - crackStarted, 1.25f, "Explosion start");
        CheckTiming(recoilFinished - attackStarted, .25f, "Recoil");
        CheckTiming(holdFinished - recoilFinished, .75f, "Tilt hold");
        CheckTiming(returnFinished - holdFinished, .35f, "Return");
        CheckTiming(crackFinished - returnFinished, .45f, "Recovery");
        CheckTiming(fallActive - fallStarted, 1f, "Fall windup");
        CheckTiming(firstJumpOrdered - leadInStarted, .5f, "Linked lead-in");
        return new
        {
            passed = true,
            framesAdvanced = Time.frameCount - initialFrame,
            explosionStart = attackStarted - crackStarted,
            recoil = recoilFinished - attackStarted,
            tiltHold = holdFinished - recoilFinished,
            returnDuration = returnFinished - holdFinished,
            recovery = crackFinished - returnFinished,
            fallWindup = fallActive - fallStarted,
            linkedLeadIn = firstJumpOrdered - leadInStarted
        };
    }

    private static async Task<float> WaitUntil(Func<bool> condition)
    {
        var timeout = System.Diagnostics.Stopwatch.StartNew();
        while (!condition())
        {
            if (!Application.isPlaying || timeout.Elapsed.TotalSeconds > 12)
                throw new InvalidOperationException("Timed out waiting for boss phase.");
            await Task.Delay(5);
        }
        return Time.time;
    }

    private static void CheckTiming(float actual, float expected, string label)
    {
        if (Mathf.Abs(actual - expected) > .12f)
            throw new InvalidOperationException(label + ": " + actual + " (expected " + expected + ")");
    }

    private static void CheckAngle(Transform pivot, float expected)
    {
        if (Mathf.Abs(Mathf.DeltaAngle(expected, pivot.localEulerAngles.x)) > 1f)
            throw new InvalidOperationException("Unexpected boss tilt: " + pivot.localEulerAngles.x);
    }
}
