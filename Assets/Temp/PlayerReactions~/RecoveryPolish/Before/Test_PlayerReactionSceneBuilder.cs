using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Cheon.Tests.PlayerReactions.Editor
{
    public static class Test_PlayerReactionSceneBuilder
    {
        public const string Root = "Assets/Test/PlayerReactions";
        public const string ScenePath = Root + "/Scenes/Test_PlayerReactions.unity";
        public const string SettingsPath = Root + "/Settings/Test_PlayerReactionSettings.asset";
        public const string ControllerPath = Root + "/Animations/Test_PlayerReactions.controller";
        private const string MainScene = "Assets/Scenes/MountainPass/MountainPass.unity";

        [MenuItem("Cheon/Tests/Player Reactions/테스트 씬 열기")]
        public static void Open()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Play Mode를 종료해 주세요.");
            if (!File.Exists(ScenePath)) { Debug.Log(Build()); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }

        public static string Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Play Mode를 종료해 주세요.");
            if (File.Exists(ScenePath)) return "이미 존재하는 테스트 씬을 보존했습니다: " + ScenePath;
            foreach (string folder in new[] { "Scenes", "Settings", "Materials", "Animations" })
                EnsureFolder(Root + "/" + folder);
            var heavy = ImportMotion(Root + "/Animations/Test_HeavyHit.fbx", "Test_HeavyHit");
            var getUp = ImportMotion(Root + "/Animations/Test_GetUp.fbx", "Test_GetUp");
            var idle = AssetDatabase.LoadAllAssetsAtPath("Assets/Arts/Characters/Player/Animations/Fan/Test_SheathedIdle.fbx")
                .OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
            var settings = ScriptableObject.CreateInstance<Test_PlayerReactionSettings>();
            SetRange(settings.heavyHit, heavy); SetRange(settings.getUp, getUp); SetRange(settings.death, heavy);
            settings.idleClip = idle;
            AssetDatabase.CreateAsset(settings, SettingsPath);

            var sourceScene = SceneManager.GetSceneByPath(MainScene);
            bool openedSource = !sourceScene.IsValid() || !sourceScene.isLoaded;
            if (openedSource) sourceScene = EditorSceneManager.OpenScene(MainScene, OpenSceneMode.Additive);
            var sourceAnimator = sourceScene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<Animator>(true))
                .First(a => a.name == "Test_CheonPlayer");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var player = Object.Instantiate(sourceAnimator.gameObject);
            player.name = "Test_Player";
            player.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            player.transform.localScale = sourceAnimator.transform.lossyScale;
            // 장비와 외형은 복제하고 전투·이동·메인 씬 이벤트 스크립트는 연결하지 않는다.
            foreach (var behaviour in player.GetComponentsInChildren<MonoBehaviour>(true))
                if (behaviour != null) Object.DestroyImmediate(behaviour);
            var animator = player.GetComponent<Animator>();
            animator.runtimeAnimatorController = null; animator.applyRootMotion = false;
            foreach (var child in player.GetComponentsInChildren<Animator>(true))
                if (child != animator) { child.runtimeAnimatorController = null; child.enabled = false; }
            foreach (var collider in player.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);

            var floorMaterial = Material("Test_M_ReactionFloor", new Color(0.13f, 0.19f, 0.20f));
            var lineMaterial = Material("Test_M_ReactionGrid", new Color(0.25f, 0.37f, 0.37f));
            var stage = new GameObject("Test_PreviewStage");
            Cube("Floor", stage.transform, new Vector3(0, -0.13f, 0), new Vector3(16, 0.2f, 16), floorMaterial);
            for (int i = -7; i <= 7; i++)
            {
                Cube("GridX_" + i, stage.transform, new Vector3(i, -0.025f, 0), new Vector3(0.012f, 0.005f, 16), lineMaterial);
                Cube("GridZ_" + i, stage.transform, new Vector3(0, -0.025f, i), new Vector3(16, 0.005f, 0.012f), lineMaterial);
            }
            RenderSettings.skybox = null; RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.5f, 0.56f, 0.61f);
            RenderSettings.fog = false;
            var light = new GameObject("Test_KeyLight", typeof(Light)).GetComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.5f; light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(45, -35, 0);
            var camera = new GameObject("Test_PreviewCamera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.tag = "MainCamera"; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.065f, 0.11f, 0.14f); camera.fieldOfView = 35;
            camera.nearClipPlane = 0.05f; camera.farClipPlane = 100f;
            camera.rect = new Rect(0.31f, 0, 0.69f, 1);
            camera.transform.position = new Vector3(-2.32f, 3.15f, 4.98f);
            camera.transform.LookAt(Vector3.up * 0.85f);
            var rig = new GameObject("Test_PlayerReactionLab");
            var preview = rig.AddComponent<Test_PlayerReactionPreview>();
            preview.Configure(animator, settings, camera, player.transform);
            var controller = CreateController(settings);
            preview.ConfigureController(controller); animator.runtimeAnimatorController = null;
            var ui = rig.AddComponent<Test_PlayerReactionUI>();
            ConfigureText(ui, preview);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("테스트 씬 저장 실패");
            AssetDatabase.SaveAssetIfDirty(settings);
            if (openedSource) EditorSceneManager.CloseScene(sourceScene, true);
            bool hasUnsavedScene = Enumerable.Range(0, SceneManager.sceneCount)
                .Any(i => SceneManager.GetSceneAt(i) != scene && SceneManager.GetSceneAt(i).isDirty);
            if (!hasUnsavedScene) EditorSceneManager.OpenScene(ScenePath);
            Selection.activeObject = settings;
            return $"생성 완료: {ScenePath} / 강피격 {settings.heavyHit.MaximumFrame}f / 일어나기 {settings.getUp.MaximumFrame}f";
        }

        public static string ApplyControllerToScene()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Play Mode를 종료해 주세요.");
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            var preview = scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<Test_PlayerReactionPreview>(true)).Single();
            var settings = AssetDatabase.LoadAssetAtPath<Test_PlayerReactionSettings>(SettingsPath);
            var controller = CreateController(settings);
            preview.ConfigureController(controller);
            preview.CharacterAnimator.runtimeAnimatorController = null;
            EditorUtility.SetDirty(preview); EditorUtility.SetDirty(preview.CharacterAnimator);
            EditorUtility.SetDirty(settings); AssetDatabase.SaveAssetIfDirty(settings);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            return "테스트 전용 Animator Controller 연결: " + ControllerPath;
        }

        private static AnimatorController CreateController(Test_PlayerReactionSettings settings)
        {
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (existing != null) return existing;
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            foreach (string parameter in new[] { "MoveAmount", "HeavyTime", "GetUpTime", "DeathTime" })
                controller.AddParameter(parameter, AnimatorControllerParameterType.Float);
            var source = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Arts/Characters/Player/Animations/MountainMap/Test_MountainPlayer.controller");
            var sourceTree = source.layers[0].stateMachine.states.First(s => s.state.name == "SHEATHED_IDLE").state.motion as BlendTree;
            if (sourceTree == null) throw new InvalidOperationException("기존 플레이어 대기 블렌드트리가 없습니다.");
            // 기존 대기/이동 트리는 복제해 재사용하고 테스트 컨트롤러에서만 관리한다.
            var idleTree = Object.Instantiate(sourceTree);
            idleTree.name = "Test_ReactionIdleLocomotion";
            var children = idleTree.children; children[0].motion = settings.idleClip; idleTree.children = children;
            AssetDatabase.AddObjectToAsset(idleTree, controller);
            var machine = controller.layers[0].stateMachine;
            var idle = machine.AddState("IDLE", new Vector3(280, 50)); idle.motion = idleTree;
            idle.writeDefaultValues = false; machine.defaultState = idle;
            string[] names = { "HEAVY_HIT", "GET_UP", "DEATH" };
            string[] times = { "HeavyTime", "GetUpTime", "DeathTime" };
            for (int index = 0; index < names.Length; index++)
            {
                var state = machine.AddState(names[index], new Vector3(index == 2 ? 540 : 280, index == 1 ? 250 : 150));
                state.motion = settings.GetRange(index).clip;
                state.timeParameter = times[index]; state.timeParameterActive = true;
                state.writeDefaultValues = false; state.iKOnFeet = false;
            }
            EditorUtility.SetDirty(controller); AssetDatabase.SaveAssetIfDirty(controller);
            return controller;
        }

        public static void ConfigureText(Test_PlayerReactionUI ui, Test_PlayerReactionPreview preview)
        {
            const string fontPath = Root + "/Settings/Test_ReactionKorean.asset";
            const string textPath = Root + "/Settings/Test_ReactionTextSettings.asset";
            if (!File.Exists(fontPath)) AssetDatabase.CopyAsset("Assets/Arts/Fonts/MountainPass/MountainKorean.asset", fontPath);
            if (!File.Exists(textPath)) AssetDatabase.CopyAsset("Assets/Settings/MountainPass/MountainTextSettings.asset", textPath);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
            var textSettings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(textPath);
            var serialized = new SerializedObject(textSettings);
            serialized.FindProperty("m_defaultFontAsset").objectReferenceValue = font;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(textSettings);
            ui.Configure(preview, font, textSettings);
            EditorUtility.SetDirty(ui);
        }

        private static void SetRange(Test_PlayerReactionSettings.MotionRange range, AnimationClip clip)
        { range.clip = clip; range.startFrame = 0; range.endFrame = range.MaximumFrame; range.speed = 1; }

        private static AnimationClip ImportMotion(string path, string name)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true; importer.importCameras = false; importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            var clips = importer.defaultClipAnimations;
            if (clips.Length == 0) throw new InvalidOperationException("애니메이션 Take 없음: " + path);
            var clip = clips[0]; clip.name = name;
            clip.loopTime = false; clip.loopPose = false;
            clip.lockRootRotation = true; clip.keepOriginalOrientation = true;
            clip.lockRootHeightY = true; clip.keepOriginalPositionY = true; clip.heightFromFeet = false;
            clip.lockRootPositionXZ = true; clip.keepOriginalPositionXZ = true;
            importer.clipAnimations = new[] { clip }; importer.SaveAndReimport();
            var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isValid || !avatar.isHuman) throw new InvalidOperationException("Humanoid Avatar 오류: " + path);
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = path.Substring(0, path.LastIndexOf('/')); EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
        private static Material Material(string name, Color color)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            material.SetColor("_BaseColor", color); material.SetFloat("_Smoothness", 0.12f);
            AssetDatabase.CreateAsset(material, Root + "/Materials/" + name + ".mat"); return material;
        }
        private static void Cube(string name, Transform parent, Vector3 position, Vector3 size, Material material)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube); cube.name = name;
            cube.transform.SetParent(parent, false); cube.transform.position = position; cube.transform.localScale = size;
            cube.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(cube.GetComponent<Collider>());
        }
    }
}
