using System;
using System.Linq;
using Cheon.Prototype.MountainPass;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ApplyEquipmentClothAudio
{
    public static object Refresh()
    {
        const string folder = "Assets/Arts/Audio/MountainPass/Presentation/";
        var draw = ImportClip(folder + "ClothDraw.wav");
        var sheathe = ImportClip(folder + "ClothSheathe.wav");
        var audio = UnityEngine.Object.FindObjectsByType<PassAudio>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single();
        var serialized = new SerializedObject(audio);
        if (serialized.FindProperty("equipment").objectReferenceValue != draw ||
            serialized.FindProperty("sheatheEquipment").objectReferenceValue != sheathe)
            throw new InvalidOperationException("씬의 발도·납도 클립 연결을 확인하세요.");
        return new { draw = InspectClip(draw), sheathe = InspectClip(sheathe), sceneDirty = audio.gameObject.scene.isDirty };
    }

    private static object InspectClip(AudioClip clip)
    {
        clip.LoadAudioData();
        var samples = new float[clip.samples * clip.channels];
        if (!clip.GetData(samples, 0)) throw new InvalidOperationException("오디오 데이터 로드 실패: " + clip.name);
        float peak = samples.Max(value => Mathf.Abs(value));
        double energy = samples.Sum(value => (double)value * value);
        return new
        {
            name = clip.name,
            guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(clip)),
            seconds = clip.length,
            channels = clip.channels,
            state = clip.loadState.ToString(),
            peakDb = 20 * Math.Log10(peak),
            rmsDb = 10 * Math.Log10(energy / samples.Length),
            edgeSamples = new[] { samples[0], samples[samples.Length - 1] }
        };
    }

    public static object Main()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("편집 모드에서 실행하세요.");
        const string folder = "Assets/Arts/Audio/MountainPass/Presentation/";
        var draw = ImportClip(folder + "ClothDraw.wav");
        var sheathe = ImportClip(folder + "ClothSheathe.wav");
        var audio = UnityEngine.Object.FindObjectsByType<PassAudio>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single();
        Undo.RecordObject(audio, "발도·납도 옷 스침 소리 적용");
        var serialized = new SerializedObject(audio);
        serialized.FindProperty("equipment").objectReferenceValue = draw;
        serialized.FindProperty("sheatheEquipment").objectReferenceValue = sheathe;
        serialized.FindProperty("equipmentVolume").floatValue = 0.45f;
        serialized.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(audio.gameObject.scene);
        if (!EditorSceneManager.SaveScene(audio.gameObject.scene))
            throw new InvalidOperationException("사운드 연결을 씬에 저장하지 못했습니다.");
        AssetDatabase.ImportAsset(folder + "SoundEffectCredits.txt", ImportAssetOptions.ForceUpdate);
        Selection.activeObject = audio;
        return new
        {
            scene = audio.gameObject.scene.path,
            draw = new { path = AssetDatabase.GetAssetPath(draw), duration = draw.length, state = draw.loadState.ToString() },
            sheathe = new { path = AssetDatabase.GetAssetPath(sheathe), duration = sheathe.length, state = sheathe.loadState.ToString() },
            equipmentVolume = serialized.FindProperty("equipmentVolume").floatValue
        };
    }

    private static AudioClip ImportClip(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        var importer = (AudioImporter)AssetImporter.GetAtPath(path);
        var settings = importer.defaultSampleSettings;
        settings.compressionFormat = AudioCompressionFormat.PCM;
        settings.loadType = AudioClipLoadType.DecompressOnLoad;
        settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
        settings.preloadAudioData = true;
        importer.defaultSampleSettings = settings;
        // 완성 WAV가 이미 모노이므로 재변환·정규화로 낮춘 음량이 커지지 않게 한다.
        importer.forceToMono = false;
        importer.loadInBackground = false;
        importer.SaveAndReimport();
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (clip == null || clip.samples == 0) throw new InvalidOperationException("클립 임포트 실패: " + path);
        return clip;
    }
}
