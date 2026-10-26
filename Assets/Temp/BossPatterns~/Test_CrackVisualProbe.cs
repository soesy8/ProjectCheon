using System.Collections;
using System.Reflection;
using UnityEngine;
using Cheon.Prototype.MountainPass;
using Cheon.Tests.BossPatterns;

public class Test_CrackVisualProbe : MonoBehaviour
{
    public static object Main()
    {
        Application.runInBackground = true; Time.timeScale = 1f;
        var probe = new GameObject("Test_CrackVisualProbe").AddComponent<Test_CrackVisualProbe>();
        probe.StartCoroutine(probe.Run()); return "지면 균열 VFX 화면 검증 시작";
    }
    private IEnumerator Run()
    {
        var settings = FindFirstObjectByType<Test_BossPatternSettings>();
        var game = FindFirstObjectByType<PassGameFlow>();
        game.BeginBossChallenge(); game.Entrance.SkipPresentation(); game.PlayerHealth.Invulnerable = true;
        var boss = settings.MainBoss; boss.enabled = true;
        var player = game.Player.GetComponent<PassPlayerMovement>(); player.InputEnabled = false;
        player.Teleport(boss.transform.position + boss.transform.forward * 6f, Quaternion.identity);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(Test_BossPatternController).GetField("openingReadyAt", flags).SetValue(boss, 0f);
        typeof(Test_BossPatternController).GetField("fallReadyAt", flags).SetValue(boss, float.PositiveInfinity);
        typeof(Test_BossPatternController).GetField("jumpReadyAt", flags).SetValue(boss, float.PositiveInfinity);
        float deadline = Time.time + 5f;
        while (boss.GroundCrack.StepsEmitted < 5 && Time.time < deadline) yield return null;
        yield return new WaitForSeconds(.05f);
        var camera = new GameObject("Test_CrackValidationCamera").AddComponent<Camera>(); camera.enabled = false;
        Vector3 focus = boss.transform.position + boss.transform.forward * 6f;
        camera.transform.position = focus + boss.transform.right * 10f + Vector3.up * 12f - boss.transform.forward * 3f;
        camera.transform.LookAt(focus); camera.fieldOfView = 55f; camera.farClipPlane = 150f;
        camera.clearFlags = CameraClearFlags.Skybox;
        System.IO.File.WriteAllText(Application.dataPath + "/Temp/BossPatterns~/Test_CrackVisualReport.json",
            Newtonsoft.Json.JsonConvert.SerializeObject(new { steps = boss.GroundCrack.StepsEmitted, shader = settings.crack.material.shader.name }));
        Time.timeScale = 0;
    }
}
