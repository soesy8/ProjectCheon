using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Cheon.Prototype.MountainPass;
using Cheon.Tests.CombatPresentation;

public static class Test_FootstepSyncVerification
{
    static PassGameFlow game;
    static PassPlayerMovement player;
    static Test_PlayerFoley foley;
    static Keyboard keyboard;
    static readonly List<object> contacts=new List<object>();
    static readonly Dictionary<string,object> checks=new Dictionary<string,object>();
    static Vector3 stonePosition;

    public static async Task<object> Run()
    {
        if(!Application.isPlaying) throw new Exception("Play Mode required");
        var previous=Keyboard.current;
        var originalSettings=InputSystem.settings;
        var testSettings=UnityEngine.Object.Instantiate(originalSettings);
        testSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        testSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings=testSettings;
        keyboard=InputSystem.AddDevice<Keyboard>();keyboard.MakeCurrent();
        game=UnityEngine.Object.FindFirstObjectByType<PassGameFlow>();
        player=UnityEngine.Object.FindFirstObjectByType<PassPlayerMovement>();
        foley=player.GetComponent<Test_PlayerFoley>();
        try
        {
            UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
            game.BeginJourney();await Until(()=>game.IsRunning);
            game.PlayerHealth.Invulnerable=true;
            var floor=UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None).First(c=>c.name=="Floor");
            if(!floor.Raycast(new Ray(floor.bounds.center+Vector3.up*5,Vector3.down),out var hit,10))throw new Exception("Stone floor missing");
            stonePosition=hit.point+Vector3.up*.05f;
            await MoveToStone();
            await Walk("walk",false,1.4f);
            await MoveToStone();
            await Walk("sprint",true,1.15f);
            await MoveToStone();
            Keys(Key.W);await Delay(.2f);
            var pause=UnityEngine.Object.FindFirstObjectByType<Test_PausePresentation>();
            pause.TogglePauseImmediately();int count=foley.PlayedStepCount;
            await Delay(.3f);checks["pauseSilent"]=foley.PlayedStepCount==count;
            pause.TogglePauseImmediately();
            await Observe("resume",.7f);
            Keys();await Delay(.15f);count=foley.PlayedStepCount;
            await Delay(.3f);checks["idleSilent"]=foley.PlayedStepCount==count;

            await MoveToStone();Keys(Key.Space);await Until(()=>player.AnimationControl.IsDodging);
            Keys();count=foley.PlayedStepCount;await Delay(.35f);
            checks["rollHasNoFootsteps"]=foley.PlayedStepCount==count;
            await Until(()=>!player.AnimationControl.IsActionPlaying);

            await MoveToStone();player.Teleport(stonePosition+Vector3.up*3,Quaternion.identity);
            await Delay(.1f);count=foley.PlayedStepCount;Keys(Key.W);await Delay(.15f);
            checks["airborneSilent"]=!player.Body.isGrounded && foley.PlayedStepCount==count;
            Keys();
            checks["originalPlayerAudioDisabled"]=!player.GetComponent<PassPlayerAudio>().enabled;
            checks["passed"]=checks.Values.OfType<bool>().All(v=>v);
            var report=new {checks,contacts};
            System.IO.File.WriteAllText("Assets/Temp/FootstepSync~/Test_PlayVerification.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
            return report;
        }
        finally
        {
            System.IO.File.WriteAllText("Assets/Temp/FootstepSync~/Test_PlayVerification.json",Newtonsoft.Json.JsonConvert.SerializeObject(new {checks,contacts},Newtonsoft.Json.Formatting.Indented));
            Keys();InputSystem.RemoveDevice(keyboard);if(previous!=null)previous.MakeCurrent();
            InputSystem.settings=originalSettings;UnityEngine.Object.Destroy(testSettings);
        }
    }

    static async Task MoveToStone()
    {
        Keys();player.Teleport(stonePosition,Quaternion.identity);game.View.LockBoss(false);game.View.SnapBehind();await Delay(.2f);
    }
    static async Task Walk(string name,bool sprint,float seconds)
    {
        int count=foley.PlayedStepCount;
        Keys(sprint?new[]{Key.W,Key.LeftShift}:new[]{Key.W});
        await Observe(name,seconds);
        Keys();await Delay(.08f);
        checks[name+"StepCount"]=foley.PlayedStepCount-count;
        checks[name+"ProducedSteps"]=foley.PlayedStepCount-count>=3;
    }
    static async Task Observe(string mode,float seconds)
    {
        float end=Time.realtimeSinceStartup+seconds;int previous=foley.PlayedStepCount;bool? lastLeft=null;
        bool aligned=true,alternating=true,stone=true;float worst=0;
        var animator=player.AnimationControl.CharacterAnimator;
        var bones=animator.GetComponentsInChildren<Transform>();
        var left=bones.First(t=>t.name=="mixamorig:LeftFoot");var right=bones.First(t=>t.name=="mixamorig:RightFoot");
        while(Time.realtimeSinceStartup<end)
        {
            if(foley.PlayedStepCount!=previous)
            {
                float contact=foley.LastStepWasLeft?8f:18.5f;
                float error=Mathf.Repeat(foley.LastStepFrame-contact,21);
                float height=animator.transform.InverseTransformPoint((foley.LastStepWasLeft?left:right).position).y;
                worst=Mathf.Max(worst,error);aligned &= error<2f;
                alternating &= !lastLeft.HasValue || lastLeft!=foley.LastStepWasLeft;
                stone &= foley.LastSurface==Test_FootstepSurface.GroundKind.Stone;
                contacts.Add(new {mode,foot=foley.LastStepWasLeft?"left":"right",frame=foley.LastStepFrame,error,height,clip=foley.LastStepClip.name});
                lastLeft=foley.LastStepWasLeft;previous=foley.PlayedStepCount;
            }
            await Task.Delay(1);
        }
        checks[mode+"Aligned"]=aligned;checks[mode+"Alternating"]=alternating;checks[mode+"Stone"]=stone;checks[mode+"MaxFrameError"]=worst;
    }
    static void Keys(params Key[] keys){keyboard.MakeCurrent();InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));}
    static Task Delay(float seconds)=>Task.Delay(Mathf.RoundToInt(seconds*1000));
    static async Task Until(Func<bool> check)
    {
        float end=Time.realtimeSinceStartup+8;
        while(!check()){if(Time.realtimeSinceStartup>end)throw new Exception("State timeout");await Task.Delay(10);}
    }
}
