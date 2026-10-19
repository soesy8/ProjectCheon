using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Cheon.Testing;
using UnityEngine;
using Object = UnityEngine.Object;

public static class Test_CheckCheonEnvironmentRuntime
{
    public static async Task<object> Run()
    {
        if (!UnityEditor.EditorApplication.isPlaying) throw new InvalidOperationException("Play Mode가 필요합니다.");
        var session = Object.FindFirstObjectByType<Test_CheonSession>();
        var player = Object.FindFirstObjectByType<Test_CheonPlayer>();
        var world = Object.FindFirstObjectByType<Test_CheonWorld>();
        var controller = player.GetComponent<CharacterController>();
        var gate = world.transform.Find("Test_경계문/Test_UpperGate");
        int before = world.GetComponentsInChildren<MeshRenderer>(true).Count(r => r.enabled && r.gameObject.activeInHierarchy);
        session.BeginTraining();
        // 플레이어의 실제 CharacterController로 열린 중앙 통로를 지나 영역 전환을 검증한다.
        controller.enabled = false; player.transform.position = new Vector3(0, .15f, session.BoundaryZ - 2); controller.enabled = true;
        for (int step = 0; step < 25; step++)
        {
            controller.Move(new Vector3(0, -.04f, .2f));
            await Task.Delay(30);
        }
        await Task.Delay(2200);
        var data = new UnityEditor.SerializedObject(world);
        var palettes = data.FindProperty("palettes");
        int importedPaletteCount = 0;
        for (int index = 0; index < palettes.arraySize; index++)
        {
            var material = palettes.GetArrayElementAtIndex(index).FindPropertyRelative("material").objectReferenceValue;
            if (UnityEditor.AssetDatabase.GetAssetPath(material).Contains("EnvironmentModels")) importedPaletteCount++;
        }
        var result = new {
            chapter = session.CurrentChapter.ToString(), crossedBoundary = player.transform.position.z > session.BoundaryZ + 1.2f,
            spiritAmount = world.SpiritAmount, gateHidden = !gate.gameObject.activeSelf,
            gatePrefabChildren = gate.GetComponentsInChildren<MeshRenderer>(true).Count(r => r.enabled),
            importedPaletteCount, beforeRenderers = before,
            invalidMaterials = world.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.enabled).SelectMany(r => r.sharedMaterials).Count(m => m == null || m.shader == null || !m.shader.isSupported),
            playerHealth = player.Health
        };
        File.WriteAllText("Assets/Temp/Test_CheonEnvironmentRuntime.json", Newtonsoft.Json.JsonConvert.SerializeObject(result, Newtonsoft.Json.Formatting.Indented));
        if (!result.crossedBoundary || result.chapter != "Battle" || result.spiritAmount < .9f || !result.gateHidden || result.invalidMaterials != 0 || importedPaletteCount != 65)
            throw new InvalidOperationException("환경 모델 런타임 검증 실패: " + Newtonsoft.Json.JsonConvert.SerializeObject(result));
        return result;
    }
}
