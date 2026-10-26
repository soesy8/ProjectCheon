using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
public static class Test_FinalizeBossVfx
{
    public static object Main()
    {
        var dissolve = Shader.Find("Cheon/Tests/BossNoiseDissolve");
        var powder = Shader.Find("Cheon/Tests/DeathPowder");
        if (dissolve == null || powder == null || ShaderUtil.ShaderHasError(dissolve) || ShaderUtil.ShaderHasError(powder)) throw new Exception("VFX 셰이더 오류");
        var previews = Resources.FindObjectsOfTypeAll<GameObject>().Where(item => item.scene.IsValid() &&
            (item.name == "Test_DustMotionProbe" || item.name == "Test_IsolatedDustPreview" || item.name == "Test_DustDetailCamera")).ToArray();
        foreach (var item in previews) UnityEngine.Object.Destroy(item);
        return Newtonsoft.Json.JsonConvert.SerializeObject(new
        {
            shaderErrors = false, removedTemporaryPreviews = previews.Length,
            testScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Test/BossPatterns/Scenes/Test_BossPatterns.unity") != null,
            dissolveMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Test/BossPatterns/Materials/Test_M_BossDeath.mat") != null,
            powderMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Test/BossPatterns/Materials/Test_M_DeathPowder.mat") != null,
            activeScene = SceneManager.GetActiveScene().path
        });
    }
}
