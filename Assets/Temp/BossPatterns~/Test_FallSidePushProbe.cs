using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Cheon.Prototype.MountainPass;
using Cheon.Tests.BossPatterns;

public class Test_FallSidePushProbe : MonoBehaviour
{
    private Test_BossPatternSettings settings;
    private PassGameFlow game;
    private PassPlayerMovement player;
    private CharacterController controller;
    private readonly List<string> failures = new List<string>();
    private readonly List<object> results = new List<object>();
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private string stage = "starting";

    public static object Main()
    {
        if (!Application.isPlaying) throw new Exception("Play Mode 전용");
        Application.runInBackground = true;
        var probe = new GameObject("Test_FallSidePushProbe").AddComponent<Test_FallSidePushProbe>();
        probe.StartCoroutine(probe.Run());
        return "본체·잡몹 정통 피격 및 지면 충돌 검증 시작";
    }

    private IEnumerator Run()
    {
        settings = FindFirstObjectByType<Test_BossPatternSettings>();
        game = FindFirstObjectByType<PassGameFlow>();
        player = game.Player.GetComponent<PassPlayerMovement>();
        controller = player.GetComponent<CharacterController>();
        game.BeginBossChallenge(); game.Entrance.SkipPresentation();
        Time.timeScale = 1f; player.InputEnabled = false;
        var boss = settings.MainBoss; boss.enabled = false;
        player.Teleport(boss.transform.position + boss.transform.forward * 8f, Quaternion.identity);
        Invoke(boss, "SpawnMinions"); yield return null;
        var mini = settings.Enemies.First(item => item.IsMinion);
        yield return Check(boss, 6f, 0f);
        yield return Check(boss, 10f, 0f);
        yield return Check(boss, 6f, -.2f);
        yield return Check(mini, 1.5f, 0f);
        yield return Check(mini, 2.4f, -.15f);
        stage = "complete"; Save(true);
        foreach (var enemy in settings.Enemies) enemy.enabled = false;
        player.InputEnabled = false; Time.timeScale = 1f;
    }

    private IEnumerator Check(Test_BossPatternController enemy, float forward, float lateral)
    {
        stage = (enemy.IsMinion ? "mini" : "boss") + "_" + forward + "_" + lateral; Save(false);
        player.InputEnabled = false;
        foreach (var other in settings.Enemies) { other.enabled = false; other.Body.enabled = false; }
        settings.MainBoss.LinkedAttack.Clear(); settings.MainBoss.GroundCrack.Clear();
        enemy.transform.SetPositionAndRotation(settings.ArenaCenter, Quaternion.identity);
        Invoke(enemy, "SetTilt", 0f); enemy.Body.enabled = true;
        player.Teleport(enemy.transform.position + Vector3.forward * forward + Vector3.right * lateral, Quaternion.identity);
        game.PlayerHealth.Restore(); game.View.LockBoss(false);
        // 입력 없이 중력 이동과 실제 CharacterController 충돌을 계속 실행한다.
        player.InputEnabled = true; Physics.SyncTransforms();
        float before = game.PlayerHealth.Current;
        Vector3 start = player.transform.position;
        float minFoot = float.PositiveInfinity, maxPenetration = 0f;
        bool hitAnimation = false, ignored = false;
        Invoke(enemy, "BeginFall"); enemy.enabled = true;
        float deadline = Time.time + settings.fall.windup + settings.fall.fallDuration + settings.fall.holdDuration + .5f;
        while (Time.time < deadline)
        {
            yield return new WaitForEndOfFrame();
            float foot = controller.transform.TransformPoint(controller.center).y - controller.height * controller.transform.lossyScale.y * .5f;
            object[] groundArgs = { controller.transform.position, Vector3.zero };
            if ((bool)typeof(Test_BossPatternController).GetMethod("TryGround", Private).Invoke(enemy, groundArgs))
                minFoot = Mathf.Min(minFoot, foot - ((Vector3)groundArgs[1]).y);
            if (enemy.Phase == Test_BossPatternController.PatternPhase.FallHold && Physics.ComputePenetration(controller, controller.transform.position, controller.transform.rotation,
                enemy.Body, enemy.Body.transform.position, enemy.Body.transform.rotation, out Vector3 direction, out float distance))
                maxPenetration = Mathf.Max(maxPenetration, distance);
            if (game.PlayerHealth.Current < before && player.AnimationControl.SelectedState == "HIT") hitAnimation = true;
            ignored |= Physics.GetIgnoreCollision(controller, enemy.Body);
            if (enemy.Phase == Test_BossPatternController.PatternPhase.Rising) break;
        }
        enemy.enabled = false; player.InputEnabled = false;
        float damage = before - game.PlayerHealth.Current;
        float expected = enemy.IsMinion ? settings.combat.minionFallDamage : settings.combat.bossFallDamage;
        float finalLateral = Vector3.Dot(player.transform.position - enemy.transform.position, enemy.transform.right);
        if (Mathf.Abs(damage - expected) > .01f) failures.Add(stage + " damage=" + damage);
        if (minFoot < -.1f) failures.Add(stage + " below_ground=" + minFoot);
        if (maxPenetration > .05f) failures.Add(stage + " penetration=" + maxPenetration);
        if (ignored) failures.Add(stage + " collision_pair_left_ignored");
        if (!hitAnimation) failures.Add(stage + " no_hit_animation");
        float pushed = Mathf.Abs(finalLateral - Vector3.Dot(start - enemy.transform.position, enemy.transform.right));
        if (pushed < settings.fall.hitSidePushDistance - .1f) failures.Add(stage + " insufficient_push=" + pushed);
        results.Add(new { stage, damage, minFoot, maxPenetration, finalLateral, pushed, hitAnimation, ignored }); Save(false);
    }

    private static object Invoke(object instance, string method, params object[] values) => instance.GetType().GetMethod(method, Private).Invoke(instance, values);
    private void Save(bool completed) => System.IO.File.WriteAllText(Application.dataPath + "/Temp/BossPatterns~/Test_FallSidePushReport.json",
        Newtonsoft.Json.JsonConvert.SerializeObject(new { stage, completed, failures, results }, Newtonsoft.Json.Formatting.Indented));
}
