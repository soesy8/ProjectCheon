using UnityEngine;

namespace Cheon.Prototype.MountainPass
{
    [DefaultExecutionOrder(50)]
    public class PassFanAttachment : MonoBehaviour
    {
        [Header("장착 기준점")]
        [Tooltip("이 소켓 Transform의 로컬 위치와 회전을 Inspector에서 직접 조절합니다.")]
        [InspectorName("부채 오른손 장착 위치")]
        [SerializeField] private Transform handSocket;
        [InspectorName("부채 허리 수납 위치")]
        [SerializeField] private Transform stowedSocket;
        [InspectorName("부채 기준 오브젝트")]
        [SerializeField] private Transform fanRoot;
        [InspectorName("부채 모델 크기 배율")]
        [SerializeField, Min(0.01f)] private float fanScale = 1f;
        [InspectorName("부채 장착 전환 시간 (초)")]
        [SerializeField, Range(0f, 0.2f)] private float attachmentBlendDuration = 0.06f;
        [Header("부채 FBX")]
        [InspectorName("접힌 부채 모델")]
        [SerializeField] private GameObject closedFan;
        [InspectorName("펼친 부채 모델")]
        [SerializeField] private GameObject openFan;
        [InspectorName("애니메이션 부채 모델")]
        [SerializeField] private GameObject animatedFan;
        [InspectorName("부채 애니메이터")]
        [SerializeField] private Animator fanAnimator;
        [InspectorName("부채 펼치기 원본 길이 (초)")]
        [SerializeField, Min(0.01f)] private float openingClipLength = 2f;
        [Header("부채 펼침")]
        [InspectorName("부채 펼침 정도 (0~1)")]
        [SerializeField, Range(0f, 1f)] private float openingAmount;
        [InspectorName("부채 펼치기 재생 속도")]
        [SerializeField, Min(0.05f)] private float openingSpeed = 1f;
        [InspectorName("시작 시 부채 펼치기")]
        [SerializeField] private bool startOpen;
        [InspectorName("애니메이션 부채 모델 사용")]
        [SerializeField] private bool useAnimatedModel = true;

        private float targetOpening, attachmentProgress = 1f;
        private Vector3 attachmentStartPosition;
        private Quaternion attachmentStartRotation;
        private bool moving, inHand, paused;
        private static readonly int OpeningState = Animator.StringToHash("OPENING");
        public float OpeningAmount => openingAmount;
        public Transform HandSocket => handSocket;
        public Transform StowedSocket => stowedSocket;
        public Transform FanRoot => fanRoot;
        public bool IsMoving => moving;
        public bool IsInHand => inHand;
        public string FanState => (inHand ? "손 장착" : "납도 보관") + (moving ? " · 전환 중" : $" · 펼침 {openingAmount * 100f:0}%");

        public void Configure(Transform socket, GameObject closed, GameObject open, GameObject animated, Animator animator, float clipLength)
        {
            handSocket = socket;
            closedFan = closed; openFan = open; animatedFan = animated; fanAnimator = animator; openingClipLength = clipLength;
        }
        public void ConfigureMounts(Transform storage, Transform root) { stowedSocket = storage; fanRoot = root; }
        private void Awake()
        {
            openingAmount = targetOpening = startOpen ? 1f : 0f;
            if (fanAnimator != null) { fanAnimator.applyRootMotion = false; fanAnimator.speed = 0f; }
            SetSequencePose(false, openingAmount, true);
        }
        private void Update()
        {
            if (!moving || paused) return;
            openingAmount = Mathf.MoveTowards(openingAmount, targetOpening, Time.deltaTime * openingSpeed / Mathf.Max(0.01f, openingClipLength));
            moving = !Mathf.Approximately(openingAmount, targetOpening); RefreshFan();
        }
        private void LateUpdate()
        {
            // 손 소켓은 Transform의 조절값을 그대로 사용하고, 장착 전환은 부채 루트에서만 처리한다.
            if (fanRoot == null) return;
            if (!paused) attachmentProgress = attachmentBlendDuration <= 0f ? 1f : Mathf.Min(1f, attachmentProgress + Time.deltaTime / attachmentBlendDuration);
            float blend = Mathf.SmoothStep(0f, 1f, attachmentProgress);
            fanRoot.localPosition = Vector3.Lerp(attachmentStartPosition, Vector3.zero, blend);
            fanRoot.localRotation = Quaternion.Slerp(attachmentStartRotation, Quaternion.identity, blend);
            fanRoot.localScale = Vector3.one * fanScale;
        }
        public void SetSequencePose(bool attachToHand, float amount, bool immediate = false)
        {
            moving = false; useAnimatedModel = true; Equip(attachToHand, immediate);
            float clamped = Mathf.Clamp01(amount);
            if (Mathf.Abs(clamped - openingAmount) < 0.00001f && animatedFan != null && animatedFan.activeSelf) return;
            openingAmount = targetOpening = clamped; RefreshFan();
        }
        private void Equip(bool attachToHand, bool immediate)
        {
            Transform target = attachToHand ? handSocket : stowedSocket;
            if (fanRoot == null || target == null) return;
            inHand = attachToHand;
            if (fanRoot.parent != target)
            {
                // 부채 하나만 부모를 바꾸므로 꺼내기·수납 중 중복 표시되지 않는다.
                fanRoot.SetParent(target, true);
                attachmentStartPosition = fanRoot.localPosition; attachmentStartRotation = fanRoot.localRotation;
                attachmentProgress = 0f;
            }
            if (!immediate) return;
            attachmentProgress = 1f; fanRoot.localPosition = Vector3.zero; fanRoot.localRotation = Quaternion.identity;
        }
        public void SetPaused(bool value) => paused = value;
        public void ShowClosed() { moving = false; useAnimatedModel = false; openingAmount = targetOpening = 0f; RefreshFan(); }
        public void ShowOpen() { moving = false; useAnimatedModel = false; openingAmount = targetOpening = 1f; RefreshFan(); }
        public void ToggleFan()
        {
            targetOpening = targetOpening >= 0.5f ? 0f : 1f;
            useAnimatedModel = true; moving = true; RefreshFan();
        }
        public void SetOpeningAmount(float amount)
        {
            moving = false; useAnimatedModel = true; openingAmount = targetOpening = Mathf.Clamp01(amount); RefreshFan();
        }
        private void RefreshFan()
        {
            if (closedFan != null) closedFan.SetActive(!useAnimatedModel && openingAmount < 0.5f);
            if (openFan != null) openFan.SetActive(!useAnimatedModel && openingAmount >= 0.5f);
            if (animatedFan != null) animatedFan.SetActive(useAnimatedModel);
            if (!useAnimatedModel || fanAnimator == null || fanAnimator.runtimeAnimatorController == null) return;
            fanAnimator.speed = 0f; fanAnimator.Play(OpeningState, 0, openingAmount); fanAnimator.Update(0f);
        }
    }
}

