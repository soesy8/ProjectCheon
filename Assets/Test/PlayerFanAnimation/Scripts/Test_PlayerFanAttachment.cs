using UnityEngine;

namespace Cheon.Testing.PlayerFan
{
    // 캐릭터와 별도 리그를 사용하는 부채를 손 소켓에 붙이고 세 FBX의 상태를 전환한다.
    public class Test_PlayerFanAttachment : MonoBehaviour
    {
        [Header("손 장착 기준점 — 손 본의 자식")]
        [SerializeField] private Transform handSocket;
        [SerializeField] private Vector3 socketLocalPosition;
        [SerializeField] private Vector3 socketLocalEulerAngles;
        [SerializeField, Min(0.01f)] private float fanScale = 1f;

        [Header("첨부 FBX 세 종류")]
        [SerializeField] private GameObject closedFan;
        [SerializeField] private GameObject openFan;
        [SerializeField] private GameObject animatedFan;
        [SerializeField] private Animator fanAnimator;
        [SerializeField, Min(0.01f)] private float openingClipLength = 2f;

        [Header("부채 펼침")]
        [SerializeField, Range(0f, 1f)] private float openingAmount = 1f;
        [SerializeField, Min(0.05f)] private float openingSpeed = 1f;
        [SerializeField] private bool startOpen = true;
        [SerializeField] private bool useAnimatedModel;

        private float targetOpening = 1f;
        private bool moving;
        private static readonly int OpeningState = Animator.StringToHash("OPENING");

        public float OpeningAmount => openingAmount;
        public Transform HandSocket => handSocket;
        public bool IsMoving => moving;
        public string FanState => moving ? "펼침 / 접힘 전환 중" : useAnimatedModel ? "리깅 FBX" : openingAmount >= 0.5f ? "펼친 FBX" : "접힌 FBX";

        public void Configure(Transform socket, GameObject closed, GameObject open, GameObject animated, Animator animator, float clipLength)
        {
            handSocket = socket;
            socketLocalPosition = socket.localPosition;
            socketLocalEulerAngles = socket.localEulerAngles;
            closedFan = closed;
            openFan = open;
            animatedFan = animated;
            fanAnimator = animator;
            openingClipLength = clipLength;
        }

        private void Start()
        {
            openingAmount = targetOpening = startOpen ? 1f : 0f;
            if (fanAnimator != null) { fanAnimator.applyRootMotion = false; fanAnimator.speed = 0f; }
            RefreshFan();
        }

        private void Update()
        {
            if (!moving) return;
            openingAmount = Mathf.MoveTowards(openingAmount, targetOpening, Time.deltaTime * openingSpeed / Mathf.Max(0.01f, openingClipLength));
            moving = !Mathf.Approximately(openingAmount, targetOpening);
            RefreshFan();
        }

        private void LateUpdate()
        {
            // 장착 보정값은 애니메이션의 손 본이 평가된 다음 적용한다.
            if (handSocket == null) return;
            handSocket.localPosition = socketLocalPosition;
            handSocket.localRotation = Quaternion.Euler(socketLocalEulerAngles);
            handSocket.localScale = Vector3.one * Mathf.Max(0.01f, fanScale);
        }

        public void ShowClosed()
        {
            moving = false;
            useAnimatedModel = false;
            openingAmount = targetOpening = 0f;
            RefreshFan();
        }

        public void ShowOpen()
        {
            moving = false;
            useAnimatedModel = false;
            openingAmount = targetOpening = 1f;
            RefreshFan();
        }

        public void ToggleFan()
        {
            targetOpening = targetOpening >= 0.5f ? 0f : 1f;
            useAnimatedModel = true;
            moving = true;
            RefreshFan();
        }

        public void SetOpeningAmount(float amount)
        {
            moving = false;
            useAnimatedModel = true;
            openingAmount = targetOpening = Mathf.Clamp01(amount);
            RefreshFan();
        }

        private void RefreshFan()
        {
            if (closedFan != null) closedFan.SetActive(!useAnimatedModel && openingAmount < 0.5f);
            if (openFan != null) openFan.SetActive(!useAnimatedModel && openingAmount >= 0.5f);
            if (animatedFan != null) animatedFan.SetActive(useAnimatedModel);
            if (!useAnimatedModel || fanAnimator == null || fanAnimator.runtimeAnimatorController == null) return;
            // 같은 클립의 진행 위치를 지정하므로 펼치기와 접기를 모두 검사할 수 있다.
            fanAnimator.speed = 0f;
            fanAnimator.Play(OpeningState, 0, openingAmount);
            fanAnimator.Update(0f);
        }
    }
}
