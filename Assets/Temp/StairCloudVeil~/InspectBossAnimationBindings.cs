var boss = UnityEngine.Object.FindFirstObjectByType<Cheon.Prototype.MountainPass.PassBossPatternController>();
var animator = boss.GetComponent<Animator>();
var controller = animator.runtimeAnimatorController;
var entrance = UnityEngine.Object.FindFirstObjectByType<Cheon.Prototype.MountainPass.PassBossEntrance>();
var timeline = entrance.Director.playableAsset as UnityEngine.Timeline.TimelineAsset;
var clips = new System.Collections.Generic.List<AnimationClip>();
if (controller != null) clips.AddRange(controller.animationClips);
var tracks = new System.Collections.Generic.List<object>();
if (timeline != null)
foreach (var track in timeline.GetOutputTracks())
{
    var animation = track as UnityEngine.Timeline.AnimationTrack;
    tracks.Add(new { track.name, binding = entrance.Director.GetGenericBinding(track) != null ? entrance.Director.GetGenericBinding(track).name : "none",
        offsets = animation != null ? animation.eulerAngles.ToString("F4") : "none" });
    foreach (var clip in track.GetClips())
        if (clip.asset is UnityEngine.Timeline.AnimationPlayableAsset playable && playable.clip != null) clips.Add(playable.clip);
}
return new { controller = AssetDatabase.GetAssetPath(controller), animator.applyRootMotion, tracks,
    clips = clips.Distinct().Select(c => new { c.name, c.length, path = AssetDatabase.GetAssetPath(c),
        rotationCurves = AnimationUtility.GetCurveBindings(c).Where(b => b.propertyName.Contains("Rotation") || b.propertyName.Contains("Euler"))
            .Select(b => new { b.path, b.propertyName, start = AnimationUtility.GetEditorCurve(c, b).Evaluate(0f),
                end = AnimationUtility.GetEditorCurve(c, b).Evaluate(c.length) }).ToArray() }).ToArray() };
