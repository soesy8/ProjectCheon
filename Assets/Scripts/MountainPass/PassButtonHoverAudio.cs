using UnityEngine;
using UnityEngine.EventSystems;

namespace Cheon.Prototype.MountainPass
{
    public class PassButtonHoverAudio : MonoBehaviour, IPointerEnterHandler
    {
        private UnityEngine.UI.Button button;
        private PassAudio sounds;

        public void Configure(UnityEngine.UI.Button target, PassAudio audio)
        {
            button = target;
            sounds = audio;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            // 비활성 버튼과 모달 뒤에서 입력이 차단된 버튼에는 호버 소리를 내지 않는다.
            if (button != null && button.IsActive() && button.IsInteractable() && sounds != null)
                sounds.MenuHover();
        }
    }
}
