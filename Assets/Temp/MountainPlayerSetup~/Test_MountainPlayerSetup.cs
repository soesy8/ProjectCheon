using System;
using System.Linq;
using Cheon.Testing.PlayerFan;
using Cheon.Testing.MountainMap;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class Test_MountainPlayerSetup
{
    public static object Main()
    {
        const string mapPath = "Assets/Test/CheonMountainMap/Scenes/Test_CheonMountainMap.unity";
        const string sourcePath = "Assets/Test/PlayerFanAnimation/Scenes/Test_PlayerFanAnimation.unity";
        const string settingsFolder = "Assets/Test/CheonMountainMap/Settings";
        const string settingsPath = settingsFolder + "/Test_MountainPlayerActionTimings.asset";
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Play 모드에서 씬을 수정할 수 없습니다.");
        Scene map = SceneManager.GetActiveScene();
        if (map.path != mapPath) throw new InvalidOperationException("대상 맵 씬을 먼저 여세요.");
        if (map.isDirty) throw new InvalidOperationException("대상 씬에 저장되지 않은 변경이 있습니다.");
        if (map.GetRootGameObjects().Any(g => g.name == "Test_MountainPlayer"))
            throw new InvalidOperationException("플레이어가 이미 설치되어 있습니다.");

        if (!AssetDatabase.IsValidFolder(settingsFolder)) AssetDatabase.CreateFolder("Assets/Test/CheonMountainMap", "Settings");
        var timings = AssetDatabase.LoadAssetAtPath<Test_PlayerActionTimings>(settingsPath);
        if (timings == null)
        {
            timings = ScriptableObject.CreateInstance<Test_PlayerActionTimings>();
            AssetDatabase.CreateAsset(timings, settingsPath);
        }

        Scene source = EditorSceneManager.OpenScene(sourcePath, OpenSceneMode.Additive);
        GameObject player;
        try
        {
            var bundle = new GameObject("Test_MountainPlayer");
            SceneManager.MoveGameObjectToScene(bundle, source);
            var sourceRoots = source.GetRootGameObjects();
            sourceRoots.First(g => g.name == "Test_Player").transform.SetParent(bundle.transform, true);
            sourceRoots.First(g => g.name == "Test_AnimationControl").transform.SetParent(bundle.transform, true);
            // 한 번에 복제해 본·소켓과 제어 스크립트 사이의 씬 참조도 복제본에 연결한다.
            player = UnityEngine.Object.Instantiate(bundle);
            player.name = "Test_MountainPlayer";
            SceneManager.MoveGameObjectToScene(player, map);
        }
        finally
        {
            EditorSceneManager.CloseScene(source, true);
            SceneManager.SetActiveScene(map);
        }

        Undo.RegisterCreatedObjectUndo(player, "Apply Mountain Player");
        player.tag = "Player";
        Physics.SyncTransforms();
        if (!Physics.Raycast(new Vector3(-12f, 300f, -12f), Vector3.down, out var ground, 500f, ~0, QueryTriggerInteraction.Ignore))
            throw new InvalidOperationException("시작 위치의 바닥을 찾을 수 없습니다.");
        player.transform.position = ground.point + Vector3.up * 0.04f;
        player.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
        var animation = player.GetComponentInChildren<Test_PlayerFanAnimationController>();
        animation.ConfigureMapPlayback(timings);
        var characterBody = player.AddComponent<CharacterController>();
        characterBody.height = 1.8f;
        characterBody.radius = 0.28f;
        characterBody.center = new Vector3(0f, 0.9f, 0f);
        characterBody.skinWidth = 0.03f;
        characterBody.stepOffset = 0.35f;
        characterBody.slopeLimit = 55f;
        characterBody.minMoveDistance = 0f;

        var camera = map.GetRootGameObjects().First(g => g.name == "Test_MainCamera").GetComponent<Camera>();
        Undo.RecordObject(camera.transform, "Follow Mountain Player");
        Undo.RecordObject(camera, "Follow Mountain Player");
        camera.fieldOfView = 52f;
        camera.nearClipPlane = 0.05f;
        camera.orthographic = false;
        camera.GetComponent<Test_MountainPlayerCamera>()?.Configure(player.transform);
        if (camera.GetComponent<Test_MountainPlayerCamera>() == null)
            Undo.AddComponent<Test_MountainPlayerCamera>(camera.gameObject).Configure(player.transform);
        player.AddComponent<Test_MountainPlayerMovement>().Configure(animation, camera.transform);

        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Test/PlayerFanAnimation/Fonts/Test_PlayerFanKorean.asset");
        var textSettings = AssetDatabase.LoadAssetAtPath<TMP_Settings>("Assets/Test/PlayerFanAnimation/Fonts/Test_TMPSettings.asset");
        var guide = new GameObject("Test_PlayerControlGuide");
        Undo.RegisterCreatedObjectUndo(guide, "Add Player Control Guide");
        guide.AddComponent<Test_PlayerControlGuide>().Configure(animation, font, textSettings);
        var timingUi = new GameObject("Test_PlayerActionTimingUI");
        Undo.RegisterCreatedObjectUndo(timingUi, "Add Player Timing UI");
        timingUi.AddComponent<Test_PlayerActionTimingUI>().Configure(animation, font, textSettings);

        var gate = UnityEngine.Object.FindFirstObjectByType<Cheon.World.GateEnvironmentTransition>();
        if (gate != null)
        {
            Undo.RecordObject(gate, "Connect Mountain Player To Gate");
            var serializedGate = new SerializedObject(gate);
            serializedGate.FindProperty("player").objectReferenceValue = player.transform;
            serializedGate.ApplyModifiedProperties();
        }

        animation.CharacterAnimator.Rebind();
        animation.CharacterAnimator.Play("SHEATHED_IDLE", 0, 0f);
        animation.CharacterAnimator.Update(0f);
        player.GetComponentInChildren<Test_PlayerFanAttachment>().SetSequencePose(false, 0f, true);
        EditorSceneManager.MarkSceneDirty(map);
        EditorSceneManager.SaveScene(map);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = timingUi;
        return new { scene = map.path, player = player.name, position = player.transform.position.ToString(),
            rendererBounds = player.GetComponentsInChildren<SkinnedMeshRenderer>().Select(r => new { r.name, size = r.bounds.size.ToString() }).ToArray(),
            profile = settingsPath, roots = map.GetRootGameObjects().Select(g => g.name).ToArray() };
    }
}

