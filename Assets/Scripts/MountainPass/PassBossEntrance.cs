using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

namespace Cheon.Prototype.MountainPass
{
    public class PassBossEntrance : MonoBehaviour
    {
        [InspectorName("게임 진행 제어기")]
        [SerializeField] private PassGameFlow game;
        [InspectorName("보스 등장 타임라인")]
        [SerializeField] private PlayableDirector director;
        [InspectorName("보스 등장 연출 카메라")]
        [SerializeField] private Camera cinematicCamera;
        [InspectorName("플레이어 카메라")]
        [SerializeField] private Camera playerCamera;
        [InspectorName("보스 전투 퇴로 차단벽")]
        [SerializeField] private Collider retreatBarrier;
        [InspectorName("산문 통과 판정 너비·높이 (m)")]
        [SerializeField] private Vector2 openingSize = new Vector2(6f, 9f);
        [InspectorName("산문 통과 판정 여유 (m)")]
        [SerializeField, Min(0.01f)] private float crossingMargin = 0.2f;
        private Vector3 previousPosition;
        private bool hasPreviousPosition, resetting;
        public bool HasPlayed { get; private set; }
        public PlayableDirector Director => director;
        public bool IsPresenting => director != null && director.state == PlayState.Playing;

        public void Configure(PassGameFlow flow, PlayableDirector timeline, Camera cinema, Camera gameplay, Collider barrier)
        { game = flow; director = timeline; cinematicCamera = cinema; playerCamera = gameplay; retreatBarrier = barrier; }
        private void OnEnable() { if (director != null) director.stopped += OnPresentationStopped; }
        private void OnDisable()
        {
            if (director != null) director.stopped -= OnPresentationStopped;
            if (Application.isPlaying) RestoreCameras();
        }
        private void Update()
        {
            if (game == null || game.IsTransitioning) return;
            if (game.State == PassGameFlow.RunState.Cinematic)
            {
                var keyboard = Keyboard.current;
                if (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame)) SkipPresentation();
                return;
            }
            if (!game.IsRunning) return;
            Vector3 current = transform.InverseTransformPoint(game.Player.position + Vector3.up);
            if (game.InEncounter)
            {
                if (retreatBarrier != null) retreatBarrier.enabled = true;
                return;
            }
            if (Mathf.Abs(current.z) < crossingMargin) return;
            if (hasPreviousPosition && previousPosition.z < -crossingMargin && current.z > crossingMargin)
            {
                float t = previousPosition.z / (previousPosition.z - current.z);
                Vector3 crossing = Vector3.Lerp(previousPosition, current, t);
                if (Mathf.Abs(crossing.x) <= openingSize.x * 0.5f && Mathf.Abs(crossing.y) <= openingSize.y * 0.5f)
                    game.BeginEncounter();
            }
            previousPosition = current; hasPreviousPosition = true;
        }
        private void LateUpdate()
        {
            // 연출 카메라도 입장하며 바뀌는 영계 배경색을 이어받는다.
            if (cinematicCamera != null && cinematicCamera.enabled && playerCamera != null)
                cinematicCamera.backgroundColor = playerCamera.backgroundColor;
        }
        public void PlayPresentation()
        {
            HasPlayed = true;
            // 차단면은 문 뒤쪽에 배치하므로 입장 직후에도 플레이어를 밀어내지 않는다.
            if (retreatBarrier != null) retreatBarrier.enabled = true;
            if (director == null || director.playableAsset == null) { game.CompleteBossPresentation(); return; }
            director.stopped -= OnPresentationStopped; director.stopped += OnPresentationStopped;
            game.View.enabled = false; playerCamera.enabled = false; cinematicCamera.enabled = true;
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
            director.time = 0; director.Play();
        }
        public void SkipPresentation() { if (IsPresenting) director.Stop(); }
        private void OnPresentationStopped(PlayableDirector stopped)
        {
            if (resetting) return;
            game.CompleteBossPresentation();
        }
        public void ResetEntrance()
        {
            resetting = true;
            if (director != null && director.state == PlayState.Playing) director.Stop();
            resetting = false; HasPlayed = false; hasPreviousPosition = false;
            if (retreatBarrier != null) retreatBarrier.enabled = false;
            RestoreCameras();
        }
        private void RestoreCameras()
        {
            if (cinematicCamera != null) cinematicCamera.enabled = false;
            if (playerCamera != null) playerCamera.enabled = true;
            if (game != null && game.View != null) game.View.enabled = true;
        }
        public void RestoreGameplayCameras() => RestoreCameras();
    }
}
