if (Application.isPlaying) throw new Exception("Play Mode를 종료해 주세요.");
const string root = "Assets/Test/CheonMountainMap/";
var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if (scene.path != root + "Scenes/Test_CheonMountainMap.unity") throw new Exception("마운틴 씬을 열어 주세요.");
var movement = UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.MountainMap.Test_MountainPlayerMovement>();
var control = movement.AnimationControl;
const string settingsPath = root + "Settings/Test_MountainPlayerPlayback.asset";
var settings = AssetDatabase.LoadAssetAtPath<Cheon.Testing.PlayerFan.Test_PlayerPlaybackSettings>(settingsPath);
if (settings == null)
{
    settings = ScriptableObject.CreateInstance<Cheon.Testing.PlayerFan.Test_PlayerPlaybackSettings>();
    control.CopyPlaybackTo(settings);
    AssetDatabase.CreateAsset(settings, settingsPath);
}
control.ConfigurePlaybackSettings(settings);
EditorUtility.SetDirty(control);
var movementData = new SerializedObject(movement);
movementData.FindProperty("dodgeDistance").floatValue = 7.5f;
movementData.ApplyModifiedPropertiesWithoutUndo();
EditorUtility.SetDirty(movement);
var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(root + "Animations/Test_MountainPlayer.controller");
if (!controller.parameters.Any(p => p.name == "LocomotionTime"))
    controller.AddParameter(new AnimatorControllerParameter { name = "LocomotionTime", type = AnimatorControllerParameterType.Float, defaultFloat = 0f });
foreach (var state in controller.layers[0].stateMachine.states.Select(s => s.state).Where(s => s.name == "SHEATHED_IDLE" || s.name == "DRAWN_IDLE"))
{
    state.timeParameter = "LocomotionTime";
    state.timeParameterActive = true;
    state.speedParameterActive = false;
    EditorUtility.SetDirty(state);
}
EditorUtility.SetDirty(controller);
var font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(root + "Arts/Fonts/Test_MountainPlayerKorean.asset");
var textSettings = AssetDatabase.LoadAssetAtPath<TMPro.TMP_Settings>(root + "Settings/Test_MountainTextSettings.asset");
typeof(TMPro.TMP_Settings).GetField("s_Instance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).SetValue(null, textSettings);
string source = string.Join("", System.IO.Directory.GetFiles(root + "Scripts", "*.cs").Select(System.IO.File.ReadAllText));
uint[] missing = source.Where(c => !char.IsControl(c) && c != '\uFEFF').Select(c => (uint)c).Distinct().Where(c => !font.HasCharacter((int)c)).ToArray();
font.atlasPopulationMode = TMPro.AtlasPopulationMode.Dynamic;
if (missing.Length > 0) font.TryAddCharacters(missing, out var unavailable);
if (missing.Any(c => !font.HasCharacter((int)c))) throw new Exception("재생 조절 패널의 한글 글자를 추가하지 못했습니다.");
font.atlasPopulationMode = TMPro.AtlasPopulationMode.Static;
EditorUtility.SetDirty(font);
foreach (var texture in font.atlasTextures) EditorUtility.SetDirty(texture);
var ui = UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.MountainMap.Test_PlayerPlaybackUI>();
if (ui == null) ui = new GameObject("Test_PlayerPlaybackUI").AddComponent<Cheon.Testing.MountainMap.Test_PlayerPlaybackUI>();
ui.Configure(control, font, textSettings);
EditorUtility.SetDirty(ui);
AssetDatabase.SaveAssets();
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
Selection.activeGameObject = ui.gameObject;
System.IO.File.WriteAllText("Assets/Temp/MountainPlaybackSettings~/Test_OriginalPlaybackSettings.json", JsonUtility.ToJson(settings));
return new { scene = scene.path, settings = settingsPath, values = JsonUtility.ToJson(settings), dodgeDistance = movementData.FindProperty("dodgeDistance").floatValue };
