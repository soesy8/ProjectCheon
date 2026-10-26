using System.Linq;
using UnityEngine;
using Cheon.Tests.BossPatterns;
public static class Test_InspectDeathState
{
    public static object Main() => Newtonsoft.Json.JsonConvert.SerializeObject(new
    {
        time = Time.time, frame = Time.frameCount,
        actors = Resources.FindObjectsOfTypeAll<Test_BossPatternController>().Where(actor => actor.gameObject.scene.IsValid()).Select(actor => new { actor.name, actor.IsMinion, actor.Phase, body = actor.Body != null, actor.enabled }),
        effects = Resources.FindObjectsOfTypeAll<Test_BossDeathVfx>().Select(effect => new { effect.name, effect.IsPlaying, effect.TintProgress, effect.DissolveProgress, effect.ModelHidden, effect.ParticlesEmitted }),
        probes = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Where(item => item.name == "Test_ExplosionDeathProbe").Select(item => new { item.enabled, type = item.GetType().FullName })
    });
}
