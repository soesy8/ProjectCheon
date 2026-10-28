// 실제 플레이 프레임에서 음원의 자연 종료와 입 접촉 시간을 검증한다.
var game = UnityEngine.Object.FindFirstObjectByType<Cheon.Prototype.MountainPass.PassGameFlow>();
var gourd = game.Gourd;
var sound = game.Audio.transform.Find("GourdHealingAudio").GetComponent<UnityEngine.AudioSource>();
var effects = game.Audio.GetComponents<UnityEngine.AudioSource>()[0];
var problems = new System.Collections.Generic.List<string>();
var rows = new System.Collections.Generic.List<string> { "frame,time,phase,progress,sipping,audioPlaying,audioSample,volume" };
double began = UnityEditor.EditorApplication.timeSinceStartup, audioStart = -1, audioEnd = -1;
float sipStart = -1, sipEnd = -1;
int stage = 0, previousFrame = -1, lastAudioSample = 0, observedFrames = 0;
bool naturallyFinishedWhileSipping = false;
UnityEditor.EditorApplication.CallbackFunction observe = null;
observe = () =>
{
    if (UnityEngine.Time.frameCount == previousFrame) return;
    previousFrame = UnityEngine.Time.frameCount;
    bool complete = false;
    try
    {
        if (UnityEditor.EditorApplication.timeSinceStartup - began > 20) throw new System.TimeoutException("마시기 연장 검증 시간 초과");
        rows.Add(string.Join(",", new object[] { previousFrame, UnityEngine.Time.time, gourd.Phase, gourd.Progress,
            gourd.IsSipping, sound.isPlaying, sound.timeSamples, sound.volume }));
        if (stage == 0 && game.IsRunning)
        {
            game.PlayerHealth.TakeDamage(80f, false, true);
            gourd.Refill();
            stage = 1;
        }
        else if (stage == 1 && gourd.TryDrink()) stage = 2;
        else if (stage == 2)
        {
            if (gourd.IsSipping)
            {
                if (sipStart < 0f) sipStart = UnityEngine.Time.time;
                observedFrames++;
            }
            else if (sipStart >= 0f && sipEnd < 0f) sipEnd = UnityEngine.Time.time;
            if (sound.isPlaying)
            {
                if (audioStart < 0) audioStart = UnityEngine.AudioSettings.dspTime;
                if (!gourd.IsSipping) problems.Add("입 접촉 구간 밖에서 치유 소리가 재생됨");
                if (sound.volume < effects.volume * 0.6f * 0.99f) problems.Add("음원 자연 종료 전에 종료 페이드가 적용됨");
                lastAudioSample = System.Math.Max(lastAudioSample, sound.timeSamples);
            }
            else if (audioStart >= 0 && audioEnd < 0)
            {
                audioEnd = UnityEngine.AudioSettings.dspTime;
                naturallyFinishedWhileSipping = gourd.IsSipping;
            }
            if (sipEnd >= 0f && !gourd.IsPlaying)
            {
                if (System.Math.Abs(sipEnd - sipStart - 2.65f) > 0.08f) problems.Add("입 접촉 시간 불일치");
                if (!naturallyFinishedWhileSipping) problems.Add("입에서 떼기 전에 음원이 자연 종료되지 않음");
                if (System.Math.Abs(audioEnd - audioStart - sound.clip.length) > 0.08) problems.Add("오디오 전체 길이 재생 불일치");
                if (lastAudioSample < sound.clip.samples - sound.clip.frequency * 0.06f) problems.Add("음원 끝까지 도달하지 못함");
                if (sound.isPlaying) problems.Add("수납 후에도 소리가 재생됨");
                complete = true;
            }
        }
    }
    catch (System.Exception error) { problems.Add(error.ToString()); complete = true; }
    if (!complete) return;
    UnityEditor.EditorApplication.update -= observe;
    gourd.CancelDrinking();
    System.IO.File.WriteAllLines("Assets/Temp/GourdHealingAudio~/ExtendedHealingFrames.csv", rows);
    System.IO.File.WriteAllLines("Assets/Temp/GourdHealingAudio~/ExtendedHealingVerification.txt", new[] {
        "success=" + (problems.Count == 0), "contactSeconds=" + (sipEnd - sipStart),
        "soundSeconds=" + sound.clip.length, "observedAudioSeconds=" + (audioEnd - audioStart),
        "lastObservedAudioSample=" + lastAudioSample, "totalAudioSamples=" + sound.clip.samples,
        "naturallyFinishedWhileSipping=" + naturallyFinishedWhileSipping, "contactFrames=" + observedFrames,
        "failures=" + string.Join(" | ", problems) });
};
UnityEditor.EditorApplication.update += observe;
if (game.State == Cheon.Prototype.MountainPass.PassGameFlow.RunState.Intro) game.BeginJourney();
return new { started = true, frame = UnityEngine.Time.frameCount };
