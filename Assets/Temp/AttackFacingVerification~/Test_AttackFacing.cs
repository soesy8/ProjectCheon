var flow = UnityEngine.Object.FindFirstObjectByType<Cheon.Prototype.MountainPass.PassGameFlow>();
var player = UnityEngine.Object.FindFirstObjectByType<Cheon.Prototype.MountainPass.PassPlayerMovement>();
var combat = player.GetComponent<Cheon.Prototype.MountainPass.PassPlayerCombat>();
var animation = player.AnimationControl;
var animator = animation.CharacterAnimator;
var camera = flow.View;
var boss = (Cheon.Prototype.MountainPass.PassBossPatternController)flow.Boss;
var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
var inputField = player.GetType().GetField("inputDirection", flags);
var lateUpdate = combat.GetType().GetMethod("LateUpdate", flags);
var settings = (Cheon.Prototype.MountainPass.PassBossPatternSettings)boss.GetType().GetField("settings", flags).GetValue(boss);
var results = new System.Collections.Generic.List<object>();
var spawned = new System.Collections.Generic.List<UnityEngine.GameObject>();
var savedBossPosition = boss.transform.position;
var savedBossRotation = boss.transform.rotation;

void Input(UnityEngine.Vector3 direction) => inputField.SetValue(player, direction);
void Check(string name, float expected)
{
    float actual = Mathf.DeltaAngle(0, player.transform.eulerAngles.y);
    bool passed = Mathf.Abs(Mathf.DeltaAngle(expected, actual)) < 0.05f;
    results.Add(new { name, expected, actual, passed });
}
void Settle(string state)
{
    animator.Play(state, 0, animation.GetPlaybackStartFrame(state == "ATTACK1" ? 1 : state == "ATTACK2" ? 2 : 3) /
        (float)animation.GetPlaybackMaximumFrame(state == "ATTACK1" ? 1 : state == "ATTACK2" ? 2 : 3));
    animator.Update(0f);
}
void Ready(bool locked)
{
    Input(UnityEngine.Vector3.zero);
    camera.LockBoss(locked);
    animation.PlayIdle();
    animator.Play("DRAWN_IDLE", 0, 0f);
    animator.Update(0f);
    player.transform.rotation = UnityEngine.Quaternion.identity;
}
void Attack(int stage)
{
    if (!animation.TryPlayAttack(stage)) throw new System.Exception("Attack rejected: " + stage + ", state=" + animation.SelectedState);
    Settle("ATTACK" + stage);
}
Cheon.Prototype.MountainPass.PassBossPatternController Enemy(string name, float yaw, float distance)
{
    var root = UnityEngine.Object.Instantiate(boss.gameObject);
    spawned.Add(root);
    root.name = name;
    var enemy = root.GetComponent<Cheon.Prototype.MountainPass.PassBossPatternController>();
    enemy.ConfigurePattern(settings, true);
    root.transform.position = player.transform.position + UnityEngine.Quaternion.Euler(0, yaw, 0) * UnityEngine.Vector3.forward * distance;
    return enemy;
}

try
{
    flow.BeginBossChallenge();
    player.InputEnabled = true;
    player.enabled = false;
    camera.enabled = false;
    flow.enabled = false;
    for (int stage = 1; stage <= 3; stage++)
    {
        var timing = animation.RuntimeTimings.Get("ATTACK" + stage);
        timing.attack1Cancel = timing.attack2Cancel = timing.attack3Cancel = 0;
    }
    if (!animation.IsCombatRequested) animation.EnterCombat();

    Ready(false);
    Attack(1);
    Input(UnityEngine.Vector3.right);
    lateUpdate.Invoke(combat, null);
    Check("free_mid_attack_no_turn", 0);
    Attack(2);
    Check("free_right_second", 45);
    lateUpdate.Invoke(combat, null);
    Check("free_second_motion_fixed", 45);
    Attack(3);
    Check("free_right_third_cumulative", 90);

    Ready(false);
    Attack(1);
    Input(UnityEngine.Vector3.left);
    Attack(2);
    Check("free_left_second", -45);
    Attack(3);
    Check("free_left_third_cumulative", -90);

    Ready(false);
    Attack(1);
    Input(UnityEngine.Quaternion.Euler(0, 15, 0) * UnityEngine.Vector3.forward);
    Attack(2);
    Check("free_small_camera_relative_angle", 15);
    Attack(3);
    Check("free_small_angle_no_overshoot", 15);

    Ready(false);
    player.transform.rotation = UnityEngine.Quaternion.Euler(0, 23, 0);
    Attack(1);
    Attack(2);
    Check("free_no_input_preserves_facing", 23);

    Ready(false);
    Attack(1);
    Input(UnityEngine.Quaternion.Euler(0, 170, 0) * UnityEngine.Vector3.forward);
    Attack(2);
    Check("free_backward_input_clamped", 45);

    Ready(true);
    boss.transform.position = player.transform.position + UnityEngine.Quaternion.Euler(0, 20, 0) * UnityEngine.Vector3.forward * 4;
    Attack(1);
    Check("locked_default_boss_priority", 20);
    boss.transform.position = player.transform.position + UnityEngine.Vector3.right * 4;
    lateUpdate.Invoke(combat, null);
    Check("locked_mid_attack_does_not_follow_boss", 20);
    Attack(2);
    Check("locked_default_boss_next_attack_capped", 65);

    var far = Enemy("Test_DirectionalFar", 80, 6);
    var near = Enemy("Test_DirectionalNear", 30, 3);
    var dead = Enemy("Test_DirectionalDead", 15, 1);
    dead.Health.Kill();
    var inactive = Enemy("Test_DirectionalInactive", 10, 0.5f);
    inactive.gameObject.SetActive(false);
    boss.transform.position = player.transform.position + UnityEngine.Vector3.forward * 4;
    Ready(true);
    Attack(1);
    Input(UnityEngine.Quaternion.Euler(0, 60, 0) * UnityEngine.Vector3.forward);
    Attack(2);
    Check("locked_nearest_directional_enemy_ignores_dead_inactive", 30);

    near.gameObject.SetActive(false);
    far.gameObject.SetActive(false);
    Ready(true);
    Attack(1);
    Input(UnityEngine.Vector3.right);
    Attack(2);
    Check("locked_no_enemy_uses_input", 45);
    Attack(3);
    Check("locked_no_enemy_cumulative_turn", 90);

    Ready(true);
    Attack(1);
    Input(UnityEngine.Quaternion.Euler(0, 15, 0) * UnityEngine.Vector3.forward);
    boss.transform.position = player.transform.position + UnityEngine.Vector3.forward * 20;
    Attack(2);
    Check("locked_small_angle_input_without_target", 15);

    Ready(false);
    Attack(1);
    Input(UnityEngine.Vector3.right);
    bool duplicateAccepted = animation.TryPlayAttack(1);
    results.Add(new { name = "duplicate_attack_rejected", passed = !duplicateAccepted });
    Check("rejected_attack_does_not_rotate", 0);

    Ready(false);
    combat.enabled = false;
    combat.enabled = true;
    Attack(1);
    Input(UnityEngine.Quaternion.Euler(0, 170, 0) * UnityEngine.Vector3.forward);
    Attack(2);
    Check("reenabled_combat_subscribes_once", 45);
}
finally
{
    foreach (var root in spawned) if (root != null) UnityEngine.Object.DestroyImmediate(root);
    boss.transform.SetPositionAndRotation(savedBossPosition, savedBossRotation);
    Input(UnityEngine.Vector3.zero);
    animation.ReloadSavedTimings();
    player.enabled = true;
    camera.enabled = true;
    flow.enabled = true;
    flow.BeginJourney();
}
string report = Newtonsoft.Json.JsonConvert.SerializeObject(results, Newtonsoft.Json.Formatting.Indented);
System.IO.File.WriteAllText("D:/project/ProjectCheon/Assets/Temp/AttackFacingVerification~/Test_AttackFacingResults.json", report);
return results;
