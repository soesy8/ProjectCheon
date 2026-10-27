using System;
using System.Linq;
using Cheon.Prototype.MountainPass;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class PursuitSlamSceneSetup
{
    public static object InspectModel()
    {
        var boss = UnityEngine.Object.FindFirstObjectByType<PassBossPatternController>();
        var head = boss.GetComponentsInChildren<MeshFilter>(true).First(item => item.name == "Head");
        var vertices = head.sharedMesh.vertices.Select(point => boss.transform.InverseTransformPoint(head.transform.TransformPoint(point))).ToArray();
        return new
        {
            rootPosition = boss.transform.position.ToString(),
            frontVertices = vertices.OrderByDescending(point => point.z).Take(24).Select(point => point.ToString()).ToArray(),
            headMinimum = new Vector3(vertices.Min(point => point.x), vertices.Min(point => point.y), vertices.Min(point => point.z)).ToString(),
            headMaximum = new Vector3(vertices.Max(point => point.x), vertices.Max(point => point.y), vertices.Max(point => point.z)).ToString()
        };
    }

    public static object Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.path != "Assets/Scenes/MountainPass/MountainPass_Optimized.unity")
            throw new InvalidOperationException("The gameplay scene must be open in Edit mode.");
        var settings = UnityEngine.Object.FindFirstObjectByType<PassBossPatternSettings>();
        Undo.RecordObject(settings, "Add pursuit jump and back slam pattern");
        var timing = settings.pursuitSlam;
        timing.enabled = true; timing.windup = 1f; timing.trackingJumps = 2;
        timing.trackingFlight = 1f; timing.landingPause = .65f; timing.trackingRadius = 4f;
        timing.heavyRise = 1.5f; timing.heavyFall = .75f; timing.heavyRadius = 6f;
        timing.recovery = 7f; timing.bodyDamageMultiplier = 1.5f;
        timing.noseOffset = new Vector3(0, 7.95f, 1.7f);
        timing.steamParticles = 16; timing.steamSize = new Vector2(.25f, 1f); timing.dustSize = 1.8f;
        timing.trackingDamage = settings.combat.bossJumpDamage;
        timing.heavyDamage = settings.combat.bossFallDamage;
        EditorUtility.SetDirty(settings); EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Scene save failed.");
        return new { saved = true, trackingDamage = timing.trackingDamage, slamDamage = timing.heavyDamage,
            directBodyDamage = timing.heavyDamage * timing.bodyDamageMultiplier, noseOffset = timing.noseOffset.ToString() };
    }
}
