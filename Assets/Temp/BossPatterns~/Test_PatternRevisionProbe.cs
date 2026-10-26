using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Cheon.Prototype.MountainPass;
using Cheon.Tests.BossPatterns;

public class Test_PatternRevisionProbe : MonoBehaviour
{
    private Test_BossPatternSettings settings;
    private PassGameFlow game;
    private PassPlayerMovement player;
    private readonly List<string> failures = new List<string>();
    private readonly List<object> results = new List<object>();
    private bool finished;
    private string stage;
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    public static object Main()
    {
        if (!Application.isPlaying) throw new Exception("Play Mode 전용");
        Application.runInBackground = true; Time.timeScale = 1f;
        var probe = new GameObject("Test_PatternRevisionProbe").AddComponent<Test_PatternRevisionProbe>();
        probe.StartCoroutine(probe.Run()); return "보스와 미니 장승의 수정된 패턴 검증 시작";
    }

    private IEnumerator Run()
    {
        settings = FindFirstObjectByType<Test_BossPatternSettings>();
        game = FindFirstObjectByType<PassGameFlow>();
        player = game.Player.GetComponent<PassPlayerMovement>();
        game.BeginBossChallenge(); game.Entrance.SkipPresentation();
        game.PlayerHealth.Invulnerable = true; player.InputEnabled = false;
        var boss = settings.MainBoss; boss.enabled = false;
        player.Teleport(boss.transform.position + boss.transform.forward * 7f, Quaternion.identity);
        Invoke(boss, "SpawnMinions");
        yield return null;
        foreach (var enemy in settings.Enemies) enemy.enabled = false;
        yield return new WaitForFixedUpdate();
        var mini = settings.Enemies.First(enemy => enemy.IsMinion);
        foreach (var enemy in new[] { boss, mini }) yield return CheckEnemy(enemy);
        finished = true; stage = "complete"; Save();
        foreach (var enemy in settings.Enemies) enemy.enabled = false;
        Time.timeScale = 1f;
    }

    private IEnumerator CheckEnemy(Test_BossPatternController enemy)
    {
        stage = enemy.IsMinion ? "minion" : "boss"; Save();
        foreach (var other in settings.Enemies) { other.enabled = false; other.Body.enabled = false; }
        enemy.Body.enabled = true;
        enemy.transform.SetPositionAndRotation(settings.ArenaCenter, Quaternion.identity);
        Invoke(enemy, "SetTilt", 0f);
        enemy.Health.Restore(); enemy.Health.Invulnerable = false;
        Set(enemy, "openingReadyAt", 0f); Set(enemy, "jumpReadyAt", float.PositiveInfinity); Set(enemy, "fallReadyAt", 0f); Set(enemy, "turnPending", false);
        Invoke(enemy, "SetPhase", Test_BossPatternController.PatternPhase.Idle);
        game.PlayerHealth.Restore(); game.PlayerHealth.Invulnerable = true;
        player.Teleport(enemy.transform.position + Vector3.forward * 2f, Quaternion.identity);
        Physics.SyncTransforms(); enemy.enabled = true;

        float deadline = Time.time + 3f;
        while (enemy.Phase != Test_BossPatternController.PatternPhase.FallWindup && Time.time < deadline) yield return null;
        if (enemy.Phase != Test_BossPatternController.PatternPhase.FallWindup) { failures.Add(stage + ": 엎어지기 진입 실패"); yield break; }
        float largestTrackingError = 0;
        bool movedFirst = false, movedSecond = false;
        while (enemy.Phase == Test_BossPatternController.PatternPhase.FallWindup)
        {
            if (!movedFirst && enemy.PhaseElapsed >= .3f)
            { player.Teleport(enemy.transform.position + new Vector3(1.6f, 0, 2f), Quaternion.identity); movedFirst = true; yield return null; }
            if (!movedSecond && enemy.PhaseElapsed >= 1f)
            { player.Teleport(enemy.transform.position + new Vector3(-1.6f, 0, 2f), Quaternion.identity); movedSecond = true; yield return null; }
            Vector3 toward = Vector3.ProjectOnPlane(player.transform.position - enemy.transform.position, Vector3.up);
            largestTrackingError = Mathf.Max(largestTrackingError, Vector3.Angle(enemy.transform.forward, toward));
            yield return null;
        }
        if (largestTrackingError > 1f) failures.Add(stage + ": 선딜레이 추적 실패 " + largestTrackingError);
        Quaternion locked = enemy.transform.rotation;
        float fallStart = Time.time;
        player.Teleport(enemy.transform.position - Vector3.forward * 3f, Quaternion.identity);
        while (enemy.Phase == Test_BossPatternController.PatternPhase.Falling)
        {
            if (Quaternion.Angle(locked, enemy.transform.rotation) > .1f) failures.Add(stage + ": 내려치기 중 방향 변경");
            yield return null;
        }
        float fallDuration = Time.time - fallStart;
        if (Math.Abs(fallDuration - .1f) > .05f) failures.Add(stage + ": 내려치기 시간 " + fallDuration);
        deadline = Time.time + 7f;
        while (enemy.FallsCompleted == 0 && Time.time < deadline) yield return null;
        float cooldown = enemy.FallReadyAt - enemy.LastFallFinishedAt;
        if (cooldown < 4f || cooldown > 6f) failures.Add(stage + ": 엎어지기 쿨타임 " + cooldown);

        Set(enemy, "jumpReadyAt", float.PositiveInfinity); Set(enemy, "fallReadyAt", float.PositiveInfinity); Set(enemy, "turnPending", true);
        Vector3 turnOrigin = enemy.transform.position;
        player.Teleport(turnOrigin - enemy.transform.forward * 3f, Quaternion.identity);
        game.PlayerHealth.Restore(); game.PlayerHealth.Invulnerable = false;
        float healthBeforeTurn = game.PlayerHealth.Current;
        deadline = Time.time + 2f;
        while (enemy.Phase != Test_BossPatternController.PatternPhase.Turning && Time.time < deadline) yield return null;
        float turnStart = Time.time, peak = 0, horizontalDrift = 0;
        while (enemy.Phase == Test_BossPatternController.PatternPhase.Turning)
        {
            peak = Mathf.Max(peak, enemy.transform.position.y - turnOrigin.y);
            horizontalDrift = Mathf.Max(horizontalDrift, Vector3.ProjectOnPlane(enemy.transform.position - turnOrigin, Vector3.up).magnitude);
            yield return null;
        }
        float turnDuration = Time.time - turnStart;
        float turnFacingError = Vector3.Angle(enemy.transform.forward, Vector3.ProjectOnPlane(player.transform.position - enemy.transform.position, Vector3.up));
        if (Math.Abs(turnDuration - .2f) > .05f || peak < .8f || horizontalDrift > .001f || Math.Abs(enemy.transform.position.y - turnOrigin.y) > .01f)
            failures.Add(stage + ": 제자리 점프 회전 시간·높이·착지 오류");
        if (turnFacingError > 1f || game.PlayerHealth.Current != healthBeforeTurn) failures.Add(stage + ": 회전 축 정렬 또는 무공격 오류");

        game.PlayerHealth.Invulnerable = true;
        Set(enemy, "jumpReadyAt", 0f); Set(enemy, "turnPending", false);
        player.Teleport(enemy.transform.position + enemy.transform.forward * 8f, Quaternion.identity);
        deadline = Time.time + 3f;
        while (enemy.Phase != Test_BossPatternController.PatternPhase.JumpFlight && Time.time < deadline) yield return null;
        if (enemy.Phase != Test_BossPatternController.PatternPhase.JumpFlight) { failures.Add(stage + ": 점프 진입 실패"); yield break; }
        float jumpStart = Time.time;
        float distance = Vector3.ProjectOnPlane(enemy.LandingPosition - enemy.transform.position, Vector3.up).magnitude;
        float expectedFlight = Mathf.Lerp(.3f, .6f, distance / 10f);
        while (enemy.Phase == Test_BossPatternController.PatternPhase.JumpFlight) yield return null;
        float jumpFlight = Time.time - jumpStart;
        if (Math.Abs(jumpFlight - expectedFlight) > .05f) failures.Add(stage + ": 단축된 점프 체공 시간 " + jumpFlight);
        enemy.enabled = false;
        results.Add(new { enemy = stage, largestTrackingError, fallDuration, cooldown, turnDuration, turnPeakHeight = peak, horizontalDrift, turnFacingError, jumpFlight, expectedFlight });
        Save();
    }

    private static void Set(Test_BossPatternController enemy, string field, object value)
    { typeof(Test_BossPatternController).GetField(field, Private).SetValue(enemy, value); }
    private static void Invoke(Test_BossPatternController enemy, string method, params object[] args)
    { typeof(Test_BossPatternController).GetMethod(method, Private).Invoke(enemy, args); }
    private void Save()
    {
        System.IO.File.WriteAllText(Application.dataPath + "/Temp/BossPatterns~/Test_PatternRevisionReport.json",
            Newtonsoft.Json.JsonConvert.SerializeObject(new { stage, finished, frame = Time.frameCount, failures, results }, Newtonsoft.Json.Formatting.Indented));
    }
}
