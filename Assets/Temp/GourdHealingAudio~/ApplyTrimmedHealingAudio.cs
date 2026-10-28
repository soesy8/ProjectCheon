// Unity CLI eval_file용 임시 실행문입니다.
const string guid = "ebd78dd96fbe68946a189de76f9b3de3";
const string destination = "Assets/Arts/Audio/MountainPass/Presentation/GourdRestore.wav";
const string prepared = "Assets/Temp/GourdHealingAudio~/Trimmed_HealingChime.wav";
const string backup = "Assets/Temp/GourdHealingAudio~/BeforeTrim";
if (UnityEditor.EditorApplication.isPlaying) throw new System.InvalidOperationException("편집 모드에서 실행하세요.");
string previous = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
if (previous != "Assets/Arts/Audio/MountainPass/Presentation/GourdRestore.mp3" && previous != destination)
    throw new System.InvalidOperationException("기존 치유 사운드 경로가 예상과 다릅니다.");
System.IO.Directory.CreateDirectory(backup);
string backupPath = backup + "/" + System.IO.Path.GetFileName(previous);
if (!System.IO.File.Exists(backupPath)) System.IO.File.Copy(previous, backupPath);
if (!System.IO.File.Exists(backupPath + ".meta")) System.IO.File.Copy(previous + ".meta", backupPath + ".meta");
UnityEditor.AssetDatabase.StartAssetEditing();
try
{
    if (previous != destination)
    {
        string error = UnityEditor.AssetDatabase.MoveAsset(previous, destination);
        if (!string.IsNullOrEmpty(error)) throw new System.InvalidOperationException(error);
    }
    System.IO.File.Copy(prepared, destination, true);
}
finally { UnityEditor.AssetDatabase.StopAssetEditing(); }
UnityEditor.AssetDatabase.ImportAsset(destination, UnityEditor.ImportAssetOptions.ForceUpdate | UnityEditor.ImportAssetOptions.ForceSynchronousImport);
if (UnityEditor.AssetDatabase.AssetPathToGUID(destination) != guid) throw new System.InvalidOperationException("GUID가 변경되었습니다.");
var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.AudioClip>(destination);
clip.LoadAudioData();
var samples = new float[clip.samples * clip.channels];
if (!clip.GetData(samples, 0)) throw new System.InvalidOperationException("편집한 치유 사운드 로드 실패");
var audio = UnityEngine.Object.FindFirstObjectByType<Cheon.Prototype.MountainPass.PassAudio>();
var assigned = new UnityEditor.SerializedObject(audio).FindProperty("drink").objectReferenceValue;
if (UnityEditor.AssetDatabase.GetAssetPath(assigned) != destination) throw new System.InvalidOperationException("호리병 음원 연결이 유지되지 않았습니다.");
const string creditsPath = "Assets/Arts/Audio/MountainPass/Presentation/SoundEffectCredits.txt";
string credits = System.IO.File.ReadAllText(creditsPath);
credits = credits.Replace("원본 음색과 재생 속도를 유지해 사용했습니다.", "가공: 앞뒤 무음 제거, 마시는 구간에 맞춘 재생 종료, 종료 직전 짧은 페이드. 음색과 재생 속도 유지.");
System.IO.File.WriteAllText(creditsPath, credits, new System.Text.UTF8Encoding(false));
UnityEditor.AssetDatabase.ImportAsset(creditsPath, UnityEditor.ImportAssetOptions.ForceUpdate);
return new { path = destination, guid, duration = clip.length, frequency = clip.frequency,
    state = clip.loadState.ToString(), firstSample = samples[0], lastSample = samples[samples.Length - 1],
    sceneDirty = audio.gameObject.scene.isDirty };
