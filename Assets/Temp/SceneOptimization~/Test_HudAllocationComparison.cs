using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using Object = UnityEngine.Object;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Cheon.Prototype.MountainPass
{
    public static class Test_HudAllocationComparison
    {
        public static object Main()
        {
            var current=UnityEngine.Object.FindFirstObjectByType<PassHud>();
            var data=new SerializedObject(current);
            var temporary=new GameObject("Test_HudAllocationComparison");temporary.SetActive(false);
            var previous=temporary.AddComponent<Test_HudBeforeOptimization>();
            previous.Configure((PassGameFlow)data.FindProperty("game").objectReferenceValue,
                (TMP_FontAsset)data.FindProperty("font").objectReferenceValue,(TMP_Settings)data.FindProperty("textSettings").objectReferenceValue);
            previous.enabled=false;temporary.SetActive(true);
            if(typeof(Test_HudBeforeOptimization).GetField("canvas",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(previous)==null)
                typeof(Test_HudBeforeOptimization).GetMethod("Awake",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(previous,null);
            var before=(Action)Delegate.CreateDelegate(typeof(Action),previous,typeof(Test_HudBeforeOptimization).GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Instance));
            var after=(Action)Delegate.CreateDelegate(typeof(Action),current,typeof(PassHud).GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Instance));
            for(int i=0;i<100;i++){before();after();}
            int iterations=1000;
            long start=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<iterations;i++)before();long oldBytes=GC.GetAllocatedBytesForCurrentThread()-start;
            start=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<iterations;i++)after();long newBytes=GC.GetAllocatedBytesForCurrentThread()-start;
            UnityEngine.Object.Destroy(temporary);
            var result=new{iterations,beforeBytesPerUpdate=oldBytes/(double)iterations,afterBytesPerUpdate=newBytes/(double)iterations};
            File.WriteAllText("Assets/Temp/SceneOptimization~/HudAllocation.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
            return result;
        }
    }
    public class Test_HudBeforeOptimization : MonoBehaviour
    {
        [SerializeField] private PassGameFlow game;
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private TMP_Settings textSettings;
        private readonly Color paper = new Color(0.94f, 0.91f, 0.82f);
        private readonly Color jade = new Color(0.35f, 0.88f, 0.73f);
        private readonly Color gold = new Color(0.84f, 0.69f, 0.43f);
        private readonly Color muted = new Color(0.64f, 0.73f, 0.73f);
        private RectTransform canvas, gameplay, introduction, result, bossPanel, marker, cinematic, interactionPanel;
        private UnityEngine.UI.Image healthFill, bossFill, damageFlash;
        private TMP_Text healthValue, recoveryHint, interactionLabel, objective, chapter, bossName, warning, notice, markerLabel;
        private TMP_Text resultEyebrow, resultTitle, resultDescription, primaryCaption, secondaryCaption;
        private float noticeUntil, noticeDuration, damageAlpha;
        private PassGameFlow.RunState previousState = (PassGameFlow.RunState)(-1);
        public static bool IsTyping => EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null
            && EventSystem.current.currentSelectedGameObject.TryGetComponent<TMP_InputField>(out var field) && field.isFocused;

        public void Configure(PassGameFlow flow, TMP_FontAsset koreanFont, TMP_Settings settings)
        { game = flow; font = koreanFont; textSettings = settings; }

        private void Awake()
        {
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
        }

        private void BuildGameplay()
        {
            gameplay = FullPanel("Gameplay", canvas, Color.clear);
            Label("Brand", gameplay, "천  /  CHEON", new Vector2(44f, -32f), new Vector2(330f, 42f), 28f, paper);
            Label("ChapterName", gameplay, "산문을 지키는 자", new Vector2(46f, -76f), new Vector2(330f, 30f), 15f, gold);
            var objectivePanel = Box("Objective", gameplay, Vector2.one, new Vector2(-44f, -40f), new Vector2(352f, 96f), new Color(0.035f, 0.07f, 0.08f, 0.82f));
            Box("Accent", objectivePanel, Vector2.up, Vector2.zero, new Vector2(3f, 96f), gold);
            chapter = Label("Chapter", objectivePanel, "01  /  산신각으로 가는 길", new Vector2(20f, -13f), new Vector2(315f, 25f), 13f, gold);
            objective = Label("Goal", objectivePanel, "산신각에 올라 기도를 올려라", new Vector2(20f, -43f), new Vector2(315f, 40f), 18f, paper);

            var vitality = Box("PlayerVitality", gameplay, Vector2.zero, new Vector2(44f, 55f), new Vector2(340f, 95f), new Color(0.035f, 0.07f, 0.08f, 0.86f));
            Label("VitalityLabel", vitality, "생명력", new Vector2(16f, -12f), new Vector2(130f, 25f), 14f, muted);
            healthValue = Label("HealthValue", vitality, "100 / 100", new Vector2(185f, -12f), new Vector2(140f, 25f), 15f, paper);
            healthValue.alignment = TextAlignmentOptions.Right;
            var healthTrack = Box("HealthTrack", vitality, Vector2.up, new Vector2(16f, -44f), new Vector2(308f, 7f), new Color(0.12f, 0.2f, 0.2f));
            healthFill = Box("HealthFill", healthTrack, Vector2.up, Vector2.zero, new Vector2(308f, 7f), jade).GetComponent<UnityEngine.UI.Image>();
            recoveryHint = Label("RecoveryHint", vitality, "회복 · 산신각에서 기도", new Vector2(16f, -61f), new Vector2(308f, 25f), 14f, gold);

            var hints = Box("ControlHints", gameplay, new Vector2(0.5f, 0f), new Vector2(0f, 15f), new Vector2(990f, 30f), new Color(0.025f, 0.05f, 0.06f, 0.68f));
            Label("Controls", hints, "WASD 이동    Shift 달리기·납도    Space 회피    좌클릭 / J 발도·연격    마우스 시점    Tab 주시    F 상호작용    Esc 메뉴", new Vector2(8f, -3f), new Vector2(974f, 26f), 13f, paper).alignment = TextAlignmentOptions.Center;
            interactionPanel = Box("ShrineInteraction", gameplay, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(630f, 52f), new Color(0.025f, 0.05f, 0.06f, 0.9f));
            interactionLabel = Label("InteractionText", interactionPanel, "", Vector2.zero, new Vector2(630f, 52f), 20f, jade);
            interactionLabel.alignment = TextAlignmentOptions.Center;

            bossPanel = Box("GuardianVitality", gameplay, new Vector2(1f, 0f), new Vector2(-44f, 55f), new Vector2(560f, 80f), new Color(0.025f, 0.045f, 0.06f, 0.82f));
            bossName = Label("BossName", bossPanel, "천하대장군  ·  산문의 수문장", new Vector2(15f, -10f), new Vector2(530f, 35f), 20f, paper);
            bossName.alignment = TextAlignmentOptions.Center;
            var bossTrack = Box("BossTrack", bossPanel, Vector2.up, new Vector2(18f, -57f), new Vector2(524f, 6f), new Color(0.18f, 0.09f, 0.08f));
            bossFill = Box("BossFill", bossTrack, Vector2.up, Vector2.zero, new Vector2(524f, 6f), new Color(0.82f, 0.3f, 0.2f)).GetComponent<UnityEngine.UI.Image>();
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
            introduction = FullPanel("Introduction", canvas, new Color(0.025f, 0.05f, 0.065f, 0.6f), true);
            var panel = Box("TitleColumn", introduction, Vector2.up, Vector2.zero, new Vector2(760f, 900f), new Color(0.025f, 0.05f, 0.065f, 0.76f));
            Label("Eyebrow", panel, "A TALE BEYOND THE MOUNTAIN GATE", new Vector2(80f, -94f), new Vector2(620f, 30f), 14f, gold);
            Label("Title", panel, "천", new Vector2(70f, -148f), new Vector2(570f, 178f), 138f, paper);
            Label("Subtitle", panel, "산문을 지키는 자", new Vector2(82f, -336f), new Vector2(610f, 62f), 42f, paper);
            Box("TitleRule", panel, Vector2.up, new Vector2(84f, -427f), new Vector2(76f, 3f), jade);
            Label("Story", panel, "등불을 따라 산신각에서 기도를 올려라.\n부채를 펼쳐 영적 산문 너머의 수문장을 깨워라.", new Vector2(84f, -464f), new Vector2(600f, 78f), 21f, muted);
            CreateButton("BeginJourney", panel, "산길에서 시작", new Vector2(84f, -590f), new Vector2(270f, 60f), jade, game.BeginJourney);
            CreateButton("BeginChallenge", panel, "수문장에게 도전", new Vector2(376f, -590f), new Vector2(270f, 60f), new Color(0.16f, 0.23f, 0.25f), game.BeginBossChallenge);
            Label("EnterHint", panel, "ENTER  산길에서 시작", new Vector2(84f, -675f), new Vector2(550f, 28f), 14f, gold);
            Label("PlayHint", panel, "마우스 시점  ·  공격키로 발도  ·  Shift 달리기·납도\nSpace 회피  ·  F 산신각에 기도", new Vector2(84f, -744f), new Vector2(610f, 58f), 16f, muted);
            Label("ChapterSeal", introduction, "01\nMOUNTAIN PASS", new Vector2(1310f, -65f), new Vector2(240f, 80f), 18f, paper).alignment = TextAlignmentOptions.Right;
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
            introduction.gameObject.SetActive(intro); gameplay.gameObject.SetActive(!intro && !presenting); cinematic.gameObject.SetActive(presenting);
            bool showResult = game.State == PassGameFlow.RunState.Paused || game.State == PassGameFlow.RunState.Defeat || game.State == PassGameFlow.RunState.Victory;
            result.gameObject.SetActive(showResult);
            if (previousState != game.State) { previousState = game.State; RefreshResult(); EventSystem.current?.SetSelectedGameObject(null); }
            healthFill.rectTransform.sizeDelta = new Vector2(308f * game.PlayerHealth.Fraction, 7f);
            healthFill.color = game.PlayerHealth.Fraction < 0.3f ? new Color(0.93f, 0.38f, 0.25f) : jade;
            healthValue.text = $"{Mathf.CeilToInt(game.PlayerHealth.Current)} / {game.PlayerHealth.Maximum:0}";
            bool nearShrine = game.Shrine != null && game.Shrine.CanInteract;
            bool praying = game.Shrine != null && game.Shrine.IsPraying;
            recoveryHint.text = nearShrine ? "F  산신각에 기도하여 회복" : "회복 · 산신각에서 기도";
            interactionPanel.gameObject.SetActive((nearShrine || praying) && game.IsRunning);
            interactionLabel.text = praying ? "산신각에 기도를 올리는 중" : "F  산신각에 기도하기  ·  생명력 회복";
            chapter.text = game.InEncounter ? "03  /  산문의 수문장" : game.ReachedShrine ? "02  /  산문 너머" : "01  /  산신각으로 가는 길";
            objective.text = game.ObjectiveTitle;
            bossPanel.gameObject.SetActive(game.InEncounter && game.State != PassGameFlow.RunState.Victory);
            if (game.Boss.Health != null)
            {
                bossFill.rectTransform.sizeDelta = new Vector2(524f * game.Boss.Health.Fraction, 6f);
                bossName.text = game.Boss.Enraged ? "천하대장군  ·  격노한 수문장" : "천하대장군  ·  산문의 수문장";
                warning.text = game.InEncounter && game.IsRunning ? game.Boss.AttackLabel : "";
                warning.color = game.Boss.State == PassBossController.BossState.Recovering ? jade : gold;
            }
            notice.color = new Color(paper.r, paper.g, paper.b, Mathf.Clamp01((noticeUntil - Time.unscaledTime) / Mathf.Min(0.7f, Mathf.Max(0.1f, noticeDuration))));
            damageAlpha = Mathf.MoveTowards(damageAlpha, 0f, Time.unscaledDeltaTime * 0.6f);
            damageFlash.color = new Color(0.7f, 0.08f, 0.04f, damageAlpha);
            marker.gameObject.SetActive(!game.InEncounter && game.IsRunning);
            if (marker.gameObject.activeSelf)
            {
                Vector3 viewport = Camera.main.WorldToViewportPoint(game.ObjectivePosition + Vector3.up * 2f);
                if (viewport.z < 0f) { viewport.x = viewport.x > 0.5f ? 0.08f : 0.92f; viewport.y = 0.5f; }
                marker.anchorMin = marker.anchorMax = new Vector2(Mathf.Clamp(viewport.x, 0.08f, 0.92f), Mathf.Clamp(viewport.y, 0.24f, 0.77f));
                marker.anchoredPosition = Vector2.zero;
                markerLabel.text = $"{(game.ReachedShrine ? "산문" : "산신각")}  {Vector3.Distance(game.Player.position, game.ObjectivePosition):0}m";
            }
        }

        private void RefreshResult()
        {
            bool paused = game.State == PassGameFlow.RunState.Paused;
            bool won = game.State == PassGameFlow.RunState.Victory;
            resultEyebrow.text = paused ? "숨 고르기" : won ? "산문 개방" : "다시 산신각으로";
            resultTitle.text = paused ? "잠시 멈춤" : won ? "길을 열었습니다" : "다시 일어설 시간";
            resultDescription.text = paused ? "붉은 원을 벗어나고, 다가오는 파동은 구르기로 통과하세요.\n보스가 회복하는 순간이 연격의 기회입니다."
                : won ? $"수문장의 귀기가 걷히고 산문에 바람이 돌아왔습니다.\n여정  {Mathf.FloorToInt(game.Elapsed / 60f):00}:{Mathf.FloorToInt(game.Elapsed % 60f):00}    ·    명중  {game.Player.GetComponent<PassPlayerCombat>().HitsLanded}회"
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
        private TMP_Text Label(string title, Transform parent, string text, Vector2 position, Vector2 size, float pointSize, Color color)
        {
            var label = Rect(title, parent, Vector2.up, position, size).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font; label.text = text; label.fontSize = pointSize; label.color = color; label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.Normal; return label;
        }
        private TMP_Text CreateButton(string title, Transform parent, string text, Vector2 position, Vector2 size, Color color, UnityEngine.Events.UnityAction action)
        {
            var rect = Box(title, parent, Vector2.up, position, size, color);
            var image = rect.GetComponent<UnityEngine.UI.Image>(); image.raycastTarget = true;
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image; button.onClick.AddListener(action);
            var colors = button.colors; colors.highlightedColor = new Color(1.18f, 1.18f, 1.18f); colors.pressedColor = new Color(0.75f, 0.8f, 0.8f); button.colors = colors;
            var caption = Label("Caption", rect, text, Vector2.zero, size, 19f, color == jade ? new Color(0.035f, 0.12f, 0.13f) : paper);
            caption.alignment = TextAlignmentOptions.Center; return caption;
        }
    }
}
