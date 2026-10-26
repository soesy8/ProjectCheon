using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cheon.Prototype.MountainPass;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Cheon.Tests.FanAttackVfx.Editor
{
    // 재사용 가능한 테스트 씬 제작 도구. 모든 쓰기 경로는 Test 폴더 아래로 제한한다.
    public static class Test_FanAttackVfxBuilder
    {
        public const string Root = "Assets/Test/FanAttackVfx";
        public const string ScenePath = Root + "/Scenes/Test_FanAttackVfx.unity";
        public const string ProfilePath = Root + "/Settings/Test_FanAttackVfxProfile.asset";
        private const string OriginalScene = "Assets/Scenes/MountainPass/MountainPass.unity";

        [MenuItem("Cheon/Tests/Fan Attack VFX/테스트 씬 만들기 또는 열기")]
        public static void OpenOrBuild() => Debug.Log(Build());

        public static string Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Play Mode를 종료한 뒤 실행하세요.");
            if (File.Exists(ScenePath))
            {
                EditorSceneManager.OpenScene(ScenePath);
                return "기존 테스트 씬을 열었습니다. 설정값은 유지했습니다.";
            }
            EnsureFolders();
            Scene source = SceneManager.GetActiveScene();
            if (source.path != OriginalScene || source.isDirty)
                throw new InvalidOperationException("원본 MountainPass 씬을 저장된 상태로 열어 주세요. 원본에는 쓰지 않습니다.");
            // SaveAsCopy로 원본의 GUID와 파일 내용을 그대로 보존한다.
            if (!EditorSceneManager.SaveScene(source, ScenePath, true)) throw new IOException("테스트 씬 복사 실패");
            Scene scene = EditorSceneManager.OpenScene(ScenePath);
            CloneEditableSettings(scene);
            var control = FindInScene<PassAnimationController>(scene);
            var owner = control.transform.parent;
            CreatePreviewStage();
            PrepareTestView();
            var profile = ScriptableObject.CreateInstance<Test_FanAttackVfxProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
            SampleMotion(control, profile);

            var flow = CreateTexture("Test_T_WindFlow", 512, 256, FlowPixel, true);
            var streak = CreateTexture("Test_T_WindStreak", 128, 256, StreakPixel, false);
            var dust = CreateTexture("Test_T_GroundDust", 256, 256, DustPixel, false);
            profile.sweepMaterial = CreateMaterial("Test_M_FanSweep", "Cheon/Tests/FanAttack/Sweep", flow);
            profile.windMaterial = CreateMaterial("Test_M_FanWind", "Cheon/Tests/FanAttack/Sweep", flow);
            profile.windMaterial.SetFloat("_WindMode", 1);
            profile.windMaterial.SetFloat("_FlowSpeed", 2.8f);
            profile.windParticleMaterial = CreateMaterial("Test_M_WindStreak", "Cheon/Tests/FanAttack/SoftParticle", streak);
            profile.dustMaterial = CreateMaterial("Test_M_GroundDust", "Cheon/Tests/FanAttack/SoftParticle", dust);

            var root = new GameObject("Test_FanAttackVfx");
            root.transform.SetPositionAndRotation(owner.position, Quaternion.Euler(0, owner.eulerAngles.y, 0));
            var attackSweeps = new Test_FanSweepMesh[3];
            for (int i = 0; i < 3; i++)
                attackSweeps[i] = CreateSweep(root.transform, "Test_Attack" + (i + 1) + "_Sweep", profile.GetSweep(i + 1), profile.sweepMaterial, false);
            var windStyle = new Test_FanAttackVfxProfile.Sweep
            {
                radius = profile.wind.radius, innerRadius = profile.wind.startRadius, angle = profile.wind.angle,
                height = AnimationCurve.Linear(0, profile.wind.height, 1, profile.wind.height), archHeight = .16f
            };
            var windMesh = CreateSweep(root.transform, "Test_Attack3_WindSector", windStyle, profile.windMaterial, true);
            var particleRoot = new GameObject("Test_WindAndGroundDust");
            particleRoot.transform.SetParent(root.transform, false);
            var particleEffects = particleRoot.AddComponent<Test_FanWindParticles>();
            var windSystem = CreateParticleSystem(particleRoot.transform, "Test_WindStreaks");
            var dustSystem = CreateParticleSystem(particleRoot.transform, "Test_GroundDust");
            particleEffects.Configure(profile, null, windSystem, dustSystem);
            var effect = root.AddComponent<Test_FanAttackVfx>();
            effect.Configure(profile, null, null, attackSweeps, windMesh, particleEffects, null);
            string prefabPath = Root + "/Prefabs/Test_FanAttackVfx.prefab";
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, prefabPath, InteractionMode.AutomatedAction);
            var ranges = FindInScene<PassAttackRangeEffects>(scene);
            effect.Configure(profile, control, owner, attackSweeps, windMesh, particleEffects, ranges);
            PrefabUtility.RecordPrefabInstancePropertyModifications(effect);
            if (ranges != null) ranges.enabled = false;
            SaveTestAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Test_FanAttackVfxRevision.Apply();
            Test_RollDustSetup.Apply();
            Selection.activeGameObject = root;
            SceneView.lastActiveSceneView?.Frame(new Bounds(owner.position + Vector3.up, Vector3.one * 10), false);
            return "생성: " + ScenePath + " / 1타 3m·35°, 2타 3m·155°, 3타 3m·210° + 바람 8m·35°.";
        }

        public static string CreatePreviewStage()
        {
            if (EditorApplication.isPlaying || SceneManager.GetActiveScene().path != ScenePath)
                throw new InvalidOperationException("테스트 씬의 Edit Mode에서 실행하세요.");
            if (GameObject.Find("Test_PreviewStage") != null) return "테스트 바닥이 이미 있습니다.";
            var owner = FindInScene<PassAnimationController>(SceneManager.GetActiveScene()).transform.parent;
            Vector3 position = owner.position + Vector3.up * 10;
            var stage = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stage.name = "Test_PreviewStage";
            stage.transform.position = position + new Vector3(0, -.2f, 3);
            stage.transform.localScale = new Vector3(26, .4f, 30);
            string materialPath = Root + "/Materials/Test_M_PreviewStage.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.name = "Test_M_PreviewStage";
                material.SetColor("_BaseColor", new Color(.16f, .2f, .19f));
                material.SetFloat("_Smoothness", .1f);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            stage.GetComponent<MeshRenderer>().sharedMaterial = material;
            owner.SetPositionAndRotation(position + Vector3.up * .05f, Quaternion.identity);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            return "복사한 씬에 평평한 VFX 확인용 바닥을 만들었습니다.";
        }

        public static string PrepareTestView()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != ScenePath)
                throw new InvalidOperationException("테스트 씬의 Edit Mode에서 실행하세요.");
            // 넓은 바람과 먼지의 색을 확인할 수 있도록 사본의 안개만 끈다.
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var mist in root.GetComponentsInChildren<PassMountainMist>(true)) mist.enabled = false;
                foreach (var fog in root.GetComponentsInChildren<PassHeightFog>(true)) fog.enabled = false;
            }
            RenderSettings.fog = false;
            var camera = FindInScene<PassCamera>(scene);
            var serialized = new SerializedObject(camera);
            serialized.FindProperty("distance").floatValue = 9.5f;
            serialized.FindProperty("pitch").floatValue = 40;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            return "테스트 사본의 안개와 카메라를 VFX 확인용으로 설정했습니다.";
        }

        public static string FinalizeDocumentation()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Play Mode를 종료한 뒤 실행하세요.");
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root + "/Documentation" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.maxTextureSize = 1024;
                foreach (string platform in new[] { "Standalone", "Android", "iPhone", "WebGL" })
                {
                    var settings = importer.GetPlatformTextureSettings(platform);
                    settings.overridden = true;
                    settings.maxTextureSize = 1024;
                    settings.format = TextureImporterFormat.Automatic;
                    importer.SetPlatformTextureSettings(settings);
                }
                importer.SaveAndReimport();
            }
            SaveTestAssets();
            return VerifyAssets();
        }

        private static void EnsureFolders()
        {
            foreach (string directory in new[] { "Assets/Test", Root, Root + "/Scenes", Root + "/Settings", Root + "/Textures", Root + "/Materials", Root + "/Meshes", Root + "/Prefabs", Root + "/Documentation" })
            {
                if (AssetDatabase.IsValidFolder(directory)) continue;
                AssetDatabase.CreateFolder(Path.GetDirectoryName(directory).Replace('\\', '/'), Path.GetFileName(directory));
            }
        }

        private static T FindInScene<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).FirstOrDefault();

        private static void CloneEditableSettings(Scene scene)
        {
            var copies = new Dictionary<Object, Object>();
            foreach (var behaviour in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true)))
            {
                if (behaviour == null) continue;
                var serialized = new SerializedObject(behaviour);
                var property = serialized.GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference || !(property.objectReferenceValue is ScriptableObject original)) continue;
                    string originalPath = AssetDatabase.GetAssetPath(original);
                    if (!originalPath.StartsWith("Assets/Settings/MountainPass/", StringComparison.Ordinal)) continue;
                    if (!copies.TryGetValue(original, out Object copy))
                    {
                        string target = Root + "/Settings/Test_" + Path.GetFileName(originalPath);
                        if (!AssetDatabase.CopyAsset(originalPath, target)) throw new IOException("설정 복사 실패: " + originalPath);
                        copy = AssetDatabase.LoadAssetAtPath<ScriptableObject>(target);
                        copies.Add(original, copy);
                    }
                    property.objectReferenceValue = copy;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static Test_FanSweepMesh CreateSweep(Transform parent, string name, Test_FanAttackVfxProfile.Sweep style, Material material, bool wind)
        {
            var item = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer), typeof(Test_FanSweepMesh));
            item.transform.SetParent(parent, false);
            var mesh = Test_FanSweepMesh.CreateGeometry(style, wind);
            mesh.name = name;
            AssetDatabase.CreateAsset(mesh, Root + "/Meshes/" + name + ".asset");
            item.GetComponent<MeshFilter>().sharedMesh = mesh;
            item.GetComponent<MeshRenderer>().sharedMaterial = material;
            item.GetComponent<MeshRenderer>().enabled = false;
            return item.GetComponent<Test_FanSweepMesh>();
        }

        private static ParticleSystem CreateParticleSystem(Transform parent, string name)
        {
            var item = new GameObject(name);
            item.transform.SetParent(parent, false);
            var particles = item.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main; main.playOnAwake = false;
            return particles;
        }

        [MenuItem("Cheon/Tests/Fan Attack VFX/공격 모션 다시 샘플링")]
        public static void Resample()
        {
            if (EditorApplication.isPlaying || SceneManager.GetActiveScene().path != ScenePath)
                throw new InvalidOperationException("테스트 씬의 Edit Mode에서 실행하세요.");
            var profile = AssetDatabase.LoadAssetAtPath<Test_FanAttackVfxProfile>(ProfilePath);
            SampleMotion(FindInScene<PassAnimationController>(SceneManager.GetActiveScene()), profile);
            SaveTestAssets();
            Debug.Log("테스트 설정의 높이·진행률·방향 곡선을 갱신했습니다. 반경과 각도는 유지했습니다.");
        }

        internal static void SampleMotion(PassAnimationController source, Test_FanAttackVfxProfile profile)
        {
            var clone = Object.Instantiate(source.transform.parent.gameObject);
            clone.name = "Test_MotionSampling";
            clone.hideFlags = HideFlags.HideAndDontSave;
            clone.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            try
            {
                foreach (var behaviour in clone.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
                foreach (var collider in clone.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                var control = clone.GetComponentInChildren<PassAnimationController>(true);
                var fan = clone.GetComponentInChildren<PassFanAttachment>(true);
                var animator = control.CharacterAnimator;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.applyRootMotion = false;
                animator.Rebind();
                animator.Update(0);
                for (int layer = 1; layer < animator.layerCount; layer++) animator.SetLayerWeight(layer, 0);
                var serialized = new SerializedObject(source);
                for (int attack = 1; attack <= 3; attack++)
                {
                    var style = profile.GetSweep(attack);
                    var clip = serialized.FindProperty("attack" + attack + "Clip").objectReferenceValue as AnimationClip;
                    if (clip == null) throw new InvalidOperationException("공격 클립 연결 누락: " + attack);
                    int samples = Mathf.CeilToInt((style.endFrame - style.startFrame) * 2) + 1;
                    var positions = new Vector3[samples];
                    var distances = new float[samples];
                    float angularTravel = 0;
                    for (int i = 0; i < samples; i++)
                    {
                        float frame = Mathf.Lerp(style.startFrame, style.endFrame, i / (float)(samples - 1));
                        animator.Play("ATTACK" + attack, 0, frame / (clip.length * clip.frameRate));
                        animator.Update(0);
                        positions[i] = clone.transform.InverseTransformPoint(fan.HandSocket.position);
                        if (i > 0)
                        {
                            distances[i] = distances[i - 1] + Vector3.Distance(positions[i - 1], positions[i]);
                            float before = Mathf.Atan2(positions[i - 1].x, positions[i - 1].z) * Mathf.Rad2Deg;
                            float after = Mathf.Atan2(positions[i].x, positions[i].z) * Mathf.Rad2Deg;
                            angularTravel += Mathf.DeltaAngle(before, after);
                        }
                    }
                    float total = distances[samples - 1];
                    var progressKeys = new Keyframe[samples];
                    var heightKeys = new Keyframe[samples];
                    for (int i = 0; i < samples; i++)
                    {
                        float time = i / (float)(samples - 1);
                        float progress = total > .0001f ? distances[i] / total : time;
                        progressKeys[i] = new Keyframe(time, progress);
                        heightKeys[i] = new Keyframe(progress, Mathf.Clamp(positions[i].y, .25f, 2.1f));
                    }
                    style.motionProgress = new AnimationCurve(progressKeys);
                    style.height = new AnimationCurve(heightKeys);
                    style.reverse = style.plane == Test_FanAttackVfxProfile.SweepPlane.Horizontal && angularTravel < 0;
                    // 부채의 넓은 궤적은 약간 더 긴 꼬리로 회전감을 읽게 한다.
                    if (attack == 3) style.tailLength = .78f;
                    Debug.Log($"Test VFX 모션 {attack}: {style.startFrame}~{style.endFrame}f, 손 이동 {total:F2}m, 회전 {angularTravel:F1}°, 높이 {positions.Min(p => p.y):F2}~{positions.Max(p => p.y):F2}m");
                }
                EditorUtility.SetDirty(profile);
            }
            finally { Object.DestroyImmediate(clone); }
        }

        internal static Texture2D CreateTexture(string name, int width, int height, Func<float, float, Color> pixel, bool repeat)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            var colors = new Color[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++) colors[y * width + x] = pixel(x / (float)(width - 1), y / (float)(height - 1));
            texture.SetPixels(colors); texture.Apply();
            string path = Root + "/Textures/" + name + ".png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = !repeat;
            importer.mipmapEnabled = true;
            importer.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.maxTextureSize = 1024;
            foreach (string platform in new[] { "Standalone", "Android", "iPhone", "WebGL" })
            {
                var settings = importer.GetPlatformTextureSettings(platform);
                settings.overridden = true;
                settings.maxTextureSize = 1024;
                settings.format = TextureImporterFormat.Automatic;
                importer.SetPlatformTextureSettings(settings);
            }
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        internal static Color FlowPixel(float u, float v)
        {
            // 셰이더가 UV를 반복해도 결이 이어지도록 두 축의 값과 변화율을 연결한다.
            u = Mathf.Repeat(u, 1);
            v = Mathf.Repeat(v, 1);
            float bend = PeriodicNoise(u, v, 2.1f, 3.7f, 2, 5);
            float grain = PeriodicNoise(u, v, 3.5f, 29, 0, bend * 2);
            float fine = PeriodicNoise(u, v, 8, 75, 10, bend * 6);
            float strands = Mathf.Pow(Mathf.Clamp01(.5f + .5f * Mathf.Sin(
                v * Mathf.PI * 42 + bend * 8 + Mathf.Sin(u * Mathf.PI * 2) * 3)), 7);
            return new Color(Mathf.Clamp01(grain * .8f + strands * .5f), fine, grain, 1);
        }

        private static float PeriodicNoise(float u, float v, float frequencyU, float frequencyV, float offsetU, float offsetV)
        {
            float blendU = Mathf.SmoothStep(0, 1, u);
            float blendV = Mathf.SmoothStep(0, 1, v);
            float first = Mathf.Lerp(Mathf.PerlinNoise(u * frequencyU + offsetU, v * frequencyV + offsetV),
                Mathf.PerlinNoise((u - 1) * frequencyU + offsetU, v * frequencyV + offsetV), blendU);
            float second = Mathf.Lerp(Mathf.PerlinNoise(u * frequencyU + offsetU, (v - 1) * frequencyV + offsetV),
                Mathf.PerlinNoise((u - 1) * frequencyU + offsetU, (v - 1) * frequencyV + offsetV), blendU);
            return Mathf.Lerp(first, second, blendV);
        }

        public static string VerifyFlowSeams()
        {
            var texture = new Texture2D(2, 2);
            try
            {
                ImageConversion.LoadImage(texture, File.ReadAllBytes(Root + "/Textures/Test_T_WindFlow.png"));
                var pixels = texture.GetPixels32();
                int maximumDifference = 0;
                for (int y = 0; y < texture.height; y++)
                    Compare(pixels[y * texture.width], pixels[(y + 1) * texture.width - 1]);
                for (int x = 0; x < texture.width; x++)
                    Compare(pixels[x], pixels[(texture.height - 1) * texture.width + x]);
                return (maximumDifference <= 1 ? "PASS" : "FAIL") + ": 궤적 텍스처 양쪽 경계 최대 차이 " + maximumDifference + "/255";

                void Compare(Color32 first, Color32 second)
                {
                    maximumDifference = Mathf.Max(maximumDifference, Mathf.Abs(first.r - second.r),
                        Mathf.Abs(first.g - second.g), Mathf.Abs(first.b - second.b));
                }
            }
            finally { Object.DestroyImmediate(texture); }
        }

        private static Color StreakPixel(float u, float v)
        {
            float bend = Mathf.Sin(v * 4) * .075f;
            float width = Mathf.Exp(-Mathf.Pow((u - .5f + bend) * 13, 2));
            float length = Mathf.Pow(Mathf.Sin(v * Mathf.PI), .8f);
            float alpha = width * length * Mathf.Lerp(.55f, 1, Mathf.PerlinNoise(u * 5, v * 16));
            return new Color(1, 1, 1, alpha);
        }

        private static Color DustPixel(float u, float v)
        {
            float radius = new Vector2((u - .5f) * 2, (v - .5f) * 2).magnitude;
            float noise = Mathf.PerlinNoise(u * 7.2f + 5, v * 7.2f + 9) * .65f + Mathf.PerlinNoise(u * 17, v * 17) * .35f;
            float alpha = Mathf.SmoothStep(0, 1, Mathf.Clamp01((1 - radius) * 2)) * Mathf.SmoothStep(.15f, .85f, noise);
            return new Color(.85f + noise * .15f, .85f + noise * .15f, .85f + noise * .15f, alpha);
        }

        private static Material CreateMaterial(string name, string shaderName, Texture texture)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null) throw new InvalidOperationException("셰이더 임포트를 확인하세요: " + shaderName);
            var material = new Material(shader) { name = name };
            material.SetTexture("_BaseMap", texture);
            AssetDatabase.CreateAsset(material, Root + "/Materials/" + name + ".mat");
            return material;
        }

        internal static void SaveTestAssets()
        {
            foreach (string guid in AssetDatabase.FindAssets("", new[] { Root }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset != null) AssetDatabase.SaveAssetIfDirty(asset);
            }
        }

        public static string VerifyAssets()
        {
            var profile = AssetDatabase.LoadAssetAtPath<Test_FanAttackVfxProfile>(ProfilePath);
            var failures = new List<string>();
            if (profile == null) failures.Add("설정 에셋 없음");
            if (!File.Exists(ScenePath)) failures.Add("테스트 씬 없음");
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root + "/Textures" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                if (texture.width > 1024 || texture.height > 1024 || importer.maxTextureSize != 1024) failures.Add("텍스처 제한: " + path);
                foreach (string platform in new[] { "Standalone", "Android", "iPhone", "WebGL" })
                    if (importer.GetPlatformTextureSettings(platform).maxTextureSize != 1024) failures.Add("플랫폼 제한: " + path + " / " + platform);
            }
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { Root + "/Materials" }))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (material.shader == null || ShaderUtil.ShaderHasError(material.shader)) failures.Add("셰이더 오류: " + material.name);
            }
            var scene = SceneManager.GetActiveScene();
            if (scene.path == ScenePath)
            {
                var control = FindInScene<PassAnimationController>(scene);
                if (control == null) failures.Add("캐릭터 연결 없음");
                else if (!AssetDatabase.GetAssetPath(control.SavedTimings).StartsWith(Root + "/")) failures.Add("원본 타이밍 설정 참조");
            }
            return failures.Count == 0 ? "PASS: 테스트 씬·프로필·머티리얼·텍스처·설정 격리 확인" : "FAIL: " + string.Join("; ", failures);
        }
    }
}
