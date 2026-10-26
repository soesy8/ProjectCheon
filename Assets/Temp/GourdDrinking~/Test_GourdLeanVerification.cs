using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Cheon.Tests.PlayerReactions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class Test_GourdLeanVerification
{
    private const string ScenePath = "Assets/Test/PlayerReactions/Scenes/Test_PlayerReactions.unity";
    private static readonly MethodInfo AttachmentUpdate = typeof(Test_GourdAttachment).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);

    public static string Setup()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("편집 모드에서 적용해야 합니다.");
        var scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            bool dirty = Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty);
            scene = EditorSceneManager.OpenScene(ScenePath, dirty ? OpenSceneMode.Additive : OpenSceneMode.Single);
        }
        SceneManager.SetActiveScene(scene);
        var preview = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Test_PlayerReactionPreview>(true)).Single();
        var pose = preview.GetComponent<Test_GourdDrinkingPose>() ?? preview.gameObject.AddComponent<Test_GourdDrinkingPose>();
        pose.Configure(preview, preview.GetComponent<Test_GourdDrinking>());
        var serialized = new SerializedObject(pose);
        serialized.FindProperty("neckBackwardReductionDegrees").floatValue = 15f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(pose); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        return "몸통 최대 5도 · 목 젖힘 15도 감소 저장: " + scene.path;
    }

    public static string VerifyNeckReduction()
    {
        var p = UnityEngine.Object.FindFirstObjectByType<Test_PlayerReactionPreview>();
        var pose = p.GetComponent<Test_GourdDrinkingPose>();
        var d = p.GetComponent<Test_GourdDrinking>();
        var a = p.GetComponent<Test_GourdAttachment>();
        var input = p.GetComponent<Test_PlayerReactionInput>();
        bool inputEnabled = input.enabled; input.enabled = false;
        var field = typeof(Test_GourdDrinkingPose).GetField("neckBackwardReductionDegrees", BindingFlags.Instance | BindingFlags.NonPublic);
        var head = p.CharacterAnimator.GetBoneTransform(HumanBodyBones.Head);
        var baseline = new List<Quaternion>();
        float maxReduction = 0f, maxExpectedError = 0f, maxBodyLean = 0f, maxMouthDistance = 0f;
        try
        {
            field.SetValue(pose, 0f); p.ResetPreview(); d.PlayDrinking();
            for (int frame = 0; frame < 160; frame++) { Tick(p, a, 1f / 60f); baseline.Add(head.rotation); }
            field.SetValue(pose, 15f); p.ResetPreview(); d.PlayDrinking();
            for (int frame = 0; frame < 160; frame++)
            {
                Tick(p, a, 1f / 60f);
                Quaternion expected = Quaternion.AngleAxis(15f * d.SipWeight, p.CharacterAnimator.transform.right) * baseline[frame];
                maxReduction = Mathf.Max(maxReduction, Quaternion.Angle(baseline[frame], head.rotation));
                maxExpectedError = Mathf.Max(maxExpectedError, Quaternion.Angle(expected, head.rotation));
                if (d.Phase == Test_GourdDrinking.DrinkingPhase.Drinking) maxBodyLean = Mathf.Max(maxBodyLean, BodyLean(p.CharacterAnimator));
                if (d.SipWeight > .999f) maxMouthDistance = Mathf.Max(maxMouthDistance, a.MouthDistance);
            }
            p.SetPaused(true); Quaternion pausedHead = head.rotation;
            for (int frame = 0; frame < 30; frame++) Tick(p, a, .1f);
            float pausedHeadDrift = Quaternion.Angle(pausedHead, head.rotation);
            bool passed = Mathf.Abs(maxReduction - 15f) < .1f && maxExpectedError < .1f && maxBodyLean <= 5.01f && maxMouthDistance < .005f && pausedHeadDrift < .1f;
            string report = Newtonsoft.Json.JsonConvert.SerializeObject(new { passed, maxReduction, maxExpectedError, maxBodyLean, maxMouthDistance, pausedHeadDrift });
            File.WriteAllText("Assets/Temp/GourdDrinking~/Test_NeckReductionResult.json", report);
            return report;
        }
        finally { field.SetValue(pose, 15f); input.enabled = inputEnabled; p.ResetPreview(); }
    }

    public static string ComparePoses()
    {
        var p = UnityEngine.Object.FindFirstObjectByType<Test_PlayerReactionPreview>();
        var pose = p.GetComponent<Test_GourdDrinkingPose>();
        var d = p.GetComponent<Test_GourdDrinking>();
        var a = p.GetComponent<Test_GourdAttachment>();
        var input = p.GetComponent<Test_PlayerReactionInput>();
        bool inputEnabled = input.enabled; input.enabled = false;
        var originalHead = new List<Quaternion>();
        var originalHand = new List<Quaternion>();
        float originalMaxLean = 0f, correctedMaxLean = 0f, maxHeadChange = 0f, maxHandChange = 0f, maxMouthDistance = 0f;
        var animator = p.CharacterAnimator;
        var head = animator.GetBoneTransform(HumanBodyBones.Head);
        var hand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        try
        {
            pose.enabled = false; p.ResetPreview(); d.PlayDrinking();
            for (int frame = 0; frame < 300; frame++)
            {
                Tick(p, a, 1f / 60f);
                originalHead.Add(head.rotation); originalHand.Add(hand.rotation);
                if (d.Phase == Test_GourdDrinking.DrinkingPhase.Drinking) originalMaxLean = Mathf.Max(originalMaxLean, BodyLean(animator));
            }
            pose.enabled = true; p.ResetPreview(); d.PlayDrinking();
            for (int frame = 0; frame < 300; frame++)
            {
                Tick(p, a, 1f / 60f);
                maxHeadChange = Mathf.Max(maxHeadChange, Quaternion.Angle(originalHead[frame], head.rotation));
                maxHandChange = Mathf.Max(maxHandChange, Quaternion.Angle(originalHand[frame], hand.rotation));
                if (d.Phase == Test_GourdDrinking.DrinkingPhase.Drinking) correctedMaxLean = Mathf.Max(correctedMaxLean, BodyLean(animator));
                if (d.SipWeight > .999f) maxMouthDistance = Mathf.Max(maxMouthDistance, a.MouthDistance);
            }
            p.ResetPreview(); d.PlayDrinking();
            for (int frame = 0; frame < 138; frame++) Tick(p, a, 1f / 60f);
            p.SetPaused(true);
            float pausedLean = BodyLean(animator);
            Quaternion pausedHead = head.rotation;
            for (int frame = 0; frame < 30; frame++) Tick(p, a, .1f);
            float pausedLeanDrift = Mathf.Abs(pausedLean - BodyLean(animator));
            float pausedHeadDrift = Quaternion.Angle(pausedHead, head.rotation);
            bool passed = originalMaxLean > 5f && correctedMaxLean <= 5.01f && maxHeadChange < .1f &&
                maxHandChange < .1f && maxMouthDistance < .005f && pausedLeanDrift < .01f && pausedHeadDrift < .1f;
            string report = Newtonsoft.Json.JsonConvert.SerializeObject(new { passed, originalMaxLean, correctedMaxLean,
                maxHeadChange, maxHandChange, maxMouthDistance, pausedLeanDrift, pausedHeadDrift });
            File.WriteAllText("Assets/Temp/GourdDrinking~/Test_BodyLeanResult.json", report);
            return report;
        }
        finally { pose.enabled = true; input.enabled = inputEnabled; p.SetInput(Vector2.zero, false); }
    }

    public static string ReviewPose()
    {
        var p = UnityEngine.Object.FindFirstObjectByType<Test_PlayerReactionPreview>();
        p.ResetPreview(); p.GetComponent<Test_GourdDrinking>().PlayDrinking();
        var a = p.GetComponent<Test_GourdAttachment>();
        for (int frame = 0; frame < 138; frame++) Tick(p, a, 1f / 60f);
        p.SetPaused(true); p.SetView(90, 5.5f);
        return Newtonsoft.Json.JsonConvert.SerializeObject(new { bodyLean = BodyLean(p.CharacterAnimator), a.MouthDistance });
    }

    private static float BodyLean(Animator animator)
    {
        Vector3 torso = animator.GetBoneTransform(HumanBodyBones.Neck).position - animator.GetBoneTransform(HumanBodyBones.Spine).position;
        return Mathf.Atan2(-Vector3.Dot(torso, animator.transform.forward), Vector3.Dot(torso, animator.transform.up)) * Mathf.Rad2Deg;
    }
    private static void Tick(Test_PlayerReactionPreview p, Test_GourdAttachment a, float seconds)
    { p.AdvancePlayback(seconds); AttachmentUpdate.Invoke(a, null); }
}
