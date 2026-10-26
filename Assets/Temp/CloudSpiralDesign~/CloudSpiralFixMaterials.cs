using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class CloudSpiralFixMaterials
{
    public static object Main()
    {
        foreach (string effect in new[] { "Slash", "Wind" })
        {
            string root = "Assets/Arts/Effects/CloudSpiral/";
            var material = AssetDatabase.LoadAssetAtPath<Material>(root + "Materials/M_CloudSpiral" + effect + ".mat");
            material.shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(root + "Textures/T_CloudSpiral" + effect + ".png"));
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Surface", 1);
            material.SetFloat("_Blend", 0);
            material.SetFloat("_Cull", 0);
            material.SetFloat("_ZWrite", 0);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ColorMode", 0);
            material.SetFloat("_FlipbookBlending", 0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.renderQueue = 3000;
            material.SetOverrideTag("RenderType", "Transparent");
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
        }
        return "Two materials configured with URP particle shader";
    }
}
