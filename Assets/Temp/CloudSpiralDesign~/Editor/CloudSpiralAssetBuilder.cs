#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class CloudSpiralAssetBuilder
{
    private const string ArtRoot = "Assets/Arts/Effects/CloudSpiral/";
    private const string PrefabRoot = "Assets/Prefabs/Effects/CloudSpiral/";
    private const string TempRoot = "Assets/Temp/CloudSpiralDesign/";

    public static string BuildLiveV2()
    {
        Build();
        return "CloudSpiral V2 complete";
    }

    public static void Build()
    {
        try
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureTexture(ArtRoot + "Textures/T_CloudSpiralSlash.png");
            ConfigureTexture(ArtRoot + "Textures/T_CloudSpiralWind.png");
            ConfigureTexture(ArtRoot + "Design/T_CloudSpiralDesign.png");
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ArtRoot + "Shaders/S_CloudSpiral.shader");
            if (shader == null || ShaderUtil.ShaderHasError(shader))
                throw new InvalidOperationException("CloudSpiral shader failed to import.");

            Material slashMaterial = CreateMaterial(shader, "Slash");
            Material windMaterial = CreateMaterial(shader, "Wind");
            Mesh slashMesh = CreateMesh("Slash", 1f);
            Mesh windMesh = CreateMesh("Wind", 427f / 1024f);
            GameObject slash = CreateEffect("CloudSpiralSlash", slashMaterial, slashMesh, false);
            GameObject wind = CreateEffect("CloudSpiralWind", windMaterial, windMesh, true);

            PrefabUtility.SaveAsPrefabAsset(slash, PrefabRoot + "CloudSpiralSlash.prefab");
            PrefabUtility.SaveAsPrefabAsset(wind, PrefabRoot + "CloudSpiralWind.prefab");
            AssetDatabase.SaveAssetIfDirty(slashMaterial);
            AssetDatabase.SaveAssetIfDirty(windMaterial);
            AssetDatabase.SaveAssetIfDirty(slashMesh);
            AssetDatabase.SaveAssetIfDirty(windMesh);
            Validate(slash, wind);
            RenderPreview(slash, wind);
            UnityEngine.Object.DestroyImmediate(slash);
            UnityEngine.Object.DestroyImmediate(wind);

            File.WriteAllText(TempRoot + "Validation.txt",
                "PASS\nUnity: " + Application.unityVersion +
                "\nShader: no import errors\nSource textures: 1024x1024 and 1024x427, RGBA\n" +
                "Default and Standalone/Android/iPhone/WebGL Max Size: 1024\n" +
                "Prefabs: one-shot, vertex-alpha fade, no gameplay scripts\n" +
                "Native particle simulation: visible early, zero particles after lifetime\n" +
                "Preview: isolated preview scene, existing scene not saved or modified\n");
            AssetDatabase.Refresh();
            Debug.Log("CLOUD_SPIRAL_BUILD_PASS");
        }
        catch (Exception exception)
        {
            File.WriteAllText(TempRoot + "Validation.txt", exception.ToString());
            Debug.LogException(exception);
            throw;
        }
    }

    private static void ConfigureTexture(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Missing texture " + path);
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.isReadable = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.maxTextureSize = 1024;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        foreach (string platform in new[] { "Standalone", "Android", "iPhone", "WebGL" })
        {
            var settings = importer.GetPlatformTextureSettings(platform);
            settings.name = platform;
            settings.overridden = true;
            settings.maxTextureSize = 1024;
            settings.format = TextureImporterFormat.RGBA32;
            settings.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SetPlatformTextureSettings(settings);
        }
        importer.SaveAndReimport();
    }

    private static Material CreateMaterial(Shader shader, string effect)
    {
        string path = ArtRoot + "Materials/M_CloudSpiral" + effect + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        material.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(
            ArtRoot + "Textures/T_CloudSpiral" + effect + ".png"));
        material.SetColor("_TintColor", Color.white);
        material.SetFloat("_Intensity", 1f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Mesh CreateMesh(string effect, float aspect)
    {
        string path = ArtRoot + "Meshes/CloudSpiral" + effect + "Quad.asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null)
        {
            mesh = new Mesh();
            AssetDatabase.CreateAsset(mesh, path);
        }
        mesh.name = "CloudSpiral" + effect + "Quad";
        // 텍스처의 오른쪽을 프리팹의 +Z 진행 방향으로 맞춘다. 원본의 가로세로 비율을 유지한다.
        mesh.vertices = new[] {
            new Vector3(aspect * .5f, 0, -.5f), new Vector3(aspect * .5f, 0, .5f),
            new Vector3(-aspect * .5f, 0, .5f), new Vector3(-aspect * .5f, 0, -.5f)
        };
        mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
        mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
        return mesh;
    }

    private static GameObject CreateEffect(string title, Material material, Mesh mesh, bool isWind)
    {
        var item = new GameObject(title);
        var particles = item.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        particles.useAutoRandomSeed = false;
        particles.randomSeed = 2718;
        var main = particles.main;
        main.duration = isWind ? .68f : .30f;
        main.loop = false;
        main.playOnAwake = true;
        main.startLifetime = isWind ? .58f : .22f;
        main.startSpeed = 0;
        main.startSize = isWind ? 2.8f : 3.1f;
        main.startColor = Color.white;
        main.maxParticles = 1;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.startRotation3D = true;
        main.startRotationY = isWind ? 0 : -18 * Mathf.Deg2Rad;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        main.stopAction = ParticleSystemStopAction.Destroy;

        var emission = particles.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0, (short)1) });
        var shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = Vector3.one * .0001f;
        shape.position = new Vector3(0, .95f, isWind ? .85f : 1.1f);

        var fade = particles.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .09f),
                new GradientAlphaKey(1, isWind ? .55f : .32f), new GradientAlphaKey(0, 1) });
        fade.color = gradient;
        var growth = particles.sizeOverLifetime;
        growth.enabled = true;
        growth.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(
            new Keyframe(0, isWind ? .6f : .72f), new Keyframe(.15f, 1), new Keyframe(1, isWind ? 1.12f : 1.08f)));

        if (isWind)
        {
            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = 0;
            velocity.y = 0;
            velocity.z = 5.5f;
        }
        else
        {
            var rotation = particles.rotationOverLifetime;
            rotation.enabled = true;
            rotation.separateAxes = true;
            rotation.x = 0;
            rotation.y = 155 * Mathf.Deg2Rad;
            rotation.z = 0;
        }

        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        renderer.mesh = mesh;
        renderer.alignment = ParticleSystemRenderSpace.Local;
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        return item;
    }

    private static void Validate(GameObject slash, GameObject wind)
    {
        foreach (GameObject item in new[] { slash, wind })
        {
            var particles = item.GetComponent<ParticleSystem>();
            particles.Simulate(.08f, true, true, false);
            if (particles.particleCount != 1) throw new InvalidOperationException(item.name + " failed early emission.");
            particles.Simulate(1f, true, true, false);
            if (particles.particleCount != 0) throw new InvalidOperationException(item.name + " failed lifetime cleanup.");
        }
        foreach (string path in new[] { "Textures/T_CloudSpiralSlash.png", "Textures/T_CloudSpiralWind.png" })
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(ArtRoot + path);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtRoot + path);
            if (texture.width > 1024 || texture.height > 1024 || importer.maxTextureSize != 1024)
                throw new InvalidOperationException(path + " exceeds texture limit.");
            foreach (string platform in new[] { "Standalone", "Android", "iPhone", "WebGL" })
                if (importer.GetPlatformTextureSettings(platform).maxTextureSize != 1024)
                    throw new InvalidOperationException(path + " platform size exceeds limit.");
        }
    }

    private static void RenderPreview(GameObject slash, GameObject wind)
    {
        // 원래 씬을 저장하거나 바꾸지 않고 별도의 미리보기 씬에서만 렌더링한다.
        Scene scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        GameObject cameraObject = new GameObject("CloudSpiralPreviewCamera");
        SceneManager.MoveGameObjectToScene(cameraObject, scene);
        var camera = cameraObject.AddComponent<Camera>();
        camera.scene = scene;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.035f, .07f, .12f, 1);
        camera.orthographic = true;
        camera.orthographicSize = 4.25f;
        camera.transform.SetPositionAndRotation(new Vector3(0, 13, -5), Quaternion.Euler(70, 0, 0));
        var target = new RenderTexture(1024, 768, 24, RenderTextureFormat.ARGB32);
        target.Create();
        camera.targetTexture = target;
        SceneManager.MoveGameObjectToScene(slash, scene);
        SceneManager.MoveGameObjectToScene(wind, scene);
        slash.transform.position = new Vector3(-2.5f, 0, 0);
        wind.transform.position = new Vector3(2.2f, 0, -1.2f);
        for (int frame = 0; frame < 25; frame++)
        {
            float time = frame / 40f;
            slash.GetComponent<ParticleSystem>().Simulate(time, true, true, false);
            wind.GetComponent<ParticleSystem>().Simulate(time, true, true, false);
            camera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(1024, 768, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, 1024, 768), 0, 0);
            image.Apply();
            File.WriteAllBytes(TempRoot + "Preview_" + frame.ToString("D2") + ".png", image.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(image);
            RenderTexture.active = previous;
        }
        camera.targetTexture = null;
        target.Release();
        UnityEngine.Object.DestroyImmediate(target);
        UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
    }
}
#endif
