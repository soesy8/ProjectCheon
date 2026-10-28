using Cheon.Prototype.MountainPass;
using UnityEngine;
using UnityEngine.UI;

namespace Cheon.MountainPass.Presentation
{
    // PassHud가 생성한 계층을 유지하므로 체력·도력·재도전 등의 기존 연결을 그대로 사용한다.
    [DefaultExecutionOrder(120)]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Cheon.Tests.CombatPresentation", "Assembly-CSharp", "Test_HudStyle")]
    public class PassHudStyle : MonoBehaviour
    {
        [SerializeField, InspectorName("게임 진행")] private PassGameFlow game;
        [SerializeField, InspectorName("기존 HUD")] private PassHud hud;
        [SerializeField, InspectorName("옥색 생명력")] private Color health = new Color(.32f,.72f,.55f);
        [SerializeField, InspectorName("청옥색 도력")] private Color energy = new Color(.28f,.62f,.76f);
        [SerializeField, InspectorName("금색 테두리")] private Color gold = new Color(.71f,.58f,.36f);
        private RectTransform objective;
        private CanvasGroup gameplayVisibility;
        public void Configure(PassGameFlow flow, PassHud display) { game=flow; hud=display; }
        private RectTransform Find(string path) => hud.transform.Find("MountainPassHUD/"+path) as RectTransform;

        private void Start()
        {
            if (hud == null || game == null || Find("Gameplay") == null) { enabled=false; return; }
            gameplayVisibility=Find("Gameplay").gameObject.AddComponent<CanvasGroup>();
            RectTransform vitality=Find("Gameplay/PlayerVitality");
            // 원화 프레임의 치수와 여백은 PassHud에서 관리한다. 이곳에서는 아이콘만 보완한다.
            foreach(var image in vitality.GetComponentsInChildren<UnityEngine.UI.Image>())
            {
                if(image.sprite!=null) continue;
                if(image.name.EndsWith("Border")) image.color=gold;
                if(image.name=="HealthFill") image.color=health;
                if(image.name=="EnergyFill") image.color=energy;
            }
            AddIcon(vitality,"Test_VitalityIcon",new Vector2(23,-33),new Vector2(25,25),PassHudIcon.IconKind.Vitality);
            AddIcon(vitality,"Test_EnergyIcon",new Vector2(23,-83),new Vector2(25,25),PassHudIcon.IconKind.Fan);

            RectTransform boss=Find("Gameplay/GuardianVitality");
            boss.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,112);
            Find("Gameplay/GuardianVitality/BossCaption").gameObject.SetActive(false);
            Move(Find("Gameplay/GuardianVitality/BossName"),new Vector2(32,-14));
            Move(Find("Gameplay/GuardianVitality/BossBorder"),new Vector2(40,-60));
            Move(Find("Gameplay/GuardianVitality/BossTrack"),new Vector2(74,-67));

            // 조작 안내와 호리병 원화는 PassHud의 공통 구성을 그대로 사용한다.
            objective=Find("Gameplay/Objective");
        }
        private void LateUpdate()
        {
            if(objective==null) return;
            // 일시정지 안내 아래로 전투 HUD가 비쳐 겹치지 않도록 표시만 숨긴다.
            gameplayVisibility.alpha=game.State==PassGameFlow.RunState.Paused?0f:1f;
            objective.gameObject.SetActive(!game.InEncounter);
        }
        private static void Move(RectTransform rect,Vector2 position) { if(rect!=null) rect.anchoredPosition=position; }
        private static void AddIcon(Transform parent,string title,Vector2 position,Vector2 size,PassHudIcon.IconKind kind)
        {
            var item=new GameObject(title,typeof(RectTransform),typeof(PassHudIcon));
            var rect=(RectTransform)item.transform; rect.SetParent(parent,false);
            rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.up; rect.anchoredPosition=position; rect.sizeDelta=size;
            item.GetComponent<PassHudIcon>().SetKind(kind);
        }
    }
}
