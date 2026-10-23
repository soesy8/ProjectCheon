using UnityEngine;
using UnityEngine.InputSystem;

namespace Cheon.Testing.PlayerFan
{
    // 빈 오브젝트에서 캐릭터 애니메이션과 공격별 재생 속도를 관리한다.
    public class Test_PlayerFanAnimationController : MonoBehaviour
    {
        [Header("재생 대상")]
        [SerializeField] private Animator characterAnimator;
        [SerializeField] private Test_PlayerFanAttachment fanAttachment;

        [Header("공격별 속도 배율 — 재생 중에도 변경 가능")]
        [Min(0.05f)] public float attack1Speed = 1f;
        [Min(0.05f)] public float attack2Speed = 1f;
        [Min(0.05f)] public float attack3Speed = 1f;

        [Header("재생 옵션")]
        [SerializeField, Min(0f)] private float transitionDuration = 0.12f;
        [SerializeField] private bool repeatSelectedAnimation;
        [SerializeField] private bool enableKeyboardShortcuts = true;

        private static readonly int Attack1Speed = Animator.StringToHash("Attack1Speed");
        private static readonly int Attack2Speed = Animator.StringToHash("Attack2Speed");
        private static readonly int Attack3Speed = Animator.StringToHash("Attack3Speed");
        private string selectedState = "IDLE";
        private bool paused;

        public Animator CharacterAnimator => characterAnimator;
        public string SelectedState => selectedState;
        public bool IsPaused => paused;
        public bool RepeatSelectedAnimation { get => repeatSelectedAnimation; set => repeatSelectedAnimation = value; }
        public float NormalizedTime => characterAnimator != null && characterAnimator.isInitialized
            ? characterAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime : 0f;

        public void Configure(Animator animator, Test_PlayerFanAttachment attachment)
        {
            characterAnimator = animator;
            fanAttachment = attachment;
        }

        private void Start()
        {
            if (characterAnimator == null || characterAnimator.runtimeAnimatorController == null)
            {
                Debug.LogError("테스트 캐릭터의 Animator와 Controller 연결을 확인하세요.", this);
                enabled = false;
                return;
            }
            characterAnimator.applyRootMotion = false;
            ApplyAttackSpeeds();
            PlayIdle();
        }

        private void Update()
        {
            ApplyAttackSpeeds();
            if (enableKeyboardShortcuts) ReadKeyboard();
            if (paused || !characterAnimator.isInitialized || characterAnimator.IsInTransition(0)) return;
            var state = characterAnimator.GetCurrentAnimatorStateInfo(0);
            // 현재 동작이 끝났을 때만 복귀한다. 전환 중 이전 상태의 시간은 사용하지 않는다.
            if (selectedState == "IDLE" || selectedState == "RUN" || !state.IsName(selectedState) || state.normalizedTime < 1f) return;
            if (repeatSelectedAnimation) characterAnimator.Play(selectedState, 0, 0f);
            else PlayIdle();
        }

        public void ApplyAttackSpeeds()
        {
            if (characterAnimator == null || characterAnimator.runtimeAnimatorController == null) return;
            characterAnimator.SetFloat(Attack1Speed, Mathf.Max(0.05f, attack1Speed));
            characterAnimator.SetFloat(Attack2Speed, Mathf.Max(0.05f, attack2Speed));
            characterAnimator.SetFloat(Attack3Speed, Mathf.Max(0.05f, attack3Speed));
        }

        public void PlayState(string state)
        {
            if (characterAnimator == null) return;
            selectedState = state;
            paused = false;
            characterAnimator.speed = 1f;
            ApplyAttackSpeeds();
            characterAnimator.CrossFadeInFixedTime(state, transitionDuration, 0, 0f);
        }

        public void PlayIdle() => PlayState("IDLE");
        public void PlayRun() => PlayState("RUN");
        public void PlayAttack1() => PlayState("ATTACK1");
        public void PlayAttack2() => PlayState("ATTACK2");
        public void PlayAttack3() => PlayState("ATTACK3");
        public void PlayHit() => PlayState("HIT");
        public void PlayDrawSheathe() => PlayState("DRAW_SHEATHE");

        public void TogglePause()
        {
            paused = !paused;
            if (characterAnimator != null) characterAnimator.speed = paused ? 0f : 1f;
        }

        private void ReadKeyboard()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.digit1Key.wasPressedThisFrame) PlayAttack1();
            if (keyboard.digit2Key.wasPressedThisFrame) PlayAttack2();
            if (keyboard.digit3Key.wasPressedThisFrame) PlayAttack3();
            if (keyboard.digit4Key.wasPressedThisFrame) PlayHit();
            if (keyboard.digit5Key.wasPressedThisFrame) PlayDrawSheathe();
            if (keyboard.iKey.wasPressedThisFrame) PlayIdle();
            if (keyboard.rKey.wasPressedThisFrame) { if (selectedState == "RUN") PlayIdle(); else PlayRun(); }
            if (keyboard.spaceKey.wasPressedThisFrame) TogglePause();
            if (keyboard.fKey.wasPressedThisFrame && fanAttachment != null) fanAttachment.ToggleFan();
        }
    }
}
