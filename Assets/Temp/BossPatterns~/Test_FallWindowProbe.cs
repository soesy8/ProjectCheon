using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Cheon.Prototype.MountainPass;
using Cheon.Tests.BossPatterns;

public class Test_FallWindowProbe : MonoBehaviour
{
    private readonly List<string> failures = new List<string>();
    public static object Main()
    {
        Application.runInBackground = true;
        var probe = new GameObject("Test_FallWindowProbe").AddComponent<Test_FallWindowProbe>();
        probe.StartCoroutine(probe.Run()); return "엎어지기 활성 구간 및 무공격 회전 검증 시작";
    }
    private IEnumerator Run()
    {
        var settings = FindFirstObjectByType<Test_BossPatternSettings>();
        var game = FindFirstObjectByType<PassGameFlow>();
        var boss = settings.MainBoss;
        boss.enabled = true;
        game.BeginBossChallenge(); game.Entrance.SkipPresentation();
        var player = game.Player.GetComponent<PassPlayerMovement>(); player.InputEnabled = false;
        player.Teleport(boss.transform.position + boss.transform.forward * 6f, Quaternion.LookRotation(-boss.transform.forward));
        game.View.LockBoss(false);
        Time.timeScale = 1f;
        float initialHealth = game.PlayerHealth.Current;
        float deadline = Time.time + 12f;
        while (boss.Phase != Test_BossPatternController.PatternPhase.Falling && Time.time < deadline)
        {
            if (game.PlayerHealth.Current != initialHealth) failures.Add("선딜레이 중 피해 발생");
            yield return null;
        }
        float fallingStarted = Time.time;
        while (boss.Phase == Test_BossPatternController.PatternPhase.Falling) yield return null;
        float fallingDuration = Time.time - fallingStarted;
        float damage = initialHealth - game.PlayerHealth.Current;
        if (Math.Abs(damage - settings.combat.bossFallDamage) > .01f) failures.Add("몸통 전체 타격 또는 1회 피해 오류: " + damage);
        if (Math.Abs(fallingDuration - .3f) > .08f) failures.Add("넘어지는 시간 오류: " + fallingDuration);
        float healthAfterFall = game.PlayerHealth.Current;
        player.Teleport(boss.transform.position + boss.transform.forward * 6f, Quaternion.LookRotation(-boss.transform.forward));
        while (boss.Phase == Test_BossPatternController.PatternPhase.FallHold || boss.Phase == Test_BossPatternController.PatternPhase.Rising || boss.Phase == Test_BossPatternController.PatternPhase.FallRecovery)
        {
            if (game.PlayerHealth.Current != healthAfterFall) failures.Add("멈춤·기상·후딜레이 중 피해 발생");
            if (boss.Phase == Test_BossPatternController.PatternPhase.FallRecovery)
                player.Teleport(boss.transform.position - boss.transform.forward * 6f, Quaternion.LookRotation(boss.transform.forward));
            yield return null;
        }
        Vector3 positionBeforeTurn = boss.transform.position;
        deadline = Time.time + 3f;
        while (boss.Phase != Test_BossPatternController.PatternPhase.Turning && Time.time < deadline) yield return null;
        float turnStart = Time.time;
        while (boss.Phase == Test_BossPatternController.PatternPhase.Turning)
        {
            if (game.PlayerHealth.Current != healthAfterFall) failures.Add("회전 중 피해 발생");
            if (Vector3.Distance(positionBeforeTurn, boss.transform.position) > .001f) failures.Add("회전 중 지상 이동");
            yield return null;
        }
        float turnDuration = Time.time - turnStart;
        Vector3 towardPlayer = Vector3.ProjectOnPlane(player.transform.position - boss.transform.position, Vector3.up);
        if (Vector3.Angle(boss.transform.forward, towardPlayer) > 1f) failures.Add("회전 후 축 정렬 실패");
        if (Math.Abs(turnDuration - 1f) > .1f) failures.Add("회전 시간 오류: " + turnDuration);
        if (FindObjectsByType<LineRenderer>(FindObjectsSortMode.None).Any(item => item.enabled && item.name.Contains("Guardian"))) failures.Add("보스 공격 범위 예고 표시");
        System.IO.File.WriteAllText(Application.dataPath + "/Temp/BossPatterns~/Test_FallWindowReport.json",
            Newtonsoft.Json.JsonConvert.SerializeObject(new { damage, fallingDuration, turnDuration, failures }, Newtonsoft.Json.Formatting.Indented));
        game.PlayerHealth.Invulnerable = true;
        boss.enabled = false;
    }
}
