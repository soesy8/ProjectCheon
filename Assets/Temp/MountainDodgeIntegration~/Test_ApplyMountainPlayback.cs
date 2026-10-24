if (Application.isPlaying) throw new Exception("Play Mode를 종료해 주세요.");
const string root = "Assets/Test/PlayerFanAnimation/";
const string mapRoot = "Assets/Test/CheonMountainMap/";
const string oldRunPath = root + "Animations/Test_Run.fbx";
var values = Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText("Assets/Temp/MountainDodgeIntegration~/Test_CurrentPlaybackValues.json"));
AnimationClip Clip(string name) => AssetDatabase.LoadAllAssetsAtPath(root + "Animations/Test_" + name + ".fbx").OfType<AnimationClip>().First(c => c.name == name);
var runClip = Clip("Run2");
var rollClip = Clip("Roll");
var oldRun = AssetDatabase.LoadAssetAtPath<GameObject>(oldRunPath);
if (oldRun != null)
{
    System.IO.File.Copy(oldRunPath, "Assets/Temp/MountainDodgeIntegration~/Test_PreviousRun.fbx", true);
    System.IO.File.Copy(oldRunPath + ".meta", "Assets/Temp/MountainDodgeIntegration~/Test_PreviousRun.fbx.meta", true);
}
var sourceScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if (sourceScene.isDirty) UnityEditor.SceneManagement.EditorSceneManager.SaveScene(sourceScene);
var map = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(mapRoot + "Scenes/Test_CheonMountainMap.unity");
var control = UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.PlayerFan.Test_PlayerFanAnimationController>();
if (control == null || control.SavedTimings == null) throw new Exception("마운틴 플레이어와 설정 연결을 확인하세요.");
var serialized = new SerializedObject(control);
foreach (var property in values.Properties())
{
    var field = serialized.FindProperty(property.Name);
    if (field == null) throw new Exception("수치 필드를 찾을 수 없습니다: " + property.Name);
    if (field.propertyType == SerializedPropertyType.Integer) field.intValue = property.Value.ToObject<int>();
    else field.floatValue = property.Value.ToObject<float>();
}
foreach (var item in new[] { (field: "attack1Clip", name: "Attack1"), (field: "attack2Clip", name: "Attack2"), (field: "attack3Clip", name: "Attack3"), (field: "rollClip", name: "Roll"), (field: "run2Clip", name: "Run2") })
    serialized.FindProperty(item.field).objectReferenceValue = Clip(item.name);
serialized.ApplyModifiedPropertiesWithoutUndo();
EditorUtility.SetDirty(control);

var profile = control.SavedTimings;
var profileData = new SerializedObject(profile);
var actions = profileData.FindProperty("actions");
for (int i = 0; i < actions.arraySize; i++)
{
    var action = actions.GetArrayElementAtIndex(i);
    action.FindPropertyRelative("dodgeCancel").floatValue = action.FindPropertyRelative("moveCancel").floatValue;
}
if (profile.Get("ROLL") == null)
{
    int index = actions.arraySize;
    actions.arraySize++;
    var action = actions.GetArrayElementAtIndex(index);
    action.FindPropertyRelative("state").stringValue = "ROLL";
    foreach (var name in new[] { "moveCancel", "dodgeCancel", "attack1Cancel", "attack2Cancel", "attack3Cancel", "equipmentCancel", "autoReturn" })
        action.FindPropertyRelative(name).floatValue = 1f;
    action.FindPropertyRelative("blendDuration").floatValue = 0.12f;
}
profileData.ApplyModifiedPropertiesWithoutUndo();
profile.Validate();
EditorUtility.SetDirty(profile);

var controllerPaths = new[] { root + "Animations/Test_Player.controller", mapRoot + "Animations/Test_MountainPlayer.controller" };
foreach (string path in controllerPaths)
{
    var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(path);
    foreach (var tree in AssetDatabase.LoadAllAssetsAtPath(path).OfType<UnityEditor.Animations.BlendTree>())
    {
        var children = tree.children;
        bool changed = false;
        for (int i = 0; i < children.Length; i++)
        {
            if (AssetDatabase.GetAssetPath(children[i].motion) != oldRunPath) continue;
            children[i].motion = runClip;
            changed = true;
        }
        if (changed) { tree.children = children; EditorUtility.SetDirty(tree); }
    }
    var machine = controller.layers[0].stateMachine;
    foreach (var item in new[] { (state: "ROLL", parameter: "RollSpeed", clip: rollClip), (state: "RUN2", parameter: "Run2Speed", clip: runClip) })
    {
        if (!controller.parameters.Any(p => p.name == item.parameter))
            controller.AddParameter(new AnimatorControllerParameter { name = item.parameter, type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
        var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == item.state);
        if (state == null) state = machine.AddState(item.state);
        state.motion = item.clip;
        state.speedParameter = item.parameter;
        state.speedParameterActive = true;
        EditorUtility.SetDirty(state);
    }
    if (path.Contains("CheonMountainMap"))
    {
        if (!controller.parameters.Any(p => p.name == "LocomotionSpeed"))
            controller.AddParameter(new AnimatorControllerParameter { name = "LocomotionSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
        foreach (var state in machine.states.Select(s => s.state).Where(s => s.name == "SHEATHED_IDLE" || s.name == "DRAWN_IDLE"))
        {
            state.speedParameter = "LocomotionSpeed";
            state.speedParameterActive = true;
            EditorUtility.SetDirty(state);
        }
    }
    EditorUtility.SetDirty(controller);
}
control.ConfigureMapPlayback(profile, true);
typeof(Cheon.Testing.PlayerFan.Test_PlayerFanSceneBuilder).GetMethod("RefreshTestFont", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).Invoke(null, null);
var font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(mapRoot + "Arts/Fonts/Test_MountainPlayerKorean.asset");
var textSettings = AssetDatabase.LoadAssetAtPath<TMPro.TMP_Settings>(mapRoot + "Settings/Test_MountainTextSettings.asset");
typeof(TMPro.TMP_Settings).GetField("s_Instance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).SetValue(null, textSettings);
string text = string.Join("", System.IO.Directory.GetFiles(mapRoot + "Scripts", "*.cs").Select(System.IO.File.ReadAllText)) + System.IO.File.ReadAllText(root + "Scripts/Test_PlayerActionTimings.cs");
uint[] missing = text.Where(c => !char.IsControl(c) && c != '\uFEFF').Select(c => (uint)c).Distinct().Where(c => !font.HasCharacter((int)c)).ToArray();
font.atlasPopulationMode = TMPro.AtlasPopulationMode.Dynamic;
if (missing.Length > 0) font.TryAddCharacters(missing, out var unavailable);
if (missing.Any(c => !font.HasCharacter((int)c))) throw new Exception("마운틴 UI의 한글 폰트 등록에 실패했습니다.");
font.atlasPopulationMode = TMPro.AtlasPopulationMode.Static;
EditorUtility.SetDirty(font);
foreach (var texture in font.atlasTextures) EditorUtility.SetDirty(texture);
AssetDatabase.SaveAssets();
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(map);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(map);
if (AssetDatabase.GetDependencies(controllerPaths, true).Contains(oldRunPath)) throw new Exception("기존 달리기 참조가 남아 있습니다.");
if (oldRun != null && !AssetDatabase.DeleteAsset(oldRunPath)) throw new Exception("기존 달리기를 제거하지 못했습니다.");
AssetDatabase.SaveAssets();
Selection.activeGameObject = UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.MountainMap.Test_PlayerActionTimingUI>().gameObject;
return new { scene = map.path, copied = values, profile = EditorJsonUtility.ToJson(profile), oldRunRemoved = AssetDatabase.LoadAssetAtPath<GameObject>(oldRunPath) == null,
    controller = AssetDatabase.GetAssetPath(control.CharacterAnimator.runtimeAnimatorController) };
