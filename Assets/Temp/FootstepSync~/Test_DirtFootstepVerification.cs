using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Cheon.Prototype.MountainPass;
using Cheon.Tests.CombatPresentation;

public static class Test_DirtFootstepVerification
{
    public static async Task<object> Run()
    {
        var originalSettings=InputSystem.settings;
        var testSettings=UnityEngine.Object.Instantiate(originalSettings);
        testSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        testSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings=testSettings;
        var previous=Keyboard.current;
        var keyboard=InputSystem.AddDevice<Keyboard>();keyboard.MakeCurrent();
        var game=UnityEngine.Object.FindFirstObjectByType<PassGameFlow>();
        var player=UnityEngine.Object.FindFirstObjectByType<PassPlayerMovement>();
        var foley=player.GetComponent<Test_PlayerFoley>();
        var checks=new Dictionary<string,object>();var contacts=new List<object>();
        PassBossPatternController boss=null;
        bool bossEnabled=false;
        try
        {
            game.BeginBossChallenge();await Until(()=>!game.IsTransitioning);
            game.Entrance.SkipPresentation();await Until(()=>game.IsRunning);
            boss=(PassBossPatternController)game.Boss;bossEnabled=boss.enabled;boss.enabled=false;
            game.PlayerHealth.Invulnerable=true;
            Vector3 position=boss.transform.position+boss.transform.forward*9;
            var ground=Physics.RaycastAll(position+Vector3.up*4,Vector3.down,10,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)
                .Where(h=>h.normal.y>.65f && h.collider.GetComponentInParent<PassHealth>()==null).OrderBy(h=>h.distance).First();
            player.Teleport(ground.point+Vector3.up*.05f,Quaternion.LookRotation(-boss.transform.right));
            game.View.LockBoss(false);game.View.SnapBehind();await Task.Delay(250);
            int count=foley.PlayedStepCount;bool? lastFoot=null;
            bool alternating=true,aligned=true,allDirt=true,noRepeat=true,movement=true;
            string previousClip=null;float peak=0;float[] output=new float[512];
            var audio=game.Audio.GetComponents<AudioSource>()[1];
            foreach(string mode in new[]{"walk","sprint","walkAfterSprint"})
            {
                int start=foley.PlayedStepCount;
                InputSystem.QueueStateEvent(keyboard,mode=="sprint"?new KeyboardState(Key.W,Key.LeftShift):new KeyboardState(Key.W));
                float end=Time.realtimeSinceStartup+1.1f;
                while(Time.realtimeSinceStartup<end)
                {
                    await Task.Delay(1);
                    audio.GetOutputData(output,0);peak=Mathf.Max(peak,output.Max(Mathf.Abs));
                    if(foley.PlayedStepCount==count)continue;
                    float error=Mathf.Repeat(foley.LastStepFrame-(foley.LastStepWasLeft?8f:18.5f),21);
                    aligned &= error<2;alternating &= !lastFoot.HasValue || lastFoot!=foley.LastStepWasLeft;
                    allDirt &= foley.LastSurface==Test_FootstepSurface.GroundKind.Dirt;
                    noRepeat &= previousClip!=foley.LastStepClip.name;movement &= player.IsMoving;
                    contacts.Add(new {mode,foot=foley.LastStepWasLeft?"left":"right",frame=foley.LastStepFrame,error,clip=foley.LastStepClip.name});
                    count=foley.PlayedStepCount;lastFoot=foley.LastStepWasLeft;previousClip=foley.LastStepClip.name;
                }
                checks[mode+"Count"]=foley.PlayedStepCount-start;
                checks[mode+"ProducedSteps"]=foley.PlayedStepCount-start>=2;
            }
            checks["aligned"]=aligned;checks["alternatingAcrossSpeedChange"]=alternating;
            checks["allDirtClips"]=allDirt;checks["noConsecutiveRepeat"]=noRepeat;
            checks["moving"]=movement;checks["audioPeak"]=peak;checks["audible"]=peak>.0001f;
            checks["passed"]=checks.Values.OfType<bool>().All(v=>v);
            return new {checks,contacts};
        }
        finally
        {
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.RemoveDevice(keyboard);
            if(previous!=null)previous.MakeCurrent();InputSystem.settings=originalSettings;UnityEngine.Object.Destroy(testSettings);
            if(boss!=null)boss.enabled=bossEnabled;
            System.IO.File.WriteAllText("Assets/Temp/FootstepSync~/Test_DirtVerification.json",Newtonsoft.Json.JsonConvert.SerializeObject(new {checks,contacts},Newtonsoft.Json.Formatting.Indented));
        }
    }
    static async Task Until(Func<bool> condition)
    {
        float end=Time.realtimeSinceStartup+10;
        while(!condition()){if(Time.realtimeSinceStartup>end)throw new Exception("State timeout");await Task.Delay(1);}
    }
}
