using Cheon.Prototype.MountainPass;
using UnityEngine;

namespace Cheon.MountainPass.Presentation
{
    // 일시정지 화면의 배경만 담당하며 입력과 상태 전환은 게임 진행 제어기에 맡긴다.
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Cheon.Tests.CombatPresentation", "Assembly-CSharp", "Test_PausePresentation")]
    public class PassPausePresentation : MonoBehaviour
    {
        [SerializeField, InspectorName("게임 진행")] private PassGameFlow game;
        [SerializeField, InspectorName("기존 HUD")] private PassHud hud;
        [SerializeField, Range(0f, 1f), InspectorName("검은 패널 불투명도")] private float panelOpacity = .7f;
        private UnityEngine.UI.Image panel;
        private Color originalPanelColor;

        public void Configure(PassGameFlow flow, PassHud display) { game=flow; hud=display; }

        private void Start()
        {
            if(game==null || hud==null) { enabled=false; return; }
            var result=hud.transform.Find("MountainPassHUD/Result");
            if(result==null) { enabled=false; return; }
            panel=result.GetComponent<UnityEngine.UI.Image>(); originalPanelColor=panel.color;
        }

        private void LateUpdate()
        {
            if(panel!=null) panel.color=game.State==PassGameFlow.RunState.Paused
                ? new Color(0f,0f,0f,panelOpacity) : originalPanelColor;
        }

        private void OnDisable() { if(panel!=null) panel.color=originalPanelColor; }
    }
}
