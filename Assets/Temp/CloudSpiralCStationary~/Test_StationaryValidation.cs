using System;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Cheon.Tests.FanAttackVfx;

public static class Test_StationaryValidation
{
    public static async Task<object> Main()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Play mode required");
        var profile = AssetDatabase.LoadAssetAtPath<Test_FanAttackVfxProfile>(
            "Assets/Test/FanAttackVfx/Settings/Test_FanAttackVfxProfile.asset");
        var player = new GameObject("Test_StationaryOwner");
        var root = new GameObject("Test_StationaryProbe");
        player.transform.position = root.transform.position = Vector3.up * 1000;
        var adapter = root.AddComponent<Test_FanCloudSpiralVfx>();
        adapter.Configure(profile.cloudSpiral, player.transform);
        try
        {
            Time.timeScale = 1;
            foreach (int attack in new[] { 1, 2, 3 })
            {
                adapter.Hide();
                adapter.ShowSweep(profile.GetSweep(attack), .22f, 1);
                var renderer = root.transform.Find("Test_C_Slash").GetComponent<MeshRenderer>();
                var initial = renderer.transform.localToWorldMatrix;
                var properties = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(properties);
                var fade = (Texture2D)properties.GetTexture("_SweepMask");
                byte before = fade.GetPixels32()[12].r;
                await Task.Delay(120);
                player.transform.position += new Vector3(2, 0, 1);
                player.transform.rotation *= Quaternion.Euler(0, 45, 0);
                root.transform.SetPositionAndRotation(player.transform.position, player.transform.rotation);
                adapter.ShowSweep(profile.GetSweep(attack), .8f, 1);
                var after = renderer.transform.localToWorldMatrix;
                for (int index = 0; index < 16; index++)
                    if (Mathf.Abs(initial[index] - after[index]) > .0001f)
                        throw new Exception("Sweep image moved: attack " + attack);
                var pixels = fade.GetPixels32();
                if (pixels[12].r >= before || pixels[90].r <= pixels[12].r || pixels[127].r != 0)
                    throw new Exception("Reveal order or independent fading failed: " + attack);
                await Task.Delay(350);
                adapter.ShowSweep(profile.GetSweep(attack), .8f, 1);
                if (adapter.IsSweepVisible) throw new Exception("Completed sections did not fade out: " + attack);
            }
            float forward = profile.attack1Wind.radius * profile.attack1Wind.forwardScale;
            if (Mathf.Abs(forward - 4.4f) > .0001f) throw new Exception("Wrong first wind extent");
            File.WriteAllText("Assets/Temp/CloudSpiralCStationary~/Test_ValidationReport.txt",
                "PASS\nAttacks 1/2/3: world position, rotation and scale unchanged while owner moves\n" +
                "Earlier sections fade independently; newly revealed sections brighter; future sections hidden\n" +
                "Visible sections fully fade after their own lifetime\nFirst wind forward extent: 4.4m\n");
            return "Stationary geometry, ordered reveal and local fading passed for 1/2/3; first wind extent=4.4m";
        }
        finally
        {
            UnityEngine.Object.Destroy(root);
            UnityEngine.Object.Destroy(player);
        }
    }
}
