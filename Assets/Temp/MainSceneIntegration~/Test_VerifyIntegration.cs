using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using Cheon.Prototype.MountainPass;

public static class Test_VerifyIntegration
{
    private static readonly List<string> checks = new List<string>();
    private static PassGameFlow Game => ObjectFind<PassGameFlow>();
    private static T ObjectFind<T>() where T : UnityEngine.Object => UnityEngine.Object.FindFirstObjectByType<T>();
    private static void Check(bool condition, string message)
    { if (!condition) throw new Exception("FAILED: " + message); checks.Add(message); }
    private static async Task Frames(int count)
    {
        int end = Time.frameCount + count;
        double until = EditorApplication.timeSinceStartup + 12;
        while (Time.frameCount < end)
        {
            if (EditorApplication.timeSinceStartup > until) throw new Exception("Frames stopped advancing");
            await Task.Delay(10);
        }
    }
    private static async Task Until(Func<bool> predicate, float timeout = 8f)
    {
        double until = EditorApplication.timeSinceStartup + timeout;
        while (!predicate())
        { if (EditorApplication.timeSinceStartup > until) throw new Exception("Timed out waiting for state"); await Frames(1); }
    }
    private static async Task Key(Key key, int frames=2)
    {
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(key));
        await Frames(frames);
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
        await Frames(2);
    }

    public static object Edit()
    {
        var scene=SceneManager.GetActiveScene();
        Check(scene.path=="Assets/Scenes/MountainPass/MountainPass.unity", "Main scene open");
        var game=Game; var vfx=ObjectFind<PassFanAttackVfx>(); var so=new SerializedObject(vfx);
        Check(game.Gourd!=null, "Game flow gourd reference");
        foreach(string field in new[]{"animationControl","playerRoot","particles","windSector","attack1WindSector","profile"})
            Check(so.FindProperty(field).objectReferenceValue!=null, "VFX reference: "+field);
        Check(ObjectFind<PassRollDust>()!=null, "Roll dust connected");
        Check(game.Player.GetComponents<PassPlayerCombat>().Length==1 && game.Player.GetComponent<PassFanAttackCombat>()!=null, "One active fan combat implementation");
        var dependencies=AssetDatabase.GetDependencies(scene.path,true).Where(p=>p.StartsWith("Assets/Test/")).ToArray();
        Check(dependencies.Length==0, "Main scene has no test asset dependencies: "+string.Join(",",dependencies));
        var drink=new SerializedObject(game.Gourd);
        Check(drink.FindProperty("maximumUses").intValue==5 && drink.FindProperty("startingUses").intValue==3 && drink.FindProperty("healAmount").floatValue==40, "Recovery defaults: 3 / 5 / 40");
        var missing=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
        Check(missing==0, "No missing scripts");
        foreach(var path in AssetDatabase.FindAssets("t:Shader",new[]{"Assets/Arts/Effects/FanAttack"}).Select(AssetDatabase.GUIDToAssetPath))
        {
            var shader=AssetDatabase.LoadAssetAtPath<Shader>(path);
            Check(!ShaderUtil.ShaderHasError(shader), "Shader compiles: "+shader.name);
        }
        return new { checks, scene=scene.path, background=Application.runInBackground };
    }

    public static async Task<object> Drinking()
    {
        var game=Game; var drink=game.Gourd; var health=game.PlayerHealth;
        await Frames(4);
        game.BeginJourney(); await Frames(4);
        Check(drink.RemainingUses==3, "Journey starts with 3 uses");
        await Key(UnityEngine.InputSystem.Key.E);
        Check(drink.RemainingUses==3 && !drink.IsPlaying, "Full health does not consume");
        health.TakeDamage(65f, ignoreHitRecovery:true);
        await Until(()=>!game.Player.GetComponent<PassPlayerMovement>().AnimationControl.IsActionPlaying);
        await Key(UnityEngine.InputSystem.Key.E);
        Check(drink.IsPlaying, "E starts the drinking animation");
        await Until(()=>drink.RemainingUses==2);
        Check(Mathf.Approximately(health.Current,75f), "First sip heals exactly 40");
        await Until(()=>!drink.IsPlaying);
        await Key(UnityEngine.InputSystem.Key.E);
        await Until(()=>drink.RemainingUses==1);
        Check(Mathf.Approximately(health.Current,health.Maximum), "Healing clamps to maximum");
        await Until(()=>!drink.IsPlaying);
        health.TakeDamage(65f, ignoreHitRecovery:true);
        await Until(()=>!game.Player.GetComponent<PassPlayerMovement>().AnimationControl.IsActionPlaying);
        await Key(UnityEngine.InputSystem.Key.E);
        await Until(()=>drink.RemainingUses==0);
        await Until(()=>!drink.IsPlaying);
        await Key(UnityEngine.InputSystem.Key.E);
        Check(drink.RemainingUses==0 && !drink.IsPlaying, "Empty gourd cannot be used");
        game.RestAtShrine(game.Player.position,game.Player.rotation);
        Check(drink.RemainingUses==5 && health.Current==health.Maximum, "Shrine restores health and 5 uses");
        game.BeginJourney(); await Frames(3);
        Check(drink.RemainingUses==3, "New journey resets to 3 uses");
        health.TakeDamage(65f, ignoreHitRecovery:true);
        await Until(()=>!game.Player.GetComponent<PassPlayerMovement>().AnimationControl.IsActionPlaying);
        await Key(UnityEngine.InputSystem.Key.E);
        await Key(UnityEngine.InputSystem.Key.Space);
        Check(!drink.IsPlaying && drink.RemainingUses==3, "Dodge before sipping cancels without consumption");
        await Until(()=>!game.Player.GetComponent<PassPlayerMovement>().AnimationControl.IsActionPlaying);
        await Key(UnityEngine.InputSystem.Key.E);
        float progress=drink.Progress;
        game.TogglePause(); await Frames(8);
        Check(drink.IsPlaying && Mathf.Approximately(progress,drink.Progress), "Pause freezes drinking");
        game.TogglePause();
        health.TakeDamage(1f, ignoreHitRecovery:true);
        Check(!drink.IsPlaying && drink.RemainingUses==3, "Damage before sipping cancels without consumption");
        await Until(()=>!game.Player.GetComponent<PassPlayerMovement>().AnimationControl.IsActionPlaying);
        await Key(UnityEngine.InputSystem.Key.E);
        await Key(UnityEngine.InputSystem.Key.W,20);
        Check(game.Player.GetComponent<PassPlayerMovement>().IsMoving || drink.IsPlaying, "Drinking permits movement");
        await Until(()=>!drink.IsPlaying);
        Check(drink.RemainingUses==2, "Moving sip consumes once");
        var attachment=drink.GetComponent<PassGourdAttachment>();
        Check(!attachment.IsInHand && attachment.Gourd.parent==attachment.BeltSocket, "Gourd returns to belt");
        return new { checks, frame=Time.frameCount, uses=drink.RemainingUses, health=health.Current };
    }

    public static async Task<object> Shrine()
    {
        var game=Game; var movement=game.Player.GetComponent<PassPlayerMovement>();
        game.BeginJourney(); await Frames(3);
        game.RetryCheckpoint(); await Frames(3);
        Check(game.Gourd.RemainingUses==3,"Retry before shrine starts with 3 uses");
        movement.Teleport(game.Shrine.Position,Quaternion.identity); await Frames(4);
        Check(game.Shrine.CanInteract,"Player can interact at shrine");
        await Key(UnityEngine.InputSystem.Key.F);
        Check(game.Shrine.IsPraying,"F starts shrine prayer");
        await Until(()=>!game.Shrine.IsPraying);
        Check(game.Gourd.RemainingUses==5 && game.ReachedShrine,"Actual shrine prayer fills all 5 uses");
        game.PlayerHealth.Kill(); await Frames(3);
        Check(game.State==PassGameFlow.RunState.Defeat,"Death enters defeat state");
        game.RetryCheckpoint(); await Frames(3);
        Check(game.Gourd.RemainingUses==5 && game.PlayerHealth.Current==game.PlayerHealth.Maximum,"Shrine checkpoint retry restores health and uses");
        game.BeginBossChallenge(); if(game.Entrance!=null)game.Entrance.SkipPresentation(); await Frames(3);
        Check(game.Gourd.RemainingUses==3,"New boss challenge starts with 3 uses");
        return new { checks, frame=Time.frameCount };
    }

    public static async Task<object> FanCombat()
    {
        var game=Game; var movement=game.Player.GetComponent<PassPlayerMovement>();
        var animation=movement.AnimationControl; var vfx=ObjectFind<PassFanAttackVfx>();
        var combat=game.Player.GetComponent<PassFanAttackCombat>();
        var boss=(PassBossPatternController)game.Boss;
        game.BeginBossChallenge();if(game.Entrance!=null)game.Entrance.SkipPresentation();await Frames(3);
        boss.enabled=false;game.PlayerHealth.Invulnerable=true;game.View.LockBoss(false);
        boss.Health.Configure(10000,0f);boss.Health.Restore();
        var position=boss.transform.position+boss.transform.forward*2.8f;
        var hit=Physics.RaycastAll(position+Vector3.up*5,Vector3.down,10,~0,QueryTriggerInteraction.Ignore)
            .Where(h=>h.normal.y>.65f && h.collider.GetComponentInParent<PassHealth>()==null).OrderBy(h=>h.distance).First();
        movement.Teleport(hit.point,Quaternion.LookRotation(-boss.transform.forward));await Frames(3);
        animation.EnterCombat();await Until(()=>!animation.IsActionPlaying);
        var observations=new List<object>();
        for(int attack=1;attack<=3;attack++)
        {
            combat.ResetHits();boss.Health.Restore();
            bool visible=false,wind=false;
            await Key(attack==1?UnityEngine.InputSystem.Key.Digit1:attack==2?UnityEngine.InputSystem.Key.Digit2:UnityEngine.InputSystem.Key.Digit3);
            Check(animation.SelectedState=="ATTACK"+attack,"Attack "+attack+" accepts key input");
            while(animation.SelectedState=="ATTACK"+attack)
            {
                visible|=vfx.IsSweepVisible;wind|=vfx.CloudWindBranchCount>0;
                await Frames(1);
            }
            Check(visible,"Attack "+attack+" sweep is visible");
            Check(combat.DirectHitsLanded>0,"Attack "+attack+" direct damage connects");
            if(attack!=2)
            {
                Check(wind,"Attack "+attack+" wind is visible");
                Check(combat.WindHitsLanded>0,"Attack "+attack+" wind damage connects");
            }
            observations.Add(new{attack,direct=combat.DirectHitsLanded,wind=combat.WindHitsLanded,damage=10000-boss.Health.Current});
            await Frames(10);
        }
        var roll=ObjectFind<PassRollDust>();
        await Key(UnityEngine.InputSystem.Key.Space);
        await Until(()=>!animation.IsActionPlaying);
        Check(roll.TrailParticles>0 || roll.LandingParticles>0,"Dodge emits dust");
        return new{checks,observations,frame=Time.frameCount};
    }

    public static async Task<object> ShowDrinking()
    {
        var game=Game;var movement=game.Player.GetComponent<PassPlayerMovement>();
        game.PlayerHealth.Configure(100,.65f);game.BeginJourney();await Frames(4);
        game.PlayerHealth.TakeDamage(65,ignoreHitRecovery:true);
        await Until(()=>!movement.AnimationControl.IsActionPlaying);
        var camera=Camera.main; game.View.enabled=false;
        Vector3 focus=game.Player.position+Vector3.up*1.2f;
        camera.transform.position=focus+game.Player.forward*2.7f-game.Player.right*2f+Vector3.up*.3f;
        camera.transform.LookAt(focus);camera.fieldOfView=42;
        await Key(UnityEngine.InputSystem.Key.E);
        await Until(()=>game.Gourd.Phase==PassGourdDrinking.DrinkingPhase.Drinking && game.Gourd.Progress>=.45f);
        Time.timeScale=0;
        var prop=game.Gourd.GetComponent<PassGourdAttachment>();
        return new{phase=game.Gourd.Phase.ToString(),progress=game.Gourd.Progress,mouthDistance=prop.MouthDistance,remaining=game.Gourd.RemainingUses,health=game.PlayerHealth.Current};
    }
}
