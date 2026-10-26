using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cheon.Prototype.MountainPass;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Cheon.Tests.FanAttackVfx.Editor
{
    // 기존 테스트의 설정을 보존하면서 요청한 궤적과 판정만 개선한다.
    public static class Test_FanAttackVfxRevision
    {
        private static readonly List<GameObject> probes = new List<GameObject>();
        private static readonly List<string> probeHits = new List<string>();
        private static void RequireTestScene()
        {
            if (EditorApplication.isPlaying || SceneManager.GetActiveScene().path != Test_FanAttackVfxBuilder.ScenePath)
                throw new InvalidOperationException("테스트 씬의 Edit Mode에서 실행하세요.");
        }

        [MenuItem("Cheon/Tests/Fan Attack VFX/개선된 궤적과 바람 적용")]
        public static void ApplyMenu() => Debug.Log(Apply());

        public static string Apply()
        {
            RequireTestScene();
            string root = Test_FanAttackVfxBuilder.Root;
            var profile = AssetDatabase.LoadAssetAtPath<Test_FanAttackVfxProfile>(Test_FanAttackVfxBuilder.ProfilePath);
            var control = Object.FindFirstObjectByType<PassAnimationController>();
            if (profile.layoutVersion < 2)
            {
                // 기존 1타의 수평 범위를 바람으로 옮긴 뒤 내려치기 궤적을 새로 구성한다.
                if (profile.attack1.plane == Test_FanAttackVfxProfile.SweepPlane.Horizontal)
                {
                    profile.attack1Wind.radius = profile.attack1.radius;
                    profile.attack1Wind.angle = profile.attack1.angle;
                    profile.attack1Wind.startFrame = profile.attack1.startFrame;
                    profile.attack1Wind.endFrame = profile.attack1.endFrame;
                }
                profile.attack1Wind.height = .3f;
                profile.attack1Wind.conformToGround = false;
                profile.attack1Wind.intensity = 1.1f;
                profile.attack1Wind.color = new Color(.58f, .9f, .81f, .68f);
                profile.attack1.label = "1타 · 세로 내려치기";
                profile.attack1.startFrame = 21; profile.attack1.endFrame = 28;
                profile.attack1.hitStartFrame = 24; profile.attack1.hitEndFrame = 28;
                profile.attack1.plane = Test_FanAttackVfxProfile.SweepPlane.Vertical;
                profile.attack1.radius = 2.45f; profile.attack1.innerRadius = 1.25f;
                profile.attack1.angle = 100; profile.attack1.pivotHeight = 1.45f;
                profile.attack1.startPitch = 65; profile.attack1.roll = -18;
                profile.attack1.heightOffset = 0; profile.attack1.tailLength = .8f;
                profile.attack1.fadeSeconds = .18f;
                profile.attack2.startFrame = 28; profile.attack2.endFrame = 34;
                profile.attack2.hitStartFrame = -1; profile.attack2.hitEndFrame = -1;
                Test_FanAttackVfxBuilder.SampleMotion(control, profile);
                profile.wind.size = new Vector2(.3f, .65f);
                profile.wind.speed = 18;
                profile.wind.waveLength = .7f;
                profile.wind.particlesPerSecond = 130;
                profile.wind.color = new Color(.58f, .9f, .81f, .68f);
                profile.wind.intensity = 1.1f;
                var noise = Test_FanAttackVfxBuilder.CreateTexture("Test_T_GustNoise", 512, 256, GustNoise, true);
                var curls = Test_FanAttackVfxBuilder.CreateTexture("Test_T_WindCurl", 256, 256, CurlPixel, false);
                profile.windMaterial.shader = Shader.Find("Cheon/Tests/FanAttack/Gust");
                profile.windMaterial.SetTexture("_BaseMap", noise);
                profile.windParticleMaterial.SetTexture("_BaseMap", curls);
                profile.sweepMaterial.SetFloat("_FlowSpeed", .35f);
                EditorUtility.SetDirty(profile.windMaterial);
                EditorUtility.SetDirty(profile.windParticleMaterial);
                EditorUtility.SetDirty(profile.sweepMaterial);
                profile.layoutVersion = 2;
            }
            if (profile.layoutVersion < 3)
            {
                profile.attack1.startFrame = 19;
                profile.attack1.innerRadius = .45f;
                profile.attack1.hitStartFrame = -1;
                profile.attack1.hitEndFrame = -1;
                profile.attack1Wind.forwardScale = 1.3f;
                profile.attack1Wind.travelProgress = Test_FanAttackVfxProfile.CreateWindTravelCurve();
                profile.wind.travelProgress = Test_FanAttackVfxProfile.CreateWindTravelCurve();
                Test_FanAttackVfxBuilder.SampleMotion(control, profile);
                profile.layoutVersion = 3;
            }
            if (profile.layoutVersion < 4)
            {
                // 앞쪽 끝(-35도)을 유지하고 시작 쪽만 45도 더 뒤로 펼친다.
                profile.attack1.startPitch = 110;
                profile.attack1.angle = 145;
                profile.attack1.tailLength = 1;
                var flow = Test_FanAttackVfxBuilder.CreateTexture("Test_T_WindFlow", 512, 256,
                    Test_FanAttackVfxBuilder.FlowPixel, true);
                var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(flow));
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                foreach (string platform in new[] { "Standalone", "Android", "iPhone", "WebGL" })
                {
                    var settings = importer.GetPlatformTextureSettings(platform);
                    settings.format = TextureImporterFormat.RGBA32;
                    settings.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.SetPlatformTextureSettings(settings);
                }
                importer.SaveAndReimport();
                profile.sweepMaterial.SetTexture("_BaseMap", flow);
                EditorUtility.SetDirty(profile.sweepMaterial);
                profile.layoutVersion = 4;
            }
            profile.NotifyChanged();
            EditorUtility.SetDirty(profile);
            string prefabPath = root + "/Prefabs/Test_FanAttackVfx.prefab";
            var prefab = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var sweeps = new Test_FanSweepMesh[3];
                for (int i = 0; i < 3; i++)
                    sweeps[i] = UpdateSweep(prefab.transform, "Test_Attack" + (i + 1) + "_Sweep", profile.GetSweep(i + 1), profile.sweepMaterial, false);
                var wind3 = UpdateSweep(prefab.transform, "Test_Attack3_WindSector", WindStyle(profile.wind), profile.windMaterial, true, profile.wind.forwardScale);
                var wind1 = UpdateSweep(prefab.transform, "Test_Attack1_WindSector", WindStyle(profile.attack1Wind), profile.windMaterial, true, profile.attack1Wind.forwardScale);
                var particles = prefab.GetComponentInChildren<Test_FanWindParticles>();
                particles.Configure(profile, null);
                prefab.GetComponent<Test_FanAttackVfx>().Configure(profile, null, null, sweeps, wind3, particles, null, wind1);
                PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }

            var effect = Object.FindFirstObjectByType<Test_FanAttackVfx>();
            var owner = control.transform.parent;
            var rangeEffects = Object.FindFirstObjectByType<PassAttackRangeEffects>();
            var attackMeshes = Enumerable.Range(1, 3).Select(i => effect.transform.Find("Test_Attack" + i + "_Sweep").GetComponent<Test_FanSweepMesh>()).ToArray();
            effect.Configure(profile, control, owner, attackMeshes,
                effect.transform.Find("Test_Attack3_WindSector").GetComponent<Test_FanSweepMesh>(), effect.GetComponentInChildren<Test_FanWindParticles>(),
                rangeEffects, effect.transform.Find("Test_Attack1_WindSector").GetComponent<Test_FanSweepMesh>());
            PrefabUtility.RecordPrefabInstancePropertyModifications(effect);

            // 원본 스크립트 대신 테스트 전용 판정을 복사한 씬에만 붙인다.
            var originalCombat = owner.GetComponent<PassPlayerCombat>();
            if (originalCombat != null) originalCombat.enabled = false;
            var detection = owner.GetComponent<Test_FanAttackHitDetection>();
            if (detection == null) detection = owner.gameObject.AddComponent<Test_FanAttackHitDetection>();
            var damageObject = owner.Find("Test_AttackDamageSettings");
            if (damageObject == null)
            {
                damageObject = new GameObject("Test_AttackDamageSettings").transform;
                damageObject.SetParent(owner, false);
            }
            var damageSettings = damageObject.GetComponent<Test_FanAttackDamageSettings>();
            if (damageSettings == null) damageSettings = damageObject.gameObject.AddComponent<Test_FanAttackDamageSettings>();
            detection.Configure(profile, control, Object.FindFirstObjectByType<PassGameFlow>(),
                Object.FindFirstObjectByType<PassEffects>(), Object.FindFirstObjectByType<PassAudio>(), damageSettings, effect);
            // 공유 체력의 피격 대기가 두 채널 중 하나를 막지 않도록 사본 씬의 적만 조정한다.
            // 중복 피해 방지는 판정기의 직접/바람 기록이 각각 담당하며 명시적인 무적은 유지한다.
            foreach (var health in Object.FindObjectsByType<PassHealth>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (health.gameObject.scene != SceneManager.GetActiveScene() || health.transform.IsChildOf(owner)) continue;
                var serializedHealth = new SerializedObject(health);
                serializedHealth.FindProperty("hitRecovery").floatValue = 0;
                serializedHealth.ApplyModifiedPropertiesWithoutUndo();
            }
            if (rangeEffects != null)
            {
                var serialized = new SerializedObject(rangeEffects);
                var ranges = serialized.FindProperty("ranges");
                for (int i = 0; i < ranges.arraySize; i++)
                {
                    var range = ranges.GetArrayElementAtIndex(i);
                    if (range.FindPropertyRelative("attack").intValue != 2) continue;
                    range.FindPropertyRelative("startFrame").intValue = 28;
                    range.FindPropertyRelative("endFrame").intValue = 34;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            Test_FanAttackVfxBuilder.SaveTestAssets();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            return "적용: 1타 후방 45도 확장·145도 궤적 / 반복 경계가 연결된 궤적 텍스처 / 직접·원격 독립 판정 / 피해 설정 유지";
        }

        private static Test_FanAttackVfxProfile.Sweep WindStyle(Test_FanAttackVfxProfile.Wind wind) =>
            new Test_FanAttackVfxProfile.Sweep { radius = wind.radius, angle = wind.angle, innerRadius = wind.startRadius,
                height = AnimationCurve.Linear(0, wind.height, 1, wind.height), archHeight = 0 };

        private static Test_FanSweepMesh UpdateSweep(Transform parent, string name, Test_FanAttackVfxProfile.Sweep style, Material material, bool wind, float forwardScale = 1)
        {
            var child = parent.Find(name);
            if (child == null)
            {
                child = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer), typeof(Test_FanSweepMesh)).transform;
                child.SetParent(parent, false);
            }
            string path = Test_FanAttackVfxBuilder.Root + "/Meshes/" + name + ".asset";
            var geometry = Test_FanSweepMesh.CreateGeometry(style, wind, forwardScale);
            geometry.name = name;
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null) { AssetDatabase.CreateAsset(geometry, path); mesh = geometry; }
            else { EditorUtility.CopySerialized(geometry, mesh); Object.DestroyImmediate(geometry); EditorUtility.SetDirty(mesh); }
            child.GetComponent<MeshFilter>().sharedMesh = mesh;
            child.GetComponent<MeshRenderer>().sharedMaterial = material;
            child.GetComponent<MeshRenderer>().enabled = false;
            return child.GetComponent<Test_FanSweepMesh>();
        }

        private static Color GustNoise(float u, float v)
        {
            float first = Mathf.PerlinNoise(u * 5.5f + 7, v * 4.3f + 3);
            float second = Mathf.PerlinNoise(u * 12 + first * 2, v * 9 + 12);
            return new Color(first, Mathf.Lerp(first, second, .6f), second, 1);
        }

        private static Color CurlPixel(float u, float v)
        {
            float x = (u - .5f) * 2, y = (v - .5f) * 2;
            float radius = Mathf.Sqrt(x * x + y * y);
            float t = (Mathf.Atan2(y, x) + Mathf.PI) / (Mathf.PI * 2);
            float wobble = (Mathf.PerlinNoise(u * 7 + 3, v * 7) - .5f) * .045f;
            float first = Mathf.Exp(-Mathf.Pow((radius - (.16f + t * .53f) + wobble) / .055f, 2));
            float second = Mathf.Exp(-Mathf.Pow((radius - (.4f + t * .38f) - wobble) / .04f, 2)) * .45f;
            float ends = Mathf.Pow(Mathf.Max(0, Mathf.Sin(t * Mathf.PI)), .65f);
            return new Color(1, 1, 1, Mathf.Clamp01((first + second) * ends * .8f));
        }

        public static string Verify()
        {
            var profile = AssetDatabase.LoadAssetAtPath<Test_FanAttackVfxProfile>(Test_FanAttackVfxBuilder.ProfilePath);
            var errors = new List<string>();
            if (profile.layoutVersion < 2 || profile.attack1.plane != Test_FanAttackVfxProfile.SweepPlane.Vertical) errors.Add("1타 세로 궤적");
            var flat = AssetDatabase.LoadAssetAtPath<Mesh>(Test_FanAttackVfxBuilder.Root + "/Meshes/Test_Attack1_WindSector.asset");
            if (flat == null || flat.bounds.size.y > .001f || profile.attack1Wind.conformToGround) errors.Add("1타 바람 평면");
            if (profile.attack2.startFrame != 28 || profile.attack2.endFrame != 34 || profile.attack2.reverse) errors.Add("2타 시점/방향");
            if (profile.windMaterial.shader == profile.sweepMaterial.shader) errors.Add("바람/궤적 셰이더 분리");
            var original = Object.FindFirstObjectByType<PassPlayerCombat>(FindObjectsInactive.Include);
            if (original == null || original.enabled || Object.FindFirstObjectByType<Test_FanAttackHitDetection>() == null) errors.Add("테스트 전용 판정 연결");
            if (profile.dust.frontShare <= 0 || profile.dust.endBurstCount <= 0) errors.Add("전방 먼지");
            var detection = Object.FindFirstObjectByType<Test_FanAttackHitDetection>();
            if (detection == null || detection.DamageSettings == null || detection.DamageSettings.transform.childCount != 0)
                errors.Add("빈 오브젝트 피해 설정");
            if (profile.attack1.startFrame != 19 || profile.attack1.innerRadius != .45f) errors.Add("1타 시점/안쪽 확장");
            if (Mathf.Abs(profile.attack1Wind.forwardScale - 1.3f) > .001f || flat == null || Mathf.Abs(flat.bounds.max.z - 3.9f) > .01f)
                errors.Add("전방 길이 배율");
            foreach (var gust in new[] { profile.attack1Wind, profile.wind })
                if (gust.EvaluateSpeed(.1f) <= gust.EvaluateSpeed(.8f) || gust.EvaluateTravel(1) != 1) errors.Add("바람 감속");
            foreach (var boss in Object.FindObjectsByType<PassBossController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (new SerializedObject(boss.GetComponent<PassHealth>()).FindProperty("hitRecovery").floatValue != 0) errors.Add("적의 공유 피격 대기");
            return errors.Count == 0 ? "PASS: 19f 궤적·안쪽 확장 / 평면 전방 3.9m / 감속 곡선 / 2타 정방향 / 피해 설정·독립 판정 연결 / 전방 먼지" : string.Join("; ", errors);
        }

        public static string SetupProbes(int attack)
        {
            if (!EditorApplication.isPlaying || SceneManager.GetActiveScene().path != Test_FanAttackVfxBuilder.ScenePath)
                throw new InvalidOperationException("테스트 씬의 Play Mode에서 실행하세요.");
            ClearProbes();
            var control = Object.FindFirstObjectByType<PassAnimationController>();
            var owner = control.transform.parent;
            owner.GetComponent<PassPlayerMovement>().InputEnabled = false;
            owner.rotation = Quaternion.identity;
            var camera = Object.FindFirstObjectByType<PassCamera>();
            camera.enabled = false;
            camera.transform.position = owner.position + new Vector3(4, 6, -7);
            camera.transform.LookAt(owner.position + new Vector3(0, .8f, 2));
            if (attack == 2)
            {
                CreateProbe("Left", -50, 2.25f, owner, control, attack);
                CreateProbe("Center", 0, 2.25f, owner, control, attack);
                CreateProbe("Right", 50, 2.25f, owner, control, attack);
            }
            else if (attack == 1)
            {
                CreateProbe("Inner", 0, .8f, owner, control, attack);
                CreateProbe("Dual", 0, 1.9f, owner, control, attack);
                CreateProbe("WindEnd", 0, 3.7f, owner, control, attack);
                CreateProbe("Beyond", 0, 4.3f, owner, control, attack);
                CreateProbe("Side", 60, 2.2f, owner, control, attack);
            }
            else
            {
                CreateProbe("Dual", 0, 1.9f, owner, control, attack);
                CreateProbe("DirectOnly", 50, 2.25f, owner, control, attack);
                CreateProbe("WindEnd", 0, 7.8f, owner, control, attack);
                CreateProbe("Beyond", 0, 8.7f, owner, control, attack);
            }
            Physics.SyncTransforms();
            return "판정 검증 대상 " + probes.Count + "개 생성";
        }

        private static void CreateProbe(string label, float angle, float distance, Transform owner, PassAnimationController control, int attack)
        {
            var target = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            target.name = "Test_HitProbe_" + label;
            target.hideFlags = HideFlags.DontSave;
            target.transform.position = owner.position + Quaternion.Euler(0, angle, 0) * Vector3.forward * distance + Vector3.up * .9f;
            target.transform.localScale = new Vector3(.3f, .85f, .3f);
            var health = target.AddComponent<PassHealth>();
            health.Configure(1000, 0); health.Restore();
            health.Damaged += damage => probeHits.Add(label + " @ " + (control.NormalizedTime * control.GetAttackMaximumFrame(attack)).ToString("F2") + "f / " + damage + " damage");
            probes.Add(target);
        }

        public static string ProbeResults() => string.Join("; ", probeHits) + " | " +
            string.Join(", ", probes.Where(p => p != null).Select(p => p.name + "=" + p.GetComponent<PassHealth>().Current));

        public static void ClearProbes()
        {
            foreach (var probe in probes) if (probe != null) Object.DestroyImmediate(probe);
            probes.Clear(); probeHits.Clear();
        }

        public static string InspectMotion()
        {
            RequireTestScene();
            var source = Object.FindFirstObjectByType<PassAnimationController>();
            var clone = Object.Instantiate(source.transform.parent.gameObject);
            clone.hideFlags = HideFlags.HideAndDontSave;
            clone.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var report = new StringBuilder();
            try
            {
                foreach (var behaviour in clone.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
                var control = clone.GetComponentInChildren<PassAnimationController>(true);
                var fan = clone.GetComponentInChildren<PassFanAttachment>(true);
                var animator = control.CharacterAnimator;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.applyRootMotion = false;
                animator.Rebind(); animator.Update(0);
                for (int layer = 1; layer < animator.layerCount; layer++) animator.SetLayerWeight(layer, 0);
                fan.SetSequencePose(true, 1, true);
                var serialized = new SerializedObject(source);
                for (int attack = 1; attack <= 2; attack++)
                {
                    var clip = (AnimationClip)serialized.FindProperty("attack" + attack + "Clip").objectReferenceValue;
                    report.AppendLine("ATTACK " + attack);
                    for (int frame = attack == 1 ? 16 : 20; frame <= (attack == 1 ? 42 : 64); frame += 2)
                    {
                        animator.Play("ATTACK" + attack, 0, frame / (clip.length * clip.frameRate));
                        animator.Update(0);
                        Vector3 hand = clone.transform.InverseTransformPoint(fan.HandSocket.position);
                        Vector3 tip = clone.transform.InverseTransformPoint(fan.HandSocket.TransformPoint(Vector3.up * .5f));
                        report.AppendLine($"{frame}: hand {hand.x:F3},{hand.y:F3},{hand.z:F3}; tip {tip.x:F3},{tip.y:F3},{tip.z:F3}; yaw {Mathf.Atan2(tip.x, tip.z) * Mathf.Rad2Deg:F1}");
                    }
                }
            }
            finally { Object.DestroyImmediate(clone); }
            return report.ToString();
        }
    }
}
