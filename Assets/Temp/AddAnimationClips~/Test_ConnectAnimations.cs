if (Application.isPlaying) throw new Exception("Play Mode를 종료해 주세요.");
var root = "Assets/Test/PlayerFanAnimation/";
var targetBones = AssetDatabase.LoadAssetAtPath<GameObject>(root + "Models/Test_CheonPlayer.fbx").GetComponentsInChildren<Transform>(true).ToDictionary(t => t.name);
var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(root + "Animations/Test_Player.controller");
var machine = controller.layers[0].stateMachine;
var reports = new List<object>();
foreach (var item in new[] { (name: "Roll", state: "ROLL"), (name: "Run2", state: "RUN2") })
{
    var path = root + "Animations/Test_" + item.name + ".fbx";
    var importer = (ModelImporter)AssetImporter.GetAtPath(path);
    var description = importer.humanDescription;
    var skeleton = description.skeleton;
    int matchedBones = 0;
    // 기존 공격 애니메이션과 같은 기준 자세를 사용해 캐릭터의 목과 팔이 꺾이지 않게 한다.
    for (int i = 0; i < skeleton.Length; i++)
    {
        if (!targetBones.TryGetValue(skeleton[i].name, out var target)) continue;
        var bone = skeleton[i];
        bone.position = target.localPosition;
        bone.rotation = target.localRotation;
        bone.scale = target.localScale;
        skeleton[i] = bone;
        matchedBones++;
    }
    if (matchedBones != 33) throw new Exception("캐릭터와 대응하는 본을 확인하세요: " + path);
    description.skeleton = skeleton;
    importer.humanDescription = description;
    var settings = importer.clipAnimations;
    // 루트 모션을 사용하지 않는 테스트에서도 구를 때의 상하 움직임을 포즈에 포함한다.
    settings[0].lockRootHeightY = true;
    importer.clipAnimations = settings;
    importer.SaveAndReimport();
    var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
    var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == item.state);
    if (state == null) state = machine.AddState(item.state, new Vector3(item.state == "ROLL" ? 500f : 0f, item.state == "ROLL" ? 180f : 270f, 0f));
    state.motion = clip;
    EditorUtility.SetDirty(state);
    reports.Add(new { state = state.name, clip = clip.name, clip.length, clip.isLooping, matchedBones });
}
EditorUtility.SetDirty(controller);
typeof(Cheon.Testing.PlayerFan.Test_PlayerFanSceneBuilder).GetMethod("RefreshTestFont", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).Invoke(null, null);
AssetDatabase.SaveAssets();
var scenePath = root + "Scenes/Test_PlayerFanAnimation.unity";
var current = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if (current.path != scenePath)
{
    if (current.isDirty) throw new Exception("현재 씬의 저장되지 않은 변경을 먼저 확인하세요.");
    UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
}
var control = UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.PlayerFan.Test_PlayerFanAnimationController>();
if (control == null || control.CharacterAnimator.runtimeAnimatorController != controller) throw new Exception("테스트 씬의 Animator 연결을 확인하세요.");
Selection.activeGameObject = control.gameObject;
return reports;
