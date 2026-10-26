using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Cheon.Prototype.MountainPass;
using Cheon.Tests.BossPatterns;
public class Test_DustMotionProbe : MonoBehaviour
{
    public static object Main()
    {
        Application.runInBackground = true;
        var probe = new GameObject("Test_DustMotionProbe").AddComponent<Test_DustMotionProbe>();
        probe.StartCoroutine(probe.Run()); return "폭발 먼지 확대 및 입자 시간 검증";
    }
    private IEnumerator Run()
    {
        var root = new GameObject("Test_IsolatedDustPreview");
        var settings = root.AddComponent<Test_BossPatternSettings>();
        settings.dust.material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Test/BossPatterns/Materials/Test_M_BossDust.mat");
        var dust = root.AddComponent<Test_BossImpactDust>(); dust.Configure(settings, .99f);
        var particles = root.GetComponentsInChildren<ParticleSystem>().First(item => item.name == "Test_ImpactDust");
        Vector3 center = new Vector3(0, 500, 0);
        dust.EmitExplosion(center, settings.crack.radius);
        var initial = new ParticleSystem.Particle[2048]; int initialCount = particles.GetParticles(initial);
        System.IO.File.WriteAllText(Application.dataPath + "/Temp/BossPatterns~/Test_DustInitialReport.json", Newtonsoft.Json.JsonConvert.SerializeObject(new { initialCount, particles.isPlaying, particles.isStopped, scale = Time.timeScale, playing = Application.isPlaying }));
        yield return new WaitForSeconds(.25f);
        yield return new WaitForEndOfFrame();
        var after = new ParticleSystem.Particle[2048]; int afterCount = particles.GetParticles(after);
        var camera = new GameObject("Test_DustDetailCamera").AddComponent<Camera>(); camera.enabled = false;
        camera.transform.position = center + Vector3.forward * 6 + Vector3.right * 5 + Vector3.up * 4;
        camera.transform.LookAt(center + Vector3.up); camera.fieldOfView = 45; camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.18f, .15f, .1f);
        var target = new RenderTexture(1024, 576, 24); var previous = RenderTexture.active;
        camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
        var texture = new Texture2D(1024, 576, TextureFormat.RGB24, false); texture.ReadPixels(new Rect(0,0,1024,576),0,0); texture.Apply();
        System.IO.File.WriteAllBytes(Application.dataPath + "/Temp/BossPatterns~/Test_ExplosionDustDetail.png", texture.EncodeToPNG());
        RenderTexture.active = previous; camera.targetTexture = null; target.Release(); Destroy(target); Destroy(texture); Destroy(camera.gameObject);
        yield return new WaitForSeconds(.9f);
        yield return new WaitForEndOfFrame();
        int remainingAfterLifetime = particles.particleCount;
        System.IO.File.WriteAllText(Application.dataPath + "/Temp/BossPatterns~/Test_DustMotionReport.json", Newtonsoft.Json.JsonConvert.SerializeObject(new
        {
            initialCount, afterCount, remainingAfterLifetime,
            passed = initialCount == 78 && after[0].remainingLifetime < initial[0].remainingLifetime && (initial[0].position - after[0].position).magnitude > .1f && remainingAfterLifetime == 0,
            particles.isPlaying, particles.isStopped, particles.isPaused, particles.time,
            culling = particles.main.cullingMode.ToString(), particles.main.simulationSpeed,
            initialLife = initial[0].remainingLifetime, afterLife = after[0].remainingLifetime,
            moved = (initial[0].position - after[0].position).magnitude,
            initialSize = initial[0].startSize, afterSize = after[0].GetCurrentSize(particles),
            color = after[0].GetCurrentColor(particles).ToString(),
            shader = particles.GetComponent<ParticleSystemRenderer>().sharedMaterial.shader.name
        }, Newtonsoft.Json.Formatting.Indented));
        Destroy(root); Destroy(gameObject);
    }
}
