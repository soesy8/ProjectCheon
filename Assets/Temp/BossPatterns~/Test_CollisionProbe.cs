using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Cheon.Prototype.MountainPass;
using Cheon.Tests.BossPatterns;

public class Test_CollisionProbe : MonoBehaviour
{
    public static object Main()
    {
        Application.runInBackground = true;
        var probe = new GameObject("Test_CollisionProbe").AddComponent<Test_CollisionProbe>();
        probe.StartCoroutine(probe.Run()); return "충돌 경로 확인";
    }
    private IEnumerator Run()
    {
        var settings = Object.FindFirstObjectByType<Test_BossPatternSettings>();
        var game = Object.FindFirstObjectByType<PassGameFlow>();
        game.BeginBossChallenge(); game.Entrance.SkipPresentation(); game.PlayerHealth.Invulnerable = true;
        var boss = settings.MainBoss; boss.enabled = false;
        var player = game.Player.GetComponent<PassPlayerMovement>(); player.InputEnabled = false;
        player.Teleport(boss.transform.position + boss.transform.forward * 7f, Quaternion.identity);
        typeof(Test_BossPatternController).GetMethod("SpawnMinions", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(boss, null);
        foreach (var enemy in settings.Enemies) enemy.enabled = false;
        yield return new WaitForFixedUpdate();
        yield return null;
        var results = new List<object>();
        foreach (var enemy in settings.Enemies)
        {
            Vector3 forward = enemy.transform.forward;
            player.Teleport(enemy.transform.position + forward * 3f, Quaternion.LookRotation(-forward));
            Physics.SyncTransforms();
            var character = player.GetComponent<CharacterController>();
            bool directHit = enemy.Body.Raycast(new Ray(player.transform.position + Vector3.up, -forward), out var hit, 6f);
            var flags = character.Move(-forward * 6f);
            results.Add(new { enemy.name, directHit, distance = hit.distance, flags = flags.ToString(),
                along = Vector3.Dot(player.transform.position - enemy.transform.position, forward), enemyLayer = enemy.Body.gameObject.layer,
                playerLayer = player.gameObject.layer, bodyActive = enemy.Body.gameObject.activeInHierarchy, bodyEnabled = enemy.Body.enabled,
                bodyTrigger = enemy.Body.isTrigger, bodyScale = enemy.Body.transform.lossyScale.ToString(),
                detect = enemy.GetComponent<Rigidbody>().detectCollisions });
        }
        System.IO.File.WriteAllText(Application.dataPath + "/Temp/BossPatterns~/Test_CollisionReport.json", Newtonsoft.Json.JsonConvert.SerializeObject(results, Newtonsoft.Json.Formatting.Indented));
    }
}
