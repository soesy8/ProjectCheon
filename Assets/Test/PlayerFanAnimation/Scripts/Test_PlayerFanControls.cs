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

        private TMP_Text status, fanStatus, pauseText;
        [SerializeField, HideInInspector] private UnityEngine.UI.Slider[] speedSliders = new UnityEngine.UI.Slider[3];
        [SerializeField, HideInInspector] private TMP_Text[] speedLabels = new TMP_Text[3];
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
                var events = new GameObject("Test_EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }

            var panel = Rect("Test_ControlPanel", canvasObject.transform, 24f, 24f, 334f, 810f);
            var background = panel.gameObject.AddComponent<UnityEngine.UI.Image>();
            background.color = new Color(0.035f, 0.065f, 0.085f, 0.96f);
            Text(panel, "PLAYER / FAN", 18f, 15f, 300f, 28f, 24f, Jade);
            Text(panel, "캐릭터 · 부채 애니메이션 테스트", 18f, 47f, 300f, 25f, 17f, Paper);
            status = Text(panel, "", 18f, 82f, 300f, 46f, 16f, Paper);

            Button(panel, "대기 [I]", 18f, 136f, 144f, animationControl.PlayIdle);
            Button(panel, "달리기 [R]", 172f, 136f, 144f, animationControl.PlayRun);
            Button(panel, "공격 1 [1]", 18f, 181f, 94f, animationControl.PlayAttack1);
            Button(panel, "공격 2 [2]", 120f, 181f, 94f, animationControl.PlayAttack2);
            Button(panel, "공격 3 [3]", 222f, 181f, 94f, animationControl.PlayAttack3);
            Button(panel, "피격 [4]", 18f, 226f, 144f, animationControl.PlayHit);
            Button(panel, "납도발도 [5]", 172f, 226f, 144f, animationControl.PlayDrawSheathe);

            Text(panel, "공격별 속도 배율", 18f, 280f, 290f, 28f, 18f, Jade);
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                speedLabels[i] = Text(panel, "", 18f, 315f + i * 53f, 290f, 24f, 15f, Paper);
                speedSliders[i] = Slider(panel, 18f, 341f + i * 53f, 298f, 0.05f, 3f);
                speedSliders[i].onValueChanged.AddListener(value =>
                {
                    if (index == 0) animationControl.attack1Speed = value;
                    else if (index == 1) animationControl.attack2Speed = value;
                    else animationControl.attack3Speed = value;
                });
            }
            pauseText = Button(panel, "일시정지 [Space]", 18f, 487f, 144f, animationControl.TogglePause);
            Button(panel, "동작 반복 전환", 172f, 487f, 144f, () => animationControl.RepeatSelectedAnimation = !animationControl.RepeatSelectedAnimation);
            Text(panel, "부채 상태", 18f, 541f, 290f, 28f, 18f, Jade);
            Button(panel, "접힌 FBX", 18f, 577f, 94f, fanAttachment.ShowClosed);
            Button(panel, "펼친 FBX", 120f, 577f, 94f, fanAttachment.ShowOpen);
            Button(panel, "펼침 / 접힘 [F]", 222f, 577f, 94f, fanAttachment.ToggleFan, 12f);
            fanStatus = Text(panel, "", 18f, 625f, 298f, 24f, 14f, Paper);
            openingSlider = Slider(panel, 18f, 656f, 298f, 0f, 1f);
            openingSlider.onValueChanged.AddListener(fanAttachment.SetOpeningAmount);
            Button(panel, "카메라 초기화", 18f, 696f, 298f, orbitCamera.ResetView);
            Text(panel, "우클릭 드래그: 회전  /  휠: 확대\n속도 값은 Inspector에서도 조절 가능합니다.", 18f, 746f, 300f, 48f, 13f, new Color(0.63f, 0.70f, 0.72f));
        }

        private void Update()
        {
            status.text = $"{animationControl.SelectedState}  ·  {(animationControl.NormalizedTime % 1f) * 100f:0}%\n동작 반복: {(animationControl.RepeatSelectedAnimation ? "켜짐" : "꺼짐")}";
            float[] speeds = { animationControl.attack1Speed, animationControl.attack2Speed, animationControl.attack3Speed };
            for (int i = 0; i < 3; i++)
            {
                speedLabels[i].text = $"공격 {i + 1}                        × {speeds[i]:0.00}";
                speedSliders[i].SetValueWithoutNotify(speeds[i]);
            }
            pauseText.text = animationControl.IsPaused ? "계속 재생 [Space]" : "일시정지 [Space]";
            fanStatus.text = $"{fanAttachment.FanState}  ·  {fanAttachment.OpeningAmount * 100f:0}%";
            openingSlider.SetValueWithoutNotify(fanAttachment.OpeningAmount);
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
            slider.targetGraphic = handle; slider.handleRect = (RectTransform)handle.transform;
            slider.minValue = minimum; slider.maxValue = maximum;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            return slider;
        }
    }
}
