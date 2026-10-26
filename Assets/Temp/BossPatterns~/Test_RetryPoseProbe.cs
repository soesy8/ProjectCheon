using System;
using System.Reflection;
using UnityEngine;
using Cheon.Prototype.MountainPass;
using Cheon.Tests.BossPatterns;

public static class Test_RetryPoseProbe
{
    public static object Main()
    {
        var settings = UnityEngine.Object.FindFirstObjectByType<Test_BossPatternSettings>();
        var game = UnityEngine.Object.FindFirstObjectByType<PassGameFlow>();
        var boss = settings.MainBoss;
        game.BeginBossChallenge(); game.Entrance.SkipPresentation(); boss.enabled = false;
        var visual = (Transform)typeof(PassBossController).GetField("visual", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(boss);
        Vector3 before = visual.lossyScale;
        typeof(Test_BossPatternController).GetMethod("SetTilt", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(boss, new object[] { 90f });
        game.RetryCheckpoint(); game.Entrance.SkipPresentation(); boss.enabled = false;
        Vector3 after = visual.lossyScale;
        bool correct = Vector3.Distance(before, after) < .001f;
        var report = new { before = before.ToString(), after = after.ToString(), correct };
        System.IO.File.WriteAllText(Application.dataPath + "/Temp/BossPatterns~/Test_RetryPoseReport.json", Newtonsoft.Json.JsonConvert.SerializeObject(report));
        return Newtonsoft.Json.JsonConvert.SerializeObject(report);
    }
}
