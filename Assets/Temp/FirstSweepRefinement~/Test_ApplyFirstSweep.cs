using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Cheon.Tests.FanAttackVfx;

public static class Test_ApplyFirstSweep
{
    public static object Main()
    {
        if (EditorApplication.isPlaying) throw new Exception("Edit Mode required");
        const string profilePath = "Assets/Test/FanAttackVfx/Settings/Test_FanAttackVfxProfile.asset";
        const string meshPath = "Assets/Test/FanAttackVfx/Meshes/Test_Attack1_Sweep.asset";
        var profile = AssetDatabase.LoadAssetAtPath<Test_FanAttackVfxProfile>(profilePath);
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        Vector3 originalStart = mesh.vertices[8];
        string attack2 = JsonUtility.ToJson(profile.attack2);
        string attack3 = JsonUtility.ToJson(profile.attack3);
        var sweep = profile.attack1;
        sweep.shapedRibbon = true;
        sweep.ribbonWidth = new AnimationCurve(
            new Keyframe(0, .32f, 0, 0),
            new Keyframe(.2f, .76f, 2, 2),
            new Keyframe(.5f, 1.2f, 0, 0),
            new Keyframe(.8f, .76f, -2, -2),
            new Keyframe(1, .32f, 0, 0));
        sweep.middleBulge = .55f;
        sweep.endExtension = 60f;
        sweep.lowerArcHeightScale = .52f;
        sweep.surfaceFill = .48f;
        sweep.tipSerration = .085f;
        sweep.color = new Color(.24f, .67f, .63f, .94f);
        sweep.intensity = 1.12f;
        var geometry = Test_FanSweepMesh.CreateGeometry(sweep);
        geometry.name = mesh.name;
        float startDifference = Vector3.Distance(originalStart, geometry.vertices[8]);
        if (startDifference > .00001f) throw new Exception("The start moved: " + startDifference);
        if (JsonUtility.ToJson(profile.attack2) != attack2 || JsonUtility.ToJson(profile.attack3) != attack3) throw new Exception("Other attacks changed");
        EditorUtility.CopySerialized(geometry, mesh);
        UnityEngine.Object.DestroyImmediate(geometry);
        EditorUtility.SetDirty(mesh);
        profile.NotifyChanged(); EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssetIfDirty(mesh); AssetDatabase.SaveAssetIfDirty(profile);
        if (ShaderUtil.ShaderHasError(profile.sweepMaterial.shader)) throw new Exception("Sweep shader compilation error");
        return new { sweep.startPitch, sweep.angle, sweep.endExtension, endPitch = sweep.startPitch - sweep.angle - sweep.endExtension, startDifference, sweep.middleBulge, sweep.surfaceFill, color = sweep.color.ToString(), otherAttacksUnchanged = true, vertices = mesh.vertexCount, minimumHeight = mesh.vertices.Min(v => v.y) };
    }
}
