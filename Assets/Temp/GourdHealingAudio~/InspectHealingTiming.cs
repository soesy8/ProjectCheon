// Unity CLI eval_file용 임시 실행문입니다.
var gourd = UnityEngine.Object.FindFirstObjectByType<Cheon.Prototype.MountainPass.PassGourdDrinking>();
var state = new UnityEditor.SerializedObject(gourd);
var motion = (UnityEngine.AnimationClip)state.FindProperty("drinkingClip").objectReferenceValue;
float start = state.FindProperty("sippingStartProgress").floatValue;
float end = state.FindProperty("sippingEndProgress").floatValue;
float speed = state.FindProperty("sippingSpeed").floatValue;
float drinkingSpeed = state.FindProperty("drinkingSpeed").floatValue;
var audioPath = UnityEditor.AssetDatabase.GUIDToAssetPath("ebd78dd96fbe68946a189de76f9b3de3");
var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.AudioClip>(audioPath);
clip.LoadAudioData();
var samples = new float[clip.samples * clip.channels];
if (!clip.GetData(samples, 0)) throw new System.InvalidOperationException("치유 음원 데이터 로드 실패");
const string output = "Assets/Temp/GourdHealingAudio~/HealingChimeDecoded.f32";
using (var writer = new System.IO.BinaryWriter(System.IO.File.Open(output, System.IO.FileMode.Create)))
{
    writer.Write(clip.frequency);
    writer.Write(clip.channels);
    writer.Write(samples.Length);
    foreach (float sample in samples) writer.Write(sample);
}
return new { playing = UnityEditor.EditorApplication.isPlaying, motionLength = motion.length,
    sippingStart = start, sippingEnd = end, sippingSpeed = speed, drinkingSpeed,
    contactSeconds = motion.length / drinkingSpeed * (end - start) / speed,
    audioLength = clip.length, output };
