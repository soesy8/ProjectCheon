using Cheon.Testing.PlayerFan;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Cheon.Testing.MountainMap
{
    public class Test_PlayerControlGuide : MonoBehaviour
    {
        [SerializeField] private Test_PlayerFanAnimationController animationControl;
        [SerializeField] private TMP_FontAsset koreanFont;
        [SerializeField] private TMP_Settings textSettings;
        private RectTransform canvas;
        private GameObject details;
        private TMP_Text state;

        public void Configure(Test_PlayerFanAnimationController controller, TMP_FontAsset font, TMP_Settings settings)
        {
            animationControl = controller;
            koreanFont = font;
            textSettings = settings;
        }

        private void Start()
        {
            canvas = Test_PlayerTestUi.Canvas(transform, koreanFont, textSettings, 0);
            var panel = Test_PlayerTestUi.Panel("Test_ControlGuidePanel", canvas, Vector2.up,
                new Vector2(20f, -20f), new Vector2(316f, 396f), false);
            Test_PlayerTestUi.Text("Test_Title", panel.transform, koreanFont, "플레이어 조작 안내  [H]", 18f, 16f, 280f, 29f, 21f, Test_PlayerTestUi.Jade);
            details = Test_PlayerTestUi.Rect("Test_Details", panel.transform, 18f, 55f, 280f, 321f).gameObject;
            Test_PlayerTestUi.Text("Test_Keys", details.transform, koreanFont,
                "W A S D / 방향키   이동\nShift   달리기\nSpace   구르기 회피\nF   발도 / 납도 전환\n좌클릭 / J   공격 1 → 2 → 3\n1 / 2 / 3   개별 공격\n4   피격 모션 테스트\n우클릭 드래그   카메라 회전\n마우스 휠   확대 / 축소\nR   시작 위치로 복귀\nTab   전환 타이밍 UI 접기",
                0f, 0f, 280f, 273f, 16f, Test_PlayerTestUi.Paper);
            state = Test_PlayerTestUi.Text("Test_State", details.transform, koreanFont, "", 0f, 278f, 280f, 44f, 13f, Test_PlayerTestUi.Jade);
        }

        private void Update()
        {
            if (canvas == null || animationControl == null) return;
            if (!Test_PlayerTestUi.IsTyping && Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame)
            {
                details.SetActive(!details.activeSelf);
                ((RectTransform)details.transform.parent).sizeDelta = new Vector2(316f, details.activeSelf ? 396f : 55f);
            }
            state.text = $"{(animationControl.IsCombatRequested ? "발도" : "납도")} · {Test_PlayerActionTimings.Label(animationControl.SelectedState)}\n발도·납도 중 이동 가능 / 공격은 발도 후 사용";
        }

        private void OnEnable() { if (canvas != null) canvas.gameObject.SetActive(true); }
        private void OnDisable() { if (canvas != null) canvas.gameObject.SetActive(false); }
    }
}
