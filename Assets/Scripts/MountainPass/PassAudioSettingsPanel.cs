using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Cheon.Prototype.MountainPass
{
    // 타이틀과 일시정지가 같은 설정 창과 저장값을 사용한다.
    public class PassAudioSettingsPanel : MonoBehaviour
    {
        private RectTransform panel;
        private RectTransform sheet;
        private TMP_FontAsset font;
        private TMP_FontAsset headingFont;
        private Material textMaterial;
        private Material headingMaterial;
        private Sprite inkBacking;
        private PassGameFlow game;
        private UnityEngine.UI.Slider masterSlider, musicSlider, effectsSlider;
        private CanvasGroup backgroundControls;
        private GameObject previousSelection;
        private bool previousInteractable, previousBlocksRaycasts;
        private readonly Color paper = new Color(.94f, .91f, .82f);
        private readonly Color gold = new Color(.84f, .69f, .43f);
        private readonly Color jade = new Color(.35f, .88f, .73f);
        private readonly Color muted = new Color(.58f, .68f, .66f);
        private readonly Color ink = new Color(.035f, .065f, .07f);

        public bool IsOpen => panel != null && panel.gameObject.activeSelf;

        public void Initialize(RectTransform canvas, PassGameFlow flow, TMP_FontAsset koreanFont, Material material,
            TMP_FontAsset titleFont = null, Material titleMaterial = null)
        {
            game = flow; font = koreanFont; textMaterial = material;
            headingFont = titleFont != null ? titleFont : font;
            headingMaterial = titleMaterial != null ? titleMaterial : textMaterial;
            foreach (var sprite in Resources.LoadAll<Sprite>("CheonHud/T_HudBars"))
                if (sprite.name == "InkBacking") inkBacking = sprite;

            panel = Rect("AudioSettings", canvas, Vector2.zero, Vector2.zero);
            panel.anchorMin = Vector2.zero; panel.anchorMax = Vector2.one;
            panel.offsetMin = panel.offsetMax = Vector2.zero;
            panel.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(.008f, .018f, .022f, .88f);
            sheet = Rect("Sheet", panel, Vector2.zero, new Vector2(920f, 680f));
            sheet.anchorMin = sheet.anchorMax = sheet.pivot = new Vector2(.5f, .5f);
            sheet.gameObject.AddComponent<UnityEngine.UI.Image>().color = ink;
            var shadow = sheet.gameObject.AddComponent<UnityEngine.UI.Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, .65f);
            shadow.effectDistance = new Vector2(0f, -12f);
            var texture = Decoration("InkTexture", sheet, Vector2.zero, sheet.sizeDelta, new Color(1f, 1f, 1f, .48f));
            texture.sprite = inkBacking;
            texture.type = UnityEngine.UI.Image.Type.Sliced;
            PassHud.FitInkBorder(texture, sheet.sizeDelta.y);
            Frame("OuterFrame", sheet, new Vector2(14f, -14f), new Vector2(892f, 652f), new Color(gold.r, gold.g, gold.b, .32f));
            Corner("TopLeftCorner", sheet, new Vector2(14f, -14f), Vector2.one);
            Corner("TopRightCorner", sheet, new Vector2(906f, -14f), new Vector2(-1f, 1f));
            Corner("BottomLeftCorner", sheet, new Vector2(14f, -666f), new Vector2(1f, -1f));
            Corner("BottomRightCorner", sheet, new Vector2(906f, -666f), -Vector2.one);

            var eyebrow = Label("Eyebrow", sheet, "천지인  /  환경 설정", new Vector2(64f, -40f), new Vector2(580f, 28f), 19f, gold);
            eyebrow.characterSpacing = 3f;
            Label("Heading", sheet, "소리 설정", new Vector2(60f, -76f), new Vector2(620f, 74f), 54f, paper, true);
            Label("Description", sheet, "여정에 귀 기울이는 시간", new Vector2(64f, -153f), new Vector2(650f, 30f), 21f, muted);
            SoundOrnament(sheet);
            Decoration("HeaderRule", sheet, new Vector2(64f, -204f), new Vector2(792f, 1f), new Color(gold.r, gold.g, gold.b, .3f));
            Diamond("HeaderDiamond", sheet, new Vector2(460f, -204f), 7f, gold);

            masterSlider = VolumeRow("MasterVolume", sheet, "마스터 볼륨", "모든 소리의 크기", -229f, PassAudioSettings.SetMasterVolume);
            musicSlider = VolumeRow("MusicVolume", sheet, "배경음악", "여정을 채우는 선율", -341f, PassAudioSettings.SetMusicVolume);
            effectsSlider = VolumeRow("EffectsVolume", sheet, "효과음", "전투와 주변의 소리", -453f, PassAudioSettings.SetEffectsVolume);
            Decoration("FooterRule", sheet, new Vector2(64f, -568f), new Vector2(792f, 1f), new Color(gold.r, gold.g, gold.b, .22f));
            Diamond("SaveIndicator", sheet, new Vector2(70f, -618f), 7f, jade);
            Label("SaveHint", sheet, "변경한 음량은 자동으로 저장됩니다", new Vector2(88f, -601f), new Vector2(450f, 34f), 19f, muted);

            var close = Rect("Close", sheet, new Vector2(592f, -590f), new Vector2(264f, 56f));
            var image = close.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = new Color(.12f, .22f, .21f);
            Frame("Border", close, Vector2.zero, close.sizeDelta, new Color(gold.r, gold.g, gold.b, .7f));
            var button = close.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = colors.selectedColor = new Color(1.4f, 1.5f, 1.4f);
            colors.pressedColor = new Color(.7f, .8f, .75f);
            button.colors = colors;
            button.onClick.AddListener(CloseWithSound);
            close.gameObject.AddComponent<PassButtonHoverAudio>().Configure(button, game.Audio);
            Label("Caption", close, "돌아가기", new Vector2(22f, 0f), new Vector2(150f, 56f), 25f, paper).alignment = TextAlignmentOptions.Center;
            Frame("EscapeKey", close, new Vector2(190f, -16f), new Vector2(50f, 25f), new Color(gold.r, gold.g, gold.b, .45f));
            Label("EscapeHint", close, "Esc", new Vector2(190f, -16f), new Vector2(50f, 25f), 16f, gold).alignment = TextAlignmentOptions.Center;
            // 방향키로 음량을 조절하고 위아래 키로만 다음 항목으로 이동한다.
            SetNavigation(masterSlider, button, musicSlider);
            SetNavigation(musicSlider, masterSlider, effectsSlider);
            SetNavigation(effectsSlider, musicSlider, button);
            SetNavigation(button, effectsSlider, masterSlider);
            panel.gameObject.SetActive(false);
        }

        private UnityEngine.UI.Slider VolumeRow(string name, Transform parent, string caption, string description, float y, UnityEngine.Events.UnityAction<float> setVolume)
        {
            var row = Rect(name, parent, new Vector2(64f, y), new Vector2(792f, 94f));
            Diamond("Marker", row, new Vector2(8f, -18f), 8f, gold);
            Label("Label", row, caption, new Vector2(30f, 0f), new Vector2(220f, 36f), 28f, paper);
            Label("Description", row, description, new Vector2(250f, -6f), new Vector2(390f, 28f), 19f, muted);
            var valueLabel = Label("Value", row, "", new Vector2(668f, 0f), new Vector2(124f, 36f), 27f, jade);
            valueLabel.alignment = TextAlignmentOptions.Right;
            // 넓은 투명 영역을 입력 대상으로 삼아 얇은 트랙도 쉽게 드래그한다.
            var sliderRect = Rect("Slider", row, new Vector2(0f, -40f), new Vector2(792f, 44f));
            sliderRect.gameObject.AddComponent<UnityEngine.UI.Image>().color = Color.clear;
            for (int index = 0; index <= 4; index++)
                Decoration("Tick" + index, sliderRect, new Vector2(12f + index * 192f, -17f), new Vector2(1f, 12f), new Color(gold.r, gold.g, gold.b, .35f));
            var track = Decoration("Track", sliderRect, new Vector2(12f, -21f), new Vector2(768f, 4f), new Color(.16f, .24f, .24f)).rectTransform;
            var fill = Rect("Fill", track, Vector2.zero, Vector2.zero);
            fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one;
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            var fillImage = fill.gameObject.AddComponent<UnityEngine.UI.Image>();
            fillImage.color = jade; fillImage.raycastTarget = false;
            var handleArea = Rect("HandleArea", sliderRect, Vector2.zero, Vector2.zero);
            handleArea.anchorMin = Vector2.zero; handleArea.anchorMax = Vector2.one;
            // Slider가 손잡이의 세로 앵커를 늘리므로 이동 영역 자체의 높이를 20으로 제한한다.
            handleArea.offsetMin = new Vector2(12f, 12f); handleArea.offsetMax = new Vector2(-12f, -12f);
            var handle = Rect("Handle", handleArea, Vector2.zero, new Vector2(20f, 0f));
            handle.pivot = new Vector2(.5f, .5f);
            handle.anchorMin = handle.anchorMax = new Vector2(0f, .5f);
            handle.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var handleImage = handle.gameObject.AddComponent<UnityEngine.UI.Image>();
            handleImage.color = gold;
            Decoration("Inset", handle, new Vector2(4f, -4f), new Vector2(12f, 12f), ink);
            var slider = sliderRect.gameObject.AddComponent<UnityEngine.UI.Slider>();
            slider.minValue = 0f; slider.maxValue = 100f; slider.wholeNumbers = true;
            slider.fillRect = fill; slider.handleRect = handle; slider.targetGraphic = handleImage;
            var colors = slider.colors;
            colors.highlightedColor = colors.selectedColor = new Color(1.2f, 1.25f, 1.2f);
            slider.colors = colors;
            slider.onValueChanged.AddListener(value =>
            {
                setVolume(value / 100f);
                valueLabel.SetText("{0:0}%", value);
            });
            return slider;
        }

        private static void SetNavigation(UnityEngine.UI.Selectable control, UnityEngine.UI.Selectable above, UnityEngine.UI.Selectable below)
        {
            control.navigation = new UnityEngine.UI.Navigation
            {
                mode = UnityEngine.UI.Navigation.Mode.Explicit,
                selectOnUp = above,
                selectOnDown = below
            };
        }

        private static UnityEngine.UI.Image Decoration(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            var image = Rect(name, parent, position, size).gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static void Frame(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            var frame = Rect(name, parent, position, size);
            Decoration("Top", frame, Vector2.zero, new Vector2(size.x, 1f), color);
            Decoration("Bottom", frame, new Vector2(0f, -size.y + 1f), new Vector2(size.x, 1f), color);
            Decoration("Left", frame, Vector2.zero, new Vector2(1f, size.y), color);
            Decoration("Right", frame, new Vector2(size.x - 1f, 0f), new Vector2(1f, size.y), color);
        }

        private void Corner(string name, Transform parent, Vector2 position, Vector2 direction)
        {
            var corner = Rect(name, parent, position, Vector2.zero);
            corner.localScale = new Vector3(direction.x, direction.y, 1f);
            Decoration("Horizontal", corner, Vector2.zero, new Vector2(30f, 2f), gold);
            Decoration("Vertical", corner, Vector2.zero, new Vector2(2f, 30f), gold);
        }

        private static void Diamond(string name, Transform parent, Vector2 position, float size, Color color)
        {
            var diamond = Decoration(name, parent, position, new Vector2(size, size), color).rectTransform;
            diamond.pivot = new Vector2(.5f, .5f);
            diamond.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }

        private void SoundOrnament(Transform parent)
        {
            var ornament = Rect("SoundOrnament", parent, new Vector2(726f, -74f), new Vector2(130f, 90f));
            for (int index = 0; index < 9; index++)
            {
                float height = 18f + (4 - Mathf.Abs(4 - index)) * 12f;
                Decoration("Wave" + index, ornament, new Vector2(index * 15f, -(78f - height) * .5f), new Vector2(2f, height),
                    index == 4 ? gold : new Color(jade.r, jade.g, jade.b, .28f));
            }
        }

        private void LateUpdate()
        {
            if (IsOpen) FitToCanvas();
        }

        private void FitToCanvas()
        {
            // 좁은 화면에서도 테두리와 닫기 버튼을 포함한 창 전체가 보이도록 맞춘다.
            var canvas = (RectTransform)panel.parent;
            float scale = Mathf.Min(1f, (canvas.rect.width - 64f) / sheet.sizeDelta.x, (canvas.rect.height - 64f) / sheet.sizeDelta.y);
            sheet.localScale = Vector3.one * Mathf.Max(.1f, scale);
        }

        public void Open(CanvasGroup controls, GameObject returnSelection)
        {
            if (panel == null || IsOpen) return;
            backgroundControls = controls; previousSelection = returnSelection;
            previousInteractable = controls.interactable; previousBlocksRaycasts = controls.blocksRaycasts;
            controls.interactable = controls.blocksRaycasts = false;
            RefreshSlider(masterSlider, PassAudioSettings.MasterVolume);
            RefreshSlider(musicSlider, PassAudioSettings.MusicVolume);
            RefreshSlider(effectsSlider, PassAudioSettings.EffectsVolume);
            FitToCanvas();
            panel.gameObject.SetActive(true);
            if (game.Audio != null) game.Audio.Confirm();
            EventSystem.current?.SetSelectedGameObject(masterSlider.gameObject);
        }

        private static void RefreshSlider(UnityEngine.UI.Slider slider, float volume)
        {
            slider.SetValueWithoutNotify(Mathf.RoundToInt(volume * 100f));
            slider.transform.parent.Find("Value").GetComponent<TMP_Text>().SetText("{0:0}%", slider.value);
        }

        public void CloseWithSound()
        {
            if (!IsOpen) return;
            if (game.Audio != null) game.Audio.MenuBack();
            Close();
        }

        public void Close()
        {
            if (!IsOpen) return;
            PassAudioSettings.Save();
            panel.gameObject.SetActive(false);
            if (backgroundControls != null)
            {
                backgroundControls.interactable = previousInteractable;
                backgroundControls.blocksRaycasts = previousBlocksRaycasts;
            }
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(previousSelection != null && previousSelection.activeInHierarchy ? previousSelection : null);
        }

        private void OnDisable() => Close();
        private void OnApplicationPause(bool paused) { if (paused) PassAudioSettings.Save(); }
        private void OnApplicationQuit() => PassAudioSettings.Save();

        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.up;
            rect.anchoredPosition = position; rect.sizeDelta = size;
            return rect;
        }

        private TMP_Text Label(string name, Transform parent, string text, Vector2 position, Vector2 size, float fontSize, Color color, bool useHeadingFont = false)
        {
            var label = Rect(name, parent, position, size).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = useHeadingFont ? headingFont : font;
            var material = useHeadingFont ? headingMaterial : textMaterial;
            if (material != null) label.fontSharedMaterial = material;
            label.text = text; label.fontSize = fontSize; label.color = color;
            label.raycastTarget = false; label.alignment = TextAlignmentOptions.Left;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            if (useHeadingFont) label.characterSpacing = 3f;
            return label;
        }
    }
}
