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

public static class Test_GourdHeadReturnVerification
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string ScenePath = "Assets/Test/PlayerReactions/Scenes/Test_PlayerReactions.unity";
    private static readonly MethodInfo AttachmentUpdate = typeof(Test_GourdAttachment).GetMethod("LateUpdate", Private);

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
        var pose = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Test_GourdDrinkingPose>(true)).Single();
        var serialized = new SerializedObject(pose);
        serialized.FindProperty("headReturnStart").floatValue = .68f;
        serialized.FindProperty("headReturnEnd").floatValue = .9f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(pose); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        return "팔 내리기 68~90% 구간에서 고개 정면 복귀 저장";
    }

    public static string Verify()
    {
        var p = UnityEngine.Object.FindFirstObjectByType<Test_PlayerReactionPreview>();
        var pose = p.GetComponent<Test_GourdDrinkingPose>();
        var d = p.GetComponent<Test_GourdDrinking>();
        var a = p.GetComponent<Test_GourdAttachment>();
        var input = p.GetComponent<Test_PlayerReactionInput>();
        bool inputEnabled = input.enabled; input.enabled = false;
        var start = typeof(Test_GourdDrinkingPose).GetField("headReturnStart", Private);
        var end = typeof(Test_GourdDrinkingPose).GetField("headReturnEnd", Private);
        var animator = p.CharacterAnimator;
        var head = animator.GetBoneTransform(HumanBodyBones.Head);
        var baseline = new List<Quaternion>();
        float maxDrinkingPoseChange = 0f, maxForwardError = 0f, maxFrameTurn = 0f, maxBodyLean = 0f, maxMouthDistance = 0f;
        int forwardSamples = 0;
        try
        {
            start.SetValue(pose, 1f); end.SetValue(pose, 1f); p.ResetPreview(); d.PlayDrinking();
            for (int frame = 0; frame < 300; frame++) { Tick(p, a); baseline.Add(head.rotation); }
            start.SetValue(pose, .68f); end.SetValue(pose, .9f); p.ResetPreview(); d.PlayDrinking();
            Quaternion previous = head.rotation;
            for (int frame = 0; frame < 300; frame++)
            {
                Tick(p, a);
                if (d.Phase == Test_GourdDrinking.DrinkingPhase.Drinking)
                {
                    if (d.Progress <= .68f) maxDrinkingPoseChange = Mathf.Max(maxDrinkingPoseChange, Quaternion.Angle(baseline[frame], head.rotation));
                    if (d.Progress > .68f) maxFrameTurn = Mathf.Max(maxFrameTurn, Quaternion.Angle(previous, head.rotation));
                    Vector3 torso = animator.GetBoneTransform(HumanBodyBones.Neck).position - animator.GetBoneTransform(HumanBodyBones.Spine).position;
                    float lean = Mathf.Atan2(-Vector3.Dot(torso, animator.transform.forward), Vector3.Dot(torso, animator.transform.up)) * Mathf.Rad2Deg;
                    maxBodyLean = Mathf.Max(maxBodyLean, lean);
                }
                if (d.Phase == Test_GourdDrinking.DrinkingPhase.Stowing ||
                    d.Phase == Test_GourdDrinking.DrinkingPhase.Drinking && d.Progress >= .9f)
                {
                    maxForwardError = Mathf.Max(maxForwardError, Quaternion.Angle(head.rotation, animator.transform.rotation)); forwardSamples++;
                }
                if (d.SipWeight > .999f) maxMouthDistance = Mathf.Max(maxMouthDistance, a.MouthDistance);
                previous = head.rotation;
            }
            p.ResetPreview(); d.PlayDrinking();
            for (int frame = 0; frame < 208; frame++) Tick(p, a);
            p.SetPaused(true); Quaternion paused = head.rotation;
            for (int frame = 0; frame < 30; frame++) Tick(p, a);
            float pausedDrift = Quaternion.Angle(paused, head.rotation);
            bool passed = maxDrinkingPoseChange < .1f && forwardSamples > 20 && maxForwardError < .1f &&
                maxFrameTurn < 10f && maxBodyLean <= 5.01f && maxMouthDistance < .005f && pausedDrift < .1f;
            string report = Newtonsoft.Json.JsonConvert.SerializeObject(new { passed, maxDrinkingPoseChange, maxForwardError,
                forwardSamples, maxFrameTurn, maxBodyLean, maxMouthDistance, pausedDrift });
            File.WriteAllText("Assets/Temp/GourdDrinking~/Test_HeadReturnResult.json", report); return report;
        }
        finally { start.SetValue(pose, .68f); end.SetValue(pose, .9f); input.enabled = inputEnabled; p.ResetPreview(); }
    }

    public static string ReviewPose()
    {
        var p = UnityEngine.Object.FindFirstObjectByType<Test_PlayerReactionPreview>();
        var a = p.GetComponent<Test_GourdAttachment>();
        p.ResetPreview(); p.GetComponent<Test_GourdDrinking>().PlayDrinking();
        for (int frame = 0; frame < 224; frame++) Tick(p, a);
        p.SetPaused(true); p.SetView(90, 5.5f);
        return "팔을 내린 시점 · 고개 정면";
    }
    private static void Tick(Test_PlayerReactionPreview p, Test_GourdAttachment a)
    { p.AdvancePlayback(1f / 60f); AttachmentUpdate.Invoke(a, null); }
}
