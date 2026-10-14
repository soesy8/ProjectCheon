using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Cheon.Testing
{
    public class Test_CheonHud : MonoBehaviour
    {
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private Test_CheonSession session;
        [SerializeField] private Test_CheonPlayer player;
        [SerializeField] private Test_CheonBoss boss;
        [SerializeField] private Test_CheonAudio sound;
        private static readonly Color Ink = new Color(.045f, .085f, .12f, .92f);
        private static readonly Color Paper = new Color(.92f, .91f, .82f);
        private static readonly Color Gold = new Color(.88f, .72f, .4f);
        private static readonly Color Jade = new Color(.38f, .9f, .76f);
        private RectTransform root, overlay, dialog, bossPanel;
        private TMP_Text realm, mission, objective, notice, healthText, energyText, bossIntent, record, overlayTitle, overlayBody, buttonText, mute;
        private UnityEngine.UI.Image healthFill, energyFill, bossFill, noticeBack;
        private TMP_Text[] skillStates;
        private UnityEngine.UI.Image[] skillAccents;
        private UnityEngine.UI.Button primaryButton;

        private void Awake()
        {
            Test_CheonTextSettings.Ensure(font);
            GameObject canvasObject = new GameObject("Test_CheonCanvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            canvasObject.transform.SetParent(transform);
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 10;
            var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = .5f;
            root = (RectTransform)canvasObject.transform;
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                GameObject events = new GameObject("Test_EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform);
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            BuildHud(); BuildOverlay();
        }

        private RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var item = new GameObject(name, typeof(RectTransform)); item.transform.SetParent(parent, false);
            var rect = item.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = anchor; rect.pivot = anchor;
            rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }
        private UnityEngine.UI.Image Box(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Color color)
        {
            var image = Rect(name, parent, anchor, position, size).gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color; image.raycastTarget = false; return image;
        }
        private TMP_Text Label(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, string text, float fontSize, Color color, TextAlignmentOptions align = TextAlignmentOptions.TopLeft)
        {
            var label = Rect(name, parent, anchor, position, size).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font; label.text = text; label.fontSize = fontSize; label.color = color;
            label.alignment = align; label.raycastTarget = false; label.textWrappingMode = TextWrappingModes.Normal;
            return label;
        }
        private UnityEngine.UI.Image Bar(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            var back = Box(name, parent, Vector2.zero, position, size, new Color(.15f, .2f, .23f));
            var fill = Box(name + "Fill", back.transform, Vector2.zero, Vector2.zero, size, color);
            fill.type = UnityEngine.UI.Image.Type.Simple;
            return fill;
        }
        private void SetFill(UnityEngine.UI.Image image, float value, float width) { image.rectTransform.sizeDelta = new Vector2(width * Mathf.Clamp01(value), image.rectTransform.sizeDelta.y); }

        private void BuildHud()
        {
            Box("BrandBackground", root, Vector2.up, new Vector2(38, -26), new Vector2(268, 85), Ink);
            Box("RealmBackground", root, Vector2.one, new Vector2(-30, -26), new Vector2(310, 78), Ink);
            Box("BrandAccent", root, Vector2.up, new Vector2(38, -36), new Vector2(4, 63), Gold);
            Label("Brand", root, Vector2.up, new Vector2(54, -31), new Vector2(260, 46), "천 지 인", 32, Paper);
            Label("Chapter", root, Vector2.up, new Vector2(57, -77), new Vector2(260, 24), "구름 너머, 경계를 걷다", 13, Gold);
            realm = Label("Realm", root, Vector2.one, new Vector2(-40, -36), new Vector2(320, 35), "현세  /  청운 수련지", 19, Jade, TextAlignmentOptions.TopRight);
            mute = Label("SystemKeys", root, Vector2.one, new Vector2(-40, -75), new Vector2(320, 24), "ESC  일시정지      M  사운드", 12, Paper, TextAlignmentOptions.TopRight);
            var quest = Box("Quest", root, Vector2.up, new Vector2(38, -130), new Vector2(268, 126), Ink);
            Label("QuestIndex", quest.transform, Vector2.up, new Vector2(18, -15), new Vector2(220, 20), "첫 번째 수련  /  01", 11, Gold);
            mission = Label("Mission", quest.transform, Vector2.up, new Vector2(18, -42), new Vector2(235, 33), "경계를 넘는 자", 21, Paper);
            objective = Label("Objective", quest.transform, Vector2.up, new Vector2(18, -81), new Vector2(235, 43), "장승 사이의 문을 통과하세요.", 13, new Color(.71f, .8f, .79f));
            var vitals = Box("Vitals", root, Vector2.zero, new Vector2(38, 35), new Vector2(312, 122), Ink);
            Label("PlayerName", vitals.transform, Vector2.up, new Vector2(18, -12), new Vector2(260, 25), "청운  ·  견습 도사", 16, Paper);
            healthFill = Bar("Health", vitals.transform, new Vector2(18, 51), new Vector2(276, 8), new Color(.84f, .37f, .33f));
            energyFill = Bar("Energy", vitals.transform, new Vector2(18, 21), new Vector2(276, 5), Jade);
            healthText = Label("HealthText", vitals.transform, Vector2.zero, new Vector2(18, 65), new Vector2(276, 19), "생명", 11, Paper);
            energyText = Label("EnergyText", vitals.transform, Vector2.zero, new Vector2(18, 31), new Vector2(276, 19), "기력", 11, Paper);
            var skills = Rect("Skills", root, new Vector2(.5f, 0), new Vector2(0, 35), new Vector2(452, 91));
            var layout = skills.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            layout.spacing = 8; layout.childControlHeight = false; layout.childControlWidth = false; layout.childForceExpandHeight = false; layout.childForceExpandWidth = false;
            skillStates = new TMP_Text[4]; skillAccents = new UnityEngine.UI.Image[4];
            string[] keys = { "J / CLICK", "SPACE", "Q", "E" };
            string[] names = { "부채 연계", "바람 걸음", "천 · 화부", "지 · 봉인진" };
            for (int i = 0; i < 4; i++)
            {
                var tile = Box("Skill" + i, skills, Vector2.zero, Vector2.zero, new Vector2(107, 91), Ink);
                skillAccents[i] = Box("Accent", tile.transform, Vector2.up, Vector2.zero, new Vector2(107, 2), i > 1 ? Gold : Jade);
                Label("Key", tile.transform, Vector2.up, new Vector2(10, -10), new Vector2(90, 20), keys[i], 11, Gold);
                Label("Name", tile.transform, Vector2.up, new Vector2(10, -34), new Vector2(92, 26), names[i], 15, Paper);
                skillStates[i] = Label("State", tile.transform, Vector2.up, new Vector2(10, -65), new Vector2(92, 19), "준비", 11, Jade);
            }
            record = Label("Record", root, Vector2.right, new Vector2(-40, 43), new Vector2(230, 66), "수련 기록\n00:00  ·  회피 0회", 13, Paper, TextAlignmentOptions.BottomRight);
            noticeBack = Box("NoticeBackground", root, new Vector2(.5f, 0), new Vector2(0, 161), new Vector2(970, 52), Ink);
            notice = Label("Notice", root, new Vector2(.5f, 0), new Vector2(0, 161), new Vector2(940, 52), "", 18, Paper, TextAlignmentOptions.Center);
            var bossBack = Box("BossStatus", root, new Vector2(.5f, 1), new Vector2(0, -33), new Vector2(600, 95), Ink);
            bossPanel = bossBack.rectTransform;
            Label("BossName", bossPanel, new Vector2(.5f, 1), new Vector2(0, -9), new Vector2(560, 29), "타락한 수호장승", 22, Paper, TextAlignmentOptions.Center);
            bossFill = Bar("BossHealth", bossPanel, new Vector2(24, 38), new Vector2(552, 7), new Color(.85f, .3f, .36f));
            bossIntent = Label("BossIntent", bossPanel, new Vector2(.5f, 0), new Vector2(0, 10), new Vector2(560, 24), "", 12, Gold, TextAlignmentOptions.Center);
        }

        private void BuildOverlay()
        {
            overlay = Rect("Overlay", root, new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
            overlay.anchorMin = Vector2.zero; overlay.anchorMax = Vector2.one; overlay.offsetMin = overlay.offsetMax = Vector2.zero;
            var shade = overlay.gameObject.AddComponent<UnityEngine.UI.Image>(); shade.color = new Color(.02f, .05f, .08f, .56f); shade.raycastTarget = true;
            dialog = Box("Dialog", overlay, new Vector2(.5f, .5f), Vector2.zero, new Vector2(650, 456), Ink).rectTransform;
            Box("TopRule", dialog, new Vector2(.5f, 1), new Vector2(0, 0), new Vector2(650, 3), Gold);
            Label("Overline", dialog, new Vector2(.5f, 1), new Vector2(0, -32), new Vector2(580, 24), "첫 번째 수련     /     경계를 넘는 자", 13, Gold, TextAlignmentOptions.Center);
            overlayTitle = Label("Title", dialog, new Vector2(.5f, 1), new Vector2(0, -70), new Vector2(580, 92), "천 지 인", 64, Paper, TextAlignmentOptions.Center);
            overlayBody = Label("Body", dialog, new Vector2(.5f, 1), new Vector2(0, -178), new Vector2(560, 160), "구름 위 수련지에 낯선 기운이 스며들었다.\n경계를 넘어, 잠식된 수호장승을 깨워라.\n\nWASD 이동   ·   J / 클릭 공격   ·   SPACE 회피\nQ 화부  ·  E 봉인진  ·  F 사당 회복", 18, Paper, TextAlignmentOptions.Center);
            var buttonImage = Box("PrimaryButton", dialog, new Vector2(.5f, 0), new Vector2(0, 43), new Vector2(280, 52), Gold);
            buttonImage.raycastTarget = true;
            primaryButton = buttonImage.gameObject.AddComponent<UnityEngine.UI.Button>(); primaryButton.targetGraphic = buttonImage;
            primaryButton.onClick.AddListener(PrimaryAction);
            buttonText = Label("Text", buttonImage.transform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(260, 40), "수련 시작   [ ENTER ]", 18, Ink, TextAlignmentOptions.Center);
            Label("Footer", dialog, new Vector2(.5f, 0), new Vector2(0, 14), new Vector2(580, 20), "공격 키를 누르면 연계  ·  가까운 적을 자동 조준합니다", 11, new Color(.57f, .68f, .69f), TextAlignmentOptions.Center);
        }

        private void PrimaryAction()
        {
            if (session.CurrentChapter == Test_CheonSession.Chapter.Introduction) session.BeginTraining();
            else if (session.Paused) session.TogglePause();
            else session.Restart();
        }

        public void RefreshOverlay()
        {
            if (overlay == null) return;
            bool intro = session.CurrentChapter == Test_CheonSession.Chapter.Introduction;
            bool dead = session.CurrentChapter == Test_CheonSession.Chapter.Defeat;
            overlay.gameObject.SetActive(intro || dead || session.Paused);
            if (intro) return;
            overlayTitle.text = dead ? "다시, 한 걸음" : "잠시 고르는 숨";
            overlayTitle.fontSize = 46;
            overlayBody.text = dead ? "수련은 실패로 끝나지 않는다.\n장승의 붉은 예고를 살피고, 바람 걸음으로 피하자.\n\n봉인진으로 공격을 끊으면 기회가 열린다.\nR 키로 바로 다시 도전할 수 있습니다." : "경계는 그대의 발걸음을 기다린다.\n\nESC  이어하기     ·     R  처음부터\nM  사운드 켜기 / 끄기";
            buttonText.text = dead ? "다시 수련하기   [ R ]" : "수련 이어하기   [ ESC ]";
        }

        private void Update()
        {
            bool battle = session.CurrentChapter == Test_CheonSession.Chapter.Battle;
            bool victory = session.CurrentChapter == Test_CheonSession.Chapter.Victory;
            bossPanel.gameObject.SetActive(battle);
            realm.text = session.InSpirit ? "영계  /  뒤틀린 수련지" : "현세  /  청운 수련지";
            realm.color = session.InSpirit ? new Color(.84f, .66f, 1) : Jade;
            mission.text = victory ? "수호장승 정화 완료" : battle ? "잠식된 장승을 정화하라" : "경계를 넘는 자";
            objective.text = victory ? "문으로 돌아가 수련지를 둘러보세요.\nR  다시 도전" : battle ? "붉은 예고를 피하고 틈을 노리세요.\n봉인진은 장승의 공격을 끊습니다." : "장승 사이의 문을 통과하세요.\n사당 근처에서 F  회복";
            notice.text = session.Notice;
            noticeBack.enabled = !string.IsNullOrEmpty(session.Notice);
            SetFill(healthFill, player.HealthRatio, 276); SetFill(energyFill, player.EnergyRatio, 276);
            healthText.text = "생명  " + Mathf.CeilToInt(player.Health);
            energyText.text = "기력  " + Mathf.FloorToInt(player.Energy);
            SetFill(bossFill, boss.HealthRatio, 552); bossIntent.text = (boss.Enraged ? "광폭화  /  " : "") + boss.Intent;
            float[] remaining = { 0, player.DodgeRemaining, player.TalismanRemaining, player.SealRemaining };
            float[] cost = { 0, 27, 22, 38 };
            for (int i = 0; i < 4; i++)
            {
                skillStates[i].text = remaining[i] > 0 ? remaining[i].ToString("0.0") + "초" : player.Energy < cost[i] ? "기력 부족" : i == 0 ? "3연계" : "준비";
                skillStates[i].color = remaining[i] > 0 || player.Energy < cost[i] ? new Color(.56f, .61f, .62f) : Jade;
                skillAccents[i].color = remaining[i] > 0 ? new Color(.3f, .34f, .35f) : i > 1 ? Gold : Jade;
            }
            int seconds = Mathf.FloorToInt(session.BattleSeconds);
            record.text = (victory ? "정화 완료" : "수련 기록") + "\n" + (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00") + "  ·  회피 " + session.DodgedAttacks + "회";
            mute.text = "ESC  일시정지      M  사운드 " + (sound.Muted ? "OFF" : "ON");
        }
    }
}
