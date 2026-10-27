using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Cheon.Prototype.MountainPass
{
    public class PassHud : MonoBehaviour
    {
        [InspectorName("게임 진행 제어기")]
        [SerializeField] private PassGameFlow game;
        [InspectorName("한국어 표시 폰트")]
        [SerializeField] private TMP_FontAsset font;
        [InspectorName("한국어 텍스트 설정")]
        [SerializeField] private TMP_Settings textSettings;
        [SerializeField, InspectorName("먹 붓터치 배경")] private Sprite inkBrush;
        [SerializeField, InspectorName("호리병 아이콘")] private Sprite gourdIcon;
        [SerializeField, InspectorName("타이틀 배경 원화")] private Sprite titleBackground;
        [SerializeField, InspectorName("천지인 서예 로고")] private Sprite titleLogo;
        private Material textMaterial;
        private readonly Color paper = new Color(0.94f, 0.91f, 0.82f);
        private readonly Color jade = new Color(0.35f, 0.88f, 0.73f);
        private readonly Color gold = new Color(0.84f, 0.69f, 0.43f);
        private readonly Color muted = new Color(0.64f, 0.73f, 0.73f);
        private readonly Color healthGreen = new Color(0.27f, 0.8f, 0.39f);
        private readonly Color energyBlue = new Color(0.26f, 0.59f, 0.95f);
        private RectTransform canvas, gameplay, introduction, result, bossPanel, marker, cinematic, interactionPanel;
        private UnityEngine.UI.Image healthFill, energyFill, bossFill, damageFlash;
        private TMP_Text energyValue;
        private TMP_Text healthValue, recoveryHint, interactionLabel, objective, chapter, bossName, warning, notice, markerLabel;
        private TMP_Text resultEyebrow, resultTitle, resultDescription, primaryCaption, secondaryCaption;
        private float noticeUntil, noticeDuration, damageAlpha;
        private PassGameFlow.RunState previousState = (PassGameFlow.RunState)(-1);
        private float displayedHealth = -1f, displayedMaximum = -1f, displayedBossHealth = -1f;
        private float displayedHealAmount = -1f;
        private float displayedEnergy = -1f, displayedEnergyMaximum = -1f;
        private int displayedUses = -1, displayedMaximumUses = -1, displayedDistance = -1;
        private bool markerWasShrine;
        public static bool IsTyping => EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null
            && EventSystem.current.currentSelectedGameObject.TryGetComponent<TMP_InputField>(out var field) && field.isFocused;

        public void Configure(PassGameFlow flow, TMP_FontAsset koreanFont, TMP_Settings settings)
        { game = flow; font = koreanFont; textSettings = settings; }
        public void ConfigureArt(Sprite brush, Sprite gourd) { inkBrush = brush; gourdIcon = gourd; }
        public void ConfigureTitle(Sprite background, Sprite logo) { titleBackground = background; titleLogo = logo; }

        private void Awake()
        {
            if (font != null)
            {
                textMaterial = new Material(font.material) { name = "MountainHudText", hideFlags = HideFlags.DontSave };
                textMaterial.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.025f, 0.05f, 0.05f, 0.9f));
                textMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.12f);
            }
            if (textSettings != null)
                typeof(TMP_Settings).GetField("s_Instance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)?.SetValue(null, textSettings);
            var root = new GameObject("MountainPassHUD", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            root.transform.SetParent(transform, false);
            canvas = (RectTransform)root.transform;
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            root.GetComponent<Canvas>().sortingOrder = 30;
            var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f); scaler.matchWidthOrHeight = 0.5f;
            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var events = new GameObject("MountainPassInput", typeof(EventSystem));
                events.transform.SetParent(transform, false);
                events.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            BuildGameplay(); BuildIntroduction(); BuildResult(); BuildCinematic();
            damageFlash = FullPanel("DamageFlash", canvas, new Color(0.7f, 0.08f, 0.04f, 0f)).GetComponent<UnityEngine.UI.Image>();
            damageFlash.raycastTarget = false;
            var fade = GetComponent<PassScreenFade>();
            if (fade == null) fade = gameObject.AddComponent<PassScreenFade>();
            game.SetScreenFade(fade); fade.Initialize(canvas);
        }

        private void BuildGameplay()
        {
            gameplay = FullPanel("Gameplay", canvas, Color.clear);
            var vitality = InkPanel("PlayerVitality", gameplay, Vector2.up, new Vector2(28f, -20f), new Vector2(480f, 116f));
            healthFill = ResourceBar("Health", vitality, new Vector2(26f, -30f), healthGreen);
            energyFill = ResourceBar("Energy", vitality, new Vector2(26f, -70f), energyBlue);
            healthValue = Label("HealthValue", vitality, "100 / 100", new Vector2(346f, -24f), new Vector2(110f, 30f), 21f, paper);
            energyValue = Label("EnergyValue", vitality, "100 / 100", new Vector2(346f, -64f), new Vector2(110f, 30f), 21f, paper);
            healthValue.alignment = energyValue.alignment = TextAlignmentOptions.Right;

            var hints = InkPanel("ControlHints", gameplay, Vector2.one, new Vector2(-28f, -20f), new Vector2(382f, 202f));
            Label("ControlsTitle", hints, "조작키", new Vector2(28f, -20f), new Vector2(310f, 28f), 17f, gold);
            Label("Controls", hints, "WASD  이동       Shift  달리기\n마우스  시점      좌클릭 / J  공격\nSpace  회피       Tab  주시\nE  호리병          F  상호작용\nEsc  메뉴", new Vector2(28f, -56f), new Vector2(335f, 136f), 17f, paper);
            var objectivePanel = InkPanel("Objective", gameplay, Vector2.one, new Vector2(-28f, -224f), new Vector2(382f, 112f));
            chapter = Label("Chapter", objectivePanel, "01  /  산신각으로 가는 길", new Vector2(28f, -20f), new Vector2(330f, 25f), 14f, gold);
            objective = Label("Goal", objectivePanel, "산신각에 올라 기도를 올려라", new Vector2(28f, -54f), new Vector2(330f, 45f), 18f, paper);

            var gourd = InkPanel("HealingGourd", gameplay, Vector2.zero, new Vector2(28f, 36f), new Vector2(280f, 132f));
            var icon = Box("GourdIcon", gourd, Vector2.up, new Vector2(20f, -15f), new Vector2(100f, 100f), Color.white).GetComponent<UnityEngine.UI.Image>();
            icon.sprite = gourdIcon; icon.preserveAspect = true;
            Label("GourdName", gourd, "호리병", new Vector2(137f, -30f), new Vector2(130f, 33f), 23f, paper);
            var key = Box("GourdKey", gourd, Vector2.up, new Vector2(138f, -75f), new Vector2(30f, 30f), paper);
            Label("Key", key, "E", Vector2.zero, new Vector2(30f, 30f), 19f, new Color(0.05f, 0.1f, 0.1f)).alignment = TextAlignmentOptions.Center;
            recoveryHint = Label("RecoveryHint", gourd, "3 / 5", new Vector2(181f, -74f), new Vector2(85f, 32f), 23f, paper);
            interactionPanel = Box("ShrineInteraction", gameplay, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(630f, 52f), new Color(0.025f, 0.05f, 0.06f, 0.9f));
            interactionLabel = Label("InteractionText", interactionPanel, "", Vector2.zero, new Vector2(630f, 52f), 20f, jade);
            interactionLabel.alignment = TextAlignmentOptions.Center;

            bossPanel = InkPanel("GuardianVitality", gameplay, new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(840f, 112f));
            Label("BossCaption", bossPanel, "산문을 지키는 자", new Vector2(15f, -12f), new Vector2(810f, 26f), 15f, paper).alignment = TextAlignmentOptions.Center;
            bossName = Label("BossName", bossPanel, "천하대장군", new Vector2(15f, -38f), new Vector2(810f, 36f), 28f, paper);
            bossName.alignment = TextAlignmentOptions.Center;
            Box("BossBorder", bossPanel, Vector2.up, new Vector2(28f, -81f), new Vector2(784f, 17f), gold);
            var bossTrack = Box("BossTrack", bossPanel, Vector2.up, new Vector2(30f, -83f), new Vector2(780f, 13f), new Color(0.13f, 0.07f, 0.06f));
            bossFill = Box("BossFill", bossTrack, Vector2.up, Vector2.zero, new Vector2(780f, 13f), new Color(0.73f, 0.21f, 0.15f)).GetComponent<UnityEngine.UI.Image>();
            var warningPanel = Box("AttackNotice", gameplay, new Vector2(0.5f, 1f), new Vector2(0f, -42f), new Vector2(760f, 38f), Color.clear);
            warning = Label("Warning", warningPanel, "", new Vector2(0f, 0f), new Vector2(760f, 38f), 19f, gold);
            warning.alignment = TextAlignmentOptions.Center;
            var noticePanel = Box("Notification", gameplay, new Vector2(0.5f, 1f), new Vector2(0f, -186f), new Vector2(820f, 44f), Color.clear);
            notice = Label("NotificationText", noticePanel, "", Vector2.zero, new Vector2(820f, 44f), 20f, paper);
            notice.alignment = TextAlignmentOptions.Center;
            marker = Box("DestinationMarker", gameplay, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(170f, 65f), Color.clear);
            Label("Diamond", marker, "◇", new Vector2(0f, 0f), new Vector2(170f, 32f), 26f, gold).alignment = TextAlignmentOptions.Center;
            markerLabel = Label("Distance", marker, "산신각", new Vector2(0f, -34f), new Vector2(170f, 28f), 14f, paper);
            markerLabel.alignment = TextAlignmentOptions.Center;
        }

        private void BuildIntroduction()
        {
            introduction = FullPanel("Introduction", canvas, titleBackground != null ? Color.white : new Color(0.025f, 0.05f, 0.065f, 0.9f), true);
            introduction.GetComponent<UnityEngine.UI.Image>().sprite = titleBackground;
            var panel = Box("TitleColumn", introduction, Vector2.up, Vector2.zero, new Vector2(630f, 900f), Color.clear);
            Label("Eyebrow", panel, "C H E O N   J I   I N", new Vector2(90f, -132f), new Vector2(450f, 30f), 16f, gold);
            if (titleLogo != null)
            {
                var logo = Box("TitleLogo", panel, Vector2.up, new Vector2(45f, -200f), new Vector2(555f, 185f), Color.white).GetComponent<UnityEngine.UI.Image>();
                logo.sprite = titleLogo; logo.preserveAspect = true;
            }
            else Label("Title", panel, "천지인", new Vector2(80f, -215f), new Vector2(550f, 160f), 120f, paper);
            Label("Subtitle", panel, "산문을 지키는 자", new Vector2(90f, -412f), new Vector2(450f, 50f), 30f, paper);
            Box("TitleRule", panel, Vector2.up, new Vector2(90f, -492f), new Vector2(76f, 2f), gold);
            var begin = CreateButton("BeginJourney", panel, "산길에서 시작", new Vector2(60f, -566f), new Vector2(450f, 80f), Color.white, game.BeginJourney);
            begin.fontSize = 27f;
            Label("Selected", begin.transform.parent, "◇", new Vector2(28f, -23f), new Vector2(32f, 32f), 25f, jade);
            var challenge = CreateButton("BeginChallenge", panel, "수문장에게 도전", new Vector2(60f, -658f), new Vector2(450f, 80f), new Color(0.65f, 0.8f, 0.76f), game.BeginBossChallenge);
            challenge.fontSize = 27f;
            Label("EnterHint", panel, "ENTER  선택", new Vector2(90f, -824f), new Vector2(450f, 28f), 17f, gold);
            Label("ChapterSeal", introduction, "01\nMOUNTAIN PASS", new Vector2(1310f, -65f), new Vector2(240f, 80f), 18f, new Color(0.08f, 0.15f, 0.15f)).alignment = TextAlignmentOptions.Right;
        }

        private void BuildResult()
        {
            result = FullPanel("Result", canvas, new Color(0.025f, 0.045f, 0.06f, 0.9f), true);
            var panel = Box("ResultColumn", result, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(800f, 470f), Color.clear);
            resultEyebrow = Label("ResultEyebrow", panel, "", new Vector2(0f, -10f), new Vector2(800f, 35f), 16f, gold);
            resultTitle = Label("ResultTitle", panel, "", new Vector2(0f, -80f), new Vector2(800f, 85f), 55f, paper);
            resultDescription = Label("ResultDescription", panel, "", new Vector2(40f, -191f), new Vector2(720f, 90f), 20f, muted);
            resultEyebrow.alignment = resultTitle.alignment = resultDescription.alignment = TextAlignmentOptions.Center;
            primaryCaption = CreateButton("PrimaryAction", panel, "", new Vector2(102f, -339f), new Vector2(285f, 58f), jade, PrimaryAction);
            secondaryCaption = CreateButton("SecondaryAction", panel, "", new Vector2(413f, -339f), new Vector2(285f, 58f), new Color(0.16f, 0.23f, 0.25f), SecondaryAction);
        }

        private void BuildCinematic()
        {
            cinematic = FullPanel("BossIntroduction", canvas, Color.clear);
            Box("LetterboxTop", cinematic, Vector2.up, Vector2.zero, new Vector2(1600f, 85f), Color.black);
            Box("LetterboxBottom", cinematic, Vector2.zero, Vector2.zero, new Vector2(1600f, 110f), Color.black);
            var title = Box("GuardianTitle", cinematic, new Vector2(0.5f, 0f), new Vector2(0f, 142f), new Vector2(900f, 100f), Color.clear);
            Label("GuardianEyebrow", title, "산문을 지키는 자", Vector2.zero, new Vector2(900f, 32f), 19f, gold).alignment = TextAlignmentOptions.Center;
            Label("GuardianName", title, "천하대장군", new Vector2(0f, -38f), new Vector2(900f, 60f), 44f, paper).alignment = TextAlignmentOptions.Center;
            Label("SkipHint", cinematic, "Space / Esc  건너뛰기", new Vector2(1250f, -835f), new Vector2(300f, 35f), 15f, muted).alignment = TextAlignmentOptions.Right;
        }

        private void PrimaryAction()
        { if (game.State == PassGameFlow.RunState.Paused) game.TogglePause(); else if (game.State == PassGameFlow.RunState.Defeat) game.RetryCheckpoint(); else game.BeginJourney(); }
        private void SecondaryAction()
        { if (game.State == PassGameFlow.RunState.Defeat) game.BeginJourney(); else game.BeginBossChallenge(); }

        private void Update()
        {
            if (game == null || canvas == null) return;
            bool intro = game.State == PassGameFlow.RunState.Intro;
            bool presenting = game.State == PassGameFlow.RunState.Cinematic;
            SetVisible(introduction, intro); SetVisible(gameplay, !intro && !presenting); SetVisible(cinematic, presenting);
            bool showResult = game.State == PassGameFlow.RunState.Paused || game.State == PassGameFlow.RunState.Defeat || game.State == PassGameFlow.RunState.Victory;
            SetVisible(result, showResult);
            if (previousState != game.State) { previousState = game.State; RefreshResult(); EventSystem.current?.SetSelectedGameObject(null); }
            if (displayedHealth != game.PlayerHealth.Current || displayedMaximum != game.PlayerHealth.Maximum)
            {
                displayedHealth = game.PlayerHealth.Current; displayedMaximum = game.PlayerHealth.Maximum;
                healthFill.rectTransform.sizeDelta = new Vector2(300f * game.PlayerHealth.Fraction, 13f);
                healthValue.SetText("{0:0} / {1:0}", Mathf.Ceil(displayedHealth), displayedMaximum);
            }
            if (game.Energy != null && (displayedEnergy != game.Energy.Current || displayedEnergyMaximum != game.Energy.Maximum))
            {
                displayedEnergy = game.Energy.Current; displayedEnergyMaximum = game.Energy.Maximum;
                energyFill.rectTransform.sizeDelta = new Vector2(300f * game.Energy.Fraction, 13f);
                energyValue.SetText("{0:0} / {1:0}", Mathf.Ceil(displayedEnergy), displayedEnergyMaximum);
            }
            bool nearShrine = game.Shrine != null && game.Shrine.CanInteract;
            bool praying = game.Shrine != null && game.Shrine.IsPraying;
            if (game.Gourd != null && (displayedUses != game.Gourd.RemainingUses || displayedMaximumUses != game.Gourd.MaximumUses || displayedHealAmount != game.Gourd.HealAmount))
            {
                displayedUses = game.Gourd.RemainingUses; displayedMaximumUses = game.Gourd.MaximumUses; displayedHealAmount = game.Gourd.HealAmount;
                recoveryHint.SetText("{0:0} / {1:0}", displayedUses, displayedMaximumUses);
            }
            SetVisible(interactionPanel, (nearShrine || praying) && game.IsRunning);
            SetLabel(interactionLabel, praying ? "산신각에 기도를 올리는 중" : "F  산신각에 기도하기  ·  생명력 회복 · 호리병 충전");
            SetLabel(chapter, game.InEncounter ? "03  /  산문의 수문장" : game.ReachedShrine ? "02  /  산문 너머" : "01  /  산신각으로 가는 길");
            SetLabel(objective, game.ObjectiveTitle);
            SetVisible(bossPanel, game.InEncounter && game.State != PassGameFlow.RunState.Victory);
            if (game.Boss.Health != null)
            {
                if (displayedBossHealth != game.Boss.Health.Fraction)
                { displayedBossHealth = game.Boss.Health.Fraction; bossFill.rectTransform.sizeDelta = new Vector2(780f * displayedBossHealth, 13f); }
                SetLabel(bossName, game.Boss.Enraged ? "천하대장군 · 격노" : "천하대장군");
                SetLabel(warning, game.InEncounter && game.IsRunning ? game.Boss.AttackLabel : "");
                warning.color = game.Boss.State == PassBossController.BossState.Recovering ? jade : gold;
            }
            notice.color = new Color(paper.r, paper.g, paper.b, Mathf.Clamp01((noticeUntil - Time.unscaledTime) / Mathf.Min(0.7f, Mathf.Max(0.1f, noticeDuration))));
            damageAlpha = Mathf.MoveTowards(damageAlpha, 0f, Time.unscaledDeltaTime * 0.6f);
            damageFlash.color = new Color(0.7f, 0.08f, 0.04f, damageAlpha);
            SetVisible(marker, !game.InEncounter && game.IsRunning);
            if (marker.gameObject.activeSelf)
            {
                Vector3 viewport = Camera.main.WorldToViewportPoint(game.ObjectivePosition + Vector3.up * 2f);
                if (viewport.z < 0f) { viewport.x = viewport.x > 0.5f ? 0.08f : 0.92f; viewport.y = 0.5f; }
                marker.anchorMin = marker.anchorMax = new Vector2(Mathf.Clamp(viewport.x, 0.08f, 0.92f), Mathf.Clamp(viewport.y, 0.24f, 0.77f));
                marker.anchoredPosition = Vector2.zero;
                int distance = Mathf.RoundToInt(Vector3.Distance(game.Player.position, game.ObjectivePosition));
                if (displayedDistance != distance || markerWasShrine != game.ReachedShrine)
                {
                    displayedDistance = distance; markerWasShrine = game.ReachedShrine;
                    markerLabel.SetText(game.ReachedShrine ? "산문  {0:0}m" : "산신각  {0:0}m", distance);
                }
            }
        }

        private static void SetVisible(RectTransform panel, bool visible)
        { if (panel.gameObject.activeSelf != visible) panel.gameObject.SetActive(visible); }
        private static void SetLabel(TMP_Text label, string value)
        { if (label.text != value) label.SetText(value); }

        private void RefreshResult()
        {
            bool paused = game.State == PassGameFlow.RunState.Paused;
            bool won = game.State == PassGameFlow.RunState.Victory;
            resultEyebrow.text = paused ? "숨 고르기" : won ? "산문 개방" : "다시 산신각으로";
            resultTitle.text = paused ? "잠시 멈춤" : won ? "길을 열었습니다" : "다시 일어설 시간";
            resultDescription.text = paused ? "붉은 원을 벗어나고, 다가오는 파동은 구르기로 통과하세요.\n보스가 회복하는 순간이 연격의 기회입니다."
                : won ? $"수문장의 귀기가 걷히고 산문에 바람이 돌아왔습니다.\n여정  {Mathf.FloorToInt(game.Elapsed / 60f):00}:{Mathf.FloorToInt(game.Elapsed % 60f):00}    ·    명중  {game.PlayerCombat.HitsLanded}회"
                : "수문장은 다시 잠들었습니다.\n산신각의 가호를 받아 다시 도전하세요.";
            primaryCaption.text = paused ? "여정 계속하기" : won ? "산길 다시 걷기" : "다시 도전하기";
            secondaryCaption.text = game.State == PassGameFlow.RunState.Defeat ? "산길 처음부터" : "수문장에게 도전";
        }

        public void Notify(string message, float duration)
        { if (notice == null) return; notice.text = message; noticeDuration = duration; noticeUntil = Time.unscaledTime + duration; }
        public void FlashDamage() => damageAlpha = 0.25f;

        private RectTransform Rect(string title, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var item = new GameObject(title, typeof(RectTransform)); item.transform.SetParent(parent, false);
            var rect = (RectTransform)item.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor; rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }
        private RectTransform FullPanel(string title, Transform parent, Color color, bool blocks = false)
        {
            var rect = Rect(title, parent, Vector2.zero, Vector2.zero, Vector2.zero);
            rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = color; image.raycastTarget = blocks; return rect;
        }
        private RectTransform Box(string title, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Color color)
        {
            var rect = Rect(title, parent, anchor, position, size);
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = color; image.raycastTarget = false; return rect;
        }
        private RectTransform InkPanel(string title, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var rect = Box(title, parent, anchor, position, size, inkBrush != null ? Color.white : new Color(0.035f, 0.07f, 0.08f, 0.88f));
            rect.GetComponent<UnityEngine.UI.Image>().sprite = inkBrush;
            return rect;
        }
        private UnityEngine.UI.Image ResourceBar(string title, RectTransform parent, Vector2 position, Color color)
        {
            Box(title + "Border", parent, Vector2.up, position - new Vector2(2f, -2f), new Vector2(304f, 17f), gold);
            var track = Box(title + "Track", parent, Vector2.up, position, new Vector2(300f, 13f), new Color(0.04f, 0.09f, 0.09f));
            var fill = Box(title + "Fill", track, Vector2.up, Vector2.zero, new Vector2(300f, 13f), color).GetComponent<UnityEngine.UI.Image>();
            var highlight = Box(title + "Highlight", fill.transform, Vector2.up, Vector2.zero, new Vector2(0f, 2f), new Color(1f, 1f, 1f, 0.2f));
            highlight.anchorMax = Vector2.one;
            return fill;
        }
        private TMP_Text Label(string title, Transform parent, string text, Vector2 position, Vector2 size, float pointSize, Color color)
        {
            var label = Rect(title, parent, Vector2.up, position, size).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font; if (textMaterial != null) label.fontSharedMaterial = textMaterial;
            label.text = text; label.fontSize = pointSize; label.color = color; label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.Normal; return label;
        }
        private TMP_Text CreateButton(string title, Transform parent, string text, Vector2 position, Vector2 size, Color color, UnityEngine.Events.UnityAction action)
        {
            var rect = Box(title, parent, Vector2.up, position, size, color);
            var image = rect.GetComponent<UnityEngine.UI.Image>(); image.raycastTarget = true;
            if (inkBrush != null) image.sprite = inkBrush;
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image; button.onClick.AddListener(action);
            var colors = button.colors; colors.highlightedColor = new Color(1.18f, 1.18f, 1.18f); colors.pressedColor = new Color(0.75f, 0.8f, 0.8f); button.colors = colors;
            var caption = Label("Caption", rect, text, Vector2.zero, size, 19f, paper);
            caption.alignment = TextAlignmentOptions.Center; return caption;
        }
        private void OnDestroy() { if (textMaterial != null) Destroy(textMaterial); }
    }
}
