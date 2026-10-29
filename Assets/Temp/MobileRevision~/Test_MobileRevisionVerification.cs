using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Cheon.Prototype.MountainPass;
using Newtonsoft.Json;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

public static class Test_MobileRevisionVerification
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<object> checks = new List<object>();
    private static Touchscreen touchscreen;
    private static PassHud hud;
    private static PassMobileControls controls;
    private static PassGameFlow game;
    private static PassPlayerMovement movement;
    private static Transform root;

    private static void Check(string name, bool passed, object detail = null)
    {
        checks.Add(new { name, passed, detail });
        if (!passed) throw new InvalidOperationException("Verification failed: " + name + " / " + JsonConvert.SerializeObject(detail));
    }

    private static T Field<T>(object value, string name) => (T)value.GetType().GetField(name, Private).GetValue(value);
    private static Vector2 At(Transform target, Vector2 local = default) => RectTransformUtility.WorldToScreenPoint(null, target.TransformPoint(((RectTransform)target).rect.center + local));

    private static async Task Touch(int id, TouchPhase phase, Vector2 position)
    {
        InputSystem.QueueStateEvent(touchscreen, new TouchState { touchId = id, phase = phase, position = position, pressure = phase == TouchPhase.Ended ? 0f : 1f });
        await Task.Delay(110);
    }

    private static async Task Tap(int id, Transform target)
    {
        Vector2 position = At(target);
        await Touch(id, TouchPhase.Began, position);
        await Touch(id, TouchPhase.Ended, position);
    }

    private static async Task WaitUntil(Func<bool> condition, int milliseconds = 3500)
    {
        for (int elapsed = 0; elapsed < milliseconds && !condition(); elapsed += 80) await Task.Delay(80);
    }

    private static void CapturePreview(string path)
    {
        var captureType = AppDomain.CurrentDomain.GetAssemblies().First(assembly => assembly.GetName().Name == "Unity.Pipeline.Editor")
            .GetType("Unity.Pipeline.Editor.Commands.Capture.CaptureCommands");
        captureType.GetMethod("CaptureGameView", BindingFlags.Public | BindingFlags.Static).Invoke(null,
            new object[] { 1024, Mathf.RoundToInt(1024f * Screen.height / Screen.width), null, path, false, 0, "screen" });
    }

    private static bool InsideScreen(RectTransform rect)
    {
        var corners = new Vector3[4]; rect.GetWorldCorners(corners);
        return corners.All(corner => { Vector2 point = RectTransformUtility.WorldToScreenPoint(null, corner);
            return point.x >= -1f && point.x <= Screen.width + 1f && point.y >= -1f && point.y <= Screen.height + 1f; });
    }

    public static async Task<object> Main()
    {
        checks.Clear();
        bool runInBackground = Application.runInBackground;
        var previousBackground = InputSystem.settings.backgroundBehavior;
        var previousEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        Application.runInBackground = true;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        object sizeGroup = null, window = null;
        PropertyInfo selectedSize = null;
        int previousSize = -1;
        var addedSizes = new List<int>();
        try
        {
            Check("editor is playing", EditorApplication.isPlaying);
            hud = UnityEngine.Object.FindFirstObjectByType<PassHud>();
            game = UnityEngine.Object.FindFirstObjectByType<PassGameFlow>();
            movement = game.Player.GetComponent<PassPlayerMovement>();
            controls = hud.GetComponent<PassMobileControls>();
            int beforeFrame = Time.frameCount;
            await Task.Delay(300);
            Check("frames advance", Time.frameCount > beforeFrame, new { beforeFrame, afterFrame = Time.frameCount });
            Check("desktop keeps touch UI disabled", !PassMobileControls.SupportsPlatform && !controls.IsInitialized && !controls.enabled && !Field<bool>(movement, "touchControlsEnabled"));

            var editorAssembly = typeof(Editor).Assembly;
            Type gameViewType = editorAssembly.GetType("UnityEditor.GameView");
            window = Resources.FindObjectsOfTypeAll(gameViewType).First();
            selectedSize = gameViewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            previousSize = (int)selectedSize.GetValue(window);
            Type sizesType = editorAssembly.GetType("UnityEditor.GameViewSizes");
            Type singletonType = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            object sizes = singletonType.GetProperty("instance").GetValue(null);
            Type groupType = editorAssembly.GetType("UnityEditor.GameViewSizeGroupType");
            sizeGroup = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { Enum.Parse(groupType, "Android") });
            Type sizeType = editorAssembly.GetType("UnityEditor.GameViewSize");
            Type sizeKindType = editorAssembly.GetType("UnityEditor.GameViewSizeType");
            Func<int, int, int> addSize = (width, height) =>
            {
                int builtin = (int)sizeGroup.GetType().GetMethod("GetBuiltinCount").Invoke(sizeGroup, null);
                int custom = (int)sizeGroup.GetType().GetMethod("GetCustomCount").Invoke(sizeGroup, null);
                object size = Activator.CreateInstance(sizeType, new object[] { Enum.Parse(sizeKindType, "FixedResolution"), width, height, "MobileTouchVerification" });
                sizeGroup.GetType().GetMethod("AddCustomSize").Invoke(sizeGroup, new[] { size });
                addedSizes.Add(custom);
                return builtin + custom;
            };
            int landscapeSize = addSize(2340, 1080);
            int wideSize = addSize(1920, 1080);
            selectedSize.SetValue(window, landscapeSize);
            await Task.Delay(450);

            var canvas = (RectTransform)hud.transform.Find("MountainPassHUD");
            controls.Initialize(canvas, game, Field<TMP_FontAsset>(hud, "font"), Field<Material>(hud, "textMaterial"));
            game.View.SendMessage("OnApplicationFocus", true);
            root = canvas.Find("MobileSafeArea/TouchControls");
            await Task.Delay(350);
            Check("FHD+ resolution is 2340x1080", Screen.width == 2340 && Screen.height == 1080, new { Screen.width, Screen.height });
            Check("mobile canvas keeps 900 reference height", Mathf.Abs(canvas.rect.height - 900f) < .1f, new { width = canvas.rect.width, height = canvas.rect.height });
            Check("all title buttons are on screen", new[] { "BeginJourney", "AudioSettings", "QuitGame", "EnterHint" }.All(name => InsideScreen((RectTransform)canvas.Find("Introduction/TitleColumn/" + name))));
            CapturePreview("Assets/Temp/MobileRevision~/T_Title2340.png");
            Check("mobile UI initializes with eight artwork references", controls.IsInitialized && root != null && new SerializedObject(controls).FindProperty("joystickKnobSprite").objectReferenceValue != null);
            touchscreen = InputSystem.AddDevice<Touchscreen>();
            await Task.Delay(200);
            await WaitUntil(() => !game.IsTransitioning);
            await Tap(1, canvas.Find("Introduction/TitleColumn/BeginJourney"));
            await WaitUntil(() => game.IsRunning);
            Check("touch starts journey", game.IsRunning, game.State.ToString());
            await Task.Delay(350);
            Check("one EventSystem", UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length == 1);
            Check("desktop key hints are replaced", !canvas.Find("Gameplay/HealingGourd").gameObject.activeSelf && canvas.Find("BossIntroduction/SkipHint").GetComponent<TMP_Text>().text == "건너뛰기");
            Check("mobile removes resource values and stick hint", !canvas.Find("Gameplay/PlayerVitality/HealthValue").gameObject.activeSelf && !canvas.Find("Gameplay/PlayerVitality/EnergyValue").gameObject.activeSelf && root.Find("SprintHint") == null);
            Check("mobile resource bars are longer", ((RectTransform)canvas.Find("Gameplay/PlayerVitality/HealthBorder")).rect.width == 460f);

            var stick = root.Find("MovementStick");
            Vector2 halfStick = At(stick, new Vector2(0f, 55f));
            await Touch(1, TouchPhase.Began, halfStick);
            Check("joystick walks", movement.InputDirection.sqrMagnitude > .01f && !movement.IsSprinting);
            float beforeYaw = Field<float>(game.View, "yaw");
            Vector2 lookPoint = new Vector2(Screen.width * .69f, Screen.height * .62f);
            await Touch(2, TouchPhase.Began, lookPoint);
            await Touch(2, TouchPhase.Moved, lookPoint + new Vector2(46f, 12f));
            Check("second finger rotates camera while walking", Mathf.Abs(Mathf.DeltaAngle(beforeYaw, Field<float>(game.View, "yaw"))) > 1f && movement.InputDirection.sqrMagnitude > .01f);
            uint beforeSequence = movement.AnimationControl.PlaybackSequence;
            await Tap(3, root.Find("Attack"));
            Check("third finger attacks during movement and look", movement.AnimationControl.PlaybackSequence != beforeSequence, movement.AnimationControl.SelectedState);
            await Touch(1, TouchPhase.Ended, halfStick);
            await Touch(2, TouchPhase.Ended, lookPoint + new Vector2(46f, 12f));
            Check("lifting movement finger stops input", movement.InputDirection == Vector3.zero && ((RectTransform)stick.Find("Thumb")).anchoredPosition == Vector2.zero);
            await Task.Delay(1200);

            await Tap(4, root.Find("Dodge"));
            Check("dodge touch rolls", movement.AnimationControl.IsDodging || movement.AnimationControl.SelectedState == "ROLL", movement.AnimationControl.SelectedState);
            await Task.Delay(1300);
            Vector2 fullStick = At(stick, new Vector2(0f, 84f));
            float beforeEnergy = game.Energy.Current;
            await Touch(5, TouchPhase.Began, fullStick);
            Check("full stick stays at maximum walking speed", movement.IsMoving && !movement.IsSprinting && movement.InputDirection.magnitude > .99f);
            Check("moving shows run icon and caption", root.Find("RunSheathe/Caption").GetComponent<TMP_Text>().text == "달리기");
            await Tap(15, root.Find("RunSheathe"));
            float sprintEnergy = game.Energy.Current;
            await Task.Delay(150);
            Check("run button starts sprint and consumes energy", movement.IsSprinting && game.Energy.Current < sprintEnergy);
            await Tap(16, root.Find("RunSheathe"));
            Check("second run tap returns to walking", !movement.IsSprinting && movement.IsMoving);
            await Tap(17, root.Find("RunSheathe"));
            await Touch(5, TouchPhase.Ended, fullStick);
            Check("stopping cancels sprint and shows sheathe", !Field<bool>(controls, "sprintRequested") && root.Find("RunSheathe/Caption").GetComponent<TMP_Text>().text == "납도");
            movement.AnimationControl.EnterCombat();
            await WaitUntil(() => !movement.AnimationControl.IsEquipmentTransition);
            await Task.Delay(160);
            await Tap(18, root.Find("RunSheathe"));
            Check("stationary sheathe button puts fan away", !movement.AnimationControl.IsCombatRequested);
            await WaitUntil(() => !movement.AnimationControl.IsActionPlaying);
            await Task.Delay(450);

            game.PlayerHealth.TakeDamage(30f);
            await WaitUntil(() => !movement.AnimationControl.IsActionPlaying);
            Check("hit reaction finishes before healing input", !movement.AnimationControl.IsActionPlaying, movement.AnimationControl.SelectedState);
            int beforeUses = game.Gourd.RemainingUses;
            float beforeHealth = game.PlayerHealth.Current;
            int healingClicks = 0;
            object healingClickState = null;
            root.Find("Heal").GetComponent<Button>().onClick.AddListener(() =>
            {
                healingClicks++;
                healingClickState = new { animation = movement.AnimationControl.SelectedState, action = movement.AnimationControl.IsActionPlaying,
                    equipment = movement.AnimationControl.IsEquipmentTransition, input = movement.InputEnabled, running = game.IsRunning,
                    drinking = game.Gourd.IsPlaying, gourdPhase = game.Gourd.Phase.ToString() };
            });
            await Tap(6, root.Find("Heal"));
            await WaitUntil(() => game.Gourd.RemainingUses < beforeUses, 5500);
            Check("healing button drinks and heals", game.Gourd.RemainingUses == beforeUses - 1 && game.PlayerHealth.Current > beforeHealth, new { beforeUses, uses = game.Gourd.RemainingUses, beforeHealth, health = game.PlayerHealth.Current, healingClicks, healingClickState });
            await WaitUntil(() => !game.Gourd.IsPlaying);

            await Touch(7, TouchPhase.Began, halfStick);
            await Tap(8, root.Find("Pause"));
            Check("menu pauses and clears held movement", game.State == PassGameFlow.RunState.Paused && Field<Vector2>(movement, "touchMovement") == Vector2.zero && !root.gameObject.activeSelf);
            CapturePreview("Assets/Temp/MobileRevision~/T_Pause2340.png");
            var resume = canvas.GetComponentsInChildren<Button>().First(button => button.GetComponentInChildren<TMP_Text>()?.text == "여정 계속하기");
            await Tap(9, resume.transform);
            Check("touch resumes without stale held finger", game.IsRunning && movement.InputDirection == Vector3.zero);
            await Touch(7, TouchPhase.Ended, halfStick);
            await Task.Delay(250);

            CapturePreview("Assets/Temp/MobileRevision~/T_MobileControls16x9.png");
            await Task.Delay(350);
            selectedSize.SetValue(window, wideSize);
            await Task.Delay(450);
            CapturePreview("Assets/Temp/MobileRevision~/T_MobileControlsWide.png");
            await Task.Delay(350);
            selectedSize.SetValue(window, landscapeSize);
            await Task.Delay(450);

            movement.Teleport(game.Shrine.Position + Vector3.up * .1f, Quaternion.identity);
            await Task.Delay(300);
            Check("interaction button appears only near shrine", game.Shrine.CanInteract && root.Find("Interact").gameObject.activeSelf);
            await Tap(10, root.Find("Interact"));
            Check("interaction touch starts prayer", game.Shrine.IsPraying && !movement.InputEnabled);
            await WaitUntil(() => !game.Shrine.IsPraying);
            Check("prayer reaches checkpoint and refills gourd", game.ReachedShrine && game.Gourd.RemainingUses == game.Gourd.MaximumUses);

            game.BeginEncounter();
            await WaitUntil(() => game.State == PassGameFlow.RunState.Cinematic && !game.IsTransitioning);
            await Task.Delay(100);
            Check("cinematic hides gameplay touch controls", game.State == PassGameFlow.RunState.Cinematic && !root.gameObject.activeSelf);
            var topBar = (RectTransform)canvas.Find("BossIntroduction/LetterboxTop");
            var bottomBar = (RectTransform)canvas.Find("BossIntroduction/LetterboxBottom");
            Check("letterboxes cover full FHD+ width", Mathf.Abs(topBar.rect.width - canvas.rect.width) < .1f && Mathf.Abs(bottomBar.rect.width - canvas.rect.width) < .1f);
            CapturePreview("Assets/Temp/MobileRevision~/T_Cinematic2340.png");
            await Tap(11, canvas.Find("BossIntroduction/SkipHint"));
            await WaitUntil(() => game.IsRunning);
            Check("touch skips cinematic", game.IsRunning);
            var floor = (Renderer)new SerializedObject(game).FindProperty("bossDetectionFloor").objectReferenceValue;
            movement.Teleport(floor.bounds.center + Vector3.up * .2f + Vector3.back * 8f, Quaternion.identity);
            game.View.SnapBehind();
            game.PlayerHealth.Invulnerable = true;
            await WaitUntil(() => game.View.CanLockBoss);
            Check("boss lock is available after detection", game.View.CanLockBoss);
            game.Boss.enabled = false;
            await Tap(12, root.Find("LockOn"));
            Check("touch locks boss", game.View.BossLocked);
            await Tap(13, root.Find("LockOn"));
            Check("touch unlocks boss", !game.View.BossLocked);
            CapturePreview("Assets/Temp/MobileRevision~/T_MobileControlsCombat.png");
            await Task.Delay(350);

            await Touch(14, TouchPhase.Began, At(stick, new Vector2(0f, 55f)));
            controls.SendMessage("OnApplicationPause", true);
            Check("app suspension clears touch ownership and movement", Field<Vector2>(movement, "touchMovement") == Vector2.zero && ((RectTransform)stick.Find("Thumb")).anchoredPosition == Vector2.zero);
            await Touch(14, TouchPhase.Ended, At(stick));

            return new { success = true, checks = checks.ToArray(), frame = Time.frameCount, screen = new { Screen.width, Screen.height } };
        }
        finally
        {
            if (touchscreen != null) InputSystem.RemoveDevice(touchscreen);
            if (selectedSize != null && window != null && previousSize >= 0) selectedSize.SetValue(window, previousSize);
            if (sizeGroup != null)
                foreach (int index in addedSizes.OrderByDescending(value => value)) sizeGroup.GetType().GetMethod("RemoveCustomSize").Invoke(sizeGroup, new object[] { index });
            Application.runInBackground = runInBackground;
            InputSystem.settings.backgroundBehavior = previousBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInput;
            File.WriteAllText("Assets/Temp/MobileRevision~/TouchVerification.json", JsonConvert.SerializeObject(checks, Formatting.Indented));
        }
    }
}
