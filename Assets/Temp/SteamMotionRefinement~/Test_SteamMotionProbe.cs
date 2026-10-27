using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Cheon.Prototype.MountainPass;
using UnityEngine;

public static class Test_SteamMotionProbe
{
    private struct Sample { public float time; public float height; public float size; }

    public static async Task<object> Inspect()
    {
        var snort = UnityEngine.Object.FindFirstObjectByType<PassBossSnortVfx>();
        var steam = (ParticleSystem)typeof(PassBossSnortVfx).GetField("steam", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(snort);
        var items = new ParticleSystem.Particle[128];
        snort.EmitSnort(); await Task.Delay(400);
        int count = steam.GetParticles(items);
        var result = new string[count];
        for (int index = 0; index < count; index++) result[index] = items[index].randomSeed + " / " + items[index].remainingLifetime + " / " + items[index].position + " / " + items[index].velocity;
        return new { count, particles = result };
    }

    public static async Task<object> Run()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Play mode required.");
        int firstFrame = Time.frameCount;
        await Task.Delay(150);
        Require(Time.frameCount > firstFrame, "Player loop is not advancing.");
        var settings = UnityEngine.Object.FindFirstObjectByType<PassBossPatternSettings>();
        var boss = settings.MainBoss;
        var snort = boss.GetComponent<PassBossSnortVfx>();
        var steam = (ParticleSystem)typeof(PassBossSnortVfx).GetField("steam", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(snort);
        var particles = new ParticleSystem.Particle[128];
        var seeds = new HashSet<uint>();
        snort.EmitSnort();
        int initial = steam.GetParticles(particles);
        Require(initial == 1, "Emission must start with exactly one particle.");
        uint firstSeed = particles[0].randomSeed;
        var samples = new Sample[4];
        float[] sampleTimes = { .06f, .16f, .4f, .5f };
        int nextSample = 0, largestNewBatch = 0, observedFrame = -1;
        float started = Time.time;
        var timeout = System.Diagnostics.Stopwatch.StartNew();
        while (Time.time - started < .75f)
        {
            if (timeout.Elapsed.TotalSeconds > 8) throw new InvalidOperationException("Steam observation timed out.");
            if (Time.frameCount != observedFrame)
            {
                observedFrame = Time.frameCount;
                int count = steam.GetParticles(particles), newCount = 0;
                for (int index = 0; index < count; index++)
                {
                    var particle = particles[index];
                    if (seeds.Add(particle.randomSeed)) newCount++;
                    if (particle.randomSeed == firstSeed && nextSample < sampleTimes.Length && Time.time - started >= sampleTimes[nextSample])
                    {
                        samples[nextSample] = new Sample { time = Time.time - started, height = particle.position.y,
                            size = particle.GetCurrentSize3D(steam).y };
                        nextSample++;
                    }
                }
                largestNewBatch = Mathf.Max(largestNewBatch, newCount);
            }
            await Task.Delay(1);
        }
        Require(nextSample == 4, "The first particle disappeared before observation completed.");
        Require(seeds.Count == settings.pursuitSlam.steamParticles, "Observed " + seeds.Count + " of " + settings.pursuitSlam.steamParticles + " particles; largest batch " + largestNewBatch);
        Require(largestNewBatch == 1, "More than one particle appeared in a frame.");
        float initialSpeed = (samples[0].height - samples[1].height) / (samples[1].time - samples[0].time);
        float laterSpeed = (samples[2].height - samples[3].height) / (samples[3].time - samples[2].time);
        Require(initialSpeed > 2f && laterSpeed > 0f && laterSpeed < initialSpeed * .7f, "Downward movement did not slow down.");
        float growth = samples[3].size / samples[0].size;
        Require(growth > 1.05f && growth < 1.3f, "The particle did not grow gradually.");
        // 분출 중 초기화해도 코루틴이 남아 새 입자를 생성하지 않는지 확인한다.
        snort.EmitSnort(); await Task.Delay(70); snort.Clear(); await Task.Delay(150);
        Require(steam.particleCount == 0, "Clearing the effect did not cancel pending particles.");

        await CapturePreview(snort, boss);
        snort.Clear();
        return new { passed = true, particles = seeds.Count, largestNewBatch, initialDownSpeed = initialSpeed,
            laterDownSpeed = laterSpeed, observedGrowth = growth, clearCancelsEmission = true };
    }

    private static async Task CapturePreview(PassBossSnortVfx snort, PassBossPatternController boss)
    {
        var cameraObject = new GameObject("SteamMotionPreviewCamera");
        var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
        camera.fieldOfView = 45f; camera.nearClipPlane = .1f; camera.farClipPlane = 300f;
        camera.transform.position = boss.transform.position + boss.transform.forward * 14f + boss.transform.right * 4f + Vector3.up * 9f;
        camera.transform.LookAt(boss.transform.position + Vector3.up * 6.5f);
        try
        {
            snort.EmitSnort(); await Task.Delay(110); Capture(camera, "Test_SteamEarly.png");
            await Task.Delay(280); Capture(camera, "Test_SteamLate.png");
        }
        finally { UnityEngine.Object.Destroy(cameraObject); }
    }

    private static void Capture(Camera camera, string filename)
    {
        var render = RenderTexture.GetTemporary(960, 720, 24);
        var previous = RenderTexture.active;
        var texture = new Texture2D(960, 720, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = render; camera.Render(); RenderTexture.active = render;
            texture.ReadPixels(new Rect(0, 0, 960, 720), 0, 0); texture.Apply();
            File.WriteAllBytes(Path.Combine(Application.dataPath, "Temp/SteamMotionRefinement~/" + filename), texture.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = null; RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(render); UnityEngine.Object.Destroy(texture);
        }
    }

    private static void Require(bool passed, string message) { if (!passed) throw new InvalidOperationException(message); }
}
