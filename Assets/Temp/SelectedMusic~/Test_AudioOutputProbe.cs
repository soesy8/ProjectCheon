using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Cheon.Tests.CombatPresentation;
public static class Test_AudioOutputProbe
{
    public static async Task<object> Measure()
    {
        var mix = UnityEngine.Object.FindFirstObjectByType<Test_TraditionalSoundscape>();
        var source = mix.GetComponentsInChildren<AudioSource>().Single(s => s.isPlaying && s.clip != null && s.clip.name.StartsWith("Test_Bgm_"));
        float start = source.time;
        float peak = 0;
        var data = new float[1024];
        // 첫 GetOutputData 호출은 출력 버퍼를 준비하므로 다음 오디오 프레임부터 측정한다.
        for (int i = 0; i < 50; i++)
        {
            source.GetOutputData(data, 0);
            foreach (var sample in data) peak = Math.Max(peak, Mathf.Abs(sample));
            await Task.Delay(40);
        }
        if (peak <= 0) throw new Exception("No audio output: " + source.clip.name);
        return new { clip=source.clip.name, start, end=source.time, peak, passed=true };
    }
}
