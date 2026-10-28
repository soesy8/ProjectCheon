using System;
using System.Collections;
using UnityEngine;

namespace Cheon.Prototype.MountainPass
{
    // 화면이 완전히 가려진 시점에만 상태를 바꿔 순간 이동·UI 교체가 드러나지 않게 한다.
    public class PassScreenFade : MonoBehaviour
    {
        [SerializeField, InspectorName("화면 전환 페이드 시간"), Range(0.1f, 1f)] private float duration = 0.35f;
        [SerializeField, InspectorName("게임 시작 · 어두워지는 시간 (초)"), Range(0.1f, 2f)] private float startFadeOutDuration = 0.65f;
        [SerializeField, InspectorName("게임 시작 · 검은 화면 유지 시간 (초)"), Range(0f, 1f)] private float startBlackHoldDuration = 0.12f;
        [SerializeField, InspectorName("게임 시작 · 나타나는 시간 (초)"), Range(0.1f, 2f)] private float startFadeInDuration = 0.85f;
        private UnityEngine.UI.Image veil;
        private Coroutine transition;
        private float previousTimeScale = 1f;
        public bool IsTransitioning => transition != null;
        public void Initialize(RectTransform canvas)
        {
            var item = new GameObject("ScreenFade", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            var rect = (RectTransform)item.transform;
            rect.SetParent(canvas, false); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            veil = item.GetComponent<UnityEngine.UI.Image>();
            veil.color = Color.black; veil.raycastTarget = true;
            transition = StartCoroutine(Reveal());
        }
        private IEnumerator Reveal()
        {
            yield return Blend(1f, 0f, startFadeInDuration);
            veil.raycastTarget = false; transition = null;
        }
        public bool Transition(Action atBlack, bool startingJourney = false)
        {
            if (IsTransitioning || veil == null) return false;
            veil.transform.SetAsLastSibling();
            transition = StartCoroutine(Change(atBlack, startingJourney));
            return true;
        }
        private IEnumerator Change(Action atBlack, bool startingJourney)
        {
            previousTimeScale = Time.timeScale;
            veil.raycastTarget = true;
            Time.timeScale = 0f;
            yield return Blend(0f, 1f, startingJourney ? startFadeOutDuration : duration);
            Time.timeScale = previousTimeScale;
            atBlack?.Invoke();
            // 목적 화면의 일시정지 상태를 보존한 채 검은 화면 아래에서 한 프레임 UI를 갱신한다.
            float destinationTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            yield return null;
            // 시작 화면과 플레이 화면 사이에 짧은 여백을 두어 전환을 부드럽게 잇는다.
            if (startingJourney && startBlackHoldDuration > 0f)
                yield return new WaitForSecondsRealtime(startBlackHoldDuration);
            yield return Blend(1f, 0f, startingJourney ? startFadeInDuration : duration);
            Time.timeScale = destinationTimeScale;
            veil.raycastTarget = false; transition = null;
        }
        private IEnumerator Blend(float from, float to, float blendDuration)
        {
            float elapsed = 0f;
            veil.color = new Color(0.015f, 0.025f, 0.025f, from);
            while (elapsed < blendDuration)
            {
                // 시작 전 프레임의 긴 deltaTime이 페이드를 건너뛰지 않도록 다음 프레임부터 센다.
                yield return null;
                elapsed += Time.unscaledDeltaTime;
                veil.color = new Color(0.015f, 0.025f, 0.025f, Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, elapsed / blendDuration)));
            }
        }
        private void OnDisable()
        {
            if (transition != null) { StopCoroutine(transition); Time.timeScale = previousTimeScale; transition = null; }
            if (veil != null) { veil.color = Color.clear; veil.raycastTarget = false; }
        }
    }
}
