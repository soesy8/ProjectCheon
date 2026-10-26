using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cheon.Tests.PlayerReactions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class Test_GourdSpeedAdjustment
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string ScenePath = "Assets/Test/PlayerReactions/Scenes/Test_PlayerReactions.unity";

    public static string Setup()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("편집 모드에서 저장해야 합니다.");
        var scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            bool dirty = Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty);
            scene = EditorSceneManager.OpenScene(ScenePath, dirty ? OpenSceneMode.Additive : OpenSceneMode.Single);
        }
        SceneManager.SetActiveScene(scene);
        var motion = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Test_GourdDrinking>(true)).Single();
        var serialized = new SerializedObject(motion);
        serialized.FindProperty("playbackSpeed").floatValue = 2f;
        serialized.FindProperty("sippingStartProgress").floatValue = .31f;
        serialized.FindProperty("sippingEndProgress").floatValue = .68f;
        serialized.FindProperty("sippingSpeed").floatValue = 1f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(motion); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        return "꺼내기·입에 대기·수납 2배속, 실제 마시기 1배속 저장";
    }

    public static string Measure()
    {
        var p = UnityEngine.Object.FindFirstObjectByType<Test_PlayerReactionPreview>();
        var d = p.GetComponent<Test_GourdDrinking>(); var a = p.GetComponent<Test_GourdAttachment>();
        var input = p.GetComponent<Test_PlayerReactionInput>(); bool inputEnabled = input.enabled; input.enabled = false;
        var speed = typeof(Test_GourdDrinking).GetField("playbackSpeed", Private);
        var update = typeof(Test_GourdAttachment).GetMethod("LateUpdate", Private);
        var durations = new List<object>();
        float maxMouthDistance = 0f, maxForwardError = 0f;
        Dictionary<string, double> original = null, adjusted = null;
        bool stowed = false;
        try
        {
            foreach (float multiplier in new[] { 1f, 2f })
            {
                speed.SetValue(d, multiplier); p.ResetPreview(); d.PlayDrinking();
                double elapsed = 0; var phases = new Dictionary<string, double>();
                for (int frame = 0; frame < 2000 && d.IsPlaying; frame++)
                {
                    string phase = d.Phase == Test_GourdDrinking.DrinkingPhase.Drinking ?
                        d.IsSipping ? "Sipping" : d.Progress < .31f ? "RaisingToMouth" : "Lowering" : d.Phase.ToString();
                    p.AdvancePlayback(1f / 240f); update.Invoke(a, null); elapsed += 1d / 240d;
                    phases[phase] = phases.TryGetValue(phase, out double seconds) ? seconds + 1d / 240d : 1d / 240d;
                    if (multiplier != 2f) continue;
                    if (d.SipWeight > .999f) maxMouthDistance = Mathf.Max(maxMouthDistance, a.MouthDistance);
                    if (d.Phase == Test_GourdDrinking.DrinkingPhase.Drinking && d.Progress >= .9f)
                        maxForwardError = Mathf.Max(maxForwardError, Quaternion.Angle(p.CharacterAnimator.GetBoneTransform(HumanBodyBones.Head).rotation, p.CharacterAnimator.transform.rotation));
                }
                durations.Add(new { multiplier, totalSeconds = elapsed, phases });
                if (multiplier == 1f) original = phases; else { adjusted = phases; stowed = !d.IsPlaying && !a.IsInHand && a.Gourd.parent == a.BeltSocket; }
            }
            bool timingMatches = original != null && adjusted != null && original.All(pair => adjusted.ContainsKey(pair.Key) &&
                Math.Abs(adjusted[pair.Key] * (pair.Key == "Sipping" ? 1d : 2d) - pair.Value) < .03d);
            return Newtonsoft.Json.JsonConvert.SerializeObject(new { passed = timingMatches && stowed && maxMouthDistance < .005f && maxForwardError < .1f,
                durations, stowed, maxMouthDistance, maxForwardError });
        }
        finally { speed.SetValue(d, 2f); p.ResetPreview(); input.enabled = inputEnabled; }
    }
}
