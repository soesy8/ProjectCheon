using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using Cheon.Prototype.MountainPass;
using Object = UnityEngine.Object;

public static class Test_FoleyTools
{
    public static object Decode()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        string output = "Assets/Temp/FoleyRefinement~/Decoded";
        Directory.CreateDirectory(output);
        int count = 0;
        foreach (string file in Directory.GetFiles("Assets/Temp/FoleyRefinementSources"))
        {
            if (!file.EndsWith(".ogg") && !file.EndsWith(".wav")) continue;
            string path = file.Replace('\\','/');
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            clip.LoadAudioData();
            float[] data = new float[clip.samples * clip.channels];
            if (!clip.GetData(data, 0)) throw new Exception(path);
            using (var writer = new BinaryWriter(File.Create(output + "/" + Path.GetFileNameWithoutExtension(path) + ".wav")))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + data.Length * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                writer.Write((short)1); writer.Write((short)clip.channels); writer.Write(clip.frequency);
                writer.Write(clip.frequency * clip.channels * 2); writer.Write((short)(clip.channels * 2)); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(data.Length * 2);
                foreach (float sample in data) writer.Write((short)(Mathf.Clamp(sample, -1, 1) * 32767));
            }
            count++;
        }
        return new { decoded = count };
    }
    static async Task Until(Func<bool> condition, float limit = 10)
    {
        float end = Time.realtimeSinceStartup + limit;
        while (!condition()) { if (Time.realtimeSinceStartup > end) throw new Exception("Timed out"); await Task.Delay(1); }
    }
    public static async Task<object> ProbeAttack()
    {
        if (!Application.isPlaying) throw new Exception("Play Mode required");
        var game = Object.FindFirstObjectByType<PassGameFlow>();
        game.BeginBossChallenge();
        await Until(() => !game.IsTransitioning);
        game.Entrance.SkipPresentation();
        await Until(() => game.IsRunning);
        var boss = (PassBossPatternController)game.Boss;
        boss.enabled = false;
        boss.Health.Invulnerable = false;
        game.PlayerHealth.Invulnerable = true;
        var movement = Object.FindFirstObjectByType<PassPlayerMovement>();
        var anim = movement.AnimationControl;
        var fan = movement.GetComponent<PassFanAttachment>();
        if (fan == null) fan = movement.GetComponentInChildren<PassFanAttachment>();
        var combat = movement.GetComponent<PassFanAttackCombat>();
        var oldAudio = movement.GetComponent<PassPlayerAudio>();
        var pending = typeof(PassPlayerAudio).GetField("pendingSwing", BindingFlags.NonPublic | BindingFlags.Instance);
        Vector3 position = boss.transform.position + boss.transform.forward * 2.3f + Vector3.up * .04f;
        movement.Teleport(position, Quaternion.LookRotation(Vector3.ProjectOnPlane(boss.transform.position - position, Vector3.up)));
        if (!anim.IsCombatRequested) anim.TryAttackInput(1, false, false);
        await Until(() => !anim.IsEquipmentTransition && !anim.IsActionPlaying);
        await Task.Delay(120);
        int before = combat.DirectHitsLanded;
        bool accepted = anim.TryAttackInput(2, false, false);
        var rows = new List<object>();
        float swingFrame = -1;
        int frame = -1;
        bool waiting = false;
        await Until(() => {
            if (Time.frameCount == frame) return false;
            frame = Time.frameCount;
            float f = anim.NormalizedTime * anim.GetAttackMaximumFrame(2);
            int p = (int)pending.GetValue(oldAudio);
            if (p == 2) waiting = true;
            if (waiting && p == 0 && swingFrame < 0) swingFrame = f;
            Vector3 hand = fan != null ? movement.transform.InverseTransformPoint(fan.HandSocket.position) : Vector3.zero;
            rows.Add(new { frame=f, progress=anim.PlaybackProgress, handX=hand.x, handZ=hand.z, hitFrame=combat.LastHitFrame, hits=combat.DirectHitsLanded-before, pending=p });
            return anim.SelectedState != "ATTACK2" || f > 42;
        });
        var clip = anim.CharacterAnimator.runtimeAnimatorController.animationClips.FirstOrDefault(c => c.name.Contains("Attack2"));
        var result = new { accepted, oldSwingFrame=swingFrame, hitFrame=combat.LastHitFrame, hits=combat.DirectHitsLanded-before,
            fps=clip != null ? clip.frameRate : -1, speed=anim.attack2Speed, rows };
        File.WriteAllText("Assets/Temp/FoleyRefinement~/AttackBefore.json", Newtonsoft.Json.JsonConvert.SerializeObject(result, Newtonsoft.Json.Formatting.Indented));
        return result;
    }
}
