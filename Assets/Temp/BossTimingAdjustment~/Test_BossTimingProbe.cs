using System;
using System.Reflection;
using System.Threading.Tasks;
using Cheon.Prototype.MountainPass;
using UnityEngine;

public static class Test_BossTimingProbe
{
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
        // 등장 연출과 자동 패턴 선택을 건너뛰고 각 패턴의 실제 시간만 관찰한다.
        typeof(PassGameFlow).GetProperty("InEncounter").SetValue(flow, true);
        player.InputEnabled = false;
        player.GetComponent<PassHealth>().Invulnerable = true;
        boss.WakeUp();
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(PassBossPatternController).GetField("openingReadyAt", flags).SetValue(boss, float.PositiveInfinity);

        typeof(PassBossPatternController).GetMethod("BeginCrack", flags).Invoke(boss, null);
        float crackStarted = Time.time;
        float crackActive = await WaitUntil(() => boss.Phase == PassBossPatternController.PatternPhase.CrackActive);
        float crackRecovery = await WaitUntil(() => boss.Phase == PassBossPatternController.PatternPhase.CrackRecovery);
        float crackFinished = await WaitUntil(() => boss.Phase == PassBossPatternController.PatternPhase.Idle);

        typeof(PassBossPatternController).GetMethod("BeginFall", flags).Invoke(boss, new object[] { false });
        await WaitUntil(() => boss.Phase == PassBossPatternController.PatternPhase.FallHold);
        float fallHitFinished = Time.time;
        float fallFinished = await WaitUntil(() => boss.Phase == PassBossPatternController.PatternPhase.Idle);

        // 착지 직후 상태에서 회복 구간을 재생해 이동 거리의 영향을 제외한다.
        typeof(PassBossPatternController).GetMethod("SetPhase", flags).Invoke(boss,
            new object[] { PassBossPatternController.PatternPhase.JumpRecovery });
        float jumpLanded = Time.time;
        float jumpFinished = await WaitUntil(() => boss.Phase == PassBossPatternController.PatternPhase.Idle);

        float measuredCrackWindup = crackActive - crackStarted;
        float measuredCrackActive = crackRecovery - crackActive;
        float measuredCrackRecovery = crackFinished - crackRecovery;
        float measuredFallRecovery = fallFinished - fallHitFinished;
        float measuredJumpRecovery = jumpFinished - jumpLanded;
        CheckTiming(measuredCrackWindup, 2f, "Crack windup");
        CheckTiming(measuredCrackActive, .85f, "Crack active");
        CheckTiming(measuredCrackRecovery, 3f, "Crack recovery");
        CheckTiming(measuredFallRecovery, 3f, "Fall recovery");
        CheckTiming(measuredJumpRecovery, 1.5f, "Jump recovery");
        return new
        {
            passed = true,
            framesAdvanced = Time.frameCount - initialFrame,
            crackWindup = measuredCrackWindup,
            crackActive = measuredCrackActive,
            crackRecovery = measuredCrackRecovery,
            fallRecovery = measuredFallRecovery,
            jumpRecovery = measuredJumpRecovery
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
}
