using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Cheon.Testing.PlayerFan
{
    // 테스트 환경을 재생성할 때 필요한 에셋 연결과 씬 구성을 한곳에서 관리한다.
    public static class Test_PlayerFanSceneBuilder
    {
        public const string Root = "Assets/Test/PlayerFanAnimation/";
        public const string ScenePath = Root + "Scenes/Test_PlayerFanAnimation.unity";

        [MenuItem("Cheon/Tests/Open Player Fan Animation")]
        public static void OpenTestScene()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Play Mode를 종료한 후 테스트 씬을 열어 주세요.");
            if (File.Exists(ScenePath)) { EditorSceneManager.OpenScene(ScenePath); return; }
            CreateTestScene();
        }

        public static string CreateTestScene()
        {
            if (File.Exists(ScenePath)) throw new InvalidOperationException("기존 테스트 씬을 덮어쓰지 않습니다. Open Player Fan Animation 메뉴로 열어 주세요.");
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("현재 씬에 저장되지 않은 변경이 있습니다. 저장한 후 다시 실행해 주세요.");

            var characterController = CreateCharacterController();
            AnimationClip openingClip = Clip(Root + "Models/Test_CheonFanOpening.fbx");
            var fanController = EmptyController(Root + "Animations/Test_Fan.controller");
            var openingState = fanController.layers[0].stateMachine.AddState("OPENING");
            openingState.motion = openingClip;
            fanController.layers[0].stateMachine.defaultState = openingState;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.60f, 0.69f, 0.75f);
            RenderSettings.ambientEquatorColor = new Color(0.38f, 0.44f, 0.46f);
            RenderSettings.ambientGroundColor = new Color(0.27f, 0.28f, 0.25f);
            RenderSettings.fog = false;

            var stage = new GameObject("Test_Stage");
            Material floor = Material("Test_M_Floor", new Color(0.19f, 0.23f, 0.25f));
            Material podium = Material("Test_M_Podium", new Color(0.39f, 0.43f, 0.43f));
            Material trim = Material("Test_M_StageTrim", new Color(0.19f, 0.39f, 0.36f));
            Material grid = Material("Test_M_Grid", new Color(0.25f, 0.29f, 0.30f));
            Primitive("Test_Floor", PrimitiveType.Plane, stage.transform, new Vector3(0f, -0.12f, 0f), new Vector3(4f, 1f, 4f), floor);
            Primitive("Test_StageTrim", PrimitiveType.Cylinder, stage.transform, new Vector3(0f, -0.065f, 0f), new Vector3(3.6f, 0.05f, 3.6f), trim);
            Primitive("Test_Podium", PrimitiveType.Cylinder, stage.transform, new Vector3(0f, -0.04f, 0f), new Vector3(3.5f, 0.04f, 3.5f), podium);
            for (int i = -8; i <= 8; i++)
            {
                Primitive("Test_GridX", PrimitiveType.Cube, stage.transform, new Vector3(i, -0.114f, 0f), new Vector3(0.008f, 0.003f, 16f), grid);
                Primitive("Test_GridZ", PrimitiveType.Cube, stage.transform, new Vector3(0f, -0.114f, i), new Vector3(16f, 0.003f, 0.008f), grid);
            }

            var playerRoot = new GameObject("Test_Player");
            var player = InstantiateModel("Models/Test_CheonPlayer.fbx", "Test_CheonPlayer", playerRoot.transform);
            var animator = player.GetComponent<Animator>();
            if (animator == null) animator = player.AddComponent<Animator>();
            animator.avatar = AssetDatabase.LoadAllAssetsAtPath(Root + "Models/Test_CheonPlayer.fbx").OfType<Avatar>().First(a => a.isHuman && a.isValid);
            animator.runtimeAnimatorController = characterController;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var playerMaterial = Material("Test_M_Player", Color.white, AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "Textures/Test_T_PlayerBaseColor.png"));
            foreach (var renderer in player.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial = playerMaterial;

            Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (hand == null) throw new InvalidOperationException("캐릭터의 오른손 Humanoid 본을 찾을 수 없습니다.");
            var socket = new GameObject("Test_RightHandFanSocket").transform;
            socket.SetParent(hand, false);
            // T 포즈에서 부채의 세로축은 위, 종이 면은 캐릭터 정면을 향하게 배치한다.
            Transform finger = animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal)
                ?? animator.GetBoneTransform(HumanBodyBones.RightIndexProximal);
            // 현재 모델처럼 손가락이 단순화된 리그도 검지 기준으로 장착할 수 있다.
            Vector3 palmDirection = finger != null ? finger.position - hand.position : Vector3.right * 0.06f;
            Vector3 palm = hand.position + palmDirection * 0.75f + new Vector3(0f, -0.015f, 0.012f);
            socket.position = palm - Vector3.up * 0.035f;
            socket.rotation = Quaternion.identity;

            var closed = InstantiateModel("Models/Test_CheonFanClosed.fbx", "Test_FanClosed", socket);
            var open = InstantiateModel("Models/Test_CheonFanOpen.fbx", "Test_FanOpen", socket);
            var animated = InstantiateModel("Models/Test_CheonFanOpening.fbx", "Test_FanOpening", socket);
            var materials = new Dictionary<string, Material>();
            foreach (var fan in new[] { closed, open, animated }) ConvertFanMaterials(fan, materials);
            var fanAnimator = animated.GetComponent<Animator>();
            if (fanAnimator == null) fanAnimator = animated.AddComponent<Animator>();
            fanAnimator.runtimeAnimatorController = fanController;
            fanAnimator.applyRootMotion = false;
            fanAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            closed.SetActive(false); animated.SetActive(false); open.SetActive(true);

            var controls = new GameObject("Test_AnimationControl");
            var attachment = controls.AddComponent<Test_PlayerFanAttachment>();
            attachment.Configure(socket, closed, open, animated, fanAnimator, openingClip.length);
            var animationControl = controls.AddComponent<Test_PlayerFanAnimationController>();
            animationControl.Configure(animator, attachment);

            var cameraObject = new GameObject("Test_MainCamera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.fieldOfView = 38f; camera.nearClipPlane = 0.03f; camera.farClipPlane = 80f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.095f, 0.13f, 0.16f);
            var orbit = cameraObject.AddComponent<Test_PlayerFanCamera>();
            orbit.Configure(playerRoot.transform);
            CreateLight("Test_KeyLight", Quaternion.Euler(38f, -30f, 0f), new Color(1f, 0.92f, 0.82f), 1.6f, true);
            CreateLight("Test_FillLight", Quaternion.Euler(25f, 145f, 0f), new Color(0.7f, 0.85f, 1f), 0.8f, false);
            var hud = new GameObject("Test_Controls").AddComponent<Test_PlayerFanControls>();
            hud.Configure(animationControl, attachment, orbit,
                AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root + "Fonts/Test_PlayerFanKorean.asset"),
                AssetDatabase.LoadAssetAtPath<TMP_Settings>(Root + "Fonts/Test_TMPSettings.asset"));

            // 씬 저장 전에 대기 포즈를 평가해 Edit Mode에서도 손 장착 상태를 확인할 수 있게 한다.
            animator.Rebind();
            animator.Play("SHEATHED_IDLE", 0, 0f);
            animator.Update(0f);
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.LookAt(new Vector3(0f, 1f, 0f), Quaternion.Euler(12f, 155f, 0f), 3f);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            UpgradeCombatSetup();
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = controls;
            return ScenePath;
        }

        public static AnimatorController CreateCharacterController()
        {
            var controller = EmptyController(Root + "Animations/Test_Player.controller");
            controller.AddParameter("MoveAmount", AnimatorControllerParameterType.Float);
            var states = new[] { ("SHEATHED_IDLE", "SheathedIdle"), ("DRAWN_IDLE", "DrawnIdle"), ("DRAW", "DrawSheathe"), ("SHEATHE", "DrawSheathe"), ("ATTACK1", "Attack1"), ("ATTACK2", "Attack2"), ("ATTACK3", "Attack3"), ("HIT", "Hit"), ("ROLL", "Roll"), ("RUN2", "Run2") };
            var machine = controller.layers[0].stateMachine;
            for (int i = 0; i < states.Length; i++)
            {
                var state = machine.AddState(states[i].Item1, new Vector3(250f * (i % 3), 90f * (i / 3), 0f));
                var clip = Clip(Root + "Animations/Test_" + states[i].Item2 + ".fbx");
                state.motion = clip;
                if (i < 2)
                {
                    var tree = new BlendTree { name = "Test_" + states[i].Item1 + "_Locomotion", blendType = BlendTreeType.Simple1D, blendParameter = "MoveAmount", useAutomaticThresholds = false };
                    AssetDatabase.AddObjectToAsset(tree, controller);
                    tree.AddChild(clip, 0f);
                    tree.AddChild(Clip(Root + "Animations/Test_Run.fbx"), 1f);
                    state.motion = tree;
                }
                if (i == 0) machine.defaultState = state;
                if (i < 2 || i > 6) continue;
                string parameter = i == 2 ? "DrawSpeed" : i == 3 ? "SheatheSpeed" : "Attack" + (i - 3) + "Speed";
                controller.AddParameter(parameter, AnimatorControllerParameterType.Float);
                var parameters = controller.parameters;
                parameters[parameters.Length - 1].defaultFloat = i == 3 ? -1f : 1f;
                controller.parameters = parameters;
                state.speedParameter = parameter;
                state.speedParameterActive = true;
            }
            return controller;
        }

        [MenuItem("Cheon/Tests/Update Player Fan Combat Setup")]
        public static void UpgradeCombatSetup()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Play Mode를 종료한 후 실행하세요.");
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath) throw new InvalidOperationException("플레이어 부채 테스트 씬을 먼저 열어 주세요.");
            Transform Find(string name) => scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).First(t => t.name == name);
            var animator = Find("Test_CheonPlayer").GetComponent<Animator>();
            animator.runtimeAnimatorController = CreateCharacterController();
            animator.avatar = AssetDatabase.LoadAllAssetsAtPath(Root + "Models/Test_CheonPlayer.fbx").OfType<Avatar>().First(a => a.isValid && a.isHuman);
            animator.applyRootMotion = false;
            animator.Rebind(); animator.Play("SHEATHED_IDLE", 0, 0f); animator.Update(0f);
            var socket = Find("Test_RightHandFanSocket");
            ConfigureHandSocket(animator, socket);

            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            var storage = hips.Find("Test_StowedFanSocket");
            if (storage == null) { storage = new GameObject("Test_StowedFanSocket").transform; storage.SetParent(hips, false); }
            // 손이 부채를 집는 시점의 실제 자세를 수납 위치로 삼아 장착 전환 때의 점프를 줄인다.
            animator.Play("DRAW", 0, 0.48f); animator.Update(0f);
            storage.SetPositionAndRotation(socket.position, socket.rotation);
            animator.Play("SHEATHED_IDLE", 0, 0f); animator.Update(0f);

            var closed = Find("Test_FanClosed").gameObject;
            var open = Find("Test_FanOpen").gameObject;
            var animated = Find("Test_FanOpening").gameObject;
            var mount = storage.Find("Test_FanMount");
            if (mount == null) { mount = new GameObject("Test_FanMount").transform; mount.SetParent(storage, false); }
            foreach (var fan in new[] { closed, open, animated })
            {
                fan.transform.SetParent(mount, false);
                fan.transform.localPosition = Vector3.zero; fan.transform.localRotation = Quaternion.identity; fan.transform.localScale = Vector3.one;
            }
            var attachment = Find("Test_AnimationControl").GetComponent<Test_PlayerFanAttachment>();
            var fanAnimator = animated.GetComponent<Animator>();
            fanAnimator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(Root + "Animations/Test_Fan.controller");
            fanAnimator.applyRootMotion = false;
            fanAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            attachment.Configure(socket, closed, open, animated, fanAnimator, Clip(Root + "Models/Test_CheonFanOpening.fbx").length);
            attachment.ConfigureMounts(storage, mount);
            var serializedFan = new SerializedObject(attachment);
            serializedFan.FindProperty("startOpen").boolValue = false;
            serializedFan.FindProperty("openingAmount").floatValue = 0f;
            serializedFan.FindProperty("fanScale").floatValue = 0.85f;
            serializedFan.ApplyModifiedPropertiesWithoutUndo();
            var control = attachment.GetComponent<Test_PlayerFanAnimationController>();
            control.Configure(animator, attachment, Clip(Root + "Animations/Test_DrawSheathe.fbx").length);
            var serializedControl = new SerializedObject(control);
            for (int i = 1; i <= 3; i++)
                serializedControl.FindProperty("attack" + i + "Clip").objectReferenceValue = Clip(Root + "Animations/Test_Attack" + i + ".fbx");
            serializedControl.FindProperty("transitionDuration").floatValue = 0.09f;
            serializedControl.FindProperty("repeatSelectedAnimation").boolValue = false;
            serializedControl.ApplyModifiedPropertiesWithoutUndo();
            attachment.SetSequencePose(false, 0f, true);
            mount.localScale = Vector3.one * 0.85f;
            RefreshTestFont();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = control.gameObject;
        }

        public static void ConfigureHandSocket(Animator animator, Transform socket)
        {
            animator.Play("SHEATHED_IDLE", 0, 0f); animator.Update(0f);
            var hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            socket.SetParent(hand, false);
            var index = animator.GetBoneTransform(HumanBodyBones.RightIndexProximal);
            var middle = animator.GetBoneTransform(HumanBodyBones.RightIndexIntermediate);
            Vector3 fingers = (index.position - hand.position).normalized;
            Vector3 bend = (middle.position - index.position).normalized;
            Vector3 palmNormal = (bend - fingers * Vector3.Dot(bend, fingers)).normalized;
            Vector3 fanUp = Vector3.Cross(fingers, palmNormal).normalized;
            if (fanUp.sqrMagnitude < 0.5f) fanUp = animator.transform.right;
            if (Vector3.Dot(fanUp, animator.transform.right) < 0f) fanUp = -fanUp;
            if (palmNormal.sqrMagnitude < 0.5f) palmNormal = animator.transform.forward;
            fanUp = -fanUp;
            Vector3 grip = Vector3.Lerp(hand.position, index.position, 0.8f) + palmNormal * 0.025f + fingers * 0.01f;
            socket.SetPositionAndRotation(grip - fanUp * 0.035f, Quaternion.LookRotation(palmNormal, fanUp));
            socket.localScale = Vector3.one;

            // 발도 대기 자세에서 부채를 위·안쪽으로 옮기고, 세로축으로 뒤집어 그림 면이 위를 보게 한다.
            animator.Play("DRAWN_IDLE", 0, 0f); animator.Update(0f);
            socket.position += animator.transform.TransformDirection(new Vector3(-0.035f, 0.055f, 0.025f));
            socket.localRotation *= Quaternion.Euler(0f, 180f, 0f);
            animator.Play("SHEATHED_IDLE", 0, 0f); animator.Update(0f);
        }

        private static void RefreshTestFont()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root + "Fonts/Test_PlayerFanKorean.asset");
            var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(Root + "Fonts/Test_TMPSettings.asset");
            typeof(TMP_Settings).GetField("s_Instance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)?.SetValue(null, settings);
            string text = new string(Enumerable.Range(32, 95).Select(i => (char)i).ToArray()) + string.Join("", Directory.GetFiles(Root + "Scripts", "*.cs").Select(File.ReadAllText));
            font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            // TMP는 전부 등록된 문자만 다시 요청해도 실패 목록을 반환하므로 새 글자만 추가한다.
            uint[] missing = text.Where(c => !char.IsControl(c)).Select(c => (uint)c).Distinct().Where(c => !font.HasCharacter((int)c)).ToArray();
            if (missing.Length > 0) font.TryAddCharacters(missing, out missing);
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            if (missing != null && missing.Any(c => !font.HasCharacter((int)c))) throw new InvalidOperationException("테스트 UI의 일부 글자를 추가하지 못했습니다.");
            EditorUtility.SetDirty(font);
            foreach (var texture in font.atlasTextures) EditorUtility.SetDirty(texture);
        }

        private static AnimationClip Clip(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));

        private static AnimatorController EmptyController(string path)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null) return AnimatorController.CreateAnimatorControllerAtPath(path);
            foreach (var child in controller.layers[0].stateMachine.states) controller.layers[0].stateMachine.RemoveState(child.state);
            foreach (var tree in AssetDatabase.LoadAllAssetsAtPath(path).OfType<BlendTree>()) UnityEngine.Object.DestroyImmediate(tree, true);
            controller.parameters = Array.Empty<AnimatorControllerParameter>();
            return controller;
        }

        private static GameObject InstantiateModel(string path, string name, Transform parent)
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root + path));
            model.name = name; model.transform.SetParent(parent, false);
            model.transform.localPosition = Vector3.zero; model.transform.localRotation = Quaternion.identity; model.transform.localScale = Vector3.one;
            return model;
        }

        private static Material Material(string name, Color color, Texture2D texture = null)
        {
            string path = Root + "Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.25f);
            material.SetFloat("_Cull", 0f);
            material.doubleSidedGI = true;
            if (texture != null) material.SetTexture("_BaseMap", texture);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ConvertFanMaterials(GameObject fan, Dictionary<string, Material> converted)
        {
            foreach (var renderer in fan.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = ShadowCastingMode.TwoSided;
                renderer.sharedMaterials = renderer.sharedMaterials.Select(source =>
                {
                    if (source == null) return null;
                    if (source.name.StartsWith("Test_", StringComparison.Ordinal)) return source;
                    if (converted.TryGetValue(source.name, out Material existing)) return existing;
                    Color color = source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white;
                    bool paper = source.name.Contains("PaintedPaper");
                    var material = Material("Test_" + source.name, paper ? Color.white : color, paper ? AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "Textures/Test_T_FanPaperBaseColor.png") : null);
                    if (source.name.Contains("Gold")) { material.SetFloat("_Metallic", 0.65f); material.SetFloat("_Smoothness", 0.5f); }
                    converted[source.name] = material;
                    return material;
                }).ToArray();
            }
        }

        private static void Primitive(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var item = GameObject.CreatePrimitive(type); item.name = name; item.transform.SetParent(parent, false);
            item.transform.localPosition = position; item.transform.localScale = scale;
            item.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(item.GetComponent<Collider>());
        }

        private static void CreateLight(string name, Quaternion rotation, Color color, float intensity, bool shadows)
        {
            var light = new GameObject(name).AddComponent<Light>();
            light.type = LightType.Directional; light.transform.rotation = rotation; light.color = color; light.intensity = intensity;
            light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
        }
    }
}
