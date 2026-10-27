using System;
using System.Collections;
using UnityEngine;

namespace Cheon.Prototype.MountainPass
{
    // 화면이 완전히 가려진 시점에만 상태를 바꿔 순간 이동·UI 교체가 드러나지 않게 한다.
    public class PassScreenFade : MonoBehaviour
    {
        [SerializeField, InspectorName("화면 전환 페이드 시간"), Range(0.1f, 1f)] private float duration = 0.35f;
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
            yield return Blend(1f, 0f);
            veil.raycastTarget = false; transition = null;
        }
        public bool Transition(Action atBlack)
        {
            if (IsTransitioning || veil == null) return false;
            transition = StartCoroutine(Change(atBlack));
            return true;
        }
        private IEnumerator Change(Action atBlack)
        {
            previousTimeScale = Time.timeScale;
            veil.raycastTarget = true;
            Time.timeScale = 0f;
            yield return Blend(0f, 1f);
            Time.timeScale = previousTimeScale;
            atBlack?.Invoke();
            // 목적 화면의 일시정지 상태를 보존한 채 검은 화면 아래에서 한 프레임 UI를 갱신한다.
            float destinationTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            yield return null;
            yield return Blend(1f, 0f);
            Time.timeScale = destinationTimeScale;
            veil.raycastTarget = false; transition = null;
        }
        private IEnumerator Blend(float from, float to)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                veil.color = new Color(0.015f, 0.025f, 0.025f, Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, elapsed / duration)));
                yield return null;
            }
        }
        private void OnDisable()
        {
            if (transition != null) { StopCoroutine(transition); Time.timeScale = previousTimeScale; transition = null; }
            if (veil != null) { veil.color = Color.clear; veil.raycastTarget = false; }
        }
    }
}
