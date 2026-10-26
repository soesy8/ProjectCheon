using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Cheon.Prototype.MountainPass;
using Cheon.Tests.BossPatterns;

public class Test_ExpandedPatternsProbe : MonoBehaviour
{
    private Test_BossPatternSettings settings;
    private PassGameFlow game;
    private PassPlayerMovement player;
    private readonly List<string> failures = new List<string>();
    private readonly List<object> results = new List<object>();
    private string stage = "starting";
    private bool finished;
    private string reportName = "Test_ExpandedPatternsReport.json";
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    public static object Main()
    {
        if (!Application.isPlaying) throw new Exception("Play Mode 전용");
        Application.runInBackground = true; Time.timeScale = 2f;
        var probe = new GameObject("Test_ExpandedPatternsProbe").AddComponent<Test_ExpandedPatternsProbe>();
        probe.StartCoroutine(probe.Run()); return "축 맞춤·지면 균열·연계 공격 통합 검증 시작";
    }

    public static object CheckLinkedOnly()
    {
        Application.runInBackground = true; Time.timeScale = 2f;
        var probe = new GameObject("Test_LinkedCooldownProbe").AddComponent<Test_ExpandedPatternsProbe>();
        probe.reportName = "Test_LinkedCooldownReport.json";
        probe.settings = FindFirstObjectByType<Test_BossPatternSettings>();
        probe.game = FindFirstObjectByType<PassGameFlow>(); probe.player = probe.game.Player.GetComponent<PassPlayerMovement>();
        probe.StartCoroutine(probe.RunLinkedOnly()); return "연계 내려치기 쿨타임 준수 검증 시작";
    }

    private IEnumerator RunLinkedOnly()
    {
        yield return CheckLinkedAttack();
        finished = true; stage = "complete"; Save(); Time.timeScale = 1;
        foreach (var enemy in settings.Enemies) enemy.enabled = false;
    }

    private IEnumerator Run()
    {
        settings = FindFirstObjectByType<Test_BossPatternSettings>();
        game = FindFirstObjectByType<PassGameFlow>(); player = game.Player.GetComponent<PassPlayerMovement>();
        game.BeginBossChallenge(); game.Entrance.SkipPresentation();
        player.InputEnabled = false; game.PlayerHealth.Invulnerable = true;
        var boss = settings.MainBoss; boss.enabled = false;
        player.Teleport(boss.transform.position + boss.transform.forward * 7f, Quaternion.identity);
        Invoke(boss, "SpawnMinions"); yield return null;
        foreach (var enemy in settings.Enemies) enemy.enabled = false;
        yield return new WaitForFixedUpdate();
        var mini = settings.Enemies.First(item => item.IsMinion);
        yield return CheckTurns(boss);
        yield return CheckTurns(mini);
        yield return CheckTiming(boss);
        yield return CheckTiming(mini);
        yield return CheckCrack(boss);
        yield return CheckLinkedAttack();
        stage = "complete"; finished = true; Save(); Time.timeScale = 1f;
        foreach (var enemy in settings.Enemies) enemy.enabled = false;
    }

    private void Prepare(Test_BossPatternController enemy)
    {
        foreach (var other in settings.Enemies) { other.enabled = false; other.Body.enabled = false; }
        if (settings.MainBoss.LinkedAttack != null) settings.MainBoss.LinkedAttack.Clear();
        if (settings.MainBoss.GroundCrack != null) settings.MainBoss.GroundCrack.Clear();
        enemy.Body.enabled = true; enemy.transform.SetPositionAndRotation(settings.ArenaCenter, Quaternion.identity);
        Invoke(enemy, "SetTilt", 0f); Invoke(enemy, "SetPhase", Test_BossPatternController.PatternPhase.Idle);
        Set(enemy, "openingReadyAt", 0f); Set(enemy, "turnReadyAt", 0f);
        Set(enemy, "jumpReadyAt", 0f); Set(enemy, "fallReadyAt", 0f); Set(enemy, "crackReadyAt", 0f);
        typeof(Test_BossPatternController).GetProperty("ConsecutiveTurns").SetValue(enemy, 0);
        game.PlayerHealth.Restore(); game.PlayerHealth.Invulnerable = true;
        game.View.LockBoss(false); enemy.enabled = true;
    }

    private IEnumerator CheckTurns(Test_BossPatternController enemy)
    {
        stage = enemy.IsMinion ? "mini_turns" : "boss_turns"; Save(); Prepare(enemy);
        int turnsBefore = enemy.TurnsCompleted;
        var cooldowns = new List<float>(); var durations = new List<float>();
        float health = game.PlayerHealth.Current;
        for (int turn = 0; turn < 3; turn++)
        {
            player.Teleport(enemy.transform.position - enemy.transform.forward * 3f, Quaternion.identity);
            float deadline = Time.time + 3f;
            while (enemy.Phase != Test_BossPatternController.PatternPhase.Turning && Time.time < deadline) yield return null;
            if (enemy.Phase != Test_BossPatternController.PatternPhase.Turning) { Fail("축 맞춤 시작 실패"); yield break; }
            float started = Time.time; Vector3 origin = enemy.transform.position;
            while (enemy.Phase == Test_BossPatternController.PatternPhase.Turning)
            {
                if (Vector3.ProjectOnPlane(enemy.transform.position - origin, Vector3.up).magnitude > .01f) Fail("축 맞춤 중 수평 이동");
                yield return null;
            }
            durations.Add(Time.time - started); cooldowns.Add(enemy.TurnReadyAt - Time.time);
            if (enemy.ConsecutiveTurns != turn + 1) Fail("연속 축 맞춤 횟수 오류");
        }
        player.Teleport(enemy.transform.position - enemy.transform.forward * 3f, Quaternion.identity);
        float attackDeadline = Time.time + 2f;
        while (enemy.Phase == Test_BossPatternController.PatternPhase.Idle && Time.time < attackDeadline) yield return null;
        bool offensive = enemy.Phase == Test_BossPatternController.PatternPhase.FallWindup || enemy.Phase == Test_BossPatternController.PatternPhase.JumpWindup || enemy.Phase == Test_BossPatternController.PatternPhase.CrackWindup;
        if (!offensive || enemy.TurnsCompleted != turnsBefore + 3) Fail("3회 후 공격 강제 실패");
        if (durations.Any(value => Math.Abs(value - .5f) > .08f) || cooldowns.Any(value => Math.Abs(value - 1f) > .04f)) Fail("축 맞춤 시간 또는 재사용 대기 오류");
        if (health != game.PlayerHealth.Current) Fail("축 맞춤 중 피해 발생");
        enemy.enabled = false;
        results.Add(new { stage, durations, cooldowns, next = enemy.Phase.ToString() }); Save();
    }

    private IEnumerator CheckTiming(Test_BossPatternController enemy)
    {
        stage = enemy.IsMinion ? "mini_timing" : "boss_timing"; Save(); Prepare(enemy);
        Set(enemy, "crackReadyAt", float.PositiveInfinity); Set(enemy, "jumpReadyAt", float.PositiveInfinity);
        player.Teleport(enemy.transform.position + enemy.transform.forward * 2f, Quaternion.identity);
        float deadline = Time.time + 3f;
        while (enemy.Phase != Test_BossPatternController.PatternPhase.Falling && Time.time < deadline) yield return null;
        float started = Time.time;
        while (enemy.Phase == Test_BossPatternController.PatternPhase.Falling) yield return null;
        float fallDuration = Time.time - started;
        if (Math.Abs(fallDuration - .25f) > .08f) Fail("내려치기 활성 시간 오류");
        enemy.enabled = false; Invoke(enemy, "SetTilt", 0f); Invoke(enemy, "SetPhase", Test_BossPatternController.PatternPhase.Idle);
        Set(enemy, "fallReadyAt", float.PositiveInfinity); Set(enemy, "jumpReadyAt", 0f);
        player.Teleport(enemy.transform.position + enemy.transform.forward * 8f, Quaternion.identity); enemy.enabled = true;
        deadline = Time.time + 3f;
        while (enemy.Phase != Test_BossPatternController.PatternPhase.JumpFlight && Time.time < deadline) yield return null;
        if (enemy.Phase != Test_BossPatternController.PatternPhase.JumpFlight) { Fail("이동 점프 진입 실패"); yield break; }
        float distance = Vector3.ProjectOnPlane(enemy.LandingPosition - enemy.transform.position, Vector3.up).magnitude;
        float expected = Mathf.Lerp(.45f, .9f, distance / 10f);
        started = Time.time;
        while (enemy.Phase == Test_BossPatternController.PatternPhase.JumpFlight) yield return null;
        float flight = Time.time - started;
        if (Math.Abs(flight - expected) > .08f) Fail("이동 점프 체공 시간 오류");
        enemy.enabled = false; results.Add(new { stage, fallDuration, flight, expected }); Save();
    }

    private IEnumerator CheckCrack(Test_BossPatternController boss)
    {
        stage = "ground_crack"; Save(); Prepare(boss);
        Set(boss, "fallReadyAt", float.PositiveInfinity); Set(boss, "jumpReadyAt", float.PositiveInfinity); Set(boss, "preferCrack", true);
        player.Teleport(boss.transform.position + boss.transform.forward * 6f, Quaternion.identity);
        game.PlayerHealth.Invulnerable = false;
        float health = game.PlayerHealth.Current;
        int before = boss.CracksCompleted;
        float deadline = Time.time + 6f;
        while (boss.Phase != Test_BossPatternController.PatternPhase.CrackActive && Time.time < deadline)
        {
            if (FindObjectsByType<LineRenderer>(FindObjectsSortMode.None).Any(line => line.name == "Test_ActiveGroundCrack" && line.enabled)) Fail("발생 전 균열 예고 표시");
            yield return null;
        }
        Vector3 lockedDirection = boss.GroundCrack.Direction;
        while (boss.CracksCompleted == before && Time.time < deadline)
        {
            if (Vector3.Angle(boss.GroundCrack.Direction, lockedDirection) > .1f) Fail("균열 진행 중 방향 변경");
            yield return null;
        }
        float damage = health - game.PlayerHealth.Current;
        if (Math.Abs(damage - settings.crack.damage) > .01f) Fail("균열 피해 또는 1회 타격 오류: " + damage);
        if (boss.GroundCrack.StepsEmitted != settings.crack.stepCount || boss.CracksCompleted == before) Fail("균열 전파 또는 종료 실패");
        float remainingCooldown = boss.CrackReadyAt - Time.time;
        if (Math.Abs(remainingCooldown - settings.crack.cooldown) > .08f) Fail("균열 종료 후 쿨타임 오류");
        results.Add(new { stage, damage, steps = boss.GroundCrack.StepsEmitted, remainingCooldown }); Save();
        boss.enabled = false;
    }

    private IEnumerator CheckLinkedAttack()
    {
        stage = "linked_attack"; Save();
        game.BeginBossChallenge(); game.Entrance.SkipPresentation(); game.PlayerHealth.Invulnerable = true; player.InputEnabled = false;
        var boss = settings.MainBoss; boss.enabled = false;
        player.Teleport(boss.transform.position + boss.transform.forward * 8f, Quaternion.identity);
        yield return null;
        Invoke(boss, "SpawnMinions"); yield return null;
        var minis = settings.Enemies.Where(item => item.IsMinion && item.IsAlive).ToArray();
        if (minis.Length != 2) { Fail("연계용 잡몹 소환 실패"); yield break; }
        float earliestBossFall = Time.time + 8f;
        Set(boss, "openingReadyAt", 0f); Set(boss, "fallReadyAt", earliestBossFall); Set(boss, "jumpReadyAt", float.PositiveInfinity); Set(boss, "crackReadyAt", float.PositiveInfinity);
        foreach (var mini in minis) Set(mini, "openingReadyAt", Time.time + 100f);
        boss.enabled = true;
        float deadline = Time.time + 20f;
        var events = new List<object>(); var phases = new Dictionary<int, Test_BossPatternController.PatternPhase>();
        while (boss.LinkedAttack.CompletedCount == 0 && Time.time < deadline)
        {
            if (boss.Phase == Test_BossPatternController.PatternPhase.FallWindup && Time.time < earliestBossFall) Fail("연계 내려치기가 기존 쿨타임을 무시함");
            foreach (var enemy in new[] { boss }.Concat(minis))
                if (!phases.TryGetValue(enemy.GetInstanceID(), out var old) || old != enemy.Phase)
                {
                    phases[enemy.GetInstanceID()] = enemy.Phase;
                    events.Add(new { time = Time.time, enemy = enemy.name, phase = enemy.Phase.ToString() });
                }
            yield return null;
        }
        if (boss.LinkedAttack.CompletedCount != 1 || boss.LinkedAttack.JumpsOrdered != 2) Fail("잡몹 2회 점프 → 본체 엎어지기 연계 실패");
        if (boss.LinkedAttack.IsActive || minis.Any(item => typeof(Test_BossPatternController).GetField("reservedBy", Private).GetValue(item) != null)) Fail("연계 예약 해제 실패");
        if (Math.Abs(boss.LinkedAttack.ReadyAt - Time.time - settings.linkedAttack.cooldown) > .08f) Fail("연계 종료 후 쿨타임 오류");
        results.Add(new { stage, completed = boss.LinkedAttack.CompletedCount, orderedJumps = boss.LinkedAttack.JumpsOrdered, events }); Save();

        boss.enabled = false;
        foreach (var mini in minis)
        {
            mini.enabled = false; Invoke(mini, "SetTilt", 0f); Invoke(mini, "SetPhase", Test_BossPatternController.PatternPhase.Idle);
            Set(mini, "jumpReadyAt", 0f);
        }
        Invoke(boss, "SetPhase", Test_BossPatternController.PatternPhase.Idle);
        typeof(Test_BossLinkedAttack).GetProperty("ReadyAt").SetValue(boss.LinkedAttack, 0f);
        foreach (var mini in minis) mini.enabled = true;
        if (!boss.LinkedAttack.TryBegin()) Fail("중단 검증용 연계 시작 실패");
        minis[0].Health.Kill(); boss.enabled = true; yield return null;
        if (boss.LinkedAttack.IsActive || typeof(Test_BossPatternController).GetField("reservedBy", Private).GetValue(minis[1]) != null) Fail("잡몹 사망 시 연계 중단 실패");
        game.RetryCheckpoint(); game.Entrance.SkipPresentation(); yield return null;
        if (settings.Enemies.Any(item => item != null && item.IsMinion) || boss.LinkedAttack.IsActive || !boss.GroundCrack.AttackFinished) Fail("재도전 시 신규 패턴 정리 실패");
    }

    private void Fail(string message) { failures.Add(stage + ": " + message); }
    private static void Set(Test_BossPatternController enemy, string field, object value) => typeof(Test_BossPatternController).GetField(field, Private).SetValue(enemy, value);
    private static void Invoke(Test_BossPatternController enemy, string method, params object[] arguments) => typeof(Test_BossPatternController).GetMethod(method, Private).Invoke(enemy, arguments);
    private void Save() => System.IO.File.WriteAllText(Application.dataPath + "/Temp/BossPatterns~/" + reportName,
        Newtonsoft.Json.JsonConvert.SerializeObject(new { stage, finished, frame = Time.frameCount, failures, results }, Newtonsoft.Json.Formatting.Indented));
}
