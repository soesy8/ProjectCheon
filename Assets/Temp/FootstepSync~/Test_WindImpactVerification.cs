using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Cheon.Prototype.MountainPass;

public static class Test_WindImpactVerification
{
    public static async Task<object> Run()
    {
        var game=UnityEngine.Object.FindFirstObjectByType<PassGameFlow>();
        game.BeginJourney();
        float deadline=Time.realtimeSinceStartup+10;
        while(!game.IsRunning){if(Time.realtimeSinceStartup>deadline)throw new Exception("Start timeout");await Task.Delay(10);}
        var source=game.Audio.GetComponents<AudioSource>()[0];
        source.Stop();await Task.Delay(150);
        game.Audio.Strike(true);
        bool windStarted=source.isPlaying;
        float windPeak=await Peak(source);
        source.Stop();await Task.Delay(150);
        // 바람 호출 직후의 직접 타격이 쿨다운에 먹히지도 않아야 한다.
        game.Audio.Strike(true);game.Audio.Strike(false);
        bool woodStarted=source.isPlaying;
        float woodPeak=await Peak(source);
        var result=new {windSilent=!windStarted && windPeak<.00001f,woodAudible=woodStarted && woodPeak>.0001f,windPeak,woodPeak,frames=Time.frameCount};
        System.IO.File.WriteAllText("Assets/Temp/FootstepSync~/Test_WindImpactVerification.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
        return result;
    }
    static async Task<float> Peak(AudioSource source)
    {
        float peak=0;float[] data=new float[1024];
        for(int i=0;i<20;i++){await Task.Delay(10);source.GetOutputData(data,0);peak=Mathf.Max(peak,data.Max(Mathf.Abs));}
        return peak;
    }
}
