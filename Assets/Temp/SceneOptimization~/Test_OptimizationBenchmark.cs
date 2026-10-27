using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Unity.Profiling;
using Cheon.Prototype.MountainPass;

public static class Test_OptimizationBenchmark
{
    public static async Task<object> Before()=>await Run("Before");
    public static async Task<object> After()=>await Run("After");
    private static async Task<object> Run(string suffix)
    {
        Application.runInBackground=true;
        var game=UnityEngine.Object.FindFirstObjectByType<PassGameFlow>();
        game.BeginJourney();
        var idle=await Sample(240);
        game.BeginBossChallenge();game.Entrance.SkipPresentation();
        game.PlayerHealth.Invulnerable=true;
        var combat=await Sample(300);
        var result=new{idle,combat};
        File.WriteAllText("Assets/Temp/SceneOptimization~/Benchmark"+suffix+".json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
        return result;
    }
    private static Task<object> Sample(int sampleCount)
    {
        var done=new TaskCompletionSource<object>();
        var gc=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame");
        var main=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"Main Thread");
        long totalGc=0,totalMain=0;int samples=0,lastFrame=Time.frameCount,start=Time.frameCount;
        long batches=0,draws=0,triangles=0;
        double deadline=EditorApplication.timeSinceStartup+25;
        EditorApplication.CallbackFunction tick=null;
        tick=()=>{
            if(EditorApplication.timeSinceStartup>deadline){EditorApplication.update-=tick;gc.Dispose();main.Dispose();done.SetException(new Exception("Benchmark frames stalled"));return;}
            if(lastFrame==Time.frameCount)return;
            lastFrame=Time.frameCount;
            if(Time.frameCount-start<60)return;
            samples++;totalGc+=gc.LastValue;totalMain+=main.LastValue;
            batches+=UnityStats.batches;draws+=UnityStats.drawCalls;triangles+=UnityStats.triangles;
            if(samples<sampleCount)return;
            EditorApplication.update-=tick;
            var result=new{frames=samples,gcRecorder=gc.Valid,mainRecorder=main.Valid,gcBytesPerFrame=totalGc/(double)samples,mainThreadMs=totalMain/(double)samples/1000000,batches=batches/(double)samples,drawCalls=draws/(double)samples,triangles=triangles/(double)samples};
            gc.Dispose();main.Dispose();done.SetResult(result);
        };
        EditorApplication.update+=tick;
        return done.Task;
    }
}
