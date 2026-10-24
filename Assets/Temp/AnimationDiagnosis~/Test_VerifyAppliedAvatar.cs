var root = "Assets/Test/PlayerFanAnimation/";
var tempRoot = "Assets/Temp/AnimationDiagnosisVerification";
if (!AssetDatabase.IsValidFolder(tempRoot)) AssetDatabase.CreateFolder("Assets/Temp", "AnimationDiagnosisVerification");
var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene(root + "Scenes/Test_PlayerFanAnimation.unity");
var reports = new List<object>();
float RotationError(Quaternion first, Quaternion second)
{
    // 가져온 쿼터니언의 미세한 길이 오차를 제거하고 배정밀도로 실제 방향 차이를 계산한다.
    double dot = (double)first.x * second.x + (double)first.y * second.y + (double)first.z * second.z + (double)first.w * second.w;
    double firstLength = (double)first.x * first.x + (double)first.y * first.y + (double)first.z * first.z + (double)first.w * first.w;
    double secondLength = (double)second.x * second.x + (double)second.y * second.y + (double)second.z * second.z + (double)second.w * second.w;
    double cosine = Math.Min(1d, Math.Abs(dot) / Math.Sqrt(firstLength * secondLength));
    return (float)(2d * Math.Acos(cosine) * 180d / Math.PI);
}
try
{
    var control = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Cheon.Testing.PlayerFan.Test_PlayerFanAnimationController>(true)).Single();
    var animator = control.CharacterAnimator;
    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
    animator.Rebind();
    control.ApplyAttackSpeeds();
    var neck = animator.GetBoneTransform(HumanBodyBones.Neck);
    var head = animator.GetBoneTransform(HumanBodyBones.Head);
    var chest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
    for (int attack = 1; attack <= 3; attack++)
    {
        var path = root + "Animations/Test_Attack" + attack + ".fbx";
        var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
        var tempPath = tempRoot + "/Test_RawAttack" + attack + ".fbx";
        if (!AssetDatabase.CopyAsset(path, tempPath)) throw new Exception("Temporary copy failed: " + tempPath);
        GameObject rawModel = null;
        try
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(tempPath);
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
            importer.sourceAvatar = null;
            importer.SaveAndReimport();
            var rawClip = AssetDatabase.LoadAllAssetsAtPath(tempPath).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
            rawModel = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(tempPath));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(rawModel, scene);
            var rawNeck = rawModel.GetComponentsInChildren<Transform>(true).First(t => t.name == "mixamorig:Neck");
            var rawHead = rawModel.GetComponentsInChildren<Transform>(true).First(t => t.name == "mixamorig:Head");
            var rawChest = rawModel.GetComponentsInChildren<Transform>(true).First(t => t.name == "mixamorig:Spine2");
            int halfFrames = Mathf.RoundToInt(clip.length * clip.frameRate * 2f);
            float maxNeckError = 0f, maxHeadError = 0f, maxCombinedError = 0f, maxCombinedAngle = 0f, maxKeyframeError = 0f;
            var samples = new List<object>();
            for (int sample = 0; sample <= halfFrames; sample++)
            {
                float time = Mathf.Min(clip.length, sample / (clip.frameRate * 2f));
                // 실제 테스트 씬의 Animator 상태를 60Hz 간격으로 재생해 원본 본 곡선과 비교한다.
                animator.Play("ATTACK" + attack, 0, time / clip.length);
                animator.Update(0f);
                if (!animator.GetCurrentAnimatorStateInfo(0).IsName("ATTACK" + attack)) throw new Exception("Animator state mismatch");
                rawClip.SampleAnimation(rawModel, time);
                float neckError = RotationError(neck.localRotation, rawNeck.localRotation);
                float headError = RotationError(head.localRotation, rawHead.localRotation);
                var combined = Quaternion.Inverse(chest.rotation) * head.rotation;
                var rawCombined = Quaternion.Inverse(rawChest.rotation) * rawHead.rotation;
                float combinedError = RotationError(combined, rawCombined);
                float angle = Quaternion.Angle(Quaternion.identity, combined);
                maxNeckError = Mathf.Max(maxNeckError, neckError);
                maxHeadError = Mathf.Max(maxHeadError, headError);
                maxCombinedError = Mathf.Max(maxCombinedError, combinedError);
                maxCombinedAngle = Mathf.Max(maxCombinedAngle, angle);
                if (sample % 2 == 0) maxKeyframeError = Mathf.Max(maxKeyframeError, Mathf.Max(neckError, Mathf.Max(headError, combinedError)));
                samples.Add(new { frame = sample * 0.5f, neckError, headError, combinedError, headRelativeChestAngle = angle });
            }
            // 원본 키 시점은 0.01도 이내로 일치해야 하며, 서로 다른 곡선 보간을 쓰는 중간 시점은 0.3도 이내로 확인한다.
            bool passed = maxKeyframeError < 0.01f && maxNeckError < 0.3f && maxHeadError < 0.3f && maxCombinedError < 0.3f;
            reports.Add(new { attack, clip = clip.name, sampleCount = samples.Count, sampleRate = 60, maxKeyframeError, maxNeckError, maxHeadError, maxCombinedError, maxCombinedAngle, passed, samples });
        }
        finally
        {
            if (rawModel != null) UnityEngine.Object.DestroyImmediate(rawModel);
            AssetDatabase.DeleteAsset(tempPath);
        }
    }
}
finally
{
    UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
    AssetDatabase.DeleteAsset(tempRoot);
}
System.IO.File.WriteAllText("Assets/Temp/AnimationDiagnosis~/Test_AfterApplyVerification.json", Newtonsoft.Json.JsonConvert.SerializeObject(reports, Newtonsoft.Json.Formatting.Indented));
return reports.Select(r => Newtonsoft.Json.Linq.JObject.FromObject(r)).Select(r => new {attack=(int)r["attack"], sampleCount=(int)r["sampleCount"], maxKeyframeError=(float)r["maxKeyframeError"], maxNeckError=(float)r["maxNeckError"], maxHeadError=(float)r["maxHeadError"], maxCombinedError=(float)r["maxCombinedError"], maxCombinedAngle=(float)r["maxCombinedAngle"], passed=(bool)r["passed"]}).ToArray();
