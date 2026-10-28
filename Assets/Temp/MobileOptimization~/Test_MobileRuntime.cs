using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Cheon.Prototype.MountainPass;
using Newtonsoft.Json;

public static class Test_MobileRuntime
{
    public static object Main()
    {
        var errors = new List<string>();
        var game = UnityEngine.Object.FindFirstObjectByType<PassGameFlow>();
        var movement = game.Player.GetComponent<PassPlayerMovement>();
        var boss = (PassBossPatternController)game.Boss;
        var playerBody = movement.GetComponent<CharacterController>();
        var winds = UnityEngine.Object.FindObjectsByType<PassFanSweepMesh>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var wind in winds) if (wind.Mesh == null || wind.Mesh.vertexCount != 125) errors.Add("Unexpected wind mesh: " + wind.name);
        int clouds = UnityEngine.Object.FindObjectsByType<UnityEngine.UI.RawImage>(FindObjectsInactive.Include, FindObjectsSortMode.None).Count(i => i.name.StartsWith("Cloud"));
        if (clouds != 12) errors.Add("Cloud layer count: " + clouds);
        int fogs = 0;
        foreach (var fog in UnityEngine.Object.FindObjectsByType<PassHeightFog>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var renderer = (Renderer)new SerializedObject(fog).FindProperty("volumeRenderer").objectReferenceValue;
            var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block); fogs++;
            if (block.GetFloat("_SampleCount") != 8f) errors.Add("Fog quality: " + fog.name);
        }
        var minions = UnityEngine.Object.FindObjectsByType<PassBossPatternController>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(b => b.IsMinion).ToArray();
        if (minions.Length != boss.Settings.summon.poolCapacity || minions.Any(b => b.gameObject.activeSelf)) errors.Add("Summon pool not ready.");

        // 기존의 공간 쿼리와 직접 형상 검사가 여러 회전·접근 위치에서 같은 결과를 내는지 비교한다.
        int cases = 0;
        Vector3 playerPosition = movement.transform.position;
        Quaternion playerRotation = movement.transform.rotation, bodyRotation = boss.Body.transform.localRotation;
        bool previousBossEnabled = boss.enabled, previousInput = movement.InputEnabled;
        boss.enabled = false; movement.InputEnabled = false;
        var hits = new Collider[256];
        try
        {
            foreach (float angle in new[] { 0f, 30f, 60f, 90f, 135f, 180f, 240f, 270f })
            {
                boss.Body.transform.localRotation = Quaternion.Euler(angle, 0f, 0f);
                boss.GetBodySegment(out Vector3 first, out Vector3 second);
                foreach (float along in new[] { .2f, .5f, .8f })
                foreach (float lateral in new[] { 0f, .35f, 4f })
                {
                    Vector3 center = Vector3.Lerp(first, second, along) + boss.transform.right * lateral;
                    movement.Teleport(center - playerBody.center, playerRotation);
                    Physics.SyncTransforms();
                    int count = Physics.OverlapCapsuleNonAlloc(first, second, boss.Body.radius, hits, ~0, QueryTriggerInteraction.Ignore);
                    bool oldResult = false;
                    for (int i = 0; i < count; i++) if (hits[i] == playerBody) oldResult = true;
                    if (oldResult != boss.OverlapsPlayerBody()) errors.Add("Collision mismatch at " + angle + "/" + along + "/" + lateral);
                    cases++;
                }
            }
        }
        finally
        {
            boss.Body.transform.localRotation = bodyRotation;
            movement.Teleport(playerPosition, playerRotation);
            boss.enabled = previousBossEnabled; movement.InputEnabled = previousInput;
            Physics.SyncTransforms(); game.View.SnapBehind();
        }
        var result = new { success = errors.Count == 0, errors, frame = Time.frameCount, state = game.State.ToString(), windMeshes = winds.Length, clouds, fogs, prewarmedMinions = minions.Length, collisionCases = cases };
        File.WriteAllText("Assets/Temp/MobileOptimization~/RuntimeVerification.json", JsonConvert.SerializeObject(result, Formatting.Indented));
        return result;
    }
}
