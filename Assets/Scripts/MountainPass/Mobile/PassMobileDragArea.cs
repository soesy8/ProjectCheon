using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Cheon.Prototype.MountainPass
{
    // 한 영역은 처음 닿은 손가락만 추적해 이동과 시점의 멀티터치가 섞이지 않게 한다.
    public class PassMobileDragArea : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IDragHandler, IInitializePotentialDragHandler
    {
        private int? pointerId;
        public event Action<PointerEventData> Pressed;
        public event Action<PointerEventData> Dragged;
        public event Action Released;

        public void OnInitializePotentialDrag(PointerEventData eventData) => eventData.useDragThreshold = false;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (pointerId.HasValue || eventData.button != PointerEventData.InputButton.Left) return;
            pointerId = eventData.pointerId;
            Pressed?.Invoke(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (pointerId == eventData.pointerId) Dragged?.Invoke(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (pointerId == eventData.pointerId) ResetPointer();
        }

        public void ResetPointer()
        {
            if (!pointerId.HasValue) return;
            pointerId = null;
            Released?.Invoke();
        }

        private void OnDisable() => ResetPointer();
    }
}
