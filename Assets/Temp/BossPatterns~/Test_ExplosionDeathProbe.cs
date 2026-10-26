using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Cheon.Prototype.MountainPass;
using Cheon.Tests.BossPatterns;

public class Test_ExplosionDeathProbe : MonoBehaviour
{
    private Test_BossPatternSettings settings;
    private PassGameFlow game;
    private PassPlayerMovement player;
    private readonly List<string> failures = new List<string>();
    private readonly List<object> results = new List<object>();
    private string stage = "starting";
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    public static object Main()
    {
        Application.runInBackground = true;
        var probe = new GameObject("Test_ExplosionDeathProbe").AddComponent<Test_ExplosionDeathProbe>();
        probe.StartCoroutine(probe.Run()); return "원형 5연속 폭발·본체/잡몹 사망·재도전 검증 시작";
    }

    private IEnumerator Run()
    {
        settings = FindFirstObjectByType<Test_BossPatternSettings>(); game = FindFirstObjectByType<PassGameFlow>();
        player = game.Player.GetComponent<PassPlayerMovement>();
        game.BeginBossChallenge(); game.Entrance.SkipPresentation(); Time.timeScale = 1f;
        player.InputEnabled = false;
        yield return CheckExplosion();
        yield return CheckDeaths();
        stage = "complete"; Save(true);
    }

    private IEnumerator CheckExplosion()
    {
        stage = "explosion"; Save(false);
        var boss = settings.MainBoss; boss.enabled = false;
        player.Teleport(boss.transform.position + boss.transform.forward * 6f, Quaternion.identity);
        game.PlayerHealth.Restore(); game.View.LockBoss(false);
        float health = game.PlayerHealth.Current;
        Invoke(boss, "BeginCrack"); boss.enabled = true;
        float start = Time.time, began = -1, maxForward = 0, minBackward = 0;
        var times = new List<float>(); int previousSteps = 0; bool captured = false;
        float deadline = start + 5;
        while (boss.CracksCompleted == 0 && Time.time < deadline)
        {
            float tilt = Mathf.DeltaAngle(0, boss.Body.transform.localEulerAngles.x);
            if (boss.Phase == Test_BossPatternController.PatternPhase.CrackWindup)
            {
                maxForward = Mathf.Max(maxForward, tilt);
                if (FindObjectsByType<LineRenderer>(FindObjectsSortMode.None).Any(line => line.name == "Test_ActiveGroundExplosion")) failures.Add("爆発前にリング生成");
            }
            if (boss.Phase == Test_BossPatternController.PatternPhase.CrackActive)
            {
                if (began < 0) began = Time.time;
                minBackward = Mathf.Min(minBackward, tilt);
            }
            if (boss.GroundCrack.StepsEmitted > previousSteps)
            {
                times.Add(Time.time - start); previousSteps = boss.GroundCrack.StepsEmitted;
            }
            if (!captured && previousSteps == 5 && Time.time - began > .68f)
            {
                var rings = FindObjectsByType<LineRenderer>(FindObjectsSortMode.None).Where(line => line.name == "Test_ActiveGroundExplosion").ToArray();
                if (rings.Length != 5 || rings.Any(line => !line.loop || line.positionCount != 48)) failures.Add("円形5爆発の構成エラー");
                foreach (var ring in rings)
                {
                    var points = new Vector3[ring.positionCount]; ring.GetPositions(points);
                    Vector3 center = points.Aggregate(Vector3.zero, (sum, p) => sum + p) / points.Length;
                    var radii = points.Select(p => Vector3.ProjectOnPlane(p - center, Vector3.up).magnitude).ToArray();
                    if (radii.Max() - radii.Min() > .03f) failures.Add("非円形の爆発");
                }
                Capture(boss, "Test_ForwardExplosions.png", true); captured = true;
            }
            yield return null;
        }
        boss.enabled = false;
        float damage = health - game.PlayerHealth.Current;
        if (previousSteps != 5 || times.Count != 5) failures.Add("5連続爆発失敗");
        for (int index = 1; index < times.Count; index++)
            if (Math.Abs(times[index] - times[index - 1] - .15f) > .06f) failures.Add("爆発間隔エラー");
        if (began < 0 || Math.Abs(began - start - .5f) > .08f || maxForward < 15 || minBackward > -25) failures.Add("前傾→反動モーションエラー");
        if (Math.Abs(damage - settings.crack.damage) > .01f) failures.Add("爆発ダメージエラー: " + damage);
        results.Add(new { stage, times, windup = began - start, maxForward, minBackward, damage, captured }); Save(false);
    }

    private IEnumerator CheckDeaths()
    {
        stage = "deaths"; Save(false);
        game.BeginBossChallenge(); game.Entrance.SkipPresentation(); player.InputEnabled = false; game.PlayerHealth.Invulnerable = true;
        var boss = settings.MainBoss; boss.enabled = false;
        player.Teleport(boss.transform.position + boss.transform.forward * 8f, Quaternion.identity);
        Invoke(boss, "SpawnMinions"); yield return null;
        var minis = settings.Enemies.Where(enemy => enemy.IsMinion).ToArray();
        foreach (var mini in minis) mini.enabled = false;
        var model = (Transform)typeof(PassBossController).GetField("visual", Private).GetValue(boss);
        var renderers = model.GetComponentsInChildren<Renderer>();
        var original = renderers.Select(renderer => renderer.sharedMaterials).ToArray();
        // 개별 잡몹 사망과 본체 사망에 따른 동시 사망을 각각 검증한다.
        minis[0].Health.Kill(); yield return new WaitForSeconds(.3f);
        if (!minis[0].DeathVfx.IsPlaying || !minis[0].gameObject.activeInHierarchy) failures.Add("単独ミニ死亡効果失敗");
        boss.Health.Kill(); float diedAt = Time.time;
        if (!minis[1].Health.IsDead || !minis[1].DeathVfx.IsPlaying) failures.Add("本体連動死亡効果失敗");
        if (boss.Body.enabled || minis.Any(mini => mini.Body.enabled)) failures.Add("死亡後当たり判定残留");
        yield return new WaitForSeconds(.6f);
        Capture(boss, "Test_JangseungDeathTint.png", false);
        float tint = boss.DeathVfx.TintProgress;
        stage = "death_tint"; Save(false);
        if (tint < .5f || boss.DeathVfx.ModelHidden) failures.Add("ノイズ着色フェーズ失敗");
        if (renderers.Any(renderer => renderer.sharedMaterials.Any(material => material.shader.name != "Cheon/Tests/BossNoiseDissolve"))) failures.Add("死亡シェーダー未適用");
        yield return new WaitForSeconds(.9f);
        Capture(boss, "Test_JangseungDeathDissolve.png", false);
        float dissolve = boss.DeathVfx.DissolveProgress; int powder = boss.DeathVfx.ParticlesEmitted;
        stage = "death_dissolve"; Save(false);
        if (dissolve < .35f || dissolve > .85f || powder < 30) failures.Add("透明化または粉粒子失敗");
        float deadline = diedAt + settings.death.tintDuration + settings.death.dissolveDuration + Mathf.Max(settings.death.particleTail, settings.death.particleLifetime) + .3f;
        while (Time.time < deadline) yield return null;
        stage = "death_finished"; Save(false);
        if (!boss.DeathVfx.ModelHidden || renderers.Any(renderer => renderer.enabled)) failures.Add("死亡モデル非表示失敗");
        if (minis.Any(mini => mini != null)) failures.Add("死亡雑魚削除失敗");
        if (FindObjectsByType<LineRenderer>(FindObjectsSortMode.None).Any(line => line.name == "Test_ActiveGroundExplosion")) failures.Add("死亡後爆発残留");
        game.BeginBossChallenge(); game.Entrance.SkipPresentation(); yield return null;
        boss.enabled = false; player.InputEnabled = false;
        bool restored = !boss.DeathVfx.IsPlaying && !boss.DeathVfx.ModelHidden && boss.Body.enabled && !boss.Health.IsDead;
        for (int index = 0; index < renderers.Length; index++)
            restored &= renderers[index].enabled && renderers[index].sharedMaterials.SequenceEqual(original[index]);
        if (!restored) failures.Add("再挑戦の外観復元失敗");
        if (Resources.FindObjectsOfTypeAll<Material>().Any(material => material.name == "Test_DeathDissolve_Instance")) failures.Add("死亡専用マテリアル残留");
        // 소멸 중 재도전해도 잔여 효과를 없애고 외형을 복구하는지 확인한다.
        boss.Health.Kill(); yield return new WaitForSeconds(.2f);
        game.BeginBossChallenge(); game.Entrance.SkipPresentation(); yield return null;
        boss.enabled = false; player.InputEnabled = false;
        if (boss.DeathVfx.IsPlaying || boss.DeathVfx.ModelHidden || !boss.Body.enabled) failures.Add("途中再挑戦復元失敗");
        results.Add(new { stage, tint, dissolve, powder, restored, survivingMinis = settings.Enemies.Count(enemy => enemy.IsMinion), state = game.State.ToString() }); Save(false);
    }

    private static void Capture(Test_BossPatternController boss, string filename, bool explosion)
    {
        var camera = new GameObject("Test_VfxCaptureCamera").AddComponent<Camera>(); camera.enabled = false;
        Vector3 focus = boss.transform.position + Vector3.up * (explosion ? 4f : 5.5f) + boss.transform.forward * (explosion ? 4f : 0f);
        camera.transform.position = focus + boss.transform.right * (explosion ? 15f : 11f) + Vector3.up * (explosion ? 12f : 3f) + boss.transform.forward * (explosion ? 15f : 17f);
        camera.transform.LookAt(focus); camera.fieldOfView = 50f; camera.farClipPlane = 150f; camera.clearFlags = CameraClearFlags.Skybox;
        var target = new RenderTexture(1024, 576, 24); var previous = RenderTexture.active;
        camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
        var texture = new Texture2D(1024, 576, TextureFormat.RGB24, false); texture.ReadPixels(new Rect(0, 0, 1024, 576), 0, 0); texture.Apply();
        System.IO.File.WriteAllBytes(Application.dataPath + "/Temp/BossPatterns~/" + filename, texture.EncodeToPNG());
        RenderTexture.active = previous; camera.targetTexture = null; target.Release(); Destroy(target); Destroy(texture); Destroy(camera.gameObject);
    }

    private static object Invoke(object instance, string name) => instance.GetType().GetMethod(name, Private).Invoke(instance, null);
    private void Save(bool completed) => System.IO.File.WriteAllText(Application.dataPath + "/Temp/BossPatterns~/Test_ExplosionDeathReport.json",
        Newtonsoft.Json.JsonConvert.SerializeObject(new { stage, completed, frame = Time.frameCount, failures, results }, Newtonsoft.Json.Formatting.Indented));
}
