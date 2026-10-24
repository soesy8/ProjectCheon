using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Cheon.Testing.MountainMap;
using Cheon.Testing.PlayerFan;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class Test_MountainPlayerVerification
{
    private static Key[] held = Array.Empty<Key>();
    private static readonly List<string> passed = new List<string>();

    public static async Task<object> Main()
    {
        passed.Clear();
        var movement = UnityEngine.Object.FindFirstObjectByType<Test_MountainPlayerMovement>();
        var control = movement.AnimationControl;
        var animator = control.CharacterAnimator;
        var profile = control.RuntimeTimings;
        string baseline = JsonUtility.ToJson(profile);
        string savedBaseline = JsonUtility.ToJson(control.SavedTimings);
        var oldKeyboard = Keyboard.current;
        var background = InputSystem.settings.backgroundBehavior;
        var keyboard = InputSystem.AddDevice<Keyboard>("Test_MountainKeyboard");
        Action feed = () => { keyboard.MakeCurrent(); InputSystem.QueueStateEvent(keyboard, new KeyboardState(held)); };
        try
        {
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.onBeforeUpdate += feed;
            held = Array.Empty<Key>();
            if (control.IsCombatRequested) control.ExitCombat();
            control.PlayIdle();
            movement.ResetToSpawn();
            await Frames(3);
            Check(control.UsesUpperBodyEquipment, "맵 전용 상체 레이어 연결");
            Check(UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length == 1, "EventSystem 한 개");

            await Press(Key.Digit1);
            Check(!control.IsCombatRequested && control.SelectedState == "SHEATHED_IDLE", "납도 상태 공격 차단");
            Vector3 start = movement.transform.position;
            held = new[] { Key.W };
            await Advance(0.22f);
            Check(Vector3.Distance(start, movement.transform.position) > 0.25f,
                $"WASD 지형 이동 (거리 {Vector3.Distance(start, movement.transform.position):0.000}, 이동 {movement.IsMoving}, 입력 {keyboard.wKey.isPressed}, 현재 {Keyboard.current.name})");
            held = new[] { Key.W, Key.F };
            await Frames(3);
            Check(control.SelectedState == "DRAW" && control.CanMove && movement.IsMoving, "이동 중 F 발도 시작");
            Check(animator.GetCurrentAnimatorStateInfo(0).IsName("DRAWN_IDLE") || animator.GetNextAnimatorStateInfo(0).IsName("DRAWN_IDLE"), "하체 이동 BlendTree 유지");
            held = new[] { Key.W };
            Vector3 beforeDraw = movement.transform.position;
            await Advance(0.35f);
            Check(control.SelectedState == "DRAW" && Vector3.Distance(beforeDraw, movement.transform.position) > 0.5f, "상체 발도 중 연속 이동");
            Check(animator.GetLayerWeight(1) > 0.95f, "발도 상체 레이어 가중치");
            float walkTimeBeforeDrawEnd = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            await Until(() => control.SelectedState == "DRAWN_IDLE", 3000);
            Check(animator.GetCurrentAnimatorStateInfo(0).normalizedTime > walkTimeBeforeDrawEnd, "발도 완료 시 보행 주기 유지");
            held = new[] { Key.W, Key.F };
            await Frames(3);
            Check(control.SelectedState == "SHEATHE" && control.CanMove && movement.IsMoving, "이동 중 F 납도 시작");
            held = new[] { Key.W };
            Vector3 beforeSheathe = movement.transform.position;
            await Advance(0.30f);
            Check(control.SelectedState == "SHEATHE" && Vector3.Distance(beforeSheathe, movement.transform.position) > 0.45f, "상체 납도 중 연속 이동");
            float walkTimeBeforeSheatheEnd = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            held = Array.Empty<Key>();
            await Until(() => control.SelectedState == "SHEATHED_IDLE", 3000);
            Check(animator.GetCurrentAnimatorStateInfo(0).normalizedTime > walkTimeBeforeSheatheEnd, "납도 완료 시 보행 주기 유지");
            await Advance(0.22f);
            Check(animator.GetLayerWeight(1) < 0.01f, "발도·납도 종료 시 상체 영향 해제");
            var fan = movement.GetComponentInChildren<Test_PlayerFanAttachment>();
            Check(!fan.IsInHand && fan.OpeningAmount < 0.01f, "납도 후 부채 수납");
            movement.ResetToSpawn();

            await Press(Key.F);
            await Until(() => control.SelectedState == "DRAWN_IDLE", 3000);
            Check(fan.IsInHand && fan.OpeningAmount > 0.99f, "발도 후 부채 손 장착");
            await Advance(0.25f);

            // 같은 이동 프레임에서 상체 레이어의 유무만 바꿔 하체 본이 보존되는지 비교한다.
            movement.enabled = false;
            animator.SetFloat("MoveAmount", 1f);
            animator.Play("DRAWN_IDLE", 0, 0.31f);
            animator.Play("DRAW", 1, 0.5f);
            animator.SetLayerWeight(1, 0f);
            animator.Update(0f);
            var leftLeg = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            var rightLeg = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            var rightArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            Quaternion leftBefore = leftLeg.localRotation, rightBefore = rightLeg.localRotation, armBefore = rightArm.localRotation;
            animator.SetLayerWeight(1, 1f);
            animator.Update(0f);
            float legDifference = Mathf.Max(Quaternion.Angle(leftBefore, leftLeg.localRotation), Quaternion.Angle(rightBefore, rightLeg.localRotation));
            float armDifference = Quaternion.Angle(armBefore, rightArm.localRotation);
            Check(legDifference < 0.2f && armDifference > 1f, $"AvatarMask 분리 (하체 차이 {legDifference:0.000}도 / 팔 차이 {armDifference:0.0}도)");
            animator.SetLayerWeight(1, 0f);
            movement.enabled = true;
            control.PlayIdle();
            Snap(control, 0f);

            var attack1 = profile.Get("ATTACK1");
            attack1.moveCancel = 0.8f;
            await Press(Key.Digit1);
            Check(control.SelectedState == "ATTACK1", "숫자키 개별 공격");
            Snap(control, 0.2f);
            animator.speed = 0f;
            Vector3 beforeLockedMove = movement.transform.position;
            held = new[] { Key.W };
            await Frames(5);
            Check(!movement.IsMoving && Vector3.ProjectOnPlane(movement.transform.position - beforeLockedMove, Vector3.up).magnitude < 0.02f, "공격 초기 이동 잠금");
            Snap(control, 0.85f);
            await Frames(5);
            Check(movement.IsMoving && control.SelectedState == "DRAWN_IDLE", "설정 시점 이후 공격 이동 캔슬");
            held = Array.Empty<Key>();
            animator.speed = 1f;
            await Frames(3);

            for (int source = 1; source <= 3; source++)
            {
                for (int destination = 1; destination <= 3; destination++)
                {
                    var timing = profile.Get("ATTACK" + source);
                    timing.attack1Cancel = timing.attack2Cancel = timing.attack3Cancel = 0.6f;
                    control.PlayIdle();
                    Snap(control, 0f);
                    Check(control.TryPlayAttack(source), $"공격 {source} 시작 ({destination})");
                    Snap(control, 0.2f);
                    Check(!control.TryPlayAttack(destination), $"공격 {source}→{destination} 조기 전환 차단");
                    Snap(control, 0.7f);
                    Check(control.TryPlayAttack(destination) && control.SelectedState == "ATTACK" + destination, $"공격 {source}→{destination} 허용 시점 전환");
                }
            }

            control.PlayIdle();
            Snap(control, 0f);
            control.TryPlayAttack(1);
            Snap(control, 0.2f);
            Check(!control.TryToggleEquipment(), "공격 중 조기 납도 차단");
            Snap(control, 0.95f);
            Check(control.TryToggleEquipment() && control.SelectedState == "SHEATHE", "공격 후 납도 허용");
            await Until(() => control.SelectedState == "SHEATHED_IDLE", 3000);
            await Press(Key.F);
            await Until(() => control.SelectedState == "DRAWN_IDLE", 3000);

            control.TryPlayAttack(1);
            Snap(control, 0.57f);
            await Press(Key.Digit2);
            await Until(() => control.SelectedState == "ATTACK2", 500);
            Check(true, "허용 시점 직전 공격 입력 예약 실행");
            control.PlayIdle();
            Snap(control, 0f);
            control.TryPlayAttack(1);
            profile.Get("ATTACK1").attack2Cancel = 0.95f;
            Snap(control, 0.1f);
            await Press(Key.Digit2);
            await Advance(0.35f);
            Snap(control, 0.96f);
            await Frames(3);
            Check(control.SelectedState != "ATTACK2", "만료된 조기 공격 입력 제거");

            control.PlayHit();
            await Until(() => control.SelectedState == "DRAWN_IDLE", 3000);
            Check(true, "피격 모션 완료 후 자동 복귀");

            var slider = GameObject.Find("Test_TimingSlider_0").GetComponent<UnityEngine.UI.Slider>();
            var input = GameObject.Find("Test_TimingInput_0").GetComponent<TMP_InputField>();
            slider.value = 0.42f;
            Check(Mathf.Abs(attack1.moveCancel - 0.42f) < 0.001f, "UI 슬라이더 즉시 적용");
            input.text = "37";
            input.onEndEdit.Invoke("37");
            Check(Mathf.Abs(attack1.moveCancel - 0.37f) < 0.001f, "UI 숫자 입력 적용");
            EventSystem.current.SetSelectedGameObject(input.gameObject);
            input.ActivateInputField();
            await Frames(3);
            Check(input.isFocused, "숫자 입력 필드 포커스");
            Vector3 beforeTyping = movement.transform.position;
            held = new[] { Key.W, Key.F, Key.Digit2 };
            await Frames(4);
            Check(control.SelectedState == "DRAWN_IDLE" && !movement.IsMoving && Vector3.ProjectOnPlane(movement.transform.position - beforeTyping, Vector3.up).magnitude < 0.02f, "숫자 편집 중 게임 단축키 차단");
            held = Array.Empty<Key>();
            input.DeactivateInputField();
            EventSystem.current.SetSelectedGameObject(null);
            await Frames(3);

            GameObject.Find("Test_SaveTimings").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Check(Mathf.Abs(control.SavedTimings.Get("ATTACK1").moveCancel - 0.37f) < 0.001f, "전환 설정 에셋 저장");
            slider.value = 0.2f;
            GameObject.Find("Test_ReloadTimings").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Check(Mathf.Abs(profile.Get("ATTACK1").moveCancel - 0.37f) < 0.001f, "저장값 불러오기");

            var timingUi = UnityEngine.Object.FindFirstObjectByType<Test_PlayerActionTimingUI>();
            var content = timingUi.GetComponentsInChildren<Transform>(true).First(t => t.name == "Test_Content").gameObject;
            await Press(Key.Tab);
            Check(!content.activeSelf, "Tab 타이밍 패널 접기");
            await Press(Key.Tab);
            Check(content.activeSelf, "Tab 타이밍 패널 펼치기");
            var guide = UnityEngine.Object.FindFirstObjectByType<Test_PlayerControlGuide>();
            var details = guide.GetComponentsInChildren<Transform>(true).First(t => t.name == "Test_Details").gameObject;
            await Press(Key.H);
            Check(!details.activeSelf, "H 조작 안내 접기");
            await Press(Key.H);
            Check(details.activeSelf, "H 조작 안내 펼치기");

            return new { success = true, checks = passed.Count, passed = passed.ToArray() };
        }
        catch (Exception exception)
        {
            return new { success = false, checks = passed.Count, passed = passed.ToArray(), error = exception.ToString() };
        }
        finally
        {
            held = Array.Empty<Key>();
            InputSystem.onBeforeUpdate -= feed;
            InputSystem.RemoveDevice(keyboard);
            if (oldKeyboard != null && oldKeyboard.added) oldKeyboard.MakeCurrent();
            InputSystem.settings.backgroundBehavior = background;
            animator.speed = 1f;
            animator.SetLayerWeight(1, 0f);
            movement.enabled = true;
            JsonUtility.FromJsonOverwrite(baseline, profile);
            JsonUtility.FromJsonOverwrite(savedBaseline, control.SavedTimings);
            EditorUtility.SetDirty(control.SavedTimings);
            AssetDatabase.SaveAssetIfDirty(control.SavedTimings);
            EventSystem.current.SetSelectedGameObject(null);
            control.PlayIdle();
            movement.ResetToSpawn();
        }
    }

    private static void Snap(Test_PlayerFanAnimationController control, float progress)
    {
        int layer = control.IsEquipmentTransition ? 1 : 0;
        float normalized = control.SelectedState == "SHEATHE" ? 1f - progress : progress;
        if (control.SelectedState.StartsWith("ATTACK"))
        {
            int index = int.Parse(control.SelectedState.Substring(6));
            float frame = Mathf.Lerp(control.GetAttackStartFrame(index), control.GetAttackEndFrame(index), progress);
            normalized = frame / control.GetAttackMaximumFrame(index);
        }
        control.CharacterAnimator.Play(control.SelectedState, layer, normalized);
        control.CharacterAnimator.Update(0f);
    }

    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
        passed.Add(label);
    }

    private static async Task Press(Key key)
    {
        held = new[] { key };
        await Frames(3);
        held = Array.Empty<Key>();
        await Frames(3);
    }

    private static async Task Frames(int count)
    {
        int target = Time.frameCount + count;
        var timer = Stopwatch.StartNew();
        while (Time.frameCount < target)
        {
            if (timer.ElapsedMilliseconds > 3000) throw new TimeoutException("프레임 진행 정지");
            await Task.Delay(15);
        }
    }

    private static async Task Until(Func<bool> condition, int milliseconds)
    {
        var timer = Stopwatch.StartNew();
        while (!condition())
        {
            if (timer.ElapsedMilliseconds > milliseconds) throw new TimeoutException("액션 완료 대기 시간 초과");
            await Task.Delay(20);
        }
    }

    private static async Task Advance(float seconds)
    {
        double target = Time.timeAsDouble + seconds;
        var timer = Stopwatch.StartNew();
        while (Time.timeAsDouble < target)
        {
            if (timer.ElapsedMilliseconds > 10000) throw new TimeoutException("게임 시간 진행 대기 시간 초과");
            await Task.Delay(15);
        }
    }
}
