using System;
using System.Text;
using Cheon.Prototype.MountainPass;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Cheon.Tests.FanAttackVfx.Editor
{
    public static class Test_RollDustSetup
    {
        private const string Root = Test_FanAttackVfxBuilder.Root;
        private const string ProfilePath = Root + "/Settings/Test_RollDustProfile.asset";
        private const string PrefabPath = Root + "/Prefabs/Test_RollDust.prefab";

        [MenuItem("Cheon/Tests/Fan Attack VFX/구르기 착지 먼지 적용")]
        public static void ApplyMenu() => Debug.Log(Apply());

        public static string Apply()
        {
            RequireTestScene();
            var control = Object.FindFirstObjectByType<PassAnimationController>();
            var owner = control.transform.parent;
            var profile = AssetDatabase.LoadAssetAtPath<Test_RollDustProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<Test_RollDustProfile>();
                profile.dustMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/Test_M_GroundDust.mat");
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            {
                var root = new GameObject("Test_RollDust");
                try
                {
                    var particles = new GameObject("Test_RollGroundDust").AddComponent<ParticleSystem>();
                    particles.transform.SetParent(root.transform, false);
                    var effect = root.AddComponent<Test_RollDust>();
                    effect.Configure(profile, null, null, particles);
                    PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                }
                finally { Object.DestroyImmediate(root); }
            }
            var instance = owner.GetComponentInChildren<Test_RollDust>(true);
            if (instance == null)
            {
                var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), owner);
                instance = root.GetComponent<Test_RollDust>();
            }
            instance.Configure(profile, control, owner, instance.GetComponentInChildren<ParticleSystem>());
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
            Test_FanAttackVfxBuilder.SaveTestAssets();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Selection.activeGameObject = instance.gameObject;
            return "구르기 12f 착지 / 12~23f 지면 먼지를 테스트 씬에 연결했습니다.";
        }

        public static string Verify()
        {
            var effect = Object.FindFirstObjectByType<Test_RollDust>();
            var profile = AssetDatabase.LoadAssetAtPath<Test_RollDustProfile>(ProfilePath);
            if (effect == null || profile == null || profile.dustMaterial == null || effect.GetComponentInChildren<ParticleSystem>() == null)
                return "FAIL: 구르기 먼지 연결을 확인하세요.";
            if (!AssetDatabase.GetAssetPath(profile.dustMaterial).StartsWith(Root + "/")) return "FAIL: 테스트 외부 머티리얼";
            return "PASS: 구르기 먼지 전용 프로필·프리팹·씬 연결 / 기존 테스트 먼지 머티리얼 재사용";
        }

        private static void RequireTestScene()
        {
            if (EditorApplication.isPlaying || SceneManager.GetActiveScene().path != Test_FanAttackVfxBuilder.ScenePath)
                throw new InvalidOperationException("테스트 씬의 Edit Mode에서 실행하세요.");
        }

        public static string InspectRoll()
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
                var animator = clone.GetComponentInChildren<PassAnimationController>(true).CharacterAnimator;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.applyRootMotion = false;
                animator.Rebind(); animator.Update(0);
                for (int layer = 1; layer < animator.layerCount; layer++) animator.SetLayerWeight(layer, 0);
                var clip = (AnimationClip)new SerializedObject(source).FindProperty("rollClip").objectReferenceValue;
                report.AppendLine($"롤 클립: {clip.frameRate}fps / {clip.length:F3}s");
                for (int frame = 0; frame <= Mathf.RoundToInt(clip.length * clip.frameRate); frame++)
                {
                    animator.Play("ROLL", 0, frame / (clip.length * clip.frameRate));
                    animator.Update(0);
                    var hips = clone.transform.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.Hips).position);
                    var foot = clone.transform.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.LeftFoot).position);
                    var hand = clone.transform.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.LeftHand).position);
                    var head = clone.transform.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.Head).position);
                    report.AppendLine($"{frame}: hips y {hips.y:F3}, z {hips.z:F3}; foot {foot.y:F3}; hand {hand.y:F3}; head {head.y:F3}");
                }
            }
            finally { Object.DestroyImmediate(clone); }
            return report.ToString();
        }
    }
}
