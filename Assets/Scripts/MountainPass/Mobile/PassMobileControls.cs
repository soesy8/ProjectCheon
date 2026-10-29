using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Cheon.Prototype.MountainPass
{
    [DefaultExecutionOrder(-20), RequireComponent(typeof(PassHud))]
    public class PassMobileControls : MonoBehaviour
    {
        [Header("모바일 버튼 원화 · 512 × 512")]
        [SerializeField] private Sprite attackSprite;
        [SerializeField] private Sprite dodgeSprite;
        [SerializeField] private Sprite healSprite;
        [SerializeField] private Sprite lockOnSprite;
        [SerializeField] private Sprite pauseSprite;
        [SerializeField] private Sprite interactSprite;
        [SerializeField] private Sprite joystickBaseSprite;
        [SerializeField] private Sprite joystickKnobSprite;
        [SerializeField] private Sprite runSprite;
        [SerializeField] private Sprite sheatheSprite;
        [Header("터치 조작")]
        [SerializeField, Range(40f, 150f), InspectorName("스틱 이동 반경")] private float joystickTravel = 84f;
        [SerializeField, Range(0f, .4f), InspectorName("스틱 입력 최소 범위")] private float joystickDeadZone = .12f;
        [SerializeField, Range(.3f, 1f), InspectorName("버튼 기본 불투명도")] private float buttonOpacity = .88f;

        private PassGameFlow game;
        private PassHud hud;
        private PassPlayerMovement movement;
        private RectTransform safeRoot, controlsRoot, joystick, knob;
        private PassMobileDragArea movementArea, lookArea;
        private UnityEngine.UI.Button attack, dodge, heal, lockOn, interact, runSheathe;
        private UnityEngine.UI.Image runSheatheImage;
        private TMP_Text runSheatheCaption;
        private Vector2 stickInput;
        private bool sprintRequested;
        private TMP_Text healingCaption, interactionHint;
        private TMP_FontAsset font;
        private Material textMaterial;
        private Rect previousSafeArea;
        private Vector2Int previousScreen;
        private Vector2 previousCanvasSize;
        private bool wasControllable;
        private int previousUses = -1;
        private readonly Color paper = new Color(.95f, .91f, .8f);
        private readonly Color jade = new Color(.55f, .95f, .8f);

        // 품질 설정이나 터치 모니터 여부와 관계없이 Android/iOS 실행 파일에서만 켠다.
        public static bool SupportsPlatform => Application.platform == RuntimePlatform.Android ||
            Application.platform == RuntimePlatform.IPhonePlayer;
        public bool IsInitialized => safeRoot != null;

        private void Awake() => enabled = false;

        public void Initialize(RectTransform canvas, PassGameFlow flow, TMP_FontAsset koreanFont, Material material)
        {
            if (IsInitialized) return;
            game = flow; hud = GetComponent<PassHud>();
            movement = game.Player.GetComponent<PassPlayerMovement>();
            font = koreanFont; textMaterial = material;
            safeRoot = Rect("MobileSafeArea", canvas, Vector2.zero, Vector2.zero, Vector2.zero);
            safeRoot.anchorMax = Vector2.one;
            controlsRoot = Rect("TouchControls", safeRoot, Vector2.zero, Vector2.zero, Vector2.zero);
            controlsRoot.anchorMax = Vector2.one;

            // 시점 영역을 먼저 만들어 나중에 생성한 버튼과 스틱이 우선 터치되도록 한다.
            var look = Rect("LookArea", controlsRoot, Vector2.zero, Vector2.zero, Vector2.zero);
            look.anchorMin = new Vector2(.5f, 0f); look.anchorMax = Vector2.one;
            var lookImage = look.gameObject.AddComponent<UnityEngine.UI.Image>();
            lookImage.color = Color.clear; lookImage.raycastTarget = true;
            lookArea = look.gameObject.AddComponent<PassMobileDragArea>();
            lookArea.Dragged += data => { if (CanControl) game.View.AddTouchLook(data.delta / Mathf.Max(1, Screen.width)); };

            joystick = Picture("MovementStick", controlsRoot, Vector2.zero, new Vector2(190f, 210f), 300f, joystickBaseSprite);
            movementArea = joystick.gameObject.AddComponent<PassMobileDragArea>();
            movementArea.Pressed += MoveStick;
            movementArea.Dragged += MoveStick;
            movementArea.Released += ReleaseStick;
            knob = Picture("Thumb", joystick, new Vector2(.5f, .5f), Vector2.zero, 112f, joystickKnobSprite);
            knob.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            Caption("MovementCaption", controlsRoot, Vector2.zero, new Vector2(190f, 45f), new Vector2(320f, 36f), "이동", 26f);

            attack = ActionButton("Attack", new Vector2(1f, 0f), new Vector2(-165f, 200f), 205f, attackSprite, "공격", () => { if (CanControl) movement.RequestTouchAttack(); });
            dodge = ActionButton("Dodge", new Vector2(1f, 0f), new Vector2(-350f, 130f), 145f, dodgeSprite, "회피", () => { if (CanControl) movement.RequestTouchDodge(); });
            runSheathe = ActionButton("RunSheathe", new Vector2(1f, 0f), new Vector2(-510f, 220f), 136f, sheatheSprite, "납도", RunOrSheathe);
            runSheatheImage = runSheathe.GetComponent<UnityEngine.UI.Image>();
            runSheatheCaption = runSheathe.GetComponentInChildren<TMP_Text>();
            heal = ActionButton("Heal", new Vector2(1f, 0f), new Vector2(-335f, 335f), 136f, healSprite, "회복", () => { if (CanControl) game.Gourd?.TryDrink(); });
            healingCaption = heal.GetComponentInChildren<TMP_Text>();
            lockOn = ActionButton("LockOn", new Vector2(1f, 0f), new Vector2(-128f, 405f), 112f, lockOnSprite, "주시", () => { if (CanControl && game.View.CanLockBoss) game.View.LockBoss(!game.View.BossLocked); });
            ActionButton("Pause", Vector2.one, new Vector2(-70f, -65f), 88f, pauseSprite, "메뉴", () => { if (game.IsRunning) game.TogglePause(true); });
            interact = ActionButton("Interact", new Vector2(.5f, 0f), new Vector2(0f, 185f), 110f, interactSprite, "기도하기", () => { if (CanControl) game.Shrine?.Interact(); });
            interactionHint = Caption("InteractionHint", controlsRoot, new Vector2(.5f, 0f), new Vector2(0f, 280f), new Vector2(640f, 36f), "산신각에 기도 · 생명력 회복 · 호리병 충전", 21f);

            movement.SetTouchControlsEnabled(true);
            game.View.SetTouchControlsEnabled(true);
            hud.ApplyMobileLayout();
            enabled = true;
            UpdateSafeArea();
            Update();
        }

        private bool CanControl => game != null && game.IsRunning && movement.InputEnabled && !PassHud.IsTyping;

        private void Update()
        {
            if (!IsInitialized) return;
            UpdateSafeArea();
            bool running = game.IsRunning;
            if (controlsRoot.gameObject.activeSelf != running) controlsRoot.gameObject.SetActive(running);
            bool controllable = CanControl;
            if (!controllable && wasControllable) ResetInput();
            wasControllable = controllable;
            attack.interactable = dodge.interactable = heal.interactable = controllable;
            bool moving = stickInput.sqrMagnitude > .0001f;
            if (sprintRequested && game.Energy != null && !game.Energy.CanSprint)
            {
                sprintRequested = false;
                movement.SetTouchMovement(stickInput, false);
            }
            runSheatheImage.sprite = moving ? runSprite : sheatheSprite;
            runSheatheCaption.text = moving ? "달리기" : "납도";
            runSheathe.interactable = controllable && (moving || movement.AnimationControl.IsCombatRequested && movement.AnimationControl.CanChangeEquipment);
            runSheatheImage.color = moving && sprintRequested ? jade : new Color(1f, 1f, 1f, buttonOpacity);
            lockOn.interactable = controllable && game.View.CanLockBoss;
            lockOn.GetComponent<UnityEngine.UI.Image>().color = game.View.BossLocked ? jade : new Color(1f, 1f, 1f, buttonOpacity);
            bool nearShrine = running && game.Shrine != null && game.Shrine.CanInteract;
            bool praying = running && game.Shrine != null && game.Shrine.IsPraying;
            if (interact.gameObject.activeSelf != nearShrine) interact.gameObject.SetActive(nearShrine);
            interactionHint.gameObject.SetActive(nearShrine || praying);
            interactionHint.text = praying ? "산신각에 기도를 올리는 중" : "산신각에 기도 · 생명력 회복 · 호리병 충전";
            if (game.Gourd != null && previousUses != game.Gourd.RemainingUses)
            {
                previousUses = game.Gourd.RemainingUses;
                healingCaption.SetText("회복  {0:0}/{1:0}", previousUses, game.Gourd.MaximumUses);
            }
        }

        private void MoveStick(PointerEventData data)
        {
            if (!CanControl || !RectTransformUtility.ScreenPointToLocalPointInRectangle(joystick, data.position, data.pressEventCamera, out Vector2 local)) return;
            Vector2 input = Vector2.ClampMagnitude(local / joystickTravel, 1f);
            knob.anchoredPosition = input * joystickTravel;
            float strength = input.magnitude;
            if (strength < joystickDeadZone) input = Vector2.zero;
            stickInput = input;
            if (input == Vector2.zero) sprintRequested = false;
            // 스틱은 항상 걷기 범위만 담당하고 달리기는 별도 버튼으로 요청한다.
            movement.SetTouchMovement(input, sprintRequested);
        }

        private void RunOrSheathe()
        {
            if (!CanControl) return;
            if (stickInput.sqrMagnitude > .0001f)
            {
                sprintRequested = !sprintRequested;
                movement.SetTouchMovement(stickInput, sprintRequested);
            }
            else if (movement.AnimationControl.IsCombatRequested && movement.AnimationControl.CanChangeEquipment)
                movement.AnimationControl.ExitCombat();
        }

        private void ReleaseStick()
        {
            stickInput = Vector2.zero;
            sprintRequested = false;
            if (knob != null) knob.anchoredPosition = Vector2.zero;
            if (movement != null) movement.SetTouchMovement(Vector2.zero, false);
        }

        private void ResetInput()
        {
            movementArea?.ResetPointer(); lookArea?.ResetPointer();
            ReleaseStick();
            if (movement != null) movement.ClearTouchInput();
            if (game != null && game.View != null) game.View.ClearTouchLook();
        }

        private void UpdateSafeArea()
        {
            var screen = new Vector2Int(Screen.width, Screen.height);
            Rect safe = Screen.safeArea;
            Vector2 canvasSize = ((RectTransform)safeRoot.parent).rect.size;
            if (screen == previousScreen && safe == previousSafeArea && canvasSize == previousCanvasSize) return;
            previousScreen = screen; previousSafeArea = safe;
            previousCanvasSize = canvasSize;
            Vector2 size = new Vector2(Mathf.Max(1, screen.x), Mathf.Max(1, screen.y));
            safeRoot.anchorMin = safe.min / size; safeRoot.anchorMax = safe.max / size;
            safeRoot.offsetMin = safeRoot.offsetMax = Vector2.zero;
            hud.SetMobileSafeArea(safeRoot.anchorMin, safeRoot.anchorMax);
        }

        private UnityEngine.UI.Button ActionButton(string name, Vector2 anchor, Vector2 position, float size, Sprite sprite, string caption, UnityEngine.Events.UnityAction action)
        {
            var rect = Picture(name, controlsRoot, anchor, position, size, sprite);
            var image = rect.GetComponent<UnityEngine.UI.Image>();
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image; button.onClick.AddListener(action);
            button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
            var colors = button.colors;
            colors.pressedColor = new Color(.65f, 1f, .88f); colors.disabledColor = new Color(.45f, .45f, .45f, .5f);
            button.colors = colors;
            Caption("Caption", rect, new Vector2(.5f, .5f), new Vector2(0f, -size * .5f - 10f), new Vector2(size + 90f, 36f), caption, 23f);
            return button;
        }

        private RectTransform Picture(string name, Transform parent, Vector2 anchor, Vector2 position, float size, Sprite sprite)
        {
            var rect = Rect(name, parent, anchor, position, Vector2.one * size);
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.sprite = sprite; image.preserveAspect = true;
            image.color = new Color(1f, 1f, 1f, buttonOpacity);
            return rect;
        }

        private TMP_Text Caption(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, string text, float pointSize)
        {
            var rect = Rect(name, parent, anchor, position, size);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            if (textMaterial != null) label.fontSharedMaterial = textMaterial;
            label.text = text; label.fontSize = pointSize; label.color = paper;
            label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            return label;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor; rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position; rect.sizeDelta = size;
            return rect;
        }

        private void OnApplicationFocus(bool focused) { if (!focused) ResetInput(); }
        private void OnApplicationPause(bool paused) { if (paused) ResetInput(); }
        private void OnDisable() => ResetInput();
    }
}
