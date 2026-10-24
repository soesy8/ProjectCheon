using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Cheon.Testing.PlayerFan
{
    public class Test_PlayerFanControls : MonoBehaviour
    {
        [SerializeField] private Test_PlayerFanAnimationController animationControl;
        [SerializeField] private Test_PlayerFanAttachment fanAttachment;
        [SerializeField] private Test_PlayerFanCamera orbitCamera;
        [SerializeField] private TMP_FontAsset koreanFont;
        [SerializeField] private TMP_Settings textSettings;

        private TMP_Text status, fanStatus, pauseText, combatText;
        [SerializeField, HideInInspector] private UnityEngine.UI.Button[] attackButtons = new UnityEngine.UI.Button[3];
        [SerializeField, HideInInspector] private UnityEngine.UI.Button[] fanButtons = new UnityEngine.UI.Button[3];
        [SerializeField, HideInInspector] private UnityEngine.UI.Slider[] speedSliders = new UnityEngine.UI.Slider[3];
        [SerializeField, HideInInspector] private TMP_Text[] speedLabels = new TMP_Text[3];
        private readonly TMP_InputField[] speedInputs = new TMP_InputField[3];
        private readonly TMP_InputField[] startFrameInputs = new TMP_InputField[3];
        private readonly TMP_InputField[] endFrameInputs = new TMP_InputField[3];
        private readonly TMP_InputField[] movementSpeedInputs = new TMP_InputField[2];
        private readonly TMP_InputField[] movementStartFrameInputs = new TMP_InputField[2];
        private readonly TMP_InputField[] movementEndFrameInputs = new TMP_InputField[2];
        private readonly UnityEngine.UI.Slider[] movementSpeedSliders = new UnityEngine.UI.Slider[2];
        private readonly TMP_Text[] movementSpeedLabels = new TMP_Text[2];
        private TMP_InputField openingInput;
        private UnityEngine.UI.Slider openingSlider;
        private static readonly Color Paper = new Color(0.89f, 0.91f, 0.88f);
        private static readonly Color Jade = new Color(0.38f, 0.85f, 0.72f);

        public void Configure(Test_PlayerFanAnimationController controller, Test_PlayerFanAttachment fan, Test_PlayerFanCamera camera, TMP_FontAsset font, TMP_Settings settings)
        {
            animationControl = controller; fanAttachment = fan; orbitCamera = camera; koreanFont = font;
            textSettings = settings;
        }

        private void Awake()
        {
            // 전용 설정 에셋을 사용해 Play Mode를 반복 실행해도 TMP 설정 참조를 유지한다.
            if (textSettings != null)
                typeof(TMP_Settings).GetField("s_Instance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)?.SetValue(null, textSettings);
            Test_CheonTextSettings.Ensure(koreanFont);
            var canvasObject = new GameObject("Test_ControlCanvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.matchWidthOrHeight = 0.5f;
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var events = new GameObject("Test_EventSystem");
                events.SetActive(false);
                events.transform.SetParent(transform, false);
                events.AddComponent<EventSystem>();
                events.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
                events.SetActive(true);
            }

            var panel = Rect("Test_ControlPanel", canvasObject.transform, 24f, 24f, 334f, 866f);
            var background = panel.gameObject.AddComponent<UnityEngine.UI.Image>();
            background.color = new Color(0.035f, 0.065f, 0.085f, 0.96f);
            Text(panel, "PLAYER / FAN", 18f, 15f, 300f, 28f, 24f, Jade);
            Text(panel, "캐릭터 · 부채 애니메이션 테스트", 18f, 47f, 300f, 25f, 17f, Paper);
            status = Text(panel, "", 18f, 82f, 300f, 46f, 16f, Paper);

            Button(panel, "대기 [I]", 18f, 136f, 144f, animationControl.PlayIdle);
            Button(panel, "달리기 [Shift]", 172f, 136f, 144f, animationControl.PlayRun);
            attackButtons[0] = Button(panel, "공격 1 [1]", 18f, 181f, 94f, animationControl.PlayAttack1).transform.parent.GetComponent<UnityEngine.UI.Button>();
            attackButtons[1] = Button(panel, "공격 2 [2]", 120f, 181f, 94f, animationControl.PlayAttack2).transform.parent.GetComponent<UnityEngine.UI.Button>();
            attackButtons[2] = Button(panel, "공격 3 [3]", 222f, 181f, 94f, animationControl.PlayAttack3).transform.parent.GetComponent<UnityEngine.UI.Button>();
            Button(panel, "피격 [4]", 18f, 226f, 144f, animationControl.PlayHit);
            combatText = Button(panel, "전투 진입 [5]", 172f, 226f, 144f, animationControl.PlayDrawSheathe);
            Button(panel, "구르기 [Space]", 18f, 271f, 298f, animationControl.PlayRoll);

            Text(panel, "공격 속도 / 재생 프레임", 18f, 316f, 298f, 28f, 18f, Jade);
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                float row = 348f + i * 77f;
                speedLabels[i] = Text(panel, "", 18f, row, 220f, 26f, 14f, Paper);
                speedInputs[i] = NumberInput(panel, "Attack" + (i + 1) + "Speed", 246f, row, 70f, false, value =>
                {
                    if (TryNumber(value, out float speed)) SetAttackSpeed(index, speed);
                });
                Text(panel, "시작 F", 18f, row + 29f, 48f, 26f, 14f, Paper);
                startFrameInputs[i] = NumberInput(panel, "Attack" + (i + 1) + "StartFrame", 66f, row + 29f, 74f, true, value =>
                {
                    if (int.TryParse(value, out int frame))
                        animationControl.SetAttackFrameRange(index + 1, frame, animationControl.GetAttackEndFrame(index + 1));
                });
                Text(panel, "종료 F", 190f, row + 29f, 52f, 26f, 14f, Paper);
                endFrameInputs[i] = NumberInput(panel, "Attack" + (i + 1) + "EndFrame", 242f, row + 29f, 74f, true, value =>
                {
                    if (int.TryParse(value, out int frame))
                        animationControl.SetAttackFrameRange(index + 1, animationControl.GetAttackStartFrame(index + 1), frame);
                });
                speedSliders[i] = Slider(panel, 18f, row + 57f, 298f, 0.05f, 3f);
                speedSliders[i].onValueChanged.AddListener(value => SetAttackSpeed(index, value));
            }
            pauseText = Button(panel, "일시정지 [P]", 18f, 584f, 144f, animationControl.TogglePause);
            Button(panel, "동작 반복 전환", 172f, 584f, 144f, () => animationControl.RepeatSelectedAnimation = !animationControl.RepeatSelectedAnimation);
            Text(panel, "부채 상태", 18f, 628f, 290f, 28f, 18f, Jade);
            fanButtons[0] = Button(panel, "접힌 FBX", 18f, 660f, 94f, fanAttachment.ShowClosed).transform.parent.GetComponent<UnityEngine.UI.Button>();
            fanButtons[1] = Button(panel, "펼친 FBX", 120f, 660f, 94f, fanAttachment.ShowOpen).transform.parent.GetComponent<UnityEngine.UI.Button>();
            fanButtons[2] = Button(panel, "펼침 / 접힘 [F]", 222f, 660f, 94f, fanAttachment.ToggleFan, 12f).transform.parent.GetComponent<UnityEngine.UI.Button>();
            fanStatus = Text(panel, "", 18f, 704f, 220f, 26f, 14f, Paper);
            openingInput = NumberInput(panel, "FanOpeningPercent", 246f, 704f, 70f, false, value =>
            {
                if (TryNumber(value, out float percent)) fanAttachment.SetOpeningAmount(Mathf.Clamp01(percent / 100f));
            });
            openingSlider = Slider(panel, 18f, 734f, 298f, 0f, 1f);
            openingSlider.onValueChanged.AddListener(fanAttachment.SetOpeningAmount);
            Button(panel, "카메라 초기화", 18f, 754f, 298f, orbitCamera.ResetView);
            Text(panel, "숫자 입력 후 Enter 또는 다른 곳 클릭\nSpace: 구르기 / Shift: 달리기 / P: 정지\n5: 전투 / 1~3: 공격 / 우클릭·휠: 카메라", 18f, 794f, 300f, 64f, 13f, new Color(0.63f, 0.70f, 0.72f));
            CreateMovementSettings(canvasObject.transform);
        }

        private void CreateMovementSettings(Transform canvas)
        {
            var panel = Rect("Test_MovementSettingsPanel", canvas, 0f, 0f, 334f, 224f);
            panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.one;
            panel.anchoredPosition = new Vector2(-24f, -24f);
            panel.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(0.035f, 0.065f, 0.085f, 0.96f);
            Text(panel, "구르기 / 달리기 조절", 18f, 14f, 298f, 28f, 18f, Jade);
            for (int i = 0; i < 2; i++)
            {
                int index = i;
                int playbackIndex = i + 4;
                string name = i == 0 ? "Roll" : "Run2";
                float row = 48f + i * 82f;
                movementSpeedLabels[i] = Text(panel, "", 18f, row, 220f, 26f, 14f, Paper);
                movementSpeedInputs[i] = NumberInput(panel, name + "Speed", 246f, row, 70f, false, value =>
                {
                    if (TryNumber(value, out float speed)) SetMovementSpeed(index, speed);
                });
                Text(panel, "시작 F", 18f, row + 29f, 48f, 26f, 14f, Paper);
                movementStartFrameInputs[i] = NumberInput(panel, name + "StartFrame", 66f, row + 29f, 74f, true, value =>
                {
                    if (int.TryParse(value, out int frame))
                        animationControl.SetPlaybackFrameRange(playbackIndex, frame, animationControl.GetPlaybackEndFrame(playbackIndex));
                });
                Text(panel, "종료 F", 190f, row + 29f, 52f, 26f, 14f, Paper);
                movementEndFrameInputs[i] = NumberInput(panel, name + "EndFrame", 242f, row + 29f, 74f, true, value =>
                {
                    if (int.TryParse(value, out int frame))
                        animationControl.SetPlaybackFrameRange(playbackIndex, animationControl.GetPlaybackStartFrame(playbackIndex), frame);
                });
                movementSpeedSliders[i] = Slider(panel, 18f, row + 57f, 298f, 0.05f, 3f);
                movementSpeedSliders[i].onValueChanged.AddListener(value => SetMovementSpeed(index, value));
            }
        }

        private void Update()
        {
            string stateLabel = animationControl.SelectedState switch
            {
                "SHEATHED_IDLE" => "납도 대기", "DRAWN_IDLE" => "발도 대기", "DRAW" => "발도", "SHEATHE" => "납도",
                "ATTACK1" => "공격 1", "ATTACK2" => "공격 2", "ATTACK3" => "공격 3", "HIT" => "피격",
                "ROLL" => "구르기", "RUN2" => "달리기", _ => "대기"
            };
            status.text = $"{stateLabel}{(animationControl.IsRunning && animationControl.SelectedState != "RUN2" ? " · 달리기" : "")}  ·  {animationControl.PlaybackProgress * 100f:0}%\n동작 반복: {(animationControl.RepeatSelectedAnimation ? "켜짐" : "꺼짐")}";
            combatText.text = animationControl.IsCombatRequested ? "전투 종료 [5]" : "전투 진입 [5]";
            float[] speeds = { animationControl.attack1Speed, animationControl.attack2Speed, animationControl.attack3Speed };
            for (int i = 0; i < 3; i++)
            {
                speedLabels[i].text = $"공격 {i + 1} · 0~{animationControl.GetAttackMaximumFrame(i + 1)}F · 속도 ×";
                speedSliders[i].SetValueWithoutNotify(speeds[i]);
                SyncInput(speedInputs[i], speeds[i].ToString("0.00", CultureInfo.InvariantCulture));
                SyncInput(startFrameInputs[i], animationControl.GetAttackStartFrame(i + 1).ToString());
                SyncInput(endFrameInputs[i], animationControl.GetAttackEndFrame(i + 1).ToString());
                attackButtons[i].interactable = animationControl.CanAttack;
                fanButtons[i].interactable = animationControl.CanPreviewFan;
            }
            for (int i = 0; i < 2; i++)
            {
                int playbackIndex = i + 4;
                float speed = i == 0 ? animationControl.rollSpeed : animationControl.run2Speed;
                string label = i == 0 ? "구르기" : "달리기";
                movementSpeedLabels[i].text = $"{label} · 0~{animationControl.GetPlaybackMaximumFrame(playbackIndex)}F · 속도 ×";
                movementSpeedSliders[i].SetValueWithoutNotify(speed);
                SyncInput(movementSpeedInputs[i], speed.ToString("0.00", CultureInfo.InvariantCulture));
                SyncInput(movementStartFrameInputs[i], animationControl.GetPlaybackStartFrame(playbackIndex).ToString());
                SyncInput(movementEndFrameInputs[i], animationControl.GetPlaybackEndFrame(playbackIndex).ToString());
            }
            pauseText.text = animationControl.IsPaused ? "계속 재생 [P]" : "일시정지 [P]";
            fanStatus.text = $"{(fanAttachment.IsInHand ? "손 장착" : "납도 보관")} · 펼침 %";
            SyncInput(openingInput, (fanAttachment.OpeningAmount * 100f).ToString("0.0", CultureInfo.InvariantCulture));
            openingInput.interactable = animationControl.CanPreviewFan;
            openingSlider.SetValueWithoutNotify(fanAttachment.OpeningAmount);
            openingSlider.interactable = animationControl.CanPreviewFan;
        }

        private void SetAttackSpeed(int index, float value)
        {
            float speed = Mathf.Clamp(value, 0.05f, 3f);
            if (index == 0) animationControl.attack1Speed = speed;
            else if (index == 1) animationControl.attack2Speed = speed;
            else animationControl.attack3Speed = speed;
        }

        private void SetMovementSpeed(int index, float value)
        {
            float speed = Mathf.Clamp(value, 0.05f, 3f);
            if (index == 0) animationControl.rollSpeed = speed;
            else animationControl.run2Speed = speed;
        }

        private static bool TryNumber(string text, out float value)
        {
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static void SyncInput(TMP_InputField input, string value)
        {
            // 편집 중인 문자열을 덮어쓰지 않고 입력 확정 후 제한된 실제 값을 표시한다.
            if (!input.isFocused && input.text != value) input.SetTextWithoutNotify(value);
        }

        private TMP_InputField NumberInput(Transform parent, string name, float x, float y, float width, bool integer, UnityAction<string> onCommit)
        {
            var rect = Rect("Test_Input_" + name, parent, x, y, width, 26f);
            var background = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            background.color = new Color(0.13f, 0.23f, 0.25f);
            var input = rect.gameObject.AddComponent<TMP_InputField>();
            var viewport = Rect("Test_TextViewport", rect, 5f, 1f, width - 10f, 24f);
            viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            var text = Text(viewport, "", 0f, 0f, width - 10f, 24f, 15f, Paper);
            text.alignment = TextAlignmentOptions.MidlineRight;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            input.targetGraphic = background;
            input.textViewport = viewport;
            input.textComponent = text;
            input.contentType = integer ? TMP_InputField.ContentType.IntegerNumber : TMP_InputField.ContentType.DecimalNumber;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.characterLimit = 8;
            input.customCaretColor = true;
            input.caretColor = Jade;
            input.selectionColor = new Color(0.2f, 0.65f, 0.5f, 0.65f);
            input.onEndEdit.AddListener(onCommit);
            return input;
        }

        private static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
        {
            var item = new GameObject(name, typeof(RectTransform));
            item.transform.SetParent(parent, false);
            var rect = (RectTransform)item.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        private TMP_Text Text(Transform parent, string content, float x, float y, float width, float height, float size, Color color)
        {
            var label = Rect("Test_Label", parent, x, y, width, height).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = koreanFont; label.text = content; label.fontSize = size; label.color = color;
            label.raycastTarget = false; label.textWrappingMode = TextWrappingModes.Normal;
            return label;
        }

        private TMP_Text Button(Transform parent, string caption, float x, float y, float width, UnityAction action, float fontSize = 15f)
        {
            var rect = Rect("Test_Button_" + caption, parent, x, y, width, 36f);
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = new Color(0.11f, 0.20f, 0.22f);
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            var colors = button.colors; colors.highlightedColor = new Color(0.57f, 0.9f, 0.79f); colors.pressedColor = new Color(0.3f, 0.6f, 0.5f); button.colors = colors;
            button.onClick.AddListener(action);
            var label = Text(rect, caption, 0f, 0f, width, 36f, fontSize, Paper);
            label.alignment = TextAlignmentOptions.Center;
            return label;
        }

        private static UnityEngine.UI.Slider Slider(Transform parent, float x, float y, float width, float minimum, float maximum)
        {
            var rect = Rect("Test_Slider", parent, x, y, width, 18f);
            var slider = rect.gameObject.AddComponent<UnityEngine.UI.Slider>();
            var back = Rect("Test_Track", rect, 0f, 6f, width, 6f).gameObject.AddComponent<UnityEngine.UI.Image>();
            back.color = new Color(0.15f, 0.24f, 0.26f);
            var handleArea = Rect("Test_HandleArea", rect, 7f, 0f, width - 14f, 18f);
            var handle = Rect("Test_Handle", handleArea, 0f, 0f, 14f, 18f).gameObject.AddComponent<UnityEngine.UI.Image>();
            handle.color = Jade;
            handle.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            handle.rectTransform.sizeDelta = new Vector2(14f, 0f);
            slider.targetGraphic = handle; slider.handleRect = (RectTransform)handle.transform;
            slider.minValue = minimum; slider.maxValue = maximum;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            return slider;
        }
    }
}
