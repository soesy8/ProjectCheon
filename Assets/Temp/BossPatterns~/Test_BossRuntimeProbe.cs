using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Cheon.Prototype.MountainPass;
using Cheon.Tests.BossPatterns;

[DefaultExecutionOrder(100)]
public class Test_BossRuntimeProbe : MonoBehaviour
{
    private Test_BossPatternSettings settings;
    private PassGameFlow game;
    private Test_BossPatternController boss;
    private readonly List<object> phases = new List<object>();
    private readonly List<string> failures = new List<string>();
    private readonly Dictionary<int, Test_BossPatternController.PatternPhase> previous = new Dictionary<int, Test_BossPatternController.PatternPhase>();
    private int frames;
    private float startTime, jumpStartTime, jumpStartY, peakJump, clearTime, maxOverlap;
    private bool concurrent;
    private bool finished;
    private string stage = "natural_patterns";
    private static string Path => Application.dataPath + "/Temp/BossPatterns~/Test_RuntimeReport.json";

    public static object Main()
    {
        if (!Application.isPlaying) throw new Exception("Play Mode 전용");
        var probe = new GameObject("Test_RuntimeProbe").AddComponent<Test_BossRuntimeProbe>();
        probe.settings = FindFirstObjectByType<Test_BossPatternSettings>();
        probe.game = FindFirstObjectByType<PassGameFlow>();
        probe.boss = probe.settings.MainBoss;
        Application.runInBackground = true;
        probe.game.BeginBossChallenge();
        probe.game.Entrance.SkipPresentation();
        probe.game.PlayerHealth.Invulnerable = true;
        var player = probe.game.Player.GetComponent<PassPlayerMovement>();
        player.Teleport(probe.boss.transform.position + probe.boss.transform.forward * 6f, Quaternion.LookRotation(-probe.boss.transform.forward));
        player.AnimationControl.EnterCombat();
        probe.startTime = Time.time;
        Time.timeScale = 2f;
        probe.Save();
        return "런타임 통합 검증 시작";
    }

    private void LateUpdate()
    {
        if (finished || settings == null) return;
        frames++;
        foreach (var enemy in settings.Enemies.ToArray())
        {
            if (enemy == null) continue;
            int id = enemy.GetInstanceID();
            if (!previous.TryGetValue(id, out var oldPhase) || oldPhase != enemy.Phase)
            {
                phases.Add(new { time = Time.time - startTime, enemy = enemy.name, phase = enemy.Phase.ToString(), jump = enemy.JumpsCompleted,
                    fall = enemy.FallsCompleted, turn = enemy.TurnsCompleted, summon = enemy.SummonsCompleted, jumpReadyAt = enemy.JumpReadyAt - Time.time, fallReadyAt = enemy.FallReadyAt - Time.time });
                if (enemy == boss && enemy.Phase == Test_BossPatternController.PatternPhase.JumpFlight)
                { jumpStartTime = Time.time; jumpStartY = enemy.transform.position.y; peakJump = 0; }
                if (enemy == boss && oldPhase == Test_BossPatternController.PatternPhase.JumpFlight)
                {
                    float duration = Time.time - jumpStartTime;
                    if (duration < .95f || duration > 2.1f) failures.Add("점프 체공 시간: " + duration);
                    if (peakJump < 4.7f || peakJump > 5.2f) failures.Add("점프 최고 높이: " + peakJump);
                }
                if (enemy.Phase == Test_BossPatternController.PatternPhase.JumpRecovery && Math.Abs(enemy.LastJumpFinishedAt - Time.time) < .01f)
                    failures.Add("점프 후딜레이 전에 종료됨");
                previous[id] = enemy.Phase;
                Save();
            }
            if (enemy == boss && enemy.Phase == Test_BossPatternController.PatternPhase.JumpFlight)
                peakJump = Mathf.Max(peakJump, enemy.transform.position.y - jumpStartY);
            if (enemy.IsMinion && enemy.IsAlive && enemy.Phase != Test_BossPatternController.PatternPhase.Idle &&
                boss.Phase != Test_BossPatternController.PatternPhase.Idle) concurrent = true;
        }
        var living = settings.Enemies.Where(item => item != null && item.IsAlive).ToArray();
        for (int a = 0; a < living.Length; a++)
            for (int b = a + 1; b < living.Length; b++)
                if (Physics.ComputePenetration(living[a].Body, living[a].Body.transform.position, living[a].Body.transform.rotation,
                    living[b].Body, living[b].Body.transform.position, living[b].Body.transform.rotation, out _, out float distance))
                    maxOverlap = Mathf.Max(maxOverlap, distance);
        if (boss.LivingMinions > 2) failures.Add("필드 잡몹 제한 초과");
        if (stage == "natural_patterns" && boss.SummonsCompleted > 0 && concurrent && boss.LivingMinions == 2)
        {
            stage = "area_combat";
            StartCoroutine(CheckAreaCombat());
        }
        if (Time.time - startTime > 180 && !finished)
        { failures.Add("검증 제한 시간 초과: " + stage); Finish(); }
    }

    private IEnumerator CheckAreaCombat()
    {
        foreach (var enemy in settings.Enemies) enemy.enabled = false;
        var player = game.Player.GetComponent<PassPlayerMovement>();
        player.Teleport(settings.ArenaCenter + Vector3.forward * 6f, Quaternion.identity);
        game.View.LockBoss(false);
        int miniIndex = 0;
        foreach (var enemy in settings.Enemies)
        {
            if (!enemy.IsAlive) continue;
            enemy.transform.rotation = Quaternion.identity;
            enemy.transform.position = player.transform.position + new Vector3(enemy.IsMinion ? (++miniIndex == 1 ? -1.4f : 1.4f) : 0, 0, 2.5f);
            enemy.Health.Restore(); enemy.Health.Invulnerable = false;
        }
        Physics.SyncTransforms();
        yield return null;
        var targets = settings.Enemies.Where(item => item.IsAlive).ToArray();
        float[] health = targets.Select(item => item.Health.Current).ToArray();
        player.AnimationControl.PlayAttack3();
        yield return new WaitForSeconds(2f);
        for (int index = 0; index < targets.Length; index++)
            if (Math.Abs((health[index] - targets[index].Health.Current) - settings.combat.playerDamage.z) > .01f)
                failures.Add("3타 다중 타격 또는 중복 타격: " + targets[index].name + ": " + (health[index] - targets[index].Health.Current));
        stage = "resummon_cooldown";
        foreach (var enemy in settings.Enemies.ToArray()) if (enemy.IsMinion && enemy.IsAlive) enemy.Health.Kill();
        clearTime = Time.time;
        boss.enabled = true;
        yield return new WaitForSeconds(14.5f);
        if (boss.LivingMinions != 0) failures.Add("15초 이전에 재소환됨");
        float deadline = Time.time + 35f;
        while (boss.SummonsCompleted < 2 && Time.time < deadline) yield return null;
        if (boss.LivingMinions != 2) failures.Add("재소환 실패");
        if (Time.time - clearTime < 15f) failures.Add("재소환 쿨타임 미준수");
        stage = "retry_cleanup";
        game.RetryCheckpoint(); game.Entrance.SkipPresentation(); game.PlayerHealth.Invulnerable = true;
        yield return null;
        if (settings.Enemies.Any(item => item != null && item.IsMinion)) failures.Add("재도전 시 잡몹 잔존");
        stage = "done";
        Finish();
    }

    private void Finish()
    {
        finished = true; Time.timeScale = 1f;
        if (!concurrent) failures.Add("동시 패턴 미관측");
        if (maxOverlap > .08f) failures.Add("적 몸체 관통: " + maxOverlap);
        Save();
    }

    private void Save()
    {
        System.IO.File.WriteAllText(Path, Newtonsoft.Json.JsonConvert.SerializeObject(new
        {
            stage, finished, frames, elapsed = Time.time - startTime, concurrent, maxOverlap,
            livingMinions = boss != null ? boss.LivingMinions : 0,
            failures, phases
        }, Newtonsoft.Json.Formatting.Indented));
    }
}
