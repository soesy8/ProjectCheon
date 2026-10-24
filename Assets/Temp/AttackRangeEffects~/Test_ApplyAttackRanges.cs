
if (Application.isPlaying) throw new Exception("플레이 모드를 종료한 뒤 적용하세요.");
var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if (scene.path != "Assets/Test/CheonMountainMap/Scenes/Test_CheonMountainMap.unity")
    throw new Exception("산악 맵 씬이 아닙니다.");
var control = UnityEngine.Object.FindFirstObjectByType<Cheon.Testing.PlayerFan.Test_PlayerFanAnimationController>();
if (control == null || control.CharacterAnimator == null) throw new Exception("공격 재생 대상을 찾을 수 없습니다.");
var player = control.transform.parent;
if (player.Find("Test_AttackRangeEffects") != null) throw new Exception("범위 이펙트가 이미 있습니다.");
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, "Assets/Temp/AttackRangeEffects~/Test_SceneBefore.unity", true);
if (!AssetDatabase.IsValidFolder("Assets/Test/PlayerFanAnimation/Materials"))
    AssetDatabase.CreateFolder("Assets/Test/PlayerFanAnimation", "Materials");
const string materialPath = "Assets/Test/PlayerFanAnimation/Materials/Test_M_AttackRange.mat";
var shader = Shader.Find("Cheon/Tests/AttackRange");
if (shader == null) throw new Exception("공격 범위 셰이더를 찾을 수 없습니다.");
var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
if (material == null)
{
    material = new Material(shader) { name = "Test_M_AttackRange" };
    AssetDatabase.CreateAsset(material, materialPath);
}
Undo.IncrementCurrentGroup();
Undo.SetCurrentGroupName("Add Temporary Attack Ranges");
int group = Undo.GetCurrentGroup();
var root = new GameObject("Test_AttackRangeEffects");
Undo.RegisterCreatedObjectUndo(root, "Add Temporary Attack Ranges");
root.transform.SetParent(player, false);
var effects = Undo.AddComponent<Cheon.Testing.PlayerFan.Test_PlayerAttackRangeEffects>(root);
var definitions = new[]
{
    new { name = "Test_Attack1_3m_25deg", attack = 1, radius = 3f, angle = 25f, start = 25, end = 38, color = new Color(0.18f, 0.9f, 1f, 1f) },
    new { name = "Test_Attack2_3m_155deg", attack = 2, radius = 3f, angle = 155f, start = 40, end = 58, color = new Color(0.25f, 1f, 0.65f, 1f) },
    new { name = "Test_Attack3_3m_210deg", attack = 3, radius = 3f, angle = 210f, start = 20, end = 40, color = new Color(1f, 0.75f, 0.2f, 1f) },
    new { name = "Test_Attack3_8m_35deg", attack = 3, radius = 8f, angle = 35f, start = 20, end = 40, color = new Color(1f, 0.4f, 0.2f, 1f) }
};
var ranges = new List<Cheon.Testing.PlayerFan.Test_PlayerAttackRangeEffects.AttackRange>();
foreach (var definition in definitions)
{
    var child = new GameObject(definition.name);
    Undo.RegisterCreatedObjectUndo(child, "Add Temporary Attack Range Sector");
    child.transform.SetParent(root.transform, false);
    var sector = Undo.AddComponent<Cheon.Testing.PlayerFan.Test_AttackRangeSector>(child);
    sector.Configure(player, material, definition.radius, definition.angle, definition.color);
    EditorUtility.SetDirty(sector);
    ranges.Add(new Cheon.Testing.PlayerFan.Test_PlayerAttackRangeEffects.AttackRange
    {
        sector = sector, attack = definition.attack, startFrame = definition.start, endFrame = definition.end
    });
}
effects.Configure(control, ranges.ToArray());
EditorUtility.SetDirty(effects);
Undo.CollapseUndoOperations(group);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
AssetDatabase.SaveAssets();
Selection.activeGameObject = root;
return Newtonsoft.Json.JsonConvert.SerializeObject(new { scene = scene.path, parent = player.name, root = root.name, ranges = definitions.Select(d => new { d.name, d.attack, d.radius, d.angle, d.start, d.end }).ToArray() });

