using Cheon.Prototype.MountainPass;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Cheon.MountainPass.Presentation
{
    // HUD가 만들어진 뒤 타이틀 화면에 크레딧을 덧붙인다.
    [DefaultExecutionOrder(-60)]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Cheon.Tests.CombatPresentation", "Assembly-CSharp", "Test_AudioCreditsPanel")]
    public class PassAudioCreditsPanel : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField, InspectorName("게임 진행")] private PassGameFlow game;
        [SerializeField, InspectorName("배경음악 크레딧")] private TextAsset musicCredits;
        [SerializeField, InspectorName("효과음 크레딧")] private TextAsset effectCredits;
        private RectTransform panel;
        private UnityEngine.UI.Button creditsButton, closeButton, musicButton, effectsButton;
        private UnityEngine.UI.ScrollRect scroll;
        private CanvasGroup titleControls;
        private TMP_Text body, template;
        private bool restoreGameEnabled, restoreTitleInteractable;
        private readonly Color paper = new Color(.94f, .91f, .82f);
        private readonly Color gold = new Color(.84f, .69f, .43f);
        private readonly Color jade = new Color(.35f, .88f, .73f);

        public bool IsOpen => panel != null && panel.gameObject.activeSelf;

        private void Start()
        {
            var canvas = transform.Find("MountainPassHUD");
            var introduction = canvas != null ? canvas.Find("Introduction") : null;
            var column = introduction != null ? introduction.Find("TitleColumn") : null;
            var challenge = column != null ? column.Find("BeginChallenge") : null;
            if (game == null || challenge == null || musicCredits == null || effectCredits == null)
            {
                Debug.LogError("테스트 크레딧: 타이틀 HUD와 크레딧 문서를 연결하세요.", this);
                enabled = false;
                return;
            }
            template = challenge.GetComponentInChildren<TMP_Text>();
            titleControls = column.GetComponent<CanvasGroup>();
            if (titleControls == null) titleControls = column.gameObject.AddComponent<CanvasGroup>();

            creditsButton = MakeButton("Test_CreditsButton", column, "크레딧", new Vector2(60, -750), new Vector2(450, 60));
            var image = creditsButton.GetComponent<UnityEngine.UI.Image>();
            image.sprite = challenge.GetComponent<UnityEngine.UI.Image>().sprite;
            image.color = challenge.GetComponent<UnityEngine.UI.Image>().color;
            image.type = challenge.GetComponent<UnityEngine.UI.Image>().type;
            PassHud.FitInkBorder(image, creditsButton.GetComponent<RectTransform>().rect.height);
            creditsButton.onClick.AddListener(OpenCredits);

            panel = Rect("Test_AudioCredits", introduction);
            Stretch(panel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            panel.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(0, 0, 0, .7f);
            var sheet = Rect("Sheet", panel);
            Stretch(sheet, new Vector2(.14f, .08f), new Vector2(.86f, .92f), Vector2.zero, Vector2.zero);
            sheet.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(.025f, .065f, .075f, 1f);
            Label("Heading", sheet, "소리를 빌려주신 분들", new Vector2(32, -22), new Vector2(720, 48), 30, paper);
            closeButton = MakeButton("Close", sheet, "닫기  Esc", new Vector2(-24, -24), new Vector2(150, 46));
            closeButton.GetComponent<RectTransform>().anchorMin = closeButton.GetComponent<RectTransform>().anchorMax = Vector2.one;
            closeButton.GetComponent<RectTransform>().pivot = Vector2.one;
            closeButton.onClick.AddListener(CloseCredits);
            musicButton = MakeButton("MusicTab", sheet, "배경음악", new Vector2(32, -86), new Vector2(180, 44));
            effectsButton = MakeButton("EffectsTab", sheet, "효과음", new Vector2(224, -86), new Vector2(180, 44));
            musicButton.onClick.AddListener(() => ShowPage(true));
            effectsButton.onClick.AddListener(() => ShowPage(false));

            var scrollRoot = Rect("Scroll", sheet);
            Stretch(scrollRoot, Vector2.zero, Vector2.one, new Vector2(32, 64), new Vector2(-24, -152));
            scroll = scrollRoot.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 42;
            var viewport = Rect("Viewport", scrollRoot);
            Stretch(viewport, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-24, 0));
            viewport.gameObject.AddComponent<UnityEngine.UI.Image>().color = Color.white;
            viewport.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic = false;
            var content = Rect("Content", viewport);
            Stretch(content, Vector2.up, Vector2.one, Vector2.zero, Vector2.zero);
            content.pivot = Vector2.up;
            var layout = content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.padding = new RectOffset(4, 20, 4, 24);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            body = Label("CreditsText", content, "", Vector2.zero, Vector2.zero, 22, paper);
            body.raycastTarget = true;
            body.textWrappingMode = TextWrappingModes.Normal;
            body.lineSpacing = 8;
            scroll.viewport = viewport;
            scroll.content = content;

            var track = Rect("Scrollbar", scrollRoot);
            Stretch(track, Vector2.right, Vector2.one, new Vector2(-12, 0), Vector2.zero);
            track.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(.15f, .22f, .23f);
            var handle = Rect("Handle", track);
            Stretch(handle, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var handleImage = handle.gameObject.AddComponent<UnityEngine.UI.Image>();
            handleImage.color = gold;
            var scrollbar = track.gameObject.AddComponent<UnityEngine.UI.Scrollbar>();
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handleImage;
            scrollbar.direction = UnityEngine.UI.Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = scrollbar;
            Label("Hint", sheet, "휠 / Page Up·Down  스크롤     밑줄  원문·이용조건 열기", new Vector2(32, 18), new Vector2(900, 30), 17, jade)
                .rectTransform.anchorMin = Vector2.zero;
            var hint = sheet.Find("Hint").GetComponent<RectTransform>();
            hint.anchorMax = Vector2.zero;
            hint.pivot = Vector2.zero;
            ShowPage(true);
            panel.gameObject.SetActive(false);
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (!IsOpen)
            {
                // 원본의 전역 Enter 시작 처리보다 먼저 선택된 크레딧 버튼을 연다.
                if (game != null && game.State == PassGameFlow.RunState.Intro && creditsButton != null &&
                    EventSystem.current != null && EventSystem.current.currentSelectedGameObject == creditsButton.gameObject &&
                    keyboard.enterKey.wasPressedThisFrame) OpenCredits();
                return;
            }
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (game != null && game.Audio != null) game.Audio.MenuBack();
                CloseCredits(); return;
            }
            if (keyboard.pageDownKey.wasPressedThisFrame) scroll.verticalNormalizedPosition -= .35f;
            if (keyboard.pageUpKey.wasPressedThisFrame) scroll.verticalNormalizedPosition += .35f;
        }

        public void OpenCredits()
        {
            if (panel == null || IsOpen || game.State != PassGameFlow.RunState.Intro || game.IsTransitioning) return;
            restoreGameEnabled = game.enabled;
            restoreTitleInteractable = titleControls.interactable;
            // 모달을 읽는 동안 타이틀의 Enter 단축키와 뒤쪽 버튼만 잠근다. 음악은 계속 재생한다.
            game.enabled = false;
            titleControls.interactable = false;
            panel.gameObject.SetActive(true);
            ShowPage(true);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(musicButton.gameObject);
        }

        public void CloseCredits()
        {
            if (!IsOpen) return;
            panel.gameObject.SetActive(false);
            if (game != null) game.enabled = restoreGameEnabled;
            if (titleControls != null) titleControls.interactable = restoreTitleInteractable;
            if (creditsButton != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(creditsButton.gameObject);
        }

        private void ShowPage(bool music)
        {
            body.text = (music ? musicCredits : effectCredits).text;
            musicButton.GetComponent<UnityEngine.UI.Image>().color = music ? new Color(.15f, .34f, .32f) : new Color(.07f, .14f, .16f);
            effectsButton.GetComponent<UnityEngine.UI.Image>().color = music ? new Color(.07f, .14f, .16f) : new Color(.15f, .34f, .32f);
            Canvas.ForceUpdateCanvases();
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = 1;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!IsOpen || eventData.button != PointerEventData.InputButton.Left || eventData.dragging) return;
            int index = TMP_TextUtilities.FindIntersectingLink(body, eventData.position, eventData.pressEventCamera);
            if (index < 0) return;
            string url = body.textInfo.linkInfo[index].GetLinkID();
            if (url.StartsWith("https://", System.StringComparison.Ordinal)) Application.OpenURL(url);
        }

        private void OnDisable() => CloseCredits();

        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.up;
            return rect;
        }

        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = offsetMin; rect.offsetMax = offsetMax;
        }

        private TMP_Text Label(string name, Transform parent, string text, Vector2 position, Vector2 size, float fontSize, Color color)
        {
            var rect = Rect(name, parent);
            rect.anchoredPosition = position; rect.sizeDelta = size;
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = template.font;
            label.fontSharedMaterial = template.fontSharedMaterial;
            label.fontSize = fontSize; label.color = color; label.text = text;
            label.raycastTarget = false;
            return label;
        }

        private UnityEngine.UI.Button MakeButton(string name, Transform parent, string text, Vector2 position, Vector2 size)
        {
            var rect = Rect(name, parent);
            rect.anchoredPosition = position; rect.sizeDelta = size;
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = new Color(.07f, .14f, .16f);
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            rect.gameObject.AddComponent<PassButtonHoverAudio>().Configure(button, game.Audio);
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = colors.selectedColor = new Color(1.3f, 1.3f, 1.15f);
            button.colors = colors;
            var caption = Label("Caption", rect, text, Vector2.zero, size, 23, gold);
            Stretch(caption.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            caption.alignment = TextAlignmentOptions.Center;
            return button;
        }
    }
}
