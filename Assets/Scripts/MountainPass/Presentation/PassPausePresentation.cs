using Cheon.Prototype.MountainPass;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Cheon.MountainPass.Presentation
{
    // 원본 진행 코드를 복제하지 않고 테스트 씬의 일시정지 전환만 즉시 처리한다.
    [DefaultExecutionOrder(-50)]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Cheon.Tests.CombatPresentation", "Assembly-CSharp", "Test_PausePresentation")]
    public class PassPausePresentation : MonoBehaviour
    {
        [SerializeField, InspectorName("게임 진행")] private PassGameFlow game;
        [SerializeField, InspectorName("기존 HUD")] private PassHud hud;
        [SerializeField, Range(0f, 1f), InspectorName("검은 패널 불투명도")] private float panelOpacity = .7f;
        private PassScreenFade screenFade;
        private Image panel;
        private Color originalPanelColor;
        private Button resumeButton;
        private Button.ButtonClickedEvent originalAction, testAction;
        private bool bypassThisFrame;

        public void Configure(PassGameFlow flow, PassHud display) { game=flow; hud=display; }

        private void Start()
        {
            if(game==null || hud==null) { enabled=false; return; }
            screenFade=hud.GetComponent<PassScreenFade>();
            var result=hud.transform.Find("MountainPassHUD/Result");
            if(result==null) { enabled=false; return; }
            panel=result.GetComponent<Image>(); originalPanelColor=panel.color;
            resumeButton=result.Find("ResultColumn/PrimaryAction").GetComponent<Button>();
            originalAction=resumeButton.onClick;
            testAction=new Button.ButtonClickedEvent();
            testAction.AddListener(() =>
            {
                if(game.State==PassGameFlow.RunState.Paused) TogglePauseImmediately();
                else originalAction.Invoke();
            });
            resumeButton.onClick=testAction;
        }

        private void Update()
        {
            var keyboard=Keyboard.current;
            if(keyboard==null || !keyboard.escapeKey.wasPressedThisFrame || game.IsTransitioning) return;
            if(game.State!=PassGameFlow.RunState.Playing && game.State!=PassGameFlow.RunState.Paused) return;
            // 바로 다음 PassGameFlow.Update의 Esc 처리에서만 페이드 연결을 우회한다.
            game.SetScreenFade(null); bypassThisFrame=true;
        }

        public void TogglePauseImmediately()
        {
            if(game.IsTransitioning) return;
            game.SetScreenFade(null);
            try { game.TogglePause(); }
            finally { game.SetScreenFade(screenFade); }
        }

        private void LateUpdate()
        {
            RestoreFade();
            if(panel!=null) panel.color=game.State==PassGameFlow.RunState.Paused
                ? new Color(0f,0f,0f,panelOpacity) : originalPanelColor;
        }

        private void RestoreFade()
        {
            if(!bypassThisFrame) return;
            if(game!=null) game.SetScreenFade(screenFade);
            bypassThisFrame=false;
        }
        private void OnDisable() => RestoreFade();
        private void OnDestroy()
        {
            RestoreFade();
            if(resumeButton!=null && resumeButton.onClick==testAction) resumeButton.onClick=originalAction;
            if(panel!=null) panel.color=originalPanelColor;
        }
    }
}
