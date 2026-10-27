using UnityEngine;

namespace Cheon.Prototype.MountainPass
{
    public class PassPlayerAudio : MonoBehaviour
    {
        [SerializeField, InspectorName("효과음 제어기")] private PassAudio audioControl;
        [SerializeField, InspectorName("게임 진행 제어기")] private PassGameFlow game;
        [SerializeField, InspectorName("걷기 발걸음 간격"), Min(0.1f)] private float walkInterval = 0.42f;
        [SerializeField, InspectorName("달리기 발걸음 간격"), Min(0.1f)] private float sprintInterval = 0.28f;
        private PassPlayerMovement movement;
        private PassAnimationController animationControl;
        private float stepTime;
        private int pendingSwing;
        private uint swingSequence;
        public void Configure(PassGameFlow flow, PassAudio sounds) { game = flow; audioControl = sounds; }
        private void Start()
        {
            movement = GetComponent<PassPlayerMovement>(); animationControl = movement.AnimationControl;
            animationControl.StateChanging += OnStateChanging;
        }
        private void OnStateChanging(string from, string to, float duration)
        {
            if (audioControl == null) return;
            pendingSwing = to == "ATTACK1" ? 1 : to == "ATTACK2" ? 2 : to == "ATTACK3" ? 3 : 0;
            swingSequence = 0;
            if (to == "ROLL") audioControl.Dodge();
            if (to == "DRAW" || to == "SHEATHE") audioControl.Equip();
        }
        private void LateUpdate()
        {
            if (game == null || !game.IsRunning || audioControl == null || animationControl.IsPaused) return;
            if (pendingSwing > 0)
            {
                if (swingSequence == 0) swingSequence = animationControl.PlaybackSequence;
                if (animationControl.PlaybackSequence != swingSequence) pendingSwing = 0;
                else if (animationControl.PlaybackProgress >= 0.2f) { audioControl.Swing(pendingSwing); pendingSwing = 0; }
            }
            if (!movement.InputEnabled || !movement.IsMoving || !movement.Body.isGrounded) { stepTime = 0f; return; }
            stepTime -= Time.deltaTime;
            if (stepTime <= 0f) { audioControl.Footstep(movement.IsSprinting); stepTime = movement.IsSprinting ? sprintInterval : walkInterval; }
        }
        private void OnDestroy() { if (animationControl != null) animationControl.StateChanging -= OnStateChanging; }
    }
}
