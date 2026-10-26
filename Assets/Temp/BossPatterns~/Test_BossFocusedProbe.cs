using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Cheon.Prototype.MountainPass;
using Cheon.Tests.BossPatterns;

[DefaultExecutionOrder(100)]
public class Test_BossFocusedProbe : MonoBehaviour
{
    private Test_BossPatternSettings settings;
    private PassGameFlow game;
    private Test_BossPatternController boss;
    private readonly List<string> failures = new List<string>();
    private readonly List<object> cooldowns = new List<object>();
    private readonly Dictionary<int, int> jumps = new Dictionary<int, int>();
    private readonly Dictionary<int, int> falls = new Dictionary<int, int>();
    private float overlap, start;
    private bool done;
    private int frames;

    public static object Main()
    {
        Application.runInBackground = true;
        var probe = new GameObject("Test_FocusedProbe").AddComponent<Test_BossFocusedProbe>();
        probe.settings = FindFirstObjectByType<Test_BossPatternSettings>();
        probe.game = FindFirstObjectByType<PassGameFlow>();
        probe.boss = probe.settings.MainBoss;
        probe.game.BeginBossChallenge(); probe.game.Entrance.SkipPresentation();
        probe.game.PlayerHealth.Invulnerable = true;
        probe.boss.enabled = false;
        var player = probe.game.Player.GetComponent<PassPlayerMovement>();
        player.InputEnabled = false;
        player.Teleport(probe.boss.transform.position + probe.boss.transform.forward * 7f, Quaternion.identity);
        typeof(Test_BossPatternController).GetMethod("SpawnMinions", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(probe.boss, null);
        var minions = probe.settings.Enemies.Where(item => item.IsMinion && item.IsAlive).ToArray();
        if (minions.Length != 2) probe.failures.Add("소환 수 오류");
        foreach (var mini in minions)
        {
            if (Math.Abs(Vector3.Distance(mini.transform.position, probe.boss.transform.position) - 4f) > .05f) probe.failures.Add("좌우 소환 간격 오류");
            if (Math.Abs(mini.Body.height / probe.boss.Body.height - .25f) > .001f) probe.failures.Add("크기 25% 오류");
        }
        foreach (var enemy in probe.settings.Enemies)
        {
            Vector3 forward = enemy.transform.forward;
            player.Teleport(enemy.transform.position + forward * 3f, Quaternion.LookRotation(-forward));
            Physics.SyncTransforms();
            player.GetComponent<CharacterController>().Move(-forward * 6f);
            float along = Vector3.Dot(player.transform.position - enemy.transform.position, forward);
            if (along < enemy.Body.radius) probe.failures.Add("플레이어 몸체 관통: " + enemy.name + " / " + along);
        }
        player.Teleport(probe.boss.transform.position + probe.boss.transform.forward * 7f, Quaternion.identity);
        probe.game.View.LockBoss(false);
        probe.start = Time.time;
        Time.timeScale = 2f;
        probe.StartCoroutine(probe.Run());
        return "충돌·미니 쿨타임·동시 사망 검증 시작";
    }

    private void LateUpdate()
    {
        if (done || settings == null) return;
        frames++;
        var enemies = settings.Enemies.Where(item => item != null && item.IsAlive).ToArray();
        foreach (var enemy in enemies)
        {
            int id = enemy.GetInstanceID();
            if (!enemy.IsMinion) continue;
            if (enemy.JumpsCompleted > 0 && (!jumps.TryGetValue(id, out int oldJumps) || oldJumps != enemy.JumpsCompleted))
            {
                jumps[id] = enemy.JumpsCompleted;
                float cooldown = enemy.JumpReadyAt - enemy.LastJumpFinishedAt;
                cooldowns.Add(new { enemy = enemy.name, pattern = "jump", cooldown });
                if (Math.Abs(cooldown - 1.5f) > .001f) failures.Add("미니 점프 쿨타임 오류");
            }
            if (enemy.FallsCompleted > 0 && (!falls.TryGetValue(id, out int oldFalls) || oldFalls != enemy.FallsCompleted))
            {
                falls[id] = enemy.FallsCompleted;
                float cooldown = enemy.FallReadyAt - enemy.LastFallFinishedAt;
                cooldowns.Add(new { enemy = enemy.name, pattern = "fall", cooldown });
                if (cooldown < 6f || cooldown > 8f) failures.Add("미니 엎어지기 쿨타임 오류");
            }
        }
        for (int a = 0; a < enemies.Length; a++)
            for (int b = a + 1; b < enemies.Length; b++)
                if (Physics.ComputePenetration(enemies[a].Body, enemies[a].Body.transform.position, enemies[a].Body.transform.rotation,
                    enemies[b].Body, enemies[b].Body.transform.position, enemies[b].Body.transform.rotation, out _, out float distance))
                    overlap = Mathf.Max(overlap, distance);
    }

    private IEnumerator Run()
    {
        yield return new WaitForSeconds(36f);
        if (jumps.Count < 2) failures.Add("두 미니의 점프 종료 미관측");
        if (falls.Count == 0) failures.Add("미니 엎어지기 종료 미관측");
        var minis = settings.Enemies.Where(item => item.IsMinion && item.IsAlive).ToArray();
        boss.Health.Kill();
        if (minis.Any(item => !item.Health.IsDead)) failures.Add("보스 사망 시 잡몹 생존");
        yield return null;
        if (settings.Enemies.Any(item => item.IsMinion && item.IsAlive)) failures.Add("보스 사망 시 잡몹 잔존");
        if (overlap > .08f) failures.Add("몸체 겹침 " + overlap);
        done = true; Time.timeScale = 1f;
        System.IO.File.WriteAllText(Application.dataPath + "/Temp/BossPatterns~/Test_FocusedReport.json",
            Newtonsoft.Json.JsonConvert.SerializeObject(new { done, frames, elapsed = Time.time - start, overlap, failures, cooldowns }, Newtonsoft.Json.Formatting.Indented));
    }
}
