using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Cheon.Prototype.MountainPass;
using Cheon.Tests.FanAttackVfx;

public static class Test_WindDesignPreview
{
    private const string Folder = "Assets/Temp/WindDesignAB~/";

    public static object Open()
    {
        if (EditorApplication.isPlaying) throw new Exception("Edit Mode required");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.isDirty) throw new Exception("Unsaved scene: " + scene.path);
        if (scene.path != "Assets/Test/FanAttackVfx/Scenes/Test_FanAttackVfx.unity")
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Test/FanAttackVfx/Scenes/Test_FanAttackVfx.unity");
        return UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
    }

    private static Test_FanAttackVfx Prepare()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Play Mode required");
        var game = UnityEngine.Object.FindFirstObjectByType<PassGameFlow>();
        var effect = UnityEngine.Object.FindFirstObjectByType<Test_FanAttackVfx>();
        if (game == null || effect == null) throw new Exception("Fan VFX test scene required");
        game.BeginBossChallenge();
        if (game.Entrance != null) game.Entrance.SkipPresentation();
        game.PlayerHealth.Invulnerable = true;
        game.Boss.gameObject.SetActive(false);
        game.Player.GetComponent<PassPlayerMovement>().InputEnabled = false;
        game.View.enabled = false;
        return effect;
    }

    private static Camera SetCamera(int attack, bool full = false)
    {
        var game = UnityEngine.Object.FindFirstObjectByType<PassGameFlow>();
        var camera = game.View.GetComponent<Camera>();
        camera.fieldOfView = 44;
        Vector3 position = attack == 1 ? new Vector3(-4.2f,4.6f,-2.8f) : new Vector3(-5.0f,7.2f,-2.5f);
        Vector3 focus = attack == 1 ? new Vector3(0,.6f,1.8f) : new Vector3(0,.6f,4.2f);
        if (full) { position = new Vector3(-7.2f,8.3f,-6.5f); focus = new Vector3(0,.5f,3.5f); camera.fieldOfView = 47; }
        camera.transform.position = game.Player.TransformPoint(position);
        camera.transform.LookAt(game.Player.TransformPoint(focus));
        return camera;
    }

    private static void Select(Test_FanAttackVfx effect, Test_FanAttackVfxProfile.WindDesign design)
    {
        effect.Profile.windDesign = design;
        effect.Profile.NotifyChanged();
    }

    private static async Task Hold(Test_FanAttackVfx effect, int attack, float frame)
    {
        effect.PlayPreview(attack, frame);
        double deadline = EditorApplication.timeSinceStartup + 12;
        do
        {
            await Task.Delay(35);
            if (!EditorApplication.isPlaying || effect == null) throw new Exception("Preview interrupted");
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Preview frame timeout");
        } while (!effect.PreviewPaused);
        if (effect.CurrentAttack != attack) throw new Exception("Wrong preview attack");
    }

    public static async Task<object> CaptureAll()
    {
        var effect = Prepare();
        var paths = new System.Collections.Generic.List<string>();
        foreach (int attack in new[] {1, 3})
        {
            Select(effect, Test_FanAttackVfxProfile.WindDesign.A);
            await Hold(effect, attack, attack == 1 ? 34 : 36);
            effect.transform.Find("Test_Attack" + attack + "_Sweep").GetComponent<Test_FanSweepMesh>().Hide();
            var renderer = effect.transform.Find("Test_Attack" + attack + "_WindSector").GetComponent<Renderer>();
            var particleRenderer = effect.GetComponentsInChildren<ParticleSystemRenderer>()
                .First(rendererEntry => rendererEntry.sharedMaterial == effect.Profile.windParticleMaterial);
            var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block);
            int count = Mathf.RoundToInt(block.GetFloat("_BranchCount"));
            if (count != (attack == 1 ? 3 : 5)) throw new Exception("Wrong branch count: " + count);
            // 같은 프레임의 좌표·진행률·카메라를 고정하고 머티리얼만 교체하여 비교한다.
            foreach (var design in new[] {Test_FanAttackVfxProfile.WindDesign.A, Test_FanAttackVfxProfile.WindDesign.B})
            {
                renderer.sharedMaterial = design == Test_FanAttackVfxProfile.WindDesign.A ? effect.Profile.windMaterial : effect.Profile.windMaterialB;
                particleRenderer.sharedMaterial = design == Test_FanAttackVfxProfile.WindDesign.A ? effect.Profile.windParticleMaterial : effect.Profile.windParticleMaterialB;
                paths.Add(SaveCamera(SetCamera(attack), "Test_Wind_" + design + "_Attack" + attack + ".png"));
            }
        }
        Select(effect, Test_FanAttackVfxProfile.WindDesign.B);
        await Hold(effect, 3, 36);
        SetCamera(3, true);
        return new { paths, firstBranches = effect.Profile.attack1Wind.branchCount, thirdBranches = effect.Profile.wind.branchCount,
            shaderErrors = ShaderUtil.ShaderHasError(effect.Profile.windMaterial.shader) || ShaderUtil.ShaderHasError(effect.Profile.windMaterialB.shader) };
    }

    public static async Task<object> Record()
    {
        var effect = Prepare();
        var reports = new System.Collections.Generic.List<object>();
        foreach (var design in new[] {Test_FanAttackVfxProfile.WindDesign.A, Test_FanAttackVfxProfile.WindDesign.B})
        {
            Select(effect, design);
            SetCamera(3, true);
            effect.PlayPreview(3);
            int initialFrame = Time.frameCount;
            int visible = 0;
            float start = Time.realtimeSinceStartup;
            int count = 0;
            for (int index = 0; index < 32; index++)
            {
                await Task.Delay(30);
                if (!EditorApplication.isPlaying || effect == null) throw new Exception("Preview interrupted");
                var gust = effect.transform.Find("Test_Attack3_WindSector").GetComponent<Test_FanSweepMesh>();
                if (gust.IsVisible) visible++;
                var block = new MaterialPropertyBlock(); gust.GetComponent<Renderer>().GetPropertyBlock(block);
                count = Mathf.RoundToInt(block.GetFloat("_BranchCount"));
                if (count != 5) throw new Exception("Third wind did not use five branches");
                if (gust.GetComponent<Renderer>().sharedMaterial != effect.Profile.GetWindMaterial()) throw new Exception("Wrong A/B material");
                SaveCamera(SetCamera(3, true), "Test_Wind_" + design + "_Frame" + index.ToString("D3") + ".png", 768, 576);
            }
            if (visible == 0 || Time.frameCount == initialFrame) throw new Exception("Wind did not animate");
            reports.Add(new { design = design.ToString(), count, visible, framesAdvanced = Time.frameCount - initialFrame,
                elapsed = Time.realtimeSinceStartup - start, material = effect.Profile.GetWindMaterial().name });
        }
        File.WriteAllText(Folder + "Test_PlaybackReport.json", Newtonsoft.Json.JsonConvert.SerializeObject(reports, Newtonsoft.Json.Formatting.Indented));
        await Hold(effect, 3, 36);
        SetCamera(3, true);
        return reports;
    }

    private static string SaveCamera(Camera camera, string name, int width = 1024, int height = 768)
    {
        var target = RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32);
        var previous = camera.targetTexture;
        var active = RenderTexture.active;
        float aspect = camera.aspect;
        Texture2D pixels = null;
        string path = Folder + name;
        try
        {
            camera.targetTexture = target; camera.aspect = (float)width / height;
            camera.Render(); RenderTexture.active = target;
            pixels = new Texture2D(width,height,TextureFormat.RGB24,false);
            pixels.ReadPixels(new Rect(0,0,width,height),0,0); pixels.Apply();
            File.WriteAllBytes(path,pixels.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previous; camera.aspect = aspect; RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(target);
            if (pixels != null) UnityEngine.Object.Destroy(pixels);
        }
        return path;
    }
}
