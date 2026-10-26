using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Cheon.Tests.PlayerReactions
{
    public class Test_PlayerReactionUI : MonoBehaviour
    {
        [SerializeField] private Test_PlayerReactionPreview preview;
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private TMP_Settings textSettings;
        private TMP_Text status, frameLabel, rangeLabel, saveMessage, pauseLabel, repeatLabel, cancelStatus;
        private TMP_InputField startInput, endInput, speedInput, landingInput, headBowInput;
        private UnityEngine.UI.Slider startSlider, endSlider, speedSlider, timeline;
        private int selectedRange;
        private bool refreshing;
        private static readonly Color Paper = new Color(0.91f, 0.94f, 0.93f);
        private static readonly Color Jade = new Color(0.36f, 0.85f, 0.72f);

        public void Configure(Test_PlayerReactionPreview target, TMP_FontAsset koreanFont, TMP_Settings settings)
        { preview = target; font = koreanFont; textSettings = settings; }

        private void Awake()
        {
            // 기존 HUD와 같은 방식으로 독립 씬의 TMP 설정을 먼저 연결한다.
            if (textSettings != null)
                typeof(TMP_Settings).GetField("s_Instance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)?.SetValue(null, textSettings);
        }

        private void Start()
        {
            var root = new GameObject("Test_ReactionCanvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            root.transform.SetParent(transform, false);
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = 1f;
            if (EventSystem.current == null)
            {
                var events = new GameObject("Test_EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            var panel = Box("Controls", root.transform, 20, 20, 450, 860, new Color(0.035f, 0.065f, 0.075f, 0.98f));
            Label("Eyebrow", panel, "PLAYER MOTION  /  REACTION LAB", 22, 18, 410, 24, 15, Jade);
            Label("Title", panel, "강피격 · 일어나기", 22, 47, 410, 38, 29, Paper);
            Label("Subtitle", panel, "원본 프레임 기준  ·  모션별 독립 조절", 22, 87, 410, 25, 15, Paper);
            Button("HeavyPattern", panel, "강피격 → 일어나기 → 대기", 22, 126, 406, 42, () => preview.Play(Test_PlayerReactionPreview.PreviewMode.HeavyThenGetUp), true);
            Button("LethalAttack", panel, "치명타 피격 → 사망 자세 유지", 22, 176, 406, 42, () => preview.Play(Test_PlayerReactionPreview.PreviewMode.Death));
            Button("HeavyOnly", panel, "강피격만 재생", 22, 226, 198, 35, () => preview.Play(Test_PlayerReactionPreview.PreviewMode.HeavyOnly));
            Button("GetUpOnly", panel, "일어나기 → 대기", 230, 226, 198, 35, () => preview.Play(Test_PlayerReactionPreview.PreviewMode.GetUpOnly));
            Label("RangeTitle", panel, "조절할 모션", 22, 278, 130, 24, 17, Jade);
            Button("HeavyTab", panel, "강피격", 22, 309, 128, 33, () => SelectRange(0));
            Button("GetUpTab", panel, "일어나기", 160, 309, 128, 33, () => SelectRange(1));
            Button("DeathTab", panel, "사망", 298, 309, 130, 33, () => SelectRange(2));
            rangeLabel = Label("RangeInfo", panel, "", 22, 348, 406, 26, 15, Paper);
            Label("StartCaption", panel, "시작 프레임", 22, 380, 130, 24, 16, Paper);
            Label("EndCaption", panel, "종료 프레임", 230, 380, 130, 24, 16, Paper);
            startInput = Input("StartFrame", panel, 22, 409, 198, value => { if (int.TryParse(value, out int n)) SetRange(n, Range.endFrame); });
            endInput = Input("EndFrame", panel, 230, 409, 198, value => { if (int.TryParse(value, out int n)) SetRange(Range.startFrame, n); });
            startSlider = Slider("StartFrameSlider", panel, 22, 451, 198, 0, 1, true, n => SetRange(Mathf.RoundToInt(n), Range.endFrame));
            endSlider = Slider("EndFrameSlider", panel, 230, 451, 198, 1, 2, true, n => SetRange(Range.startFrame, Mathf.RoundToInt(n)));
            Label("SpeedCaption", panel, "재생 배속", 22, 487, 125, 27, 17, Paper);
            speedInput = Input("PlaybackSpeed", panel, 330, 483, 98, value =>
            { if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float n) && !float.IsNaN(n)) { Range.speed = n; preview.Settings.Validate(); Refresh(); } });
            speedSlider = Slider("SpeedSlider", panel, 22, 523, 406, 0.05f, 3f, false, n => { Range.speed = n; Refresh(); });
            Label("HoldCaption", panel, "착지 후 입력 · 초", 22, 557, 122, 25, 15, Paper);
            var hold = Input("DownHold", panel, 22, 588, 122, value =>
            { if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float n) && !float.IsNaN(n)) { preview.Settings.landedRecoveryDelay = Mathf.Clamp(n, 0, 2); } });
            hold.SetTextWithoutNotify(preview.Settings.landedRecoveryDelay.ToString("0.00", CultureInfo.InvariantCulture));
            Label("BlendCaption", panel, "모션 전환 · 초", 164, 557, 122, 25, 15, Paper);
            var blend = Input("Transition", panel, 164, 588, 122, value =>
            { if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float n) && !float.IsNaN(n)) preview.Settings.transitionSeconds = Mathf.Clamp(n, 0, 0.5f); });
            blend.SetTextWithoutNotify(preview.Settings.transitionSeconds.ToString("0.00", CultureInfo.InvariantCulture));
            Label("IdleBlendCaption", panel, "대기 전환 · 초", 306, 557, 122, 25, 15, Paper);
            var idleBlend = Input("IdleTransition", panel, 306, 588, 122, value =>
            { if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float n) && !float.IsNaN(n)) preview.Settings.returnToIdleSeconds = Mathf.Clamp(n, 0, 0.8f); });
            idleBlend.SetTextWithoutNotify(preview.Settings.returnToIdleSeconds.ToString("0.00", CultureInfo.InvariantCulture));
            frameLabel = Label("FramePosition", panel, "", 22, 640, 406, 24, 16, Jade);
            timeline = Slider("Timeline", panel, 22, 670, 406, 0, 1, false, n => { preview.Scrub(selectedRange, n); Refresh(); });
            Button("PreviousFrame", panel, "-1f", 22, 707, 64, 34, () => preview.StepFrame(selectedRange, -1));
            pauseLabel = Button("Pause", panel, "일시정지", 94, 707, 144, 34, preview.TogglePaused);
            Button("NextFrame", panel, "+1f", 246, 707, 64, 34, () => preview.StepFrame(selectedRange, 1));
            Button("Reset", panel, "초기화", 318, 707, 110, 34, preview.ResetPreview);
            repeatLabel = Button("Repeat", panel, "연속 반복: 끔", 22, 751, 198, 34, () => preview.repeatSequence = !preview.repeatSequence);
            Button("SaveSettings", panel, "설정 저장", 230, 751, 96, 34, preview.SaveSettings, true);
            Button("ReloadSettings", panel, "불러오기", 334, 751, 94, 34, () => { preview.ReloadSettings(); hold.SetTextWithoutNotify(preview.Settings.landedRecoveryDelay.ToString("0.00", CultureInfo.InvariantCulture)); blend.SetTextWithoutNotify(preview.Settings.transitionSeconds.ToString("0.00", CultureInfo.InvariantCulture)); idleBlend.SetTextWithoutNotify(preview.Settings.returnToIdleSeconds.ToString("0.00", CultureInfo.InvariantCulture)); Refresh(); });
            saveMessage = Label("SaveStatus", panel, "", 22, 800, 406, 44, 14, Paper);

            status = Label("PlaybackStatus", root.transform, "", 520, 35, 1020, 65, 22, Paper);
            Label("ViewHint", root.transform, "WASD / 방향키: 이동 · Space: 회피\n착지 + 0.5초 → 일어나기 · 일어나기 55% → 회피 / 90% → 이동·대기", 520, 103, 1020, 65, 17, Jade);
            cancelStatus = Label("CancelStatus", root.transform, "", 520, 180, 610, 75, 16, Paper);
            Label("LandingCaption", root.transform, "착지 프레임", 1160, 180, 160, 34, 16, Paper);
            landingInput = Input("LandingFrame", root.transform, 1340, 180, 160, value =>
            { if (int.TryParse(value, out int n)) { preview.Settings.landingFrame = n; preview.Settings.Validate(); Refresh(); } });
            Label("HeadBowCaption", root.transform, "머리 숙임 · 도", 1160, 225, 160, 34, 16, Paper);
            headBowInput = Input("HeadBow", root.transform, 1340, 225, 160, value =>
            { if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float n) && !float.IsNaN(n)) { preview.Settings.getUpHeadBowDegrees = n; preview.Settings.Validate(); Refresh(); } });
            Button("FrontView", root.transform, "정면", 520, 820, 100, 40, () => preview.SetView(180, 5.5f));
            Button("SideView", root.transform, "측면", 630, 820, 100, 40, () => preview.SetView(90, 5.5f));
            Button("BackView", root.transform, "후면", 740, 820, 100, 40, () => preview.SetView(0, 5.5f));
            Button("CloseView", root.transform, "가까이", 850, 820, 100, 40, () => preview.SetView(155, 3.7f));
            Button("WideView", root.transform, "전체", 960, 820, 100, 40, () => preview.SetView(155, 7.5f));
            Refresh();
        }

        private Test_PlayerReactionSettings.MotionRange Range => preview.Settings.GetRange(selectedRange);
        private static string RangeName(int index) => index == 1 ? "일어나기" : index == 2 ? "사망 (강피격 원본)" : "강피격";
        private void SelectRange(int index) { selectedRange = index; Refresh(); }
        private void SetRange(int start, int end)
        { if (refreshing) return; Range.startFrame = start; Range.endFrame = end; preview.Settings.Validate(); Refresh(); }
        private void Update()
        {
            if (status == null) return;
            string phase = preview.Phase switch
            {
                Test_PlayerReactionPreview.PreviewPhase.Idle => "대기",
                Test_PlayerReactionPreview.PreviewPhase.HeavyHit => "강피격 재생 중",
                Test_PlayerReactionPreview.PreviewPhase.DownHold => "쓰러진 자세 유지 → 일어나기 대기",
                Test_PlayerReactionPreview.PreviewPhase.GetUp => "일어나기 재생 중",
                Test_PlayerReactionPreview.PreviewPhase.Dead => "사망 · 마지막 자세 유지",
                Test_PlayerReactionPreview.PreviewPhase.Moving => "이동 · 대기/달리기 블렌드트리",
                Test_PlayerReactionPreview.PreviewPhase.Dodging => "회피 · 구르기",
                _ => preview.AnimatorState == "IDLE" ? "대기 · 회복 완료" : "재생 완료"
            };
            status.text = phase + (preview.IsBlending ? " · 블렌딩" : "") + (preview.IsPaused ? "  |  정지" : "") + $"\n{preview.CurrentFrame:0.0} f";
            cancelStatus.text = (preview.Phase == Test_PlayerReactionPreview.PreviewPhase.GetUp ? $"일어나기 {preview.GetUpProgress:P0} · 회피 {(preview.CanDodge ? "가능" : "잠김")} · 이동 {(preview.CanMove ? "가능" : "잠김")}" :
                $"착지 후 {preview.LandedSeconds:0.00} / {preview.Settings.landedRecoveryDelay:0.00}초 · 일어나기 입력 {(preview.CanRecover ? "가능" : "잠김")}") + "\n" + preview.LastInputResult;
            pauseLabel.text = preview.IsPaused ? "계속 재생" : "일시정지";
            repeatLabel.text = "연속 반복: " + (preview.repeatSequence ? "켬" : "끔");
            saveMessage.text = preview.SettingsMessage;
            frameLabel.text = $"{RangeName(selectedRange)}  ·  탐색 {timeline.value:0.0} / {Range.MaximumFrame} f";
            if (selectedRange == preview.ActiveRangeIndex) timeline.SetValueWithoutNotify(preview.CurrentFrame);
        }
        private void Refresh()
        {
            if (startInput == null) return;
            refreshing = true;
            preview.Settings.Validate();
            if (!startInput.isFocused) startInput.SetTextWithoutNotify(Range.startFrame.ToString());
            if (!endInput.isFocused) endInput.SetTextWithoutNotify(Range.endFrame.ToString());
            if (!speedInput.isFocused) speedInput.SetTextWithoutNotify(Range.speed.ToString("0.00", CultureInfo.InvariantCulture));
            if (landingInput != null && !landingInput.isFocused) landingInput.SetTextWithoutNotify(preview.Settings.landingFrame.ToString());
            if (headBowInput != null && !headBowInput.isFocused) headBowInput.SetTextWithoutNotify(preview.Settings.getUpHeadBowDegrees.ToString("0.0", CultureInfo.InvariantCulture));
            startSlider.maxValue = Range.MaximumFrame - 1; endSlider.maxValue = Range.MaximumFrame;
            startSlider.SetValueWithoutNotify(Range.startFrame); endSlider.SetValueWithoutNotify(Range.endFrame);
            speedSlider.SetValueWithoutNotify(Range.speed);
            timeline.maxValue = Range.MaximumFrame;
            timeline.SetValueWithoutNotify(selectedRange == preview.ActiveRangeIndex ? preview.CurrentFrame : Range.startFrame);
            rangeLabel.text = $"{RangeName(selectedRange)}  |  {Range.FrameRate:0.##} fps  |  {Range.Duration:0.00}초";
            refreshing = false;
        }

        private RectTransform Box(string name, Transform parent, float x, float y, float w, float h, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
            var rect = (RectTransform)go.transform; rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h);
            var image = go.GetComponent<UnityEngine.UI.Image>(); image.color = color; image.raycastTarget = false;
            return rect;
        }
        private TMP_Text Label(string name, Transform parent, string text, float x, float y, float w, float h, float size, Color color)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font; label.text = text; label.fontSize = size; label.color = color;
            label.raycastTarget = false; label.textWrappingMode = TextWrappingModes.Normal;
            return label;
        }
        private TMP_Text Button(string name, Transform parent, string caption, float x, float y, float w, float h, UnityAction action, bool accent = false)
        {
            var rect = Box(name, parent, x, y, w, h, accent ? new Color(0.1f, 0.37f, 0.32f) : new Color(0.12f, 0.19f, 0.21f));
            var image = rect.GetComponent<UnityEngine.UI.Image>(); image.raycastTarget = true;
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image; button.onClick.AddListener(action);
            var text = Label("Caption", rect, caption, 5, 0, w - 10, h, 16, Paper); text.alignment = TextAlignmentOptions.Center;
            return text;
        }
        private TMP_InputField Input(string name, Transform parent, float x, float y, float width, UnityAction<string> action)
        {
            var rect = Box(name, parent, x, y, width, 34, new Color(0.07f, 0.12f, 0.14f));
            rect.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            var input = rect.gameObject.AddComponent<TMP_InputField>();
            var text = Label("Value", rect, "", 10, 0, width - 20, 34, 18, Paper);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            input.textViewport = (RectTransform)text.transform; input.textComponent = (TextMeshProUGUI)text;
            input.fontAsset = font; input.contentType = TMP_InputField.ContentType.DecimalNumber;
            input.onEndEdit.AddListener(action); return input;
        }
        private UnityEngine.UI.Slider Slider(string name, Transform parent, float x, float y, float width, float min, float max, bool whole, UnityAction<float> action)
        {
            var rect = Box(name, parent, x, y, width, 26, Color.clear);
            var track = Box("Track", rect, 0, 10, width, 6, new Color(0.18f, 0.28f, 0.3f));
            track.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            var handle = Box("Handle", rect, 0, 2, 16, 22, Jade);
            handle.pivot = new Vector2(0.5f, 0.5f);
            handle.sizeDelta = new Vector2(12, -8);
            handle.anchoredPosition = Vector2.zero;
            handle.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            var slider = rect.gameObject.AddComponent<UnityEngine.UI.Slider>();
            slider.handleRect = handle; slider.targetGraphic = handle.GetComponent<UnityEngine.UI.Image>();
            slider.minValue = min; slider.maxValue = max; slider.wholeNumbers = whole;
            slider.onValueChanged.AddListener(n => { if (!refreshing) action(n); }); return slider;
        }
    }
}
