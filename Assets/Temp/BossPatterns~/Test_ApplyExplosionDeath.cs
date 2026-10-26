using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Cheon.Tests.BossPatterns;

public static class Test_ApplyExplosionDeath
{
    public static object Main()
    {
        var scene = SceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != "Assets/Test/BossPatterns/Scenes/Test_BossPatterns.unity") throw new Exception("테스트 씬 Edit Mode 전용");
        var settings = UnityEngine.Object.FindFirstObjectByType<Test_BossPatternSettings>();
        Undo.RecordObject(settings, "전방 원형 폭발 및 장승 사망 VFX");
        settings.crack.windup = .5f; settings.crack.stepCount = 5; settings.crack.stepInterval = .15f;
        settings.crack.windupTilt = 20f; settings.crack.strikeTilt = 30f;
        settings.crack.color = new Color(.3f, .16f, .055f, .65f);
        var ringMaterial = settings.crack.material;
        Undo.RecordObject(ringMaterial, "원형 충격파 투명도");
        ringMaterial.SetFloat("_Surface", 1f); ringMaterial.SetFloat("_Blend", 0f);
        ringMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        ringMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        ringMaterial.SetFloat("_ZWrite", 0f); ringMaterial.SetFloat("_Cull", 0f);
        ringMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        ringMaterial.SetOverrideTag("RenderType", "Transparent"); ringMaterial.renderQueue = (int)RenderQueue.Transparent;
        ringMaterial.SetColor("_BaseColor", settings.crack.color); EditorUtility.SetDirty(ringMaterial);
        const string materialPath = "Assets/Test/BossPatterns/Materials/Test_M_BossDeath.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        var shader = Shader.Find("Cheon/Tests/BossNoiseDissolve");
        if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new Exception("노이즈 소멸 셰이더 오류");
        if (material == null)
        {
            material = new Material(shader) { name = "Test_M_BossDeath" };
            AssetDatabase.CreateAsset(material, materialPath);
        }
        settings.death.material = material;
        const string powderPath = "Assets/Test/BossPatterns/Materials/Test_M_DeathPowder.mat";
        var powder = AssetDatabase.LoadAssetAtPath<Material>(powderPath);
        var powderShader = Shader.Find("Cheon/Tests/DeathPowder");
        if (powderShader == null || ShaderUtil.ShaderHasError(powderShader)) throw new Exception("가루 셰이더 오류");
        if (powder == null)
        {
            powder = new Material(powderShader) { name = "Test_M_DeathPowder" };
            AssetDatabase.CreateAsset(powder, powderPath);
        }
        settings.death.powderMaterial = powder;
        settings.death.particleSize = new Vector2(.06f, .14f);
        settings.death.particleColor = new Color(.58f, .32f, .72f, .95f);
        settings.crack.centerDustScale = 3f; settings.crack.centerDustCount = 24;
        settings.crack.mediumDustScale = 1.3f; settings.crack.smallDustScale = .6f;
        EditorUtility.SetDirty(settings); EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        if (!EditorSceneManager.SaveScene(scene)) throw new Exception("씬 저장 실패");
        Selection.activeGameObject = settings.gameObject;
        return Newtonsoft.Json.JsonConvert.SerializeObject(new
        {
            saved = !scene.isDirty, settings.crack.windup, settings.crack.stepCount, settings.crack.stepInterval,
            settings.crack.windupTilt, settings.crack.strikeTilt, settings.crack.recoilDuration,
            shader = shader.name, settings.death.tintDuration, settings.death.dissolveDuration
        });
    }
}
