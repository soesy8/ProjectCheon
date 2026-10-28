var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Arts/Animations/MountainPass/BossAwakening.anim");
var bindings = AnimationUtility.GetCurveBindings(clip);
var rotations = new[] { "x", "y", "z", "w" }.Select(axis => AnimationUtility.GetEditorCurve(clip,
    bindings.First(b => b.path == "Visual" && b.propertyName == "m_LocalRotation." + axis))).ToArray();
return new { samples = new[] { 0f, 1f, 2f, 2.9f, 3.5f, 4.5f, 5.8f }.Select(t => {
    var rotation = new Quaternion(rotations[0].Evaluate(t), rotations[1].Evaluate(t), rotations[2].Evaluate(t), rotations[3].Evaluate(t));
    return new { time = t, angles = rotation.eulerAngles.ToString("F3"), tilt = Vector3.Angle(rotation * Vector3.up, Vector3.up) };
}).ToArray() };
