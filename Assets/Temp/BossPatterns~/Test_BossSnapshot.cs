using System.Linq;
using UnityEngine;
using UnityEditor;
using Cheon.Prototype.MountainPass;
using Cheon.Tests.BossPatterns;

public static class Test_BossSnapshot
{
    public static object Main()
    {
        Application.runInBackground = true;
        var settings = Object.FindFirstObjectByType<Test_BossPatternSettings>();
        var game = Object.FindFirstObjectByType<PassGameFlow>();
        var texture = (TextureImporter)AssetImporter.GetAtPath("Assets/Test/BossPatterns/Textures/Test_T_BossDust.png");
        return Newtonsoft.Json.JsonConvert.SerializeObject(new
        {
            time = Time.time, frame = Time.frameCount, game = game.State.ToString(), running = game.IsRunning, scale = Time.timeScale,
            player = new[] { game.Player.position.x, game.Player.position.y, game.Player.position.z },
            enemies = settings.Enemies.Select(enemy => new { enemy.name, phase = enemy.Phase.ToString(), elapsed = enemy.PhaseElapsed,
                position = new[] { enemy.transform.position.x, enemy.transform.position.y, enemy.transform.position.z },
                forward = new[] { enemy.transform.forward.x, enemy.transform.forward.z },
                target = new[] { enemy.LandingPosition.x, enemy.LandingPosition.y, enemy.LandingPosition.z },
                bodyScale = new[] { enemy.Body.transform.lossyScale.x, enemy.Body.transform.lossyScale.y, enemy.Body.transform.lossyScale.z },
                radius = enemy.Body.radius, height = enemy.Body.height,
                dust = enemy.GetComponentsInChildren<ParticleSystem>().Select(item => new { count = item.particleCount, playing = item.isPlaying, stopped = item.isStopped }),
                enemy.JumpsCompleted, enemy.FallsCompleted, enemy.TurnsCompleted, enemy.SummonsCompleted }),
            defaultTextureMax = texture.maxTextureSize
        });
    }
}
