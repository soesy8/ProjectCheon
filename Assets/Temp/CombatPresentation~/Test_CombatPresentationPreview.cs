using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Cheon.Prototype.MountainPass;
using Cheon.Tests.CombatPresentation;
using UnityEngine;
using Object=UnityEngine.Object;

public static class Test_CombatPresentationPreview
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static PassGameFlow flow;
    static PassBossPatternController boss;
    static PassPlayerMovement player;
    static void Load()
    {
        flow=Object.FindFirstObjectByType<PassGameFlow>();boss=(PassBossPatternController)flow.Boss;player=flow.Player.GetComponent<PassPlayerMovement>();
        UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
        typeof(PassCamera).GetField("applicationFocused",Flags).SetValue(flow.View,true);
    }
    public static async Task<object> Overview()
    {
        Load(); await Until(()=>!flow.IsTransitioning);
        if(!flow.InEncounter)
        {
            flow.BeginBossChallenge(); await Until(()=>!flow.IsTransitioning);
            if(flow.Entrance!=null) flow.Entrance.SkipPresentation();
            await Until(()=>!flow.IsTransitioning && flow.State==PassGameFlow.RunState.Playing);
        }
        boss.enabled=false;flow.PlayerHealth.Invulnerable=true;
        typeof(PassBossPatternController).GetMethod("SetTilt",Flags).Invoke(boss,new object[]{0f});
        Place(15f);flow.View.LockBoss(true);
        await Frames(120);
        return Report("Overview");
    }
    public static async Task<object> Close()
    {
        Load();Place(3.8f);flow.View.LockBoss(true);await Frames(100);return Report("Close");
    }
    public static async Task<object> FreeClose()
    {
        Load();Place(3.8f);flow.View.LockBoss(false);await Frames(100);return Report("FreeClose");
    }
    public static async Task<object> ActualHit()
    {
        Load();typeof(PassBossPatternController).GetMethod("SetTilt",Flags).Invoke(boss,new object[]{0f});
        Place(boss.Body.radius+1.2f);flow.View.LockBoss(true);await Frames(100);
        var animation=player.AnimationControl;var combat=player.GetComponent<PassFanAttackCombat>();
        boss.Health.Invulnerable=false;flow.PlayerHealth.Invulnerable=true;
        if(!animation.IsCombatRequested)
        {animation.TryAttackInput(1,false,false);await Until(()=>!animation.IsEquipmentTransition);}
        await Until(()=>!animation.IsActionPlaying);
        combat.ResetHits();boss.Health.Restore();boss.Health.Invulnerable=false;
        if(!animation.TryAttackInput(1,false,false))throw new Exception("Attack was rejected");
        await Until(()=>combat.DirectHitsLanded>0);
        ScreenCapture.CaptureScreenshot("Assets/Temp/CombatPresentation~/Test_ActualHit.png");
        int particles=Object.FindFirstObjectByType<PassHitEffects>().ActiveParticles;
        await Until(()=>!animation.IsActionPlaying);await Frames(3);
        var result=new {direct=combat.DirectHitsLanded,wind=combat.WindHitsLanded,bossHealth=boss.Health.Current,particles,
            passed=combat.DirectHitsLanded==1 && combat.WindHitsLanded==1 && Mathf.Abs(boss.Health.Current-985)<.01f};
        File.WriteAllText("Assets/Temp/CombatPresentation~/Test_ActualHit.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
        if(!result.passed)throw new Exception("Hit regression");
        return result;
    }
    public static async Task<object> LiveBattle()
    {
        Load();if(flow.State==PassGameFlow.RunState.Paused){flow.TogglePause();await Until(()=>!flow.IsTransitioning);}
        await Overview();boss.enabled=true;int frame=Time.frameCount;int attacks=boss.AttacksPerformed;
        await Frames(420);boss.enabled=false;
        var result=new {frames=Time.frameCount-frame,bossPhase=boss.Phase.ToString(),attacks=boss.AttacksPerformed-attacks,flow.IsRunning};
        File.WriteAllText("Assets/Temp/CombatPresentation~/Test_LiveBattle.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
        return result;
    }
    public static async Task<object> Fallen()
    {
        Load();typeof(PassBossPatternController).GetMethod("SetTilt",Flags).Invoke(boss,new object[]{85f});
        Vector3 head=flow.View.GetComponent<Test_BossFraming>().HeadPoint;
        Vector3 desired=head+boss.transform.right*6f-boss.transform.forward*2f;desired.y=boss.transform.position.y+.05f;
        player.Teleport(desired,Quaternion.LookRotation(Vector3.ProjectOnPlane(boss.transform.position-desired,Vector3.up)));
        flow.View.SnapBehind();flow.View.LockBoss(true);await Frames(110);return Report("Fallen");
    }
    public static async Task<object> Pause()
    {
        Load();Object.FindFirstObjectByType<Test_PausePresentation>().TogglePauseImmediately();await Frames(5);
        return new {state=flow.State.ToString(),fullHelp=GameObject.Find("Test_PauseControls")!=null,flow.IsTransitioning};
    }
    public static async Task<object> VerifyPause()
    {
        Load();
        var hud=Object.FindFirstObjectByType<PassHud>();
        var pause=hud.GetComponent<Test_PausePresentation>();
        var fade=hud.GetComponent<PassScreenFade>();
        Transform canvas=hud.transform.Find("MountainPassHUD");
        var panel=canvas.Find("Result").GetComponent<UnityEngine.UI.Image>();
        var veil=canvas.Find("ScreenFade").GetComponent<UnityEngine.UI.Image>();
        var button=canvas.Find("Result/ResultColumn/PrimaryAction").GetComponent<UnityEngine.UI.Button>();
        await Until(()=>!flow.IsTransitioning);
        pause.TogglePauseImmediately();
        bool directImmediate=flow.State==PassGameFlow.RunState.Paused && !fade.IsTransitioning;
        await Frames(3);
        Vector3 cameraPosition=flow.View.transform.position;
        await Frames(35);
        bool panelCorrect=panel.color==new Color(0,0,0,.7f) && veil.color.a<.001f;
        bool frozen=Time.timeScale==0 && Vector3.Distance(cameraPosition,flow.View.transform.position)<.001f;
        button.onClick.Invoke();
        bool buttonImmediate=flow.State==PassGameFlow.RunState.Playing && !fade.IsTransitioning && Time.timeScale==1;
        await Frames(3);
        var keyboard=UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
        bool escapePause=false,escapeResume=false;
        try
        {
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.Escape));
            await Frames(2);
            escapePause=flow.State==PassGameFlow.RunState.Paused && !fade.IsTransitioning && veil.color.a<.001f;
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState());
            await Frames(2);
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.Escape));
            await Frames(2);
            escapeResume=flow.State==PassGameFlow.RunState.Playing && !fade.IsTransitioning && Time.timeScale==1;
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState());
            await Frames(2);
        }
        finally {UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard);}
        var result=new {directImmediate,buttonImmediate,escapePause,escapeResume,panelCorrect,frozen,
            passed=directImmediate&&buttonImmediate&&escapePause&&escapeResume&&panelCorrect&&frozen};
        File.WriteAllText("Assets/Temp/CombatPresentation~/Test_PauseVerification.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
        if(!result.passed)throw new Exception(Newtonsoft.Json.JsonConvert.SerializeObject(result));
        return result;
    }
    static void Place(float distance)
    {
        Vector3 desired=boss.transform.position+boss.transform.forward*distance;
        desired.y=boss.transform.position.y+.04f;
        player.Teleport(desired,Quaternion.LookRotation(Vector3.ProjectOnPlane(boss.transform.position-desired,Vector3.up)));
        player.AnimationControl.PlayIdle();flow.View.SnapBehind();
    }
    static object Report(string name)
    {
        var camera=flow.View.GetComponent<Camera>();var framing=flow.View.GetComponent<Test_BossFraming>();
        Vector3 head=camera.WorldToViewportPoint(framing.HeadPoint),feet=camera.WorldToViewportPoint(player.transform.position);
        var result=new {name,frame=Time.frameCount,state=flow.State.ToString(),head=head.ToString(),feet=feet.ToString(),framing.CurrentRetreat,
            headVisible=head.z>0 && head.x>.04f && head.x<.96f && head.y>.2f && head.y<.89f,
            feetVisible=feet.z>0 && feet.y>.12f && feet.y<.9f};
        File.WriteAllText("Assets/Temp/CombatPresentation~/Test_"+name+".json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
        return result;
    }
    static async Task Frames(int count)
    {
        int target=Time.frameCount+count;var watch=System.Diagnostics.Stopwatch.StartNew();
        while(Time.frameCount<target) {if(watch.Elapsed.TotalSeconds>15)throw new Exception("Player loop stopped");await Task.Delay(1);}
    }
    static async Task Until(Func<bool> ready)
    {
        var watch=System.Diagnostics.Stopwatch.StartNew();
        while(!ready()){if(watch.Elapsed.TotalSeconds>15)throw new Exception("State timed out");await Task.Delay(1);}
    }
}
