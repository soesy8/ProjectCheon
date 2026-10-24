var movement = UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.MountainMap.Test_MountainPlayerMovement>();
var control = movement.AnimationControl;
var original = control.CharacterAnimator;
var preview = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
var actor = UnityEngine.Object.Instantiate(original.gameObject);
UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(actor, preview);
actor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
actor.transform.localScale = original.transform.lossyScale;
var animator = actor.GetComponent<Animator>();
var clip = (AnimationClip)new SerializedObject(control).FindProperty("run2Clip").objectReferenceValue;
var samples = new List<object>();
try
{
    animator.applyRootMotion = false;
    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
    animator.Rebind();
    animator.Update(0f);
    if (animator.layerCount > 1) animator.SetLayerWeight(1, 0f);
    var feet = actor.GetComponentsInChildren<Transform>().Where(t => t.name.EndsWith("LeftFoot") || t.name.EndsWith("RightFoot") || t.name.EndsWith("LeftToeBase") || t.name.EndsWith("RightToeBase")).ToArray();
    var positions = new List<Vector3[]>();
    const int count = 168;
    for (int i = 0; i <= count; i++)
    {
        animator.Play("RUN2", 0, i / (float)count);
        animator.Update(0f);
        positions.Add(feet.Select(t => t.position).ToArray());
    }
    for (int foot = 0; foot < feet.Length; foot++)
    {
        float low = positions.Min(p => p[foot].y);
        var groundSpeeds = new List<float>();
        for (int i = 1; i < positions.Count; i++)
        {
            var a = positions[i - 1][foot];
            var b = positions[i][foot];
            float speed = -(b.z - a.z) / (clip.length / count);
            if (Mathf.Max(a.y, b.y) < low + 0.09f && speed > 0f) groundSpeeds.Add(speed);
        }
        groundSpeeds.Sort();
        samples.Add(new { foot = feet[foot].name, low, contacts = groundSpeeds.Count,
            medianBackwardSpeed = groundSpeeds.Count > 0 ? groundSpeeds[groundSpeeds.Count / 2] : 0f,
            minimum = groundSpeeds.Count > 0 ? groundSpeeds.First() : 0f,
            maximum = groundSpeeds.Count > 0 ? groundSpeeds.Last() : 0f });
    }
}
finally
{
    UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(preview);
}
var fields = new SerializedObject(movement);
var report = new { clip = clip.name, duration = clip.length, frameRate = clip.frameRate, samples,
    moveSpeed = fields.FindProperty("moveSpeed").floatValue, sprintSpeed = fields.FindProperty("sprintSpeed").floatValue };
System.IO.File.WriteAllText("Assets/Temp/LocomotionSpeedTuning~/Test_FootContactReport.json", Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented));
return report;
