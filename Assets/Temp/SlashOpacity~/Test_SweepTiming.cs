using System;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Cheon.Prototype.MountainPass;
using Cheon.Tests.FanAttackVfx;

public static class Test_SweepTiming
{
    public static object Apply()
    {
        var effect = UnityEngine.Object.FindFirstObjectByType<Test_FanAttackVfx>();
        var animation = UnityEngine.Object.FindFirstObjectByType<PassAnimationController>();
        var profile = effect.Profile;
        var serialized = new SerializedObject(animation);
        float referenceDuration = Duration(profile, animation, serialized, 3);
        float fadeRatio = profile.attack3.fadeSeconds / referenceDuration;
        Undo.RecordObject(profile, "1·2타 궤적을 3타 생성·소멸 비율에 맞춤");
        foreach (int index in new[] { 1, 2 })
        {
            var sweep = profile.GetSweep(index);
            sweep.revealTimingSourceAttack = 3;
            sweep.fadeSeconds = Duration(profile, animation, serialized, index) * fadeRatio;
        }
        profile.NotifyChanged();
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssetIfDirty(profile);
        return Inspect();
    }

    private static float Duration(Test_FanAttackVfxProfile profile, PassAnimationController animation, SerializedObject serialized, int index)
    {
        var clip = (AnimationClip)serialized.FindProperty("attack" + index + "Clip").objectReferenceValue;
        var sweep = profile.GetSweep(index);
        float speed = index == 1 ? animation.attack1Speed : index == 2 ? animation.attack2Speed : animation.attack3Speed;
        return (Mathf.Min(sweep.endFrame, animation.GetAttackEndFrame(index))
            - Mathf.Max(sweep.startFrame, animation.GetAttackStartFrame(index))) / clip.frameRate / speed;
    }

    public static async Task<object> Playback()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Play Mode required");
        var effect = UnityEngine.Object.FindFirstObjectByType<Test_FanAttackVfx>();
        var animation = UnityEngine.Object.FindFirstObjectByType<PassAnimationController>();
        var game = UnityEngine.Object.FindFirstObjectByType<PassGameFlow>();
        game.BeginBossChallenge();
        if (game.Entrance != null) game.Entrance.SkipPresentation();
        game.PlayerHealth.Invulnerable = true;
        game.Boss.gameObject.SetActive(false);
        game.Player.GetComponent<PassPlayerMovement>().InputEnabled = false;
        var serialized = new SerializedObject(animation);
        float referenceRatio = effect.Profile.attack3.fadeSeconds / Duration(effect.Profile, animation, serialized, 3);
        var reports = new System.Collections.Generic.List<object>();
        int initialFrame = Time.frameCount;
        float originalTimeScale = Time.timeScale;
        try
        {
            foreach (int index in new[] { 1, 2, 3 })
            {
                Time.timeScale = 1;
                effect.PlayPreview(index);
                double deadline = EditorApplication.timeSinceStartup + 15;
                while (animation.SelectedState != "ATTACK" + index)
                {
                    await Task.Delay(10);
                    if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Attack did not start: " + index);
                }
                // 빠른 1·2타도 생성 중간 구간을 충분히 관찰한다. 게임 시간의 비율은 유지한다.
                Time.timeScale = .25f;
                var sweep = effect.Profile.GetSweep(index);
                float ratio = sweep.fadeSeconds / Duration(effect.Profile, animation, serialized, index);
                if (Mathf.Abs(ratio - referenceRatio) > .0001f) throw new Exception("Fade ratio mismatch: " + index);
                int partialSamples = 0, fadeSamples = 0;
                bool complete = false, hidden = false;
                float maxCurveError = 0, previousHead = 0, previousOpacity = 1;
                var block = new MaterialPropertyBlock();
                while (EditorApplication.timeSinceStartup < deadline)
                {
                    await Task.Delay(10);
                    var child = effect.transform.Find("Test_C_Slash");
                    if (child == null) continue;
                    var renderer = child.GetComponent<Renderer>();
                    if (!effect.IsSweepVisible)
                    {
                        if (complete) { hidden = true; break; }
                        continue;
                    }
                    renderer.GetPropertyBlock(block);
                    float head = block.GetFloat("_SweepHead");
                    float opacity = block.GetColor("_BaseColor").a;
                    float progress = Mathf.InverseLerp(sweep.startFrame, sweep.endFrame, effect.CurrentFrame);
                    if (effect.CurrentFrame >= sweep.startFrame && effect.CurrentFrame <= sweep.endFrame)
                    {
                        float expected = effect.Profile.attack3.motionProgress.Evaluate(progress);
                        float error = Mathf.Abs(head - expected);
                        maxCurveError = Mathf.Max(maxCurveError, error);
                        if (error > .002f) throw new Exception("Reveal differs from third attack: " + index);
                        if (head < previousHead - .002f) throw new Exception("Reveal moved backwards: " + index);
                        if (head > .02f && head < .98f) partialSamples++;
                    }
                    if (head >= .999f) complete = true;
                    if (complete && opacity < .99f && opacity > .01f)
                    {
                        if (opacity > previousOpacity + .002f) throw new Exception("Fade increased after completion: " + index);
                        fadeSamples++;
                    }
                    previousHead = head;
                    previousOpacity = opacity;
                }
                if (partialSamples < 3 || fadeSamples < 2 || !complete || !hidden)
                    throw new Exception("Missing gradual reveal / fade phases: " + index + ", partial=" + partialSamples + ", fade=" + fadeSamples + ", complete=" + complete + ", hidden=" + hidden);
                reports.Add(new { attack = index, partialSamples, fadeSamples, complete, hidden, ratio,
                    fadeSeconds = sweep.fadeSeconds, maxCurveError });
            }
            var result = new { framesAdvanced = Time.frameCount - initialFrame, reports };
            File.WriteAllText("Assets/Temp/SlashOpacity~/TimingValidation.json", Newtonsoft.Json.JsonConvert.SerializeObject(result, Newtonsoft.Json.Formatting.Indented));
            return result;
        }
        finally
        {
            effect.ResumePreview();
            animation.PlayIdle();
            Time.timeScale = originalTimeScale;
        }
    }

    public static object Inspect()
    {
        var effect = UnityEngine.Object.FindFirstObjectByType<Test_FanAttackVfx>();
        var animation = UnityEngine.Object.FindFirstObjectByType<PassAnimationController>();
        var serialized = new SerializedObject(animation);
        return Enumerable.Range(1, 3).Select(index =>
        {
            var clip = (AnimationClip)serialized.FindProperty("attack" + index + "Clip").objectReferenceValue;
            var sweep = effect.Profile.GetSweep(index);
            float speed = index == 1 ? animation.attack1Speed : index == 2 ? animation.attack2Speed : animation.attack3Speed;
            float duration = (Mathf.Min(sweep.endFrame, animation.GetAttackEndFrame(index))
                - Mathf.Max(sweep.startFrame, animation.GetAttackStartFrame(index))) / clip.frameRate / speed;
            var reveal = sweep.revealTimingSourceAttack > 0 ? effect.Profile.GetSweep(sweep.revealTimingSourceAttack).motionProgress : sweep.motionProgress;
            return new { attack = index, clip = clip.name, fps = clip.frameRate, speed,
                start = sweep.startFrame, end = sweep.endFrame, duration, fadeSeconds = sweep.fadeSeconds,
                fadeRatio = sweep.fadeSeconds / duration, tail = sweep.tailLength,
                revealSource = sweep.revealTimingSourceAttack,
                curve = Enumerable.Range(0, 11).Select(step => reveal.Evaluate(step / 10f)).ToArray() };
        }).ToArray();
    }
}
