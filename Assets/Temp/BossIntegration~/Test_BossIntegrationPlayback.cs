using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Cheon.Prototype.MountainPass;

public static class Test_BossIntegrationPlayback
{
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
    private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args);

    public static object Main()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Play Mode required");
        var boss = UnityEngine.Object.FindFirstObjectByType<PassBossPatternController>();
        var game = UnityEngine.Object.FindFirstObjectByType<PassGameFlow>();
        var settings = UnityEngine.Object.FindFirstObjectByType<PassBossPatternSettings>();
        if (boss == null || game == null || settings == null) throw new Exception("Expected shared boss scene");
        Application.runInBackground = true;
        game.BeginBossChallenge();
        if (game.Entrance != null) game.Entrance.SkipPresentation();
        game.PlayerHealth.Configure(10000f, 0f); game.PlayerHealth.Restore();
        var movement = game.Player.GetComponent<PassPlayerMovement>();
        movement.InputEnabled = false;
        Set(boss, "openingReadyAt", float.PositiveInfinity);
        SetPlayerInFront(boss, movement);
        var data = new Dictionary<string, object> { ["scene"] = boss.gameObject.scene.path, ["firstFrame"] = Time.frameCount, ["success"] = false, ["stage"] = "crack" };
        var observedStates = new HashSet<string>();
        string path = "Assets/Temp/BossIntegration~/" + Path.GetFileNameWithoutExtension(boss.gameObject.scene.path) + ".playback.json";
        double began = EditorApplication.timeSinceStartup;
        float stageBegan = Time.time;
        int stage = 0;
        Call(boss, "BeginCrack");
        EditorApplication.CallbackFunction tick = null;
        Action save = () => File.WriteAllText(path, Newtonsoft.Json.JsonConvert.SerializeObject(data, Newtonsoft.Json.Formatting.Indented));
        tick = () =>
        {
            try
            {
                if (!EditorApplication.isPlaying) throw new Exception("Play Mode ended early");
                if (EditorApplication.timeSinceStartup - began > 100) throw new Exception("Playback timed out in stage " + stage + ": " + boss.Phase);
                observedStates.Add(movement.AnimationControl.SelectedState);
                switch (stage)
                {
                    case 0:
                        if (boss.CracksCompleted < 1) return;
                        data["crackSteps"] = boss.GroundCrack.StepsEmitted;
                        if (boss.GroundCrack.StepsEmitted != settings.crack.stepCount) throw new Exception("Incomplete crack steps");
                        if (!(bool)Call(boss, "TryBeginJump")) throw new Exception("No safe jump landing");
                        stage = 1; data["stage"] = "jump"; save(); break;
                    case 1:
                        if (boss.JumpsCompleted < 1) return;
                        data["jumps"] = boss.JumpsCompleted;
                        SetPlayerInFront(boss, movement);
                        Call(boss, "BeginFall"); stage = 2; data["stage"] = "fall and reactions"; save(); break;
                    case 2:
                        if (boss.FallsCompleted < 1 || movement.AnimationControl.IsReactionPlaying) return;
                        data["heavyHit"] = observedStates.Contains("HEAVY_HIT"); data["getUp"] = observedStates.Contains("GET_UP");
                        if (!observedStates.Contains("HEAVY_HIT") || !observedStates.Contains("GET_UP")) throw new Exception("Missing heavy hit / get-up playback");
                        Call(boss, "SpawnMinions"); stage = 3; stageBegan = Time.time; data["stage"] = "summon"; save(); break;
                    case 3:
                        if (Time.time - stageBegan < .25f) return;
                        var minions = settings.Enemies.Where(e => e.IsMinion && e.IsAlive).ToArray();
                        data["minions"] = minions.Length;
                        if (minions.Length != 2) throw new Exception("Summon failed: " + minions.Length);
                        foreach (var minion in minions) { Set(minion, "openingReadyAt", float.PositiveInfinity); Set(minion, "jumpReadyAt", 0f); }
                        Set(boss, "fallReadyAt", 0f);
                        if (!boss.LinkedAttack.TryBegin()) throw new Exception("Linked attack did not start");
                        stage = 4; data["stage"] = "linked attack"; save(); break;
                    case 4:
                        if (boss.LinkedAttack.CompletedCount < 1) return;
                        data["linkedAttacks"] = boss.LinkedAttack.CompletedCount;
                        data["linkedJumps"] = boss.LinkedAttack.JumpsOrdered;
                        data["cooldownRemaining"] = boss.LinkedAttack.ReadyAt - Time.time;
                        if (Mathf.Abs(boss.LinkedAttack.ReadyAt - Time.time - 6f) > .2f) throw new Exception("Incorrect linked cooldown");
                        if (boss.LinkedAttack.TryBegin()) throw new Exception("Linked cooldown was bypassed");
                        boss.Health.Kill(); stage = 5; data["stage"] = "boss death"; save(); break;
                    case 5:
                        if (!boss.DeathVfx.ModelHidden) return;
                        data["deathPowder"] = boss.DeathVfx.ParticlesEmitted;
                        data["modelHidden"] = boss.DeathVfx.ModelHidden;
                        game.BeginBossChallenge(); if (game.Entrance != null) game.Entrance.SkipPresentation();
                        Set(boss, "openingReadyAt", float.PositiveInfinity);
                        data["reset"] = boss.Phase == PassBossPatternController.PatternPhase.Idle && boss.LivingMinions == 0 && !boss.DeathVfx.ModelHidden && boss.Body.enabled && boss.LinkedAttack.CompletedCount == 0;
                        if (!(bool)data["reset"]) throw new Exception("Encounter reset failed");
                        game.PlayerHealth.Invulnerable = false;
                        game.PlayerHealth.TakeDamage(100000f, true);
                        stage = 6; stageBegan = Time.time; data["stage"] = "player death"; save(); break;
                    case 6:
                        if (Time.time - stageBegan < .5f) return;
                        data["playerDeath"] = movement.AnimationControl.SelectedState == "DEATH";
                        if (!(bool)data["playerDeath"]) throw new Exception("Death animation missing");
                        data["lastFrame"] = Time.frameCount;
                        if (Time.frameCount <= (int)data["firstFrame"] + 10) throw new Exception("Player loop did not advance");
                        data["success"] = true; data["stage"] = "completed";
                        EditorApplication.update -= tick; save(); break;
                }
            }
            catch (Exception exception)
            {
                data["error"] = exception.ToString(); data["stage"] = "failed";
                EditorApplication.update -= tick; save();
            }
        };
        EditorApplication.update += tick;
        save();
        return new { path, scene = boss.gameObject.scene.path, frame = Time.frameCount, state = game.State.ToString() };
    }

    private static void SetPlayerInFront(PassBossPatternController boss, PassPlayerMovement movement)
    {
        Vector3 candidate = boss.transform.position + boss.transform.forward * 5f;
        var hits = Physics.RaycastAll(candidate + Vector3.up * 5f, Vector3.down, 10f, ~0, QueryTriggerInteraction.Ignore);
        var ground = hits.Where(h => h.normal.y > .65f && h.collider.GetComponentInParent<PassHealth>() == null).OrderBy(h => h.distance).First();
        movement.Teleport(ground.point, Quaternion.LookRotation(-boss.transform.forward));
        movement.InputEnabled = false;
    }
}
