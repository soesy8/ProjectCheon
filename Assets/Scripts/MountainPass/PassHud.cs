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
        [SerializeField, InspectorName("본문 글꼴 (비우면 천지인 기본값)")] private TMP_FontAsset bodyFont;
        [SerializeField, InspectorName("붓글씨 제목 (비우면 천지인 기본값)")] private TMP_FontAsset titleFont;
        [SerializeField, Range(0f, 10f), InspectorName("제목 자간")] private float titleCharacterSpacing = 2f;
        [InspectorName("한국어 텍스트 설정")]
        [SerializeField] private TMP_Settings textSettings;
        [SerializeField, InspectorName("먹 붓터치 배경")] private Sprite inkBrush;
        [SerializeField, InspectorName("호리병 아이콘")] private Sprite gourdIcon;
        [SerializeField, InspectorName("체력 아이콘")] private Sprite healthIcon;
        [SerializeField, InspectorName("타이틀 배경 원화")] private Sprite titleBackground;
        [SerializeField, InspectorName("천지인 서예 로고")] private Sprite titleLogo;
        [Header("게이지 원화")]
        [SerializeField, InspectorName("플레이어 금색 프레임")] private Sprite playerBarFrame;
        [SerializeField, InspectorName("보스 금색 프레임")] private Sprite bossBarFrame;
        [SerializeField, InspectorName("생명력 채움 질감")] private Sprite healthBarTexture;
        [SerializeField, InspectorName("스태미나 채움 질감")] private Sprite energyBarTexture;
        [SerializeField, InspectorName("보스 채움 질감")] private Sprite bossBarTexture;
        [SerializeField, InspectorName("패널 먹 배경")] private Sprite panelInkTexture;
        [SerializeField, Min(0f), InspectorName("좌측 먹 패널 왼쪽 여백")] private float leftInkInset = 12f;
        [SerializeField, InspectorName("스탯 먹 패널 크기")] private Vector2 vitalityInkSize = new Vector2(600f, 132f);
        [SerializeField, InspectorName("호리병 먹 패널 크기")] private Vector2 gourdInkSize = new Vector2(317.4f, 163.3f);
        private Material textMaterial;
        private Material titleTextMaterial;
        private readonly Color paper = new Color(0.94f, 0.91f, 0.82f);
        private readonly Color jade = new Color(0.35f, 0.88f, 0.73f);
        private readonly Color gold = new Color(0.84f, 0.69f, 0.43f);
        private readonly Color muted = new Color(0.64f, 0.73f, 0.73f);
        private readonly Color healthGreen = new Color(0.27f, 0.8f, 0.39f);
        private readonly Color energyBlue = new Color(0.26f, 0.59f, 0.95f);
        private RectTransform canvas, gameplay, introduction, result, bossPanel, marker, cinematic, interactionPanel;
        private RectTransform pauseEyebrowInk, pauseTitleInk, pauseDescriptionInk, pauseControls;
        private UnityEngine.UI.Image healthFill, energyFill, bossFill, damageFlash;
        private TMP_Text energyValue;
        private TMP_Text healthValue, recoveryHint, interactionLabel, objective, chapter, bossName, warning, notice, markerLabel;
        private TMP_Text resultEyebrow, resultTitle, resultDescription, primaryCaption, secondaryCaption;
        private TMP_Text pauseControlsText;
        private PassAudioSettingsPanel audioSettings;
        private PassMobileControls mobileControls;
        public bool UsesMobileControls => mobileControls != null && mobileControls.IsInitialized;
        private RectTransform pauseSettingsButton, pauseQuitButton;
        public bool IsAudioSettingsOpen => audioSettings != null && audioSettings.IsOpen;
        public void CloseAudioSettings() => audioSettings?.CloseWithSound();
        private float noticeUntil, noticeDuration, damageAlpha;
        private PassGameFlow.RunState previousState = (PassGameFlow.RunState)(-1);
        private float displayedHealth = -1f, displayedMaximum = -1f, displayedBossHealth = -1f;
        private float displayedHealAmount = -1f;
        private float displayedEnergy = -1f, displayedEnergyMaximum = -1f;
        private float previousCanvasWidth = -1f;
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
            if (textSettings != null)
                typeof(TMP_Settings).GetField("s_Instance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)?.SetValue(null, textSettings);
            // 공용 글꼴을 한 번 불러오므로 기존 씬도 같은 서체를 사용한다. Inspector 지정값을 우선한다.
            if (bodyFont == null) bodyFont = Resources.Load<TMP_FontAsset>("CheonFonts/CheonYeonSung");
            if (titleFont == null) titleFont = Resources.Load<TMP_FontAsset>("CheonFonts/CheonTitle");
            if (bodyFont != null) font = bodyFont;
            LoadHudArtwork();
            if (font != null)
            {
                textMaterial = new Material(font.material) { name = "MountainHudText", hideFlags = HideFlags.DontSave };
                textMaterial.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.025f, 0.05f, 0.05f, 0.9f));
                textMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.12f);
            }
            if (titleFont != null)
            {
                titleTextMaterial = new Material(titleFont.material) { name = "M_CheonHudTitle", hideFlags = HideFlags.DontSave };
                titleTextMaterial.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.025f, 0.05f, 0.05f, 0.9f));
                titleTextMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.08f);
            }
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
            mobileControls = GetComponent<PassMobileControls>();
            if (PassMobileControls.SupportsPlatform && mobileControls != null)
                mobileControls.Initialize(canvas, game, font, textMaterial);
            audioSettings = gameObject.AddComponent<PassAudioSettingsPanel>();
            audioSettings.Initialize(canvas, game, font, textMaterial, titleFont, titleTextMaterial);
            damageFlash = FullPanel("DamageFlash", canvas, new Color(0.7f, 0.08f, 0.04f, 0f)).GetComponent<UnityEngine.UI.Image>();
            damageFlash.raycastTarget = false;
            var fade = GetComponent<PassScreenFade>();
            if (fade == null) fade = gameObject.AddComponent<PassScreenFade>();
            game.SetScreenFade(fade); fade.Initialize(canvas);
        }

        private void BuildGameplay()
        {
            gameplay = FullPanel("Gameplay", canvas, Color.clear);
            var vitality = Rect("PlayerVitality", gameplay, Vector2.up, new Vector2(24f, -18f), new Vector2(544f, 140f));
            // 먹 배경이 화면 밖으로 잘리지 않도록, 좌측 패널의 시작선을 같은 여백에 맞춘다.
            Vector2 vitalityInkPosition = new Vector2(leftInkInset - vitality.anchoredPosition.x, -4f);
            InkPanel("VitalityInk", vitality, Vector2.up, vitalityInkPosition, vitalityInkSize);
            if (healthIcon != null)
            {
                var healthImage = Box("HealthIcon", vitality, Vector2.up, new Vector2(8f, -26f), new Vector2(40f, 40f), Color.white).GetComponent<UnityEngine.UI.Image>();
                healthImage.sprite = healthIcon;
                healthImage.preserveAspect = true;
            }
            healthFill = ResourceBar("Health", vitality, new Vector2(54f, -30f), new Vector2(344f, 32f), playerBarFrame, healthBarTexture, healthGreen, 23f);
            energyFill = ResourceBar("Energy", vitality, new Vector2(54f, -80f), new Vector2(344f, 32f), playerBarFrame, energyBarTexture, energyBlue, 23f);
            healthValue = Label("HealthValue", vitality, "100 / 100", new Vector2(410f, -28f), new Vector2(110f, 36f), 24f, paper);
            energyValue = Label("EnergyValue", vitality, "100 / 100", new Vector2(410f, -78f), new Vector2(110f, 36f), 24f, paper);
            healthValue.alignment = energyValue.alignment = TextAlignmentOptions.Right;
            healthValue.textWrappingMode = energyValue.textWrappingMode = TextWrappingModes.NoWrap;

            var objectivePanel = InkPanel("Objective", gameplay, Vector2.one, new Vector2(-32f, -24f), new Vector2(384f, 144f));
            // 두 줄의 중심과 먹 배경의 중심을 맞추고 흐린 가장자리에는 여백을 둔다.
            chapter = Label("Chapter", objectivePanel, "01  /  산신각으로 가는 길", new Vector2(56f, -26f), new Vector2(272f, 30f), 20f, gold);
            objective = Label("Goal", objectivePanel, "산신각에 올라 기도를 올려라", new Vector2(56f, -64f), new Vector2(272f, 60f), 22f, paper);
            chapter.alignment = objective.alignment = TextAlignmentOptions.Center;

            var gourd = Rect("HealingGourd", gameplay, Vector2.zero, new Vector2(40f, 24f), new Vector2(276f, 142f));
            Vector2 gourdInkPosition = new Vector2(leftInkInset - gourd.anchoredPosition.x, (gourdInkSize.y - gourd.sizeDelta.y) * .5f);
            InkPanel("GourdInk", gourd, Vector2.up, gourdInkPosition, gourdInkSize);
            var icon = Box("GourdIcon", gourd, Vector2.up, new Vector2(16f, -18f), new Vector2(88f, 106f), Color.white).GetComponent<UnityEngine.UI.Image>();
            icon.sprite = gourdIcon; icon.preserveAspect = true;
            Label("GourdName", gourd, "호리병", new Vector2(122f, -27f), new Vector2(128f, 42f), 28f, paper, true);
            var key = Box("GourdKey", gourd, Vector2.up, new Vector2(122f, -83f), new Vector2(30f, 30f), paper);
            Label("Key", key, "E", Vector2.zero, new Vector2(30f, 30f), 19f, new Color(0.05f, 0.1f, 0.1f)).alignment = TextAlignmentOptions.Center;
            recoveryHint = Label("RecoveryHint", gourd, "3 / 5", new Vector2(166f, -80f), new Vector2(88f, 38f), 28f, paper);
            interactionPanel = InkPanel("ShrineInteraction", gameplay, new Vector2(0.5f, 0f), new Vector2(0f, 158f), new Vector2(700f, 66f));
            interactionLabel = Label("InteractionText", interactionPanel, "", new Vector2(28f, -12f), new Vector2(644f, 42f), 22f, jade);
            interactionLabel.alignment = TextAlignmentOptions.Center;

            bossPanel = InkPanel("GuardianVitality", gameplay, new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(930f, 140f));
            Label("BossCaption", bossPanel, "산문을 지키는 자", new Vector2(32f, -12f), new Vector2(866f, 26f), 17f, paper).alignment = TextAlignmentOptions.Center;
            bossName = Label("BossName", bossPanel, "천하대장군", new Vector2(32f, -38f), new Vector2(866f, 40f), 32f, paper, true);
            bossName.alignment = TextAlignmentOptions.Center;
            bossFill = ResourceBar("Boss", bossPanel, new Vector2(40f, -84f), new Vector2(850f, 38f), bossBarFrame, bossBarTexture, new Color(0.73f, 0.21f, 0.15f), 34f);
            var warningPanel = Box("AttackNotice", gameplay, new Vector2(0.5f, 1f), new Vector2(0f, -164f), new Vector2(760f, 38f), Color.clear);
            warning = Label("Warning", warningPanel, "", new Vector2(0f, 0f), new Vector2(760f, 38f), 19f, gold);
            warning.alignment = TextAlignmentOptions.Center;
            var noticePanel = Box("Notification", gameplay, new Vector2(0.5f, 1f), new Vector2(0f, -212f), new Vector2(820f, 44f), Color.clear);
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
            else Label("Title", panel, "천지인", new Vector2(80f, -215f), new Vector2(550f, 160f), 120f, paper, true);
            Label("Subtitle", panel, "산문을 지키는 자", new Vector2(90f, -412f), new Vector2(450f, 50f), 30f, paper, true);
            Box("TitleRule", panel, Vector2.up, new Vector2(90f, -492f), new Vector2(76f, 2f), gold);
            var begin = CreateButton("BeginJourney", panel, "여정 시작하기", new Vector2(60f, -534f), new Vector2(450f, 72f), Color.white, game.BeginJourney);
            begin.fontSize = 27f;
            Label("Selected", begin.transform.parent, "◇", new Vector2(28f, -23f), new Vector2(32f, 32f), 25f, jade);
            var settings = CreateButton("AudioSettings", panel, "소리 설정", new Vector2(60f, -614f), new Vector2(450f, 72f), new Color(0.65f, 0.8f, 0.76f), OpenAudioSettings);
            settings.fontSize = 27f;
            CreateButton("QuitGame", panel, "게임 종료", new Vector2(60f, -766f), new Vector2(450f, 60f), new Color(0.72f, 0.63f, 0.51f), game.QuitGame);
            Label("EnterHint", panel, "방향키  이동    ENTER  선택", new Vector2(90f, -850f), new Vector2(450f, 28f), 17f, gold);
            var seal = Label("ChapterSeal", introduction, "01\nMOUNTAIN PASS", new Vector2(-50f, -65f), new Vector2(240f, 80f), 18f, new Color(0.08f, 0.15f, 0.15f));
            seal.rectTransform.anchorMin = seal.rectTransform.anchorMax = seal.rectTransform.pivot = Vector2.one;
            seal.alignment = TextAlignmentOptions.Right;
        }

        private void BuildResult()
        {
            result = FullPanel("Result", canvas, new Color(0.025f, 0.045f, 0.06f, 0.9f), true);
            var panel = Box("ResultColumn", result, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(800f, 470f), Color.clear);
            pauseEyebrowInk = InkPanel("PauseEyebrowInk", panel, Vector2.up, new Vector2(310f, 2f), new Vector2(180f, 64f));
            pauseTitleInk = InkPanel("PauseTitleInk", panel, Vector2.up, new Vector2(210f, -64f), new Vector2(380f, 116f));
            pauseDescriptionInk = InkPanel("PauseDescriptionInk", panel, Vector2.up, new Vector2(0f, -173f), new Vector2(800f, 132f));
            resultEyebrow = Label("ResultEyebrow", panel, "", new Vector2(0f, -10f), new Vector2(800f, 35f), 16f, gold);
            resultTitle = Label("ResultTitle", panel, "", new Vector2(0f, -80f), new Vector2(800f, 85f), 55f, paper, true);
            resultDescription = Label("ResultDescription", panel, "", new Vector2(40f, -191f), new Vector2(720f, 96f), 24f, muted);
            resultEyebrow.alignment = resultTitle.alignment = resultDescription.alignment = TextAlignmentOptions.Center;
            primaryCaption = CreateButton("PrimaryAction", panel, "", new Vector2(102f, -339f), new Vector2(285f, 58f), jade, PrimaryAction);
            secondaryCaption = CreateButton("SecondaryAction", panel, "", new Vector2(413f, -339f), new Vector2(285f, 58f), new Color(0.16f, 0.23f, 0.25f), SecondaryAction);
            pauseSettingsButton = (RectTransform)CreateButton("AudioSettings", panel, "소리 설정", new Vector2(102f, -411f), new Vector2(285f, 58f), new Color(0.16f, 0.23f, 0.25f), OpenAudioSettings).transform.parent;
            pauseQuitButton = (RectTransform)CreateButton("QuitGame", panel, "게임 종료", new Vector2(413f, -411f), new Vector2(285f, 58f), new Color(0.35f, 0.25f, 0.19f), game.QuitGame).transform.parent;
            pauseControls = InkPanel("PauseControls", result, new Vector2(0.5f, 0f), new Vector2(0f, 27f), new Vector2(1280f, 96f));
            pauseControlsText = Label("Controls", pauseControls, "WASD 이동    Shift 달리기    마우스 시점    좌클릭 / J 공격    Space 회피\nTab 주시    E 호리병    F 상호작용    Esc 메뉴", Vector2.zero, Vector2.zero, 21f, paper);
            pauseControlsText.rectTransform.anchorMin = Vector2.zero; pauseControlsText.rectTransform.anchorMax = Vector2.one;
            pauseControlsText.rectTransform.offsetMin = new Vector2(24f, 12f); pauseControlsText.rectTransform.offsetMax = new Vector2(-24f, -12f);
            pauseControlsText.alignment = TextAlignmentOptions.Center; pauseControlsText.lineSpacing = 5f;
        }

        private void BuildCinematic()
        {
            cinematic = FullPanel("BossIntroduction", canvas, Color.clear);
            Box("LetterboxTop", cinematic, Vector2.up, Vector2.zero, new Vector2(1600f, 85f), Color.black);
            Box("LetterboxBottom", cinematic, Vector2.zero, Vector2.zero, new Vector2(1600f, 110f), Color.black);
            var title = Box("GuardianTitle", cinematic, new Vector2(0.5f, 0f), new Vector2(0f, 142f), new Vector2(900f, 100f), Color.clear);
            Label("GuardianEyebrow", title, "산문을 지키는 자", Vector2.zero, new Vector2(900f, 32f), 19f, gold).alignment = TextAlignmentOptions.Center;
            Label("GuardianName", title, "천하대장군", new Vector2(0f, -38f), new Vector2(900f, 60f), 44f, paper, true).alignment = TextAlignmentOptions.Center;
            var skip = Label("SkipHint", cinematic, "Space / Esc  건너뛰기", new Vector2(-50f, 30f), new Vector2(300f, 35f), 19f, muted);
            skip.rectTransform.anchorMin = skip.rectTransform.anchorMax = skip.rectTransform.pivot = Vector2.right;
            skip.alignment = TextAlignmentOptions.Right;
        }

        private void PrimaryAction()
        { if (game.State == PassGameFlow.RunState.Paused) game.TogglePause(); else if (game.State == PassGameFlow.RunState.Defeat) game.RetryCheckpoint(); else game.BeginJourney(); }
        private void SecondaryAction()
        { game.ReturnToTitle(); }

        public void OpenAudioSettings()
        {
            if (game.IsTransitioning || (game.State != PassGameFlow.RunState.Intro && game.State != PassGameFlow.RunState.Paused)) return;
            var menu = game.State == PassGameFlow.RunState.Intro ? introduction : result;
            var controls = menu.GetComponent<CanvasGroup>();
            if (controls == null) controls = menu.gameObject.AddComponent<CanvasGroup>();
            var returnButton = game.State == PassGameFlow.RunState.Intro
                ? introduction.Find("TitleColumn/AudioSettings").gameObject : pauseSettingsButton.gameObject;
            audioSettings.Open(controls, returnButton);
        }

        private void Update()
        {
            if (game == null || canvas == null) return;
            ResizeHudPanels();
            bool intro = game.State == PassGameFlow.RunState.Intro;
            bool presenting = game.State == PassGameFlow.RunState.Cinematic;
            SetVisible(introduction, intro); SetVisible(gameplay, !intro && !presenting); SetVisible(cinematic, presenting);
            bool showResult = game.State == PassGameFlow.RunState.Paused || game.State == PassGameFlow.RunState.Defeat || game.State == PassGameFlow.RunState.Victory;
            SetVisible(result, showResult);
            if (previousState != game.State)
            {
                if (IsAudioSettingsOpen) audioSettings.Close();
                previousState = game.State; RefreshResult();
                EventSystem.current?.SetSelectedGameObject(intro ? introduction.Find("TitleColumn/BeginJourney").gameObject
                    : showResult ? primaryCaption.transform.parent.gameObject : null);
            }
            if (displayedHealth != game.PlayerHealth.Current || displayedMaximum != game.PlayerHealth.Maximum)
            {
                displayedHealth = game.PlayerHealth.Current; displayedMaximum = game.PlayerHealth.Maximum;
                healthFill.fillAmount = game.PlayerHealth.Fraction;
                healthValue.SetText("{0:0} / {1:0}", Mathf.Ceil(displayedHealth), displayedMaximum);
            }
            if (game.Energy != null && (displayedEnergy != game.Energy.Current || displayedEnergyMaximum != game.Energy.Maximum))
            {
                displayedEnergy = game.Energy.Current; displayedEnergyMaximum = game.Energy.Maximum;
                energyFill.fillAmount = game.Energy.Fraction;
                energyValue.SetText("{0:0} / {1:0}", Mathf.Ceil(displayedEnergy), displayedEnergyMaximum);
            }
            bool nearShrine = game.Shrine != null && game.Shrine.CanInteract;
            bool praying = game.Shrine != null && game.Shrine.IsPraying;
            if (game.Gourd != null && (displayedUses != game.Gourd.RemainingUses || displayedMaximumUses != game.Gourd.MaximumUses || displayedHealAmount != game.Gourd.HealAmount))
            {
                displayedUses = game.Gourd.RemainingUses; displayedMaximumUses = game.Gourd.MaximumUses; displayedHealAmount = game.Gourd.HealAmount;
                recoveryHint.SetText("{0:0} / {1:0}", displayedUses, displayedMaximumUses);
            }
            SetVisible(interactionPanel, (nearShrine || praying) && game.IsRunning && !UsesMobileControls);
            SetLabel(interactionLabel, praying ? "산신각에 기도를 올리는 중" : "F  산신각에 기도하기  ·  생명력 회복 · 호리병 충전");
            SetLabel(chapter, game.InEncounter ? "03  /  산문의 수문장" : game.ReachedShrine ? "02  /  산문 너머" : "01  /  산신각으로 가는 길");
            SetLabel(objective, game.ObjectiveTitle);
            SetVisible(bossPanel, game.InEncounter && game.State != PassGameFlow.RunState.Victory);
            if (game.Boss.Health != null)
            {
                if (displayedBossHealth != game.Boss.Health.Fraction)
                { displayedBossHealth = game.Boss.Health.Fraction; bossFill.fillAmount = displayedBossHealth; }
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
                marker.anchorMin = marker.anchorMax = new Vector2(Mathf.Clamp(viewport.x, 0.08f, 0.92f), Mathf.Clamp(viewport.y, UsesMobileControls ? .58f : .24f, .77f));
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
            SetVisible(pauseEyebrowInk, paused);
            SetVisible(pauseTitleInk, paused);
            SetVisible(pauseDescriptionInk, paused);
            SetVisible(pauseControls, paused);
            SetVisible(pauseSettingsButton, paused);
            SetVisible(pauseQuitButton, paused);
            bool won = game.State == PassGameFlow.RunState.Victory;
            resultEyebrow.text = paused ? "숨 고르기" : won ? "산문 개방" : "다시 산신각으로";
            resultTitle.text = paused ? "잠시 멈춤" : won ? "길을 열었습니다" : "다시 일어설 시간";
            resultDescription.text = paused ? "붉은 원을 벗어나고, 다가오는 파동은 구르기로 통과하세요.\n보스가 회복하는 순간이 연격의 기회입니다."
                : won ? $"수문장의 귀기가 걷히고 산문에 바람이 돌아왔습니다.\n여정  {Mathf.FloorToInt(game.Elapsed / 60f):00}:{Mathf.FloorToInt(game.Elapsed % 60f):00}    ·    명중  {game.PlayerCombat.HitsLanded}회"
                : "수문장은 다시 잠들었습니다.\n산신각의 가호를 받아 다시 도전하세요.";
            primaryCaption.text = paused ? "여정 계속하기" : won ? "산길 다시 걷기" : "다시 도전하기";
            secondaryCaption.text = "타이틀로 돌아가기";
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
            var brush = panelInkTexture != null ? panelInkTexture : inkBrush;
            var rect = Box(title, parent, anchor, position, size, brush != null ? Color.white : new Color(0.035f, 0.07f, 0.08f, 0.88f));
            var image = rect.GetComponent<UnityEngine.UI.Image>();
            image.sprite = brush; image.type = UnityEngine.UI.Image.Type.Sliced;
            FitInkBorder(image, size.y);
            return rect;
        }
        private UnityEngine.UI.Image ResourceBar(string title, RectTransform parent, Vector2 position, Vector2 size, Sprite frame, Sprite texture, Color color, float inset)
        {
            var trackSize = new Vector2(size.x - inset * 2f, size.y - 14f);
            var track = Box(title + "Track", parent, Vector2.up, position + new Vector2(inset, -7f), trackSize, new Color(0.025f, 0.045f, 0.045f, .98f));
            var fill = Box(title + "Fill", track, Vector2.up, Vector2.zero, trackSize, texture != null ? Color.white : color).GetComponent<UnityEngine.UI.Image>();
            fill.sprite = texture; fill.type = UnityEngine.UI.Image.Type.Filled;
            fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal; fill.fillOrigin = 0;
            // 프레임은 크기를 유지하고 내부 채움만 잘라서, 낮은 체력에서도 장식과 질감이 찌그러지지 않는다.
            var border = Box(title + "Border", parent, Vector2.up, position, size, frame != null ? Color.white : gold).GetComponent<UnityEngine.UI.Image>();
            border.sprite = frame; border.type = UnityEngine.UI.Image.Type.Sliced; border.pixelsPerUnitMultiplier = 2f;
            return fill;
        }
        private void LoadHudArtwork()
        {
            if (healthIcon == null) healthIcon = Resources.Load<Sprite>("CheonHud/T_HealthIcon");
            foreach (var sprite in Resources.LoadAll<Sprite>("CheonHud/T_HudBars"))
            {
                switch (sprite.name)
                {
                    case "PlayerFrame": if (playerBarFrame == null) playerBarFrame = sprite; break;
                    case "BossFrame": if (bossBarFrame == null) bossBarFrame = sprite; break;
                    case "HealthFill": if (healthBarTexture == null) healthBarTexture = sprite; break;
                    case "EnergyFill": if (energyBarTexture == null) energyBarTexture = sprite; break;
                    case "BossFill": if (bossBarTexture == null) bossBarTexture = sprite; break;
                    case "InkBacking": if (panelInkTexture == null) panelInkTexture = sprite; break;
                }
            }
        }
        public static void FitInkBorder(UnityEngine.UI.Image image, float height)
        {
            if (image.sprite == null || height <= 0f) return;
            // 투명한 붓 가장자리가 작은 패널 높이를 모두 차지하지 않도록 중심 면적을 남긴다.
            float edgeHeight = image.sprite.border.y + image.sprite.border.w;
            image.pixelsPerUnitMultiplier = Mathf.Max(2f, edgeHeight / (height * .4f));
        }
        private void ResizeHudPanels()
        {
            float width = UsesMobileControls ? gameplay.rect.width : canvas.rect.width;
            if (Mathf.Approximately(width, previousCanvasWidth)) return;
            previousCanvasWidth = width;
            // 안내 문구의 실제 폭에 먹 가장자리 여백만 더해 불필요하게 긴 배경을 줄인다.
            float controlsWidth = pauseControlsText.GetPreferredValues().x + 128f;
            pauseControls.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Min(controlsWidth, width - 64f));
            // 화면 비율이 좁아져도 좌측 호리병 패널과 보스 게이지가 겹치지 않도록 여백을 확보한다.
            float panelWidth = UsesMobileControls ? Mathf.Clamp(width - 860f, 420f, 760f) : Mathf.Clamp(width - 660f, 540f, 930f);
            bossPanel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, panelWidth);
            ((RectTransform)bossPanel.Find("BossBorder")).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, panelWidth - 80f);
            var track = (RectTransform)bossFill.transform.parent;
            track.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, panelWidth - 148f);
            bossFill.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, track.rect.width);
            bossName.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, panelWidth - 64f);
            ((RectTransform)bossPanel.Find("BossCaption")).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, panelWidth - 64f);
        }
        public void ApplyMobileLayout()
        {
            // 화면 높이를 기준으로 배율을 맞춰 19.5:9에서도 900 높이의 메뉴가 잘리지 않게 한다.
            canvas.GetComponent<UnityEngine.UI.CanvasScaler>().matchWidthOrHeight = 1f;
            if (titleBackground != null)
            {
                var background = introduction.GetComponent<UnityEngine.UI.Image>();
                background.sprite = null; background.color = Color.clear;
                var artwork = FullPanel("MobileTitleArtwork", introduction, Color.white);
                artwork.SetAsFirstSibling();
                artwork.GetComponent<UnityEngine.UI.Image>().sprite = titleBackground;
                var aspect = artwork.gameObject.AddComponent<UnityEngine.UI.AspectRatioFitter>();
                aspect.aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.EnvelopeParent;
                aspect.aspectRatio = titleBackground.rect.width / titleBackground.rect.height;
            }
            StretchMobileLetterbox((RectTransform)cinematic.Find("LetterboxTop"), true, 85f);
            StretchMobileLetterbox((RectTransform)cinematic.Find("LetterboxBottom"), false, 110f);
            gameplay.Find("HealingGourd").gameObject.SetActive(false);
            var vitality = (RectTransform)gameplay.Find("PlayerVitality");
            vitality.localScale = Vector3.one * .85f;
            vitality.anchoredPosition = new Vector2(32f, -18f);
            healthValue.gameObject.SetActive(false); energyValue.gameObject.SetActive(false);
            var healthIconRect = vitality.Find("HealthIcon") as RectTransform;
            if (healthIconRect != null) healthIconRect.anchoredPosition = new Vector2(36f, -26f);
            var energyIconRect = vitality.Find("Test_EnergyIcon") as RectTransform;
            if (energyIconRect != null) energyIconRect.anchoredPosition = new Vector2(43.5f, -83f);
            // 수치가 있던 공간까지 게이지를 늘리고 아이콘은 먹 패널의 진한 영역 안쪽에 둔다.
            ResizeMobilePlayerBar(vitality, "Health");
            ResizeMobilePlayerBar(vitality, "Energy");
            previousCanvasWidth = -1f;
            ((RectTransform)gameplay.Find("Objective")).anchoredPosition = new Vector2(-150f, -24f);
            pauseControlsText.text = "왼쪽 스틱 걷기    이동 중 달리기 버튼 · 정지 중 납도 버튼\n오른쪽 드래그 시점    공격 · 회피 · 회복 · 주시    메뉴 일시정지";
            introduction.Find("TitleColumn/EnterHint").GetComponent<TMP_Text>().text = "버튼을 터치하여 선택";
            var skip = cinematic.Find("SkipHint").GetComponent<TMP_Text>();
            skip.text = "건너뛰기"; skip.raycastTarget = true;
            skip.rectTransform.sizeDelta = new Vector2(230f, 70f);
            var button = skip.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = skip;
            button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
            button.onClick.AddListener(() => { if (!game.IsTransitioning) game.Entrance?.SkipPresentation(); });
        }

        public void SetMobileSafeArea(Vector2 minimum, Vector2 maximum)
        {
            gameplay.anchorMin = minimum; gameplay.anchorMax = maximum;
            gameplay.offsetMin = gameplay.offsetMax = Vector2.zero;
            Canvas.ForceUpdateCanvases();
            var titleColumn = (RectTransform)introduction.Find("TitleColumn");
            titleColumn.anchorMin = titleColumn.anchorMax = titleColumn.pivot = new Vector2(0f, .5f);
            float safeHeight = (maximum.y - minimum.y) * canvas.rect.height;
            titleColumn.localScale = Vector3.one * Mathf.Min(1f, safeHeight / 900f);
            titleColumn.anchoredPosition = new Vector2(minimum.x * canvas.rect.width,
                (minimum.y + maximum.y - 1f) * canvas.rect.height * .5f);
            ((RectTransform)result.Find("ResultColumn")).anchoredPosition = new Vector2(
                (minimum.x + maximum.x - 1f) * canvas.rect.width * .5f,
                (minimum.y + maximum.y - 1f) * canvas.rect.height * .5f);
            pauseControls.anchoredPosition = new Vector2(0f, 27f + minimum.y * canvas.rect.height);
            cinematic.Find("SkipHint").GetComponent<RectTransform>().anchoredPosition =
                new Vector2(-50f - (1f - maximum.x) * canvas.rect.width, 30f + minimum.y * canvas.rect.height);
        }

        private static void StretchMobileLetterbox(RectTransform rect, bool top, float height)
        {
            float edge = top ? 1f : 0f;
            rect.anchorMin = new Vector2(0f, edge); rect.anchorMax = new Vector2(1f, edge);
            rect.pivot = new Vector2(.5f, edge); rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, height);
        }

        private static void ResizeMobilePlayerBar(RectTransform vitality, string prefix)
        {
            var border = (RectTransform)vitality.Find(prefix + "Border");
            var track = (RectTransform)vitality.Find(prefix + "Track");
            border.anchoredPosition = new Vector2(86f, border.anchoredPosition.y);
            border.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 460f);
            track.anchoredPosition = new Vector2(109f, track.anchoredPosition.y);
            track.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 414f);
            ((RectTransform)track.GetChild(0)).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 414f);
        }

        private TMP_Text Label(string title, Transform parent, string text, Vector2 position, Vector2 size, float pointSize, Color color, bool useTitleFont = false)
        {
            var label = Rect(title, parent, Vector2.up, position, size).gameObject.AddComponent<TextMeshProUGUI>();
            bool hasTitleFont = useTitleFont && titleFont != null;
            label.font = hasTitleFont ? titleFont : font;
            var material = hasTitleFont ? titleTextMaterial : textMaterial;
            if (material != null) label.fontSharedMaterial = material;
            if (hasTitleFont) label.characterSpacing = titleCharacterSpacing;
            label.text = text; label.fontSize = pointSize; label.color = color; label.raycastTarget = false;
            label.alignment = text.Contains("\n") ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.Left;
            if (text.Contains("\n")) label.lineSpacing = 4f;
            label.textWrappingMode = TextWrappingModes.Normal; return label;
        }
        private TMP_Text CreateButton(string title, Transform parent, string text, Vector2 position, Vector2 size, Color color, UnityEngine.Events.UnityAction action)
        {
            var rect = Box(title, parent, Vector2.up, position, size, color);
            var image = rect.GetComponent<UnityEngine.UI.Image>(); image.raycastTarget = true;
            if (panelInkTexture != null) { image.sprite = panelInkTexture; image.type = UnityEngine.UI.Image.Type.Sliced; FitInkBorder(image, size.y); }
            else if (inkBrush != null) image.sprite = inkBrush;
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image; button.onClick.AddListener(action);
            rect.gameObject.AddComponent<PassButtonHoverAudio>().Configure(button, game.Audio);
            var colors = button.colors; colors.highlightedColor = new Color(1.18f, 1.18f, 1.18f); colors.pressedColor = new Color(0.75f, 0.8f, 0.8f); button.colors = colors;
            var caption = Label("Caption", rect, text, Vector2.zero, size, 24f, paper);
            caption.margin = new Vector4(24f, 6f, 24f, 6f);
            caption.alignment = TextAlignmentOptions.Center; return caption;
        }
        private void OnDestroy()
        {
            if (textMaterial != null) Destroy(textMaterial);
            if (titleTextMaterial != null) Destroy(titleTextMaterial);
        }
    }
}
