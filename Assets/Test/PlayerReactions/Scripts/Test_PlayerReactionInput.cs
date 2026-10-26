using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Cheon.Tests.PlayerReactions
{
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(Test_PlayerReactionPreview))]
    public class Test_PlayerReactionInput : MonoBehaviour
    {
        private Test_PlayerReactionPreview preview;
        private void Awake() => preview = GetComponent<Test_PlayerReactionPreview>();

        private void Update()
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            bool typing = selected != null && selected.GetComponentInParent<TMP_InputField>() is TMP_InputField field && field.isFocused;
            var keyboard = Keyboard.current;
            if (keyboard == null || typing) { preview.SetInput(Vector2.zero, false); return; }
            var movement = new Vector2(
                (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f),
                (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1f : 0f));
            // 회피는 누른 순간만 받는다. 캔슬 가능 시점 전에 누른 입력을 나중에 실행하지 않는다.
            preview.SetInput(Vector2.ClampMagnitude(movement, 1f), keyboard.spaceKey.wasPressedThisFrame);
        }

        private void OnDisable() { if (preview != null) preview.SetInput(Vector2.zero, false); }
    }
}
