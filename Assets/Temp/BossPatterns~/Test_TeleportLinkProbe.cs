using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using Cheon.Prototype.MountainPass;
using Cheon.Tests.BossPatterns;

public class Test_TeleportLinkProbe : MonoBehaviour
{
    private Test_BossPatternSettings settings;
    private PassGameFlow game;
    private PassPlayerMovement player;
    private Test_BossPatternController boss;
    private Test_BossPatternController[] minis;
    private readonly List<string> failures = new List<string>();
    private readonly List<object> results = new List<object>();
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private string stage = "starting";

    public static object Main()
    {
        if (!Application.isPlaying || SceneManager.GetActiveScene().path != "Assets/Test/BossPatterns/Scenes/Test_BossPatterns.unity") throw new Exception("보스 테스트 씬 Play Mode 전용");
        Application.runInBackground = true;
        var probe = new GameObject("Test_TeleportLinkProbe").AddComponent<Test_TeleportLinkProbe>();
        probe.StartCoroutine(probe.Run()); return "순간이동·0.5초 연속 점프·착지 후 본체 타격·중단/재도전 검증 시작";
    }

    private IEnumerator Run()
    {
        settings = FindFirstObjectByType<Test_BossPatternSettings>(); game = FindFirstObjectByType<PassGameFlow>();
        player = game.Player.GetComponent<PassPlayerMovement>(); boss = settings.MainBoss;
        yield return Prepare();
        yield return CheckSequence();
        yield return Prepare();
        yield return CheckBlockedArrival();
        yield return Prepare();
        yield return CheckDeathAndRetry();
        stage = "complete"; Save(true);
        boss.enabled = false; player.InputEnabled = false;
    }

    private IEnumerator Prepare()
    {
        game.BeginBossChallenge(); game.Entrance.SkipPresentation(); Time.timeScale = 1f;
        boss.enabled = false; player.InputEnabled = false; game.PlayerHealth.Invulnerable = true; game.View.LockBoss(false);
        player.Teleport(boss.transform.position + boss.transform.forward * 6f, Quaternion.identity);
        yield return null;
        Invoke(boss, "SpawnMinions"); yield return null;
        minis = settings.Enemies.Where(enemy => enemy.IsMinion && enemy.IsAlive).ToArray();
        if (minis.Length != 2) throw new Exception("검증 잡몹 소환 실패");
        for (int index = 0; index < minis.Length; index++)
        {
            var mini = minis[index];
            mini.transform.position = boss.transform.position + boss.transform.right * (index == 0 ? -7f : 7f) - boss.transform.forward * 3f;
            Set(mini, "openingReadyAt", Time.time + 100f); Set(mini, "jumpReadyAt", 0f);
        }
        Set(boss, "openingReadyAt", 0f); Set(boss, "jumpReadyAt", float.PositiveInfinity); Set(boss, "crackReadyAt", float.PositiveInfinity); Set(boss, "fallReadyAt", 0f);
        Physics.SyncTransforms(); yield return new WaitForEndOfFrame();
    }

    private IEnumerator CheckSequence()
    {
        stage = "sequence"; Save(false);
        Set(boss, "fallReadyAt", Time.time + 2f);
        if (boss.LinkedAttack.TryBegin()) Fail("본체 쿨타임이 남았는데 연계 시작");
        Set(boss, "fallReadyAt", 0f); boss.enabled = true;
        float started = Time.time, hiddenAt = -1, fallingAt = -1;
        var hidden = new bool[2]; var appeared = new bool[2]; var hops = new float[2]; var arrivals = new float[2];
        var facing = new float[2]; var destinations = new Vector3[2];
        var phases = new Dictionary<int, Test_BossPatternController.PatternPhase>();
        var events = new List<object>();
        bool smokeCaptured = false, arrivalCaptured = false, movedPlayer = false;
        float deadline = Time.time + 16f;
        while (boss.LinkedAttack.CompletedCount == 0 && Time.time < deadline)
        {
            yield return new WaitForEndOfFrame();
            for (int index = 0; index < 2; index++)
            {
                var mini = minis[index];
                if (mini.Phase == Test_BossPatternController.PatternPhase.Teleporting)
                {
                    hops[index] = Mathf.Max(hops[index], mini.transform.position.y - settings.ArenaCenter.y);
                    if (mini.TeleportVfx.ModelHidden)
                    {
                        if (!hidden[index] && hiddenAt < 0) hiddenAt = Time.time;
                        hidden[index] = true;
                        if (!mini.IsAlive || mini.Body.enabled || Model(mini).GetComponentsInChildren<Renderer>().Any(renderer => renderer.enabled)) Fail("숨김 중 생존/충돌/모델 상태 오류");
                    }
                }
                // 캡처로 프레임이 길어지면 재등장과 착지가 같은 프레임일 수 있으므로 완료 상태도 관찰한다.
                if (mini.TeleportVfx.Succeeded && !appeared[index])
                {
                    appeared[index] = true; arrivals[index] = Time.time - started;
                    destinations[index] = mini.TeleportVfx.Destination;
                    facing[index] = Vector3.Angle(mini.transform.forward, Vector3.ProjectOnPlane(player.transform.position - mini.transform.position, Vector3.up));
                }
                if (mini.Phase == Test_BossPatternController.PatternPhase.JumpFlight)
                {
                    float angle = Vector3.Angle(mini.transform.forward, Vector3.ProjectOnPlane(player.transform.position - mini.transform.position, Vector3.up));
                    if (Math.Abs(Time.time - mini.LastJumpStartedAt) < .04f && angle > 2f) Fail("점프 직전 플레이어 방향 추적 오류");
                }
                if (!phases.TryGetValue(mini.GetInstanceID(), out var old) || old != mini.Phase)
                { phases[mini.GetInstanceID()] = mini.Phase; events.Add(new { actor = mini.name, phase = mini.Phase.ToString(), time = Time.time - started }); }
            }
            if (!movedPlayer && hidden.All(value => value))
            {
                player.Teleport(boss.transform.position + boss.transform.forward * 6f - boss.transform.right * 2f, Quaternion.identity);
                movedPlayer = true;
            }
            if (!smokeCaptured && hiddenAt >= 0 && Time.time - hiddenAt >= .04f && minis.All(mini => mini.TeleportVfx.ModelHidden))
            { Capture("Test_LinkTeleportSmoke.png"); smokeCaptured = true; }
            if (!arrivalCaptured && appeared.All(value => value)) { Capture("Test_LinkTeleportArrival.png"); arrivalCaptured = true; }
            if (boss.FallDamageActive && fallingAt < 0) fallingAt = Time.time;
        }
        boss.enabled = false;
        foreach (var mini in minis) mini.enabled = false;
        float jumpGap = minis[1].LastJumpStartedAt - minis[0].LastJumpStartedAt;
        float hitGap = fallingAt - minis[1].LastJumpLandedAt;
        if (Math.Abs(jumpGap - .5f) > .06f) Fail("잡몹 점프 간격 오류: " + jumpGap);
        if (Math.Abs(hitGap - .5f) > .06f) Fail("두 번째 착지 후 본체 판정 시점 오류: " + hitGap);
        if (boss.LinkedAttack.CompletedCount != 1 || boss.LinkedAttack.JumpsOrdered != 2) Fail("연계 완료/점프 횟수 오류");
        if (Math.Abs(boss.LinkedAttack.ReadyAt - Time.time - settings.linkedAttack.cooldown) > .06f) Fail("연계 종료 후 쿨타임 오류");
        if (!hidden.All(value => value) || !appeared.All(value => value) || hops.Any(value => value < .1f)) Fail("작은 점프/숨김/재등장 누락");
        if (arrivals.Any(value => value >= settings.jump.minimumFlight) || facing.Any(value => value > 1f)) Fail("순간이동 시간/자동 축 정렬 오류");
        for (int index = 0; index < 2; index++)
        {
            float side = Vector3.Dot(destinations[index] - boss.transform.position, boss.transform.right);
            if (Math.Abs(Math.Abs(side) - settings.linkedAttack.teleportSideDistance) > 1.01f) Fail("보스 옆 순간이동 위치 오류");
        }
        results.Add(new { stage, jumpGap, hitGap, arrivals, facing, hops, smokeCaptured, arrivalCaptured, events }); Save(false);
    }

    private IEnumerator CheckBlockedArrival()
    {
        stage = "blocked_arrival"; Save(false);
        if (!boss.LinkedAttack.TryBegin()) { Fail("막힘 검증 연계 시작 실패"); yield break; }
        boss.enabled = true;
        float deadline = Time.time + 2f;
        while (!minis[0].TeleportVfx.ModelHidden && Time.time < deadline) yield return null;
        player.Teleport(minis[0].TeleportVfx.Destination, Quaternion.identity);
        while (boss.LinkedAttack.IsActive && Time.time < deadline) yield return null;
        boss.enabled = false;
        foreach (var mini in minis) mini.enabled = false;
        if (boss.LinkedAttack.IsActive || minis.Any(mini => mini.TeleportVfx.ModelHidden || !mini.Body.enabled || mini.Health.Invulnerable)) Fail("도착지 막힘 중단 후 상태 복원 실패");
        results.Add(new { stage, cancelled = !boss.LinkedAttack.IsActive }); Save(false);
    }

    private IEnumerator CheckDeathAndRetry()
    {
        stage = "death_retry"; Save(false);
        if (!boss.LinkedAttack.TryBegin()) { Fail("사망 검증 연계 시작 실패"); yield break; }
        boss.enabled = true;
        float deadline = Time.time + 2;
        while (!minis[0].TeleportVfx.ModelHidden && Time.time < deadline) yield return null;
        boss.Health.Kill(); yield return null;
        if (minis.Any(mini => !mini.Health.IsDead || !mini.DeathVfx.IsPlaying || mini.TeleportVfx.ModelHidden || mini.Body.enabled)) Fail("순간이동 중 본체 사망 처리 오류");
        yield return new WaitForSeconds(.2f);
        game.BeginBossChallenge(); game.Entrance.SkipPresentation(); yield return null; yield return new WaitForEndOfFrame();
        boss.enabled = false; player.InputEnabled = false;
        if (settings.Enemies.Any(enemy => enemy.IsMinion) || boss.LinkedAttack.IsActive || boss.DeathVfx.IsPlaying || !boss.Body.enabled) Fail("순간이동/사망 중 재도전 정리 실패");
        if (Resources.FindObjectsOfTypeAll<Material>().Any(material => material.name == "Test_WhiteSmoke_Instance")) Fail("흰 연기 런타임 머티리얼 잔류");
        results.Add(new { stage, cleaned = !boss.LinkedAttack.IsActive }); Save(false);
    }

    private void Capture(string filename)
    {
        var camera = new GameObject("Test_TeleportCaptureCamera").AddComponent<Camera>(); camera.enabled = false;
        Vector3 focus = boss.transform.position + Vector3.up * 5f;
        camera.transform.position = focus + boss.transform.right * 13f + Vector3.up * 6f + boss.transform.forward * 20f;
        camera.transform.LookAt(focus); camera.fieldOfView = 50; camera.farClipPlane = 150; camera.clearFlags = CameraClearFlags.Skybox;
        var target = new RenderTexture(1024, 576, 24); var previous = RenderTexture.active;
        camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
        var texture = new Texture2D(1024, 576, TextureFormat.RGB24, false); texture.ReadPixels(new Rect(0,0,1024,576),0,0); texture.Apply();
        System.IO.File.WriteAllBytes(Application.dataPath + "/Temp/BossPatterns~/" + filename, texture.EncodeToPNG());
        RenderTexture.active = previous; camera.targetTexture = null; target.Release(); Destroy(target); Destroy(texture); Destroy(camera.gameObject);
    }
    private static Transform Model(Test_BossPatternController actor) => (Transform)typeof(PassBossController).GetField("visual", Private).GetValue(actor);
    private static void Set(object actor, string field, object value) => actor.GetType().GetField(field, Private).SetValue(actor, value);
    private static object Invoke(object actor, string method) => actor.GetType().GetMethod(method, Private).Invoke(actor, null);
    private void Fail(string message) { if (!failures.Contains(message)) failures.Add(message); }
    private void Save(bool completed) => System.IO.File.WriteAllText(Application.dataPath + "/Temp/BossPatterns~/Test_TeleportLinkReport.json",
        Newtonsoft.Json.JsonConvert.SerializeObject(new { stage, completed, frame = Time.frameCount, failures, results }, Newtonsoft.Json.Formatting.Indented));
}
