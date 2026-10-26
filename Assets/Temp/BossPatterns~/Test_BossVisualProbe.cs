using System.Collections;
using System.Reflection;
using UnityEngine;
using Cheon.Tests.BossPatterns;

public class Test_BossVisualProbe : MonoBehaviour
{
    public static object Main()
    {
        Application.runInBackground = true;
        var probe = new GameObject("Test_VisualProbe").AddComponent<Test_BossVisualProbe>();
        probe.StartCoroutine(probe.Run()); return "먼지와 누운 몸체 화면 검증";
    }
    private IEnumerator Run()
    {
        var boss = FindFirstObjectByType<Test_BossPatternSettings>().MainBoss;
        boss.enabled = false;
        typeof(Test_BossPatternController).GetMethod("SetTilt", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(boss, new object[] { 90f });
        Physics.SyncTransforms();
        var camera = new GameObject("Test_ValidationCamera").AddComponent<Camera>();
        camera.enabled = false;
        Vector3 focus = boss.transform.position + boss.transform.forward * 5f;
        camera.transform.position = focus + boss.transform.right * 10f - boss.transform.forward * 6f + Vector3.up * 10f;
        camera.transform.LookAt(focus);
        camera.fieldOfView = 55f; camera.farClipPlane = 150f;
        camera.clearFlags = CameraClearFlags.Skybox;
        boss.GetComponent<Test_BossImpactDust>().EmitFall(boss.Body);
        yield return new WaitForSeconds(.15f);
        var particles = boss.GetComponentInChildren<ParticleSystem>();
        System.IO.File.WriteAllText(Application.dataPath + "/Temp/BossPatterns~/Test_VisualReport.json",
            Newtonsoft.Json.JsonConvert.SerializeObject(new { count = particles.particleCount, playing = particles.isPlaying, stopped = particles.isStopped }));
        Time.timeScale = 0;
    }
}
