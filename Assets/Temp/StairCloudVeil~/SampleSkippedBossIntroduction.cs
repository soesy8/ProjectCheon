var game = UnityEngine.Object.FindFirstObjectByType<Cheon.Prototype.MountainPass.PassGameFlow>();
var boss = (Cheon.Prototype.MountainPass.PassBossPatternController)game.Boss;
var visual = (Transform)new SerializedObject(boss).FindProperty("visual").objectReferenceValue;
var entrance = game.Entrance;
entrance.Director.time = 3.5;
entrance.Director.Evaluate();
Vector3 before = visual.localEulerAngles;
float tiltBefore = Vector3.Angle(visual.up, Vector3.up);
entrance.SkipPresentation();
return new { beforeAngles = before.ToString("F3"), tiltBefore, afterStopAngles = visual.localEulerAngles.ToString("F3"),
    tiltAfterStop = Vector3.Angle(visual.up, Vector3.up), directorPlaying = entrance.IsPresenting, state = game.State.ToString() };
