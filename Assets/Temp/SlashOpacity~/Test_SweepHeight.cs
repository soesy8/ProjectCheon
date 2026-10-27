using System.Linq;
using UnityEditor;
using UnityEngine;
using Cheon.Tests.FanAttackVfx;

public static class Test_SweepHeight
{
    public static object Apply()
    {
        var profile = UnityEngine.Object.FindFirstObjectByType<Test_FanAttackVfx>().Profile;
        Undo.RecordObject(profile, "1·2타 타이밍 롤백 및 3타 높이 맞춤");
        profile.attack1.fadeSeconds = .26f;
        profile.attack2.fadeSeconds = .22f;
        float secondHeight = profile.attack2.height.Evaluate(0) * profile.attack2.heightMultiplier + profile.attack2.heightOffset;
        profile.attack3.heightOffset = secondHeight - profile.attack3.height.Evaluate(0) * profile.attack3.heightMultiplier;
        profile.NotifyChanged();
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssetIfDirty(profile);
        return Inspect();
    }

    public static object Inspect()
    {
        var profile = UnityEngine.Object.FindFirstObjectByType<Test_FanAttackVfx>().Profile;
        return Enumerable.Range(1, 3).Select(index =>
        {
            var sweep = profile.GetSweep(index);
            return new { attack = index, fadeSeconds = sweep.fadeSeconds, offset = sweep.heightOffset,
                multiplier = sweep.heightMultiplier,
                heights = Enumerable.Range(0, 11).Select(step => sweep.height.Evaluate(step / 10f)).ToArray() };
        }).ToArray();
    }
}
