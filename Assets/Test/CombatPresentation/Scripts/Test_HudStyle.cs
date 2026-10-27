using Cheon.Prototype.MountainPass;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cheon.Tests.CombatPresentation
{
    // PassHud가 생성한 계층을 유지하므로 체력·도력·재도전 등의 기존 연결을 그대로 사용한다.
    [DefaultExecutionOrder(120)]
    public class Test_HudStyle : MonoBehaviour
    {
        [SerializeField, InspectorName("게임 진행")] private PassGameFlow game;
        [SerializeField, InspectorName("기존 HUD")] private PassHud hud;
        [SerializeField, InspectorName("옥색 생명력")] private Color health = new Color(.32f,.72f,.55f);
        [SerializeField, InspectorName("청옥색 도력")] private Color energy = new Color(.28f,.62f,.76f);
        [SerializeField, InspectorName("금색 테두리")] private Color gold = new Color(.71f,.58f,.36f);
        private RectTransform controls, objective, pauseHelp;
        private TMP_Text controlsText, controlsTitle;
        private CanvasGroup gameplayVisibility;
        private bool previousCompact;
        public void Configure(PassGameFlow flow, PassHud display) { game=flow; hud=display; }
        private RectTransform Find(string path) => hud.transform.Find("MountainPassHUD/"+path) as RectTransform;

        private void Start()
        {
            if (hud == null || game == null || Find("Gameplay") == null) { enabled=false; return; }
            gameplayVisibility=Find("Gameplay").gameObject.AddComponent<CanvasGroup>();
            RectTransform vitality=Find("Gameplay/PlayerVitality");
            vitality.sizeDelta=new Vector2(480,112);
            foreach(var image in vitality.GetComponentsInChildren<UnityEngine.UI.Image>())
            {
                if(image.name.EndsWith("Border")) image.color=gold;
                if(image.name=="HealthFill") image.color=health;
                if(image.name=="EnergyFill") image.color=energy;
            }
            Move(Find("Gameplay/PlayerVitality/HealthBorder"),new Vector2(52,-28));
            Move(Find("Gameplay/PlayerVitality/HealthTrack"),new Vector2(54,-30));
            Move(Find("Gameplay/PlayerVitality/EnergyBorder"),new Vector2(52,-68));
            Move(Find("Gameplay/PlayerVitality/EnergyTrack"),new Vector2(54,-70));
            Move(Find("Gameplay/PlayerVitality/HealthValue"),new Vector2(364,-24));
            Move(Find("Gameplay/PlayerVitality/EnergyValue"),new Vector2(364,-64));
            Find("Gameplay/PlayerVitality/HealthValue").sizeDelta=new Vector2(94,30);
            Find("Gameplay/PlayerVitality/EnergyValue").sizeDelta=new Vector2(94,30);
            AddIcon(vitality,"Test_VitalityIcon",new Vector2(20,-20),new Vector2(26,27),Test_HudIcon.IconKind.Vitality);
            AddIcon(vitality,"Test_EnergyIcon",new Vector2(20,-59),new Vector2(26,27),Test_HudIcon.IconKind.Fan);

            RectTransform gourd=Find("Gameplay/HealingGourd");
            gourd.sizeDelta=new Vector2(236,110);
            RectTransform oldIcon=Find("Gameplay/HealingGourd/GourdIcon");
            oldIcon.GetComponent<UnityEngine.UI.Image>().enabled=false;
            oldIcon.sizeDelta=new Vector2(79,86); oldIcon.anchoredPosition=new Vector2(14,-10);
            AddIcon(oldIcon,"Test_FlatGourd",Vector2.zero,oldIcon.sizeDelta,Test_HudIcon.IconKind.Gourd);
            Move(Find("Gameplay/HealingGourd/GourdName"),new Vector2(106,-20));
            Move(Find("Gameplay/HealingGourd/GourdKey"),new Vector2(108,-62));
            Move(Find("Gameplay/HealingGourd/RecoveryHint"),new Vector2(147,-62));

            RectTransform boss=Find("Gameplay/GuardianVitality");
            boss.anchoredPosition=new Vector2(0,29); boss.sizeDelta=new Vector2(840,91);
            Find("Gameplay/GuardianVitality/BossCaption").gameObject.SetActive(false);
            Move(Find("Gameplay/GuardianVitality/BossName"),new Vector2(15,-17));
            Move(Find("Gameplay/GuardianVitality/BossBorder"),new Vector2(28,-63));
            Move(Find("Gameplay/GuardianVitality/BossTrack"),new Vector2(30,-65));
            Find("Gameplay/GuardianVitality/BossBorder").GetComponent<UnityEngine.UI.Image>().color=gold;

            controls=Find("Gameplay/ControlHints"); objective=Find("Gameplay/Objective");
            controlsText=Find("Gameplay/ControlHints/Controls").GetComponent<TMP_Text>();
            controlsTitle=Find("Gameplay/ControlHints/ControlsTitle").GetComponent<TMP_Text>();
            // 조작법은 전투 중 축약하되 일시정지 화면에서 전체 내용을 다시 읽을 수 있게 한다.
            TMP_Text template=controlsText;
            var item=new GameObject("Test_PauseControls",typeof(RectTransform),typeof(TextMeshProUGUI));
            pauseHelp=(RectTransform)item.transform; pauseHelp.SetParent(Find("Result"),false);
            pauseHelp.anchorMin=pauseHelp.anchorMax=pauseHelp.pivot=new Vector2(.5f,0);
            pauseHelp.anchoredPosition=new Vector2(0,35); pauseHelp.sizeDelta=new Vector2(1280,70);
            var label=item.GetComponent<TMP_Text>(); label.font=template.font; label.fontSharedMaterial=template.fontSharedMaterial;
            label.fontSize=18; label.color=new Color(.86f,.85f,.76f); label.alignment=TextAlignmentOptions.Center; label.raycastTarget=false;
            label.text="WASD 이동    Shift 달리기    마우스 시점    좌클릭 / J 공격    Space 회피\nTab 주시    E 호리병    F 상호작용    Esc 메뉴";
            ApplyControls(game.InEncounter);
        }
        private void LateUpdate()
        {
            if(controls==null) return;
            // 일시정지 안내 아래로 전투 HUD가 비쳐 겹치지 않도록 표시만 숨긴다.
            gameplayVisibility.alpha=game.State==PassGameFlow.RunState.Paused?0f:1f;
            if(previousCompact!=game.InEncounter) ApplyControls(game.InEncounter);
            objective.gameObject.SetActive(!game.InEncounter);
            pauseHelp.gameObject.SetActive(game.State==PassGameFlow.RunState.Paused);
        }
        private void ApplyControls(bool compact)
        {
            previousCompact=compact;
            controlsTitle.gameObject.SetActive(!compact);
            controls.sizeDelta=new Vector2(382,compact?62:202);
            controlsText.rectTransform.anchoredPosition=new Vector2(28,compact?-19:-56);
            controlsText.rectTransform.sizeDelta=new Vector2(335,compact?30:136);
            controlsText.text=compact ? "Tab  주시    ·    Esc  메뉴" : "WASD  이동       Shift  달리기\n마우스  시점      좌클릭 / J  공격\nSpace  회피       Tab  주시\nE  호리병          F  상호작용\nEsc  메뉴";
        }
        private static void Move(RectTransform rect,Vector2 position) { if(rect!=null) rect.anchoredPosition=position; }
        private static void AddIcon(Transform parent,string title,Vector2 position,Vector2 size,Test_HudIcon.IconKind kind)
        {
            var item=new GameObject(title,typeof(RectTransform),typeof(Test_HudIcon));
            var rect=(RectTransform)item.transform; rect.SetParent(parent,false);
            rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.up; rect.anchoredPosition=position; rect.sizeDelta=size;
            item.GetComponent<Test_HudIcon>().SetKind(kind);
        }
    }
}
