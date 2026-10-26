using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Cheon.Tests.PlayerReactions.Editor
{
    // 마시기 테스트 에셋과 씬 연결만 관리한다. 메인 플레이어 에셋에는 쓰지 않는다.
    public static class Test_GourdDrinkingBuilder
    {
        public const string Root = "Assets/Test/PlayerReactions/Drinking";
        public const string ControllerPath = Root + "/Animations/Test_PlayerReactionsDrinking.controller";
        public const string PrefabPath = Root + "/Prefabs/Test_Gourd.prefab";
        private const string ScenePath = "Assets/Test/PlayerReactions/Scenes/Test_PlayerReactions.unity";

        [MenuItem("Cheon/Tests/Player Reactions/왼손 호리병 마시기 적용")]
        public static void ApplyFromMenu() => Debug.Log(Apply());

        public static string Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Play Mode를 종료한 뒤 적용하세요.");
            foreach (string directory in new[] { Root, Root + "/Animations", Root + "/Meshes", Root + "/Materials", Root + "/Prefabs" }) EnsureFolder(directory);
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                bool dirty = Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty);
                scene = EditorSceneManager.OpenScene(ScenePath, dirty ? OpenSceneMode.Additive : OpenSceneMode.Single);
            }
            SceneManager.SetActiveScene(scene);
            var preview = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Test_PlayerReactionPreview>(true)).Single();
            var animator = preview.CharacterAnimator;
            var draw = Clip("Assets/Arts/Characters/Player/Animations/Fan/Test_DrawSheathe.fbx");
            var drink = Clip(Root + "/Animations/Test_Drinking.fbx");
            var controller = BuildController(draw, drink);
            preview.ConfigureController(controller);

            BuildGourdPrefab();
            var belt = Socket(animator.GetBoneTransform(HumanBodyBones.Hips), "Test_GourdBeltSocket");
            belt.localPosition = new Vector3(-0.19f, -0.005f, -0.18f);
            belt.localRotation = Quaternion.Euler(8, 0, -16);
            var grip = Socket(animator.GetBoneTransform(HumanBodyBones.LeftHand), "Test_LeftGourdGrip");
            grip.localPosition = new Vector3(0.01f, 0.015f, 0);
            grip.localRotation = Quaternion.Euler(0, 0, -90);
            Transform model = belt.Find("Test_Gourd");
            if (model == null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), belt);
                instance.name = "Test_Gourd"; model = instance.transform;
            }
            model.localPosition = Vector3.zero; model.localRotation = Quaternion.identity;
            var motion = preview.GetComponent<Test_GourdDrinking>() ?? preview.gameObject.AddComponent<Test_GourdDrinking>();
            var attachment = preview.GetComponent<Test_GourdAttachment>() ?? preview.gameObject.AddComponent<Test_GourdAttachment>();
            motion.Configure(preview, attachment, draw, drink);
            attachment.Configure(animator, motion, model, belt, grip, model.Find("Test_Cork").gameObject);
            var pose = preview.GetComponent<Test_GourdDrinkingPose>() ?? preview.gameObject.AddComponent<Test_GourdDrinkingPose>();
            pose.Configure(preview, motion);
            EditorUtility.SetDirty(pose);
            EditorUtility.SetDirty(preview); EditorUtility.SetDirty(motion); EditorUtility.SetDirty(attachment);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = preview.gameObject;
            return "왼손 호리병 꺼내기 → 마시기 → 수납 적용: " + ScenePath;
        }

        private static AnimatorController BuildController(AnimationClip draw, AnimationClip drink)
        {
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) == null)
                if (!AssetDatabase.CopyAsset("Assets/Test/PlayerReactions/Animations/Test_PlayerReactions.controller", ControllerPath))
                    throw new IOException("테스트 Animator Controller 복사 실패");
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(Root + "/Animations/Test_LeftUpperBody.mask");
            if (mask == null)
            {
                mask = new AvatarMask { name = "Test_LeftUpperBody" };
                for (int part = 0; part < (int)AvatarMaskBodyPart.LastBodyPart; part++) mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)part, false);
                foreach (var part in new[] { AvatarMaskBodyPart.Body, AvatarMaskBodyPart.Head, AvatarMaskBodyPart.LeftArm, AvatarMaskBodyPart.LeftFingers })
                    mask.SetHumanoidBodyPartActive(part, true);
                AssetDatabase.CreateAsset(mask, Root + "/Animations/Test_LeftUpperBody.mask");
            }
            foreach (string parameter in new[] { "GourdDrawTime", "GourdDrinkTime", "GourdStowTime" })
                if (!controller.parameters.Any(p => p.name == parameter)) controller.AddParameter(parameter, AnimatorControllerParameterType.Float);
            var layers = controller.layers;
            var layer = layers.FirstOrDefault(l => l.name == "GourdUpperBody");
            if (layer == null)
            {
                var machine = new AnimatorStateMachine { name = "GourdUpperBody" };
                AssetDatabase.AddObjectToAsset(machine, controller);
                layer = new AnimatorControllerLayer { name = "GourdUpperBody", stateMachine = machine, avatarMask = mask,
                    blendingMode = AnimatorLayerBlendingMode.Override, defaultWeight = 0, iKPass = false };
                controller.AddLayer(layer);
            }
            layer.stateMachine.name = "GourdUpperBody";
            AddState(layer.stateMachine, "EMPTY", null, null, false, 0);
            AddState(layer.stateMachine, "GOURD_DRAW", draw, "GourdDrawTime", true, 1);
            AddState(layer.stateMachine, "GOURD_DRINK", drink, "GourdDrinkTime", false, 2);
            AddState(layer.stateMachine, "GOURD_STOW", draw, "GourdStowTime", true, 3);
            layer.stateMachine.defaultState = layer.stateMachine.states.First(s => s.state.name == "EMPTY").state;
            EditorUtility.SetDirty(layer.stateMachine); EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssetIfDirty(controller);
            return controller;
        }

        private static void AddState(AnimatorStateMachine machine, string name, AnimationClip clip, string parameter, bool mirror, int row)
        {
            var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == name) ?? machine.AddState(name, new Vector3(300, row * 80));
            state.motion = clip; state.mirror = mirror; state.iKOnFeet = false; state.writeDefaultValues = false;
            if (parameter != null) { state.timeParameter = parameter; state.timeParameterActive = true; }
            EditorUtility.SetDirty(state);
        }

        private static void BuildGourdPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null) return;
            var amber = Material("Test_M_GourdAmber", new Color(0.57f, 0.25f, 0.065f), 0.16f, 0.05f);
            var rim = Material("Test_M_GourdRim", new Color(0.7f, 0.43f, 0.14f), 0.3f, 0.3f);
            var red = Material("Test_M_GourdCord", new Color(0.38f, 0.035f, 0.025f), 0.15f, 0f);
            var corkMaterial = Material("Test_M_GourdCork", new Color(0.22f, 0.105f, 0.035f), 0.1f, 0f);
            var root = new GameObject("Test_Gourd");
            try
            {
                // 단면을 회전시켜 속이 빈 주둥이와 두 개의 둥근 몸통을 하나의 메시로 만든다.
                var body = Lathe("Test_GourdBody", new[] {
                    new Vector2(0,-.22f), new Vector2(.045f,-.215f), new Vector2(.074f,-.195f),
                    new Vector2(.091f,-.165f), new Vector2(.097f,-.13f), new Vector2(.091f,-.095f),
                    new Vector2(.075f,-.06f), new Vector2(.052f,-.032f), new Vector2(.044f,-.014f),
                    new Vector2(.047f,.004f), new Vector2(.063f,.027f), new Vector2(.068f,.052f),
                    new Vector2(.06f,.077f), new Vector2(.041f,.099f), new Vector2(.024f,.111f),
                    new Vector2(.024f,.13f), new Vector2(.017f,.13f), new Vector2(.017f,.108f) }, 32);
                MeshObject(root.transform, "Test_AmberBody", body, amber);
                MeshObject(root.transform, "Test_MouthRim", Ring("Test_GourdRim", .024f, .13f, .004f), rim);
                MeshObject(root.transform, "Test_WaistCord", Ring("Test_GourdWaistCord", .047f, -.012f, .006f), red);
                MeshObject(root.transform, "Test_NeckBand", Ring("Test_GourdNeckBand", .026f, .114f, .004f), rim);
                var cork = Lathe("Test_GourdCork", new[] { new Vector2(0,.124f), new Vector2(.017f,.124f),
                    new Vector2(.019f,.14f), new Vector2(.021f,.141f), new Vector2(.021f,.15f), new Vector2(0,.15f) }, 20);
                MeshObject(root.transform, "Test_Cork", cork, corkMaterial);
                var knot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                knot.name = "Test_CordKnot"; knot.transform.SetParent(root.transform, false);
                knot.transform.localPosition = new Vector3(-.049f, -.013f, 0);
                knot.transform.localScale = new Vector3(.021f, .025f, .021f);
                knot.GetComponent<Renderer>().sharedMaterial = red; Object.DestroyImmediate(knot.GetComponent<Collider>());
                for (int i = 0; i < 2; i++)
                {
                    var cord = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    cord.name = "Test_CordTail" + i; cord.transform.SetParent(root.transform, false);
                    cord.transform.localPosition = new Vector3(-.057f - i * .008f, -.056f, i * .006f);
                    cord.transform.localRotation = Quaternion.Euler(0, 0, -12 - 12 * i);
                    cord.transform.localScale = new Vector3(.005f, .033f + i * .006f, .005f);
                    cord.GetComponent<Renderer>().sharedMaterial = red; Object.DestroyImmediate(cord.GetComponent<Collider>());
                }
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static Material Material(string name, Color color, float smoothness, float metallic)
        {
            string path = Root + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            material.SetColor("_BaseColor", color); material.SetFloat("_Smoothness", smoothness); material.SetFloat("_Metallic", metallic);
            AssetDatabase.CreateAsset(material, path); return material;
        }

        private static Mesh Ring(string name, float radius, float height, float thickness)
        {
            var profile = new Vector2[9];
            for (int i = 0; i < profile.Length; i++)
            {
                float angle = i * Mathf.PI * 2 / 8;
                profile[i] = new Vector2(radius + Mathf.Cos(angle) * thickness, height + Mathf.Sin(angle) * thickness);
            }
            return Lathe(name, profile, 32);
        }

        private static Mesh Lathe(string name, Vector2[] profile, int radialSegments)
        {
            string path = Root + "/Meshes/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;
            var vertices = new Vector3[profile.Length * (radialSegments + 1)];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[(profile.Length - 1) * radialSegments * 6];
            int triangle = 0;
            for (int row = 0; row < profile.Length; row++)
            for (int column = 0; column <= radialSegments; column++)
            {
                float angle = column * Mathf.PI * 2 / radialSegments;
                int index = row * (radialSegments + 1) + column;
                vertices[index] = new Vector3(Mathf.Cos(angle) * profile[row].x, profile[row].y, Mathf.Sin(angle) * profile[row].x);
                uv[index] = new Vector2((float)column / radialSegments, (float)row / (profile.Length - 1));
                if (row == profile.Length - 1 || column == radialSegments) continue;
                int next = index + radialSegments + 1;
                triangles[triangle++] = index; triangles[triangle++] = next; triangles[triangle++] = index + 1;
                triangles[triangle++] = index + 1; triangles[triangle++] = next; triangles[triangle++] = next + 1;
            }
            var mesh = new Mesh { name = name, vertices = vertices, uv = uv, triangles = triangles };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, path); return mesh;
        }

        private static void MeshObject(Transform parent, string name, Mesh mesh, Material material)
        {
            var item = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); item.transform.SetParent(parent, false);
            item.GetComponent<MeshFilter>().sharedMesh = mesh; item.GetComponent<MeshRenderer>().sharedMaterial = material;
        }
        private static Transform Socket(Transform parent, string name)
        {
            var socket = parent.Find(name);
            if (socket != null) return socket;
            socket = new GameObject(name).transform; socket.SetParent(parent, false); return socket;
        }
        private static AnimationClip Clip(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/'); EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
