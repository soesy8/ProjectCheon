// 플레이 모드에서만 실행하고, 결과를 Temp에 기록하는 검증용 임시 실행문입니다.
var game = UnityEngine.Object.FindFirstObjectByType<Cheon.Prototype.MountainPass.PassGameFlow>();
var gourd = game.Gourd;
var audio = game.Audio;
var healing = audio.transform.Find("GourdHealingAudio").GetComponent<UnityEngine.AudioSource>();
var effects = audio.GetComponents<UnityEngine.AudioSource>()[0];
var failures = new System.Collections.Generic.List<string>();
var rows = new System.Collections.Generic.List<string> { "frame,time,stage,phase,progress,sipping,soundPlaying,soundSample,volume" };
if (audio.GetComponents<UnityEngine.AudioSource>().Length != 3) failures.Add("공용 오디오 소스 개수가 변경됨");
double began = UnityEditor.EditorApplication.timeSinceStartup;
int stage = 0, previousFrame = -1, normalSamples = 0, pausedSamples = 0;
float normalStart = -1f, normalStop = -1f, interruptedStart = -1f, resumedStart = -1f;
double pauseStarted = -1;
int pausedAudioPosition = -1;
float pausedProgress = -1f;
bool damageStoppedSound = false, sharedSoundPreserved = false, resumed = false;
UnityEditor.EditorApplication.CallbackFunction observe = null;
observe = () =>
{
    if (UnityEngine.Time.frameCount == previousFrame) return;
    previousFrame = UnityEngine.Time.frameCount;
    double now = UnityEditor.EditorApplication.timeSinceStartup;
    bool finished = false;
    try
    {
        rows.Add(string.Join(",", new object[] { previousFrame, UnityEngine.Time.time, stage, gourd.Phase,
            gourd.Progress, gourd.IsSipping, healing.isPlaying, healing.timeSamples, healing.volume }));
        if (now - began > 35) throw new System.TimeoutException("재생 검증 시간 초과, stage=" + stage);
        if (stage == 0 && game.IsRunning)
        {
            game.PlayerHealth.TakeDamage(80f, false, true);
            gourd.Refill();
            stage = 1;
        }
        else if (stage == 1 && gourd.TryDrink()) stage = 2;
        else if (stage == 2)
        {
            if (healing.isPlaying != gourd.IsSipping) failures.Add("정상 마시기 구간과 소리 불일치, progress=" + gourd.Progress);
            if (gourd.IsSipping)
            {
                if (normalStart < 0f) normalStart = UnityEngine.Time.time;
                normalSamples++;
            }
            else if (normalStart >= 0f && normalStop < 0f) normalStop = UnityEngine.Time.time;
            if (!gourd.IsPlaying && normalStop >= 0f)
            {
                if (System.Math.Abs(normalStop - normalStart - 1.147f) > 0.09f) failures.Add("정상 재생 길이 불일치");
                if (normalSamples < 10) failures.Add("정상 마시기 관측 프레임 부족");
                stage = 3;
            }
        }
        else if (stage == 3 && gourd.TryDrink()) stage = 4;
        else if (stage == 4 && gourd.IsSipping)
        {
            if (interruptedStart < 0f) interruptedStart = UnityEngine.Time.time;
            if (UnityEngine.Time.time - interruptedStart >= 0.24f)
            {
                audio.Dodge();
                bool sharedWasPlaying = effects.isPlaying;
                game.PlayerHealth.TakeDamage(1f, false, true);
                damageStoppedSound = !healing.isPlaying && !gourd.IsPlaying;
                sharedSoundPreserved = sharedWasPlaying && effects.isPlaying;
                if (!damageStoppedSound) failures.Add("피격 취소 후 치유 소리가 남음");
                if (!sharedSoundPreserved) failures.Add("치유 중단이 다른 효과음도 중단함");
                stage = 5;
            }
        }
        else if (stage == 5 && gourd.TryDrink()) stage = 6;
        else if (stage == 6 && gourd.IsSipping)
        {
            if (resumedStart < 0f) resumedStart = UnityEngine.Time.time;
            if (UnityEngine.Time.time - resumedStart >= 0.24f)
            {
                game.TogglePause();
                pauseStarted = now;
                stage = 7;
            }
        }
        else if (stage == 7)
        {
            if (now - pauseStarted >= 0.08)
            {
                if (healing.isPlaying) failures.Add("일시정지 중 치유 소리가 계속 재생됨");
                if (pausedAudioPosition < 0) { pausedAudioPosition = healing.timeSamples; pausedProgress = gourd.Progress; }
                if (healing.timeSamples != pausedAudioPosition) failures.Add("일시정지 중 오디오 위치가 진행됨");
                if (System.Math.Abs(gourd.Progress - pausedProgress) > 0.0001f) failures.Add("일시정지 중 마시기가 진행됨");
                pausedSamples++;
            }
            if (now - pauseStarted >= 0.45)
            {
                game.TogglePause();
                stage = 8;
            }
        }
        else if (stage == 8)
        {
            if (gourd.IsSipping && healing.isPlaying) resumed = true;
            if (resumed && !gourd.IsPlaying)
            {
                if (healing.isPlaying) failures.Add("마시기 종료 후 소리가 남음");
                if (pausedSamples < 3) failures.Add("일시정지 관측 프레임 부족");
                finished = true;
            }
        }
    }
    catch (System.Exception error) { failures.Add(error.ToString()); finished = true; }
    if (!finished) return;
    UnityEditor.EditorApplication.update -= observe;
    if (game.State == Cheon.Prototype.MountainPass.PassGameFlow.RunState.Paused) game.TogglePause();
    gourd.CancelDrinking();
    System.IO.File.WriteAllLines("Assets/Temp/GourdHealingAudio~/HealingPlaybackFrames.csv", rows);
    System.IO.File.WriteAllLines("Assets/Temp/GourdHealingAudio~/HealingPlaybackVerification.txt", new[] {
        "success=" + (failures.Count == 0 && resumed), "normalDuration=" + (normalStop - normalStart),
        "normalObservedFrames=" + normalSamples, "pauseObservedFrames=" + pausedSamples,
        "damageStoppedSound=" + damageStoppedSound, "sharedSoundPreserved=" + sharedSoundPreserved,
        "resumedAfterPause=" + resumed, "failures=" + string.Join(" | ", failures) });
};
UnityEditor.EditorApplication.update += observe;
if (game.State == Cheon.Prototype.MountainPass.PassGameFlow.RunState.Intro) game.BeginJourney();
return new { started = true, frame = UnityEngine.Time.frameCount, gameState = game.State.ToString() };
