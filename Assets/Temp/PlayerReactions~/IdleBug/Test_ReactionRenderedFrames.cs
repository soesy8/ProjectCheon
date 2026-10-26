using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Cheon.Tests.PlayerReactions;
using UnityEngine;
using UnityEngine.EventSystems;

public class Test_ReactionRenderedFrames : MonoBehaviour
{
    [Serializable] public class Sample
    {
        public string label, phase, state;
        public int frame;
        public float hipHeight, headHeight, skinTop, idleTime;
    }
    [Serializable] public class Report
    {
        public string status;
        public int startFrame, endFrame;
        public List<string> checks = new List<string>();
        public List<Sample> samples = new List<Sample>();
    }
    private readonly TaskCompletionSource<string> completion = new TaskCompletionSource<string>();
    private readonly Report report = new Report();
    private Test_PlayerReactionPreview preview;
    private string originalSettings;
    private int stage, settled, stageFrame;
    private float startedAt, idleBefore, deathStarted;
    private bool middleRecorded, hitBlend, getUpBlend, idleBlend, downRecorded;

    public static Task<string> Main()
    {
        if (!Application.isPlaying) throw new Exception("Play Mode required");
        var runner = new GameObject("Test_ReactionRenderedFrames").AddComponent<Test_ReactionRenderedFrames>();
        runner.preview = UnityEngine.Object.FindFirstObjectByType<Test_PlayerReactionPreview>();
        if (runner.preview == null || string.IsNullOrEmpty(runner.preview.AnimatorState)) throw new Exception("Preview is not initialized");
        runner.originalSettings = JsonUtility.ToJson(runner.preview.Settings);
        runner.preview.enabled = true; runner.preview.repeatSequence = false;
        runner.report.startFrame = Time.frameCount; runner.startedAt = Time.realtimeSinceStartup;
        runner.Click("HeavyOnly");
        return runner.completion.Task;
    }
    private void Click(string name)
    {
        var button = UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None)
            .Single(b => b.name == name && b.gameObject.scene == preview.gameObject.scene);
        ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
        settled = 0; stageFrame = Time.frameCount; middleRecorded = false;
    }
    private void Check(bool value, string label)
    {
        if (!value) throw new Exception("FAIL: " + label);
        report.checks.Add(label);
    }
    private Sample Record(string label)
    {
        var animator = preview.CharacterAnimator;
        float skinTop = float.NegativeInfinity;
        foreach (var renderer in animator.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
            var mesh = new Mesh(); renderer.BakeMesh(mesh);
            foreach (var vertex in mesh.vertices) skinTop = Mathf.Max(skinTop, renderer.transform.TransformPoint(vertex).y);
            Destroy(mesh);
        }
        var sample = new Sample { label = label, frame = Time.frameCount, phase = preview.Phase.ToString(), state = preview.AnimatorState,
            hipHeight = animator.GetBoneTransform(HumanBodyBones.Hips).position.y,
            headHeight = animator.GetBoneTransform(HumanBodyBones.Head).position.y,
            skinTop = skinTop, idleTime = preview.IdleNormalizedTime };
        report.samples.Add(sample); return sample;
    }
    private void LateUpdate()
    {
        try
        {
            if (Time.realtimeSinceStartup - startedAt > 25f) throw new Exception("Timed out at stage " + stage);
            if (stage == 0 && preview.Phase == Test_PlayerReactionPreview.PreviewPhase.Complete && ++settled >= 8)
            {
                var pose = Record("Heavy hit held for 8 rendered frames");
                Check(pose.hipHeight < 0.4f && pose.headHeight < 0.7f && pose.skinTop < 0.9f, "강피격 완료 후 실제 쓰러진 스킨 메시 유지");
                Check(preview.CharacterAnimator.runtimeAnimatorController == null, "중복 기본 Animator 출력 제거");
                stage = 1; Click("GetUpOnly");
            }
            else if (stage == 1)
            {
                if (!middleRecorded && preview.Phase == Test_PlayerReactionPreview.PreviewPhase.GetUp && preview.CurrentFrame >= 24f)
                {
                    var pose = Record("Get up middle frame"); middleRecorded = true;
                    Check(pose.hipHeight > 0.3f && pose.hipHeight < 0.8f, "일어나기 중간 자세가 대기와 구별됨");
                }
                if (preview.Phase == Test_PlayerReactionPreview.PreviewPhase.Complete && !preview.IsBlending && ++settled >= 8)
                {
                    var pose = Record("Get up returned to idle");
                    Check(middleRecorded && pose.hipHeight > 0.8f, "일어나기 단독 버튼에서 대기로 복귀");
                    stage = 2; Click("HeavyPattern");
                }
            }
            else if (stage == 2)
            {
                if (preview.IsBlending)
                {
                    if (preview.Phase == Test_PlayerReactionPreview.PreviewPhase.HeavyHit) hitBlend = true;
                    else if (preview.Phase == Test_PlayerReactionPreview.PreviewPhase.GetUp) getUpBlend = true;
                    else if (preview.AnimatorState == "IDLE") idleBlend = true;
                }
                if (!downRecorded && preview.Phase == Test_PlayerReactionPreview.PreviewPhase.DownHold)
                {
                    var pose = Record("Sequence down hold"); downRecorded = true;
                    Check(pose.hipHeight < 0.4f && pose.skinTop < 0.9f, "연속 재생에서 실제 강피격 자세 확인");
                }
                if (!middleRecorded && preview.Phase == Test_PlayerReactionPreview.PreviewPhase.GetUp && preview.CurrentFrame >= 24f)
                {
                    var pose = Record("Sequence get up"); middleRecorded = true;
                    Check(pose.hipHeight > 0.3f && pose.hipHeight < 0.8f, "연속 재생에서 실제 일어나기 자세 확인");
                }
                if (preview.Phase == Test_PlayerReactionPreview.PreviewPhase.Complete && !preview.IsBlending && ++settled >= 8)
                {
                    var pose = Record("Sequence standing idle");
                    Check(hitBlend && getUpBlend && idleBlend && downRecorded && middleRecorded && pose.hipHeight > 0.8f, "강피격 → 일어나기 → 대기 자연 재생 및 세 구간 블렌딩");
                    idleBefore = preview.IdleNormalizedTime; stageFrame = Time.frameCount; stage = 3;
                }
            }
            else if (stage == 3 && Time.frameCount - stageFrame >= 30)
            {
                Record("Idle after 30 additional frames");
                Check(preview.IdleNormalizedTime > idleBefore, "회복 후 대기 애니메이션 계속 재생");
                preview.repeatSequence = true; stage = 4; Click("LethalAttack");
            }
            else if (stage == 4 && preview.Phase == Test_PlayerReactionPreview.PreviewPhase.Dead && ++settled >= 8)
            {
                var pose = Record("Death held for 8 rendered frames");
                Check(pose.hipHeight < 0.4f && pose.skinTop < 0.9f, "사망 버튼에서 실제 쓰러진 자세 유지");
                stage = 5; stageFrame = Time.frameCount; deathStarted = Time.realtimeSinceStartup;
            }
            else if (stage == 5 && Time.frameCount - stageFrame >= 30 && Time.realtimeSinceStartup - deathStarted >= 0.5f)
            {
                var pose = Record("Death after 30 additional frames");
                Check(preview.Phase == Test_PlayerReactionPreview.PreviewPhase.Dead && preview.IsPaused && pose.hipHeight < 0.4f, "반복 옵션이 켜져 있어도 사망 후 자동 회복 없음");
                JsonUtility.FromJsonOverwrite(originalSettings, preview.Settings); preview.repeatSequence = false;
                report.status = "passed"; report.endFrame = Time.frameCount;
                string json = JsonUtility.ToJson(report, true);
                File.WriteAllText("Assets/Temp/PlayerReactions~/IdleBug/Test_RenderedFramesReport.json", json);
                completion.TrySetResult(json); Destroy(gameObject);
            }
        }
        catch (Exception error)
        {
            if (preview != null) { JsonUtility.FromJsonOverwrite(originalSettings, preview.Settings); preview.repeatSequence = false; }
            completion.TrySetException(error); Destroy(gameObject);
        }
    }
}
