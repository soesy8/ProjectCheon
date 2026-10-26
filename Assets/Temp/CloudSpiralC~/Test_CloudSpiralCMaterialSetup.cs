using System;
using UnityEditor;
using UnityEngine;

public static class Test_CloudSpiralCMaterialSetup
{
    public static object Main()
    {
        string root = "Assets/Test/FanAttackVfx/";
        AssetDatabase.ImportAsset(root + "Shaders/Test_S_CloudSpiral_C.shader", ImportAssetOptions.ForceSynchronousImport);
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(root + "Shaders/Test_S_CloudSpiral_C.shader");
        if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new Exception("C shader import failed");
        foreach (string effect in new[] { "Slash", "Wind" })
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(root + "Materials/Test_M_CloudSpiral" + effect + "_C.mat");
            material.shader = shader;
            material.shaderKeywords = Array.Empty<string>();
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/Arts/Effects/CloudSpiral/Textures/T_CloudSpiral" + effect + ".png"));
            material.SetColor("_BaseColor", Color.white);
            material.renderQueue = 3000;
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
        }
        return "C materials use cloud texture shader without distance fog";
    }
}
