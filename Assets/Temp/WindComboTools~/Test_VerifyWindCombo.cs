using System;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Cheon.Combat;
using Object = UnityEngine.Object;

public static class Test_VerifyWindCombo
{
    public static object Run()
    {
        var attack = Object.FindFirstObjectByType<WindComboAttack>();
        var input = attack.GetComponent<WindComboPlayer>();
        input.AutoDemonstration = false;
        input.enabled = false;
        attack.CancelAttack();
        var controller = attack.GetComponent<CharacterController>();
        controller.enabled=false;
        attack.transform.SetPositionAndRotation(new Vector3(0,.07f,-18),Quaternion.Euler(0,90,0));
        controller.enabled=true;
        Time.timeScale=1;
        Time.captureDeltaTime=1f/60;
        var targets = Object.FindObjectsByType<WindComboTarget>(FindObjectsSortMode.None).OrderBy(t=>t.name).ToArray();
        var before=targets.Select(t=>t.HitCount).ToArray();
        var damages=targets.Select(t=>t.TotalDamage).ToArray();
        var log=new List<string>();
        var strikes=new List<float>();
        Action<int,float> record=(stage,time)=> { strikes.Add(time); log.Add("strike "+stage+" at "+time.ToString("F5")); };
        attack.Strike+=record;
        int frame=Time.frameCount;
        int startFrame=frame;
        float started=Time.time;
        bool[] captured=new bool[4];
        attack.RequestAttack();
        IEnumerator Sequence()
        {
            while (Time.time-started<1.85f)
            {
                yield return null;
                frame=Time.frameCount;
                if (attack.Stage<3 && attack.Stage>0 && attack.Elapsed>=.06f && !attack.HasBufferedInput) attack.RequestAttack();
                if (attack.Stage==1 && attack.Elapsed>=.10f && !captured[0]) { Capture("01_Brush"); captured[0]=true; }
                if (attack.Stage==2 && attack.Elapsed>=.12f && !captured[1]) { Capture("02_Return"); captured[1]=true; }
                if (attack.Stage==3 && attack.Elapsed>=.30f && !captured[2]) { Capture("03_Gather"); captured[2]=true; }
                if (attack.Stage==3 && attack.Elapsed>=.53f && !captured[3]) { Capture("04_Release"); captured[3]=true; }
            }
            attack.Strike-=record;
            log.Add("frames advanced: "+(Time.frameCount-startFrame));
            log.Add("intervals: "+(strikes.Count==3 ? (strikes[1]-strikes[0]).ToString("F5")+", "+(strikes[2]-strikes[1]).ToString("F5"):"INVALID"));
            log.Add("stage after full combo: "+attack.Stage);
            log.Add("step distance: "+attack.transform.position.x.ToString("F5"));
            for(int i=0;i<targets.Length;i++) log.Add(targets[i].name+": hits="+(targets[i].HitCount-before[i])+", damage="+(targets[i].TotalDamage-damages[i]));
            log.Add("remaining projectiles: "+Object.FindObjectsByType<WindComboProjectile>(FindObjectsSortMode.None).Length);
            File.WriteAllLines("Assets/Temp/WindComboPreviews/Verification.txt",log);
            input.enabled=true;
            Time.captureDeltaTime=0;
        }
        attack.StartCoroutine(Sequence());
        return "Verification started; writes Verification.txt after 1.85 game seconds.";
    }
    public static void Capture(string name)
    {
        var camera=Camera.main;
        var previous=camera.targetTexture;
        var active=RenderTexture.active;
        var render=new RenderTexture(1280,720,24);
        var pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);
        try
        {
            camera.targetTexture=render;
            camera.Render();
            RenderTexture.active=render;
            pixels.ReadPixels(new Rect(0,0,1280,720),0,0); pixels.Apply();
            File.WriteAllBytes("Assets/Temp/WindComboPreviews/"+name+".png",pixels.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture=previous; RenderTexture.active=active;
            Object.DestroyImmediate(pixels); render.Release(); Object.DestroyImmediate(render);
        }
    }
}
