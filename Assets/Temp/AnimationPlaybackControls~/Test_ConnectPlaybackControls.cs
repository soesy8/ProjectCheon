if (Application.isPlaying) throw new Exception("Play Mode를 종료해 주세요.");
const string root = "Assets/Test/PlayerFanAnimation/";
var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if (scene.path != root + "Scenes/Test_PlayerFanAnimation.unity") throw new Exception("애니메이션 테스트 씬을 먼저 열어 주세요.");
var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(root + "Animations/Test_Player.controller");
var states = controller.layers[0].stateMachine.states.Select(s => s.state).ToArray();
foreach (var item in new[] { (state: "ROLL", parameter: "RollSpeed"), (state: "RUN2", parameter: "Run2Speed") })
{
    if (!controller.parameters.Any(p => p.name == item.parameter))
        controller.AddParameter(new AnimatorControllerParameter { name = item.parameter, type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
    var state = states.First(s => s.name == item.state);
    state.speedParameter = item.parameter;
    state.speedParameterActive = true;
    EditorUtility.SetDirty(state);
}
EditorUtility.SetDirty(controller);
var control = UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.PlayerFan.Test_PlayerFanAnimationController>();
var serialized = new SerializedObject(control);
foreach (var item in new[] { (field: "roll", name: "Roll"), (field: "run2", name: "Run2") })
{
    var clip = AssetDatabase.LoadAllAssetsAtPath(root + "Animations/Test_" + item.name + ".fbx").OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
    serialized.FindProperty(item.field + "Clip").objectReferenceValue = clip;
}
serialized.ApplyModifiedPropertiesWithoutUndo();
EditorUtility.SetDirty(control);
typeof(Cheon.Testing.PlayerFan.Test_PlayerFanSceneBuilder).GetMethod("RefreshTestFont", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).Invoke(null, null);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
AssetDatabase.SaveAssets();
return new { control.rollSpeed, control.rollStartFrame, control.rollEndFrame, control.run2Speed, control.run2StartFrame, control.run2EndFrame, states = states.Where(s => s.name == "ROLL" || s.name == "RUN2").Select(s => new { s.name, s.speedParameter, s.speedParameterActive }).ToArray() };
