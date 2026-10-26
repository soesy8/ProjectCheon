using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Cheon.Tests.PlayerReactions;

public static class Test_GourdSetup
{
    public const string Root = "Assets/Test/PlayerReactions/Drinking";
    public const string ScenePath = "Assets/Test/PlayerReactions/Scenes/Test_PlayerReactions.unity";
    public const string MotionPath = Root + "/Animations/Test_Drinking.fbx";
    public static string Apply() => Cheon.Tests.PlayerReactions.Editor.Test_GourdDrinkingBuilder.Apply();

    public static string ImportAndInspect()
    {
        foreach (string folder in new[] { Root, Root + "/Animations", Root + "/Meshes", Root + "/Materials", Root + "/Prefabs", Root + "/Scripts" })
            EnsureFolder(folder);
        if (!File.Exists(MotionPath)) File.Copy(@"C:\Users\MBC\Downloads\Drinking.fbx", MotionPath);
        AssetDatabase.ImportAsset(MotionPath, ImportAssetOptions.ForceSynchronousImport);
        var importer = (ModelImporter)AssetImporter.GetAtPath(MotionPath);
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.importCameras = false;
        importer.importLights = false;
        importer.animationCompression = ModelImporterAnimationCompression.Off;
        importer.SaveAndReimport();
        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            if (scene.isDirty) throw new InvalidOperationException("현재 씬에 저장하지 않은 변경이 있습니다.");
            scene = EditorSceneManager.OpenScene(ScenePath);
        }
        var preview = UnityEngine.Object.FindFirstObjectByType<Test_PlayerReactionPreview>();
        var animator = preview.CharacterAnimator;
        return Newtonsoft.Json.JsonConvert.SerializeObject(new {
            scene = scene.path,
            animator = animator.name,
            scale = animator.transform.lossyScale.ToString("F4"),
            avatar = AssetDatabase.GetAssetPath(animator.avatar),
            clips = AssetDatabase.LoadAllAssetsAtPath(MotionPath).OfType<AnimationClip>().Select(c => new { c.name, c.length, c.frameRate, c.humanMotion }).ToArray(),
            bones = new[] { HumanBodyBones.Hips, HumanBodyBones.Head, HumanBodyBones.LeftHand, HumanBodyBones.RightHand }.Select(b => new { name = b.ToString(), position = animator.GetBoneTransform(b).position.ToString("F4"), rotation = animator.GetBoneTransform(b).rotation.eulerAngles.ToString("F2") }).ToArray(),
            renderers = animator.GetComponentsInChildren<SkinnedMeshRenderer>().Select(r => new { r.name, size = r.bounds.size.ToString("F3") }).ToArray()
        });
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    public static string SampleOriginals()
    {
        var source = UnityEngine.Object.FindFirstObjectByType<Test_PlayerReactionPreview>().CharacterAnimator;
        var clone = UnityEngine.Object.Instantiate(source.gameObject);
        clone.name = "Test_GourdSampling";
        var animator = clone.GetComponent<Animator>();
        animator.runtimeAnimatorController = null;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var graph = PlayableGraph.Create("Test_GourdSampling");
        graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        var output = AnimationPlayableOutput.Create(graph, "Pose", animator);
        var result = new System.Collections.Generic.List<object>();
        try
        {
            foreach (string path in new[] { "Assets/Arts/Characters/Player/Animations/Fan/Test_DrawSheathe.fbx", MotionPath })
            {
                var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
                var playable = AnimationClipPlayable.Create(graph, clip);
                playable.SetSpeed(0); playable.SetApplyFootIK(false);
                output.SetSourcePlayable(playable); graph.Play();
                for (int i = 0; i <= 8; i++)
                {
                    float time = clip.length * i / 8f;
                    playable.SetTime(time); graph.Evaluate(0);
                    result.Add(new { path, frame = time * clip.frameRate,
                        left = animator.GetBoneTransform(HumanBodyBones.LeftHand).position.ToString("F3"),
                        right = animator.GetBoneTransform(HumanBodyBones.RightHand).position.ToString("F3"),
                        head = animator.GetBoneTransform(HumanBodyBones.Head).position.ToString("F3"),
                        gripToMouth = animator.GetBoneTransform(HumanBodyBones.LeftHand).InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.Head).TransformPoint(new Vector3(0, 0.035f, 0.085f))).ToString("F3"),
                        handRotation = animator.GetBoneTransform(HumanBodyBones.LeftHand).eulerAngles.ToString("F2"),
                        headRotation = animator.GetBoneTransform(HumanBodyBones.Head).eulerAngles.ToString("F2") });
                }
                graph.DestroyPlayable(playable);
            }
            return Newtonsoft.Json.JsonConvert.SerializeObject(result);
        }
        finally { graph.Destroy(); UnityEngine.Object.DestroyImmediate(clone); }
    }
}
