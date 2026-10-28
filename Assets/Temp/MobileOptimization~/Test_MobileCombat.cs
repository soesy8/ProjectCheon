using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using Cheon.Prototype.MountainPass;
using Newtonsoft.Json;

public static class Test_MobileCombat
{
    public static async Task<object> Main()
    {
        var errors = new List<string>();
        var game = UnityEngine.Object.FindFirstObjectByType<PassGameFlow>();
        var movement = game.Player.GetComponent<PassPlayerMovement>();
        var boss = (PassBossPatternController)game.Boss;
        var floor = (Renderer)new SerializedObject(game).FindProperty("bossDetectionFloor").objectReferenceValue;
        movement.Teleport(floor.bounds.center + Vector3.up * .2f + Vector3.back * 8f, Quaternion.identity);
        movement.InputEnabled = false;
        game.BeginEncounter();
        await Task.Delay(1800);
        if (game.State == PassGameFlow.RunState.Cinematic) game.Entrance.SkipPresentation();
        await Task.Delay(800);
        game.PlayerHealth.Invulnerable = true;
        boss.enabled = false;
        int before = UnityEngine.Object.FindObjectsByType<PassBossPatternController>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        typeof(PassBossPatternController).GetMethod("SpawnMinions", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(boss, null);
        var minions = UnityEngine.Object.FindObjectsByType<PassBossPatternController>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(b => b.IsMinion).ToArray();
        int active = minions.Count(b => b.gameObject.activeSelf);
        foreach (var enemy in minions) enemy.enabled = false;
        if (active != 2 || minions.Length + 1 != before) errors.Add("Summoning did not reuse two prewarmed enemies.");
        movement.AnimationControl.EnterCombat();
        await Task.Delay(1400);
        bool attack = movement.AnimationControl.TryPlayAttack(3);
        await Task.Delay(450);
        var vfx = UnityEngine.Object.FindFirstObjectByType<PassFanAttackVfx>();
        if (!attack) errors.Add("Third attack was rejected.");
        await Task.Delay(1700);
        if (game.State != PassGameFlow.RunState.Playing) errors.Add("Combat state did not remain playable.");
        var result = new { success = errors.Count == 0, errors, activeMinions = active, reusedPool = minions.Length + 1 == before, thirdAttack = attack, frame = Time.frameCount, state = game.State.ToString(), windVertices = UnityEngine.Object.FindObjectsByType<PassFanSweepMesh>(FindObjectsInactive.Include, FindObjectsSortMode.None).Select(w => w.Mesh.vertexCount).ToArray() };
        File.WriteAllText("Assets/Temp/MobileOptimization~/CombatVerification.json", JsonConvert.SerializeObject(result, Formatting.Indented));
        return result;
    }
}
