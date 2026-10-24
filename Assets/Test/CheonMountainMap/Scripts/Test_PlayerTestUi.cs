using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Cheon.Testing.MountainMap
{
    // 두 독립 UI에서 사용하는 캔버스와 입력 요소의 공통 생성 규칙만 담당한다.
    public static class Test_PlayerTestUi
    {
        public static readonly Color Paper = new Color(0.90f, 0.94f, 0.92f);
        public static readonly Color Jade = new Color(0.39f, 0.89f, 0.74f);
        public static readonly Color Muted = new Color(0.65f, 0.75f, 0.76f);
        public static readonly Color Ink = new Color(0.035f, 0.065f, 0.085f, 0.95f);

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void PrepareMapTextSettings()
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path !=
                "Assets/Test/CheonMountainMap/Scenes/Test_CheonMountainMap.unity") return;
            // 폰트 에셋을 읽는 단계부터 전용 설정을 제공해 TMP 가져오기 창이 뜨지 않게 한다.
            Cheon.Testing.Test_CheonTextSettings.Ensure();
            var settings = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_Settings>(
                "Assets/Test/CheonMountainMap/Settings/Test_MountainTextSettings.asset");
            if (settings != null) BindTextSettings(settings);
        }
#endif

        private static void BindTextSettings(TMP_Settings settings)
        {
            typeof(TMP_Settings).GetField("s_Instance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)?.SetValue(null, settings);
        }

        public static bool IsTyping
        {
            get
            {
                var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
                return selected != null && selected.TryGetComponent<TMP_InputField>(out var field) && field.isFocused;
            }
        }

        public static RectTransform Canvas(Transform parent, TMP_FontAsset font, TMP_Settings settings, int order)
        {
            if (settings != null)
                BindTextSettings(settings);
            Cheon.Testing.Test_CheonTextSettings.Ensure(font);
            var item = new GameObject("Test_Canvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            item.transform.SetParent(parent, false);
            var canvas = item.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = item.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.matchWidthOrHeight = 0.5f;
            return (RectTransform)item.transform;
        }

        public static void EnsureEventSystem(Transform parent)
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            var item = new GameObject("Test_EventSystem");
            item.SetActive(false);
            item.transform.SetParent(parent, false);
            item.AddComponent<EventSystem>();
            item.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            item.SetActive(true);
        }

        public static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
        {
            var item = new GameObject(name, typeof(RectTransform));
            item.transform.SetParent(parent, false);
            var rect = (RectTransform)item.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        public static UnityEngine.UI.Image Panel(string name, Transform parent, Vector2 anchor, Vector2 offset, Vector2 size, bool blocksPointer)
        {
            var rect = Rect(name, parent, 0f, 0f, size.x, size.y);
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = offset;
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = Ink;
            image.raycastTarget = blocksPointer;
            return image;
        }

        public static TMP_Text Text(string name, Transform parent, TMP_FontAsset font, string content, float x, float y, float width, float height, float size, Color color)
        {
            var label = Rect(name, parent, x, y, width, height).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = content;
            label.fontSize = size;
            label.color = color;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.Normal;
            return label;
        }

        public static UnityEngine.UI.Button Button(string name, Transform parent, TMP_FontAsset font, string caption, float x, float y, float width, UnityAction action)
        {
            var rect = Rect(name, parent, x, y, width, 34f);
            var background = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            background.color = new Color(0.11f, 0.23f, 0.24f);
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(action);
            var label = Text("Test_Caption", rect, font, caption, 0f, 0f, width, 34f, 15f, Paper);
            label.alignment = TextAlignmentOptions.Center;
            return button;
        }

        public static TMP_InputField Number(string name, Transform parent, TMP_FontAsset font, float x, float y, float width, UnityAction<string> onCommit)
        {
            var rect = Rect(name, parent, x, y, width, 29f);
            var background = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            background.color = new Color(0.13f, 0.25f, 0.27f);
            var input = rect.gameObject.AddComponent<TMP_InputField>();
            var viewport = Rect("Test_TextViewport", rect, 5f, 1f, width - 10f, 27f);
            viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            var label = Text("Test_Value", viewport, font, "", 0f, 0f, width - 10f, 27f, 16f, Paper);
            label.alignment = TextAlignmentOptions.MidlineRight;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            input.targetGraphic = background;
            input.textViewport = viewport;
            input.textComponent = label;
            input.contentType = TMP_InputField.ContentType.DecimalNumber;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.characterLimit = 7;
            input.onEndEdit.AddListener(onCommit);
            return input;
        }

        public static UnityEngine.UI.Slider Slider(string name, Transform parent, float x, float y, float width, float maximum)
        {
            var rect = Rect(name, parent, x, y, width, 18f);
            var slider = rect.gameObject.AddComponent<UnityEngine.UI.Slider>();
            var track = Rect("Test_Track", rect, 0f, 6f, width, 5f).gameObject.AddComponent<UnityEngine.UI.Image>();
            track.color = new Color(0.19f, 0.31f, 0.32f);
            track.raycastTarget = false;
            var area = Rect("Test_HandleArea", rect, 7f, 0f, width - 14f, 18f);
            var handle = Rect("Test_Handle", area, 0f, 0f, 14f, 18f).gameObject.AddComponent<UnityEngine.UI.Image>();
            handle.color = Jade;
            handle.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            handle.rectTransform.sizeDelta = new Vector2(14f, 0f);
            slider.targetGraphic = handle;
            slider.handleRect = handle.rectTransform;
            slider.minValue = 0f;
            slider.maxValue = maximum;
            return slider;
        }

        public static bool TryNumber(string text, out float value) =>
            float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
