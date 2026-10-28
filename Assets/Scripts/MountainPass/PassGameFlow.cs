using UnityEngine;
using UnityEngine.InputSystem;

namespace Cheon.Prototype.MountainPass
{
    [DefaultExecutionOrder(-40)]
    public class PassGameFlow : MonoBehaviour
    {
        public enum RunState { Intro, Playing, Paused, Defeat, Victory, Cinematic }
        [InspectorName("플레이어")]
        [SerializeField] private PassPlayerMovement player;
        [InspectorName("플레이어 생명력")]
        [SerializeField] private PassHealth playerHealth;
        [InspectorName("보스 제어기")]
        [SerializeField] private PassBossController boss;
        [InspectorName("게임 카메라 제어기")]
        [SerializeField] private PassCamera view;
        [InspectorName("공통 시각 효과 제어기")]
        [SerializeField] private PassEffects effects;
        [InspectorName("효과음 제어기")]
        [SerializeField] private PassAudio audioControl;
        [InspectorName("게임 화면 표시 제어기")]
        [SerializeField] private PassHud hud;
        [InspectorName("현세·영계 환경 제어기")]
        [SerializeField] private PassWorldTransition environment;
        [InspectorName("산신각 상호작용 제어기")]
        [SerializeField] private PassShrineInteraction shrine;
        [InspectorName("보스 입구 제어기")]
        [SerializeField] private PassBossEntrance entrance;
        [InspectorName("회복 호리병")]
        [SerializeField] private PassGourdDrinking gourd;
        [InspectorName("보스 도전 시작 위치")]
        [SerializeField] private Vector3 arenaCheckpoint = new Vector3(-15f, 37.2f, -43f);
        [Tooltip("지정한 원형 바닥에 들어선 뒤 보스가 플레이어를 인식합니다. 비워 두면 등장 연출 직후 인식합니다.")]
        [InspectorName("보스 인식 시작 바닥")]
        [SerializeField] private Renderer bossDetectionFloor;
        [InspectorName("보스 인식 바닥 높이 여유 (m)")]
        [SerializeField, Min(.1f)] private float bossDetectionHeightTolerance = 3f;
        [InspectorName("추락 시 피해량")]
        [SerializeField, Min(0f)] private float fallDamage = 20f;
        [InspectorName("체크포인트 아래 추락 판정 거리 (m)")]
        [SerializeField, Min(1f)] private float checkpointFallDistance = 12f;
        private PassPlayerCombat playerCombat;
        private PassPlayerEnergy energy;
        private PassScreenFade screenFade;
        private bool checkpointAtBoss;
        private Vector3 startPosition, checkpoint;
        private Quaternion startRotation, checkpointRotation;
        public RunState State { get; private set; }
        public bool IsRunning => State == RunState.Playing && !IsTransitioning;
        public bool IsTransitioning => screenFade != null && screenFade.IsTransitioning;
        public bool InEncounter { get; private set; }
        public bool BossHasDetectedPlayer { get; private set; }
        public bool ReachedShrine { get; private set; }
        public float Elapsed { get; private set; }
        public PassHealth PlayerHealth => playerHealth;
        public PassBossController Boss => boss;
        public PassCamera View => view;
        public PassShrineInteraction Shrine => shrine;
        public PassBossEntrance Entrance => entrance;
        public PassGourdDrinking Gourd => gourd;
        public PassPlayerCombat PlayerCombat => playerCombat;
        public PassPlayerEnergy Energy => energy;
        public PassAudio Audio => audioControl;
        public void SetScreenFade(PassScreenFade fade) => screenFade = fade;
        public Transform Player => player.transform;
        public Vector3 ObjectivePosition => InEncounter ? boss.transform.position : ReachedShrine && entrance != null ? entrance.transform.position : shrine != null ? shrine.Position : startPosition;
        public string ObjectiveTitle => InEncounter ? "장승 수문장을 쓰러뜨려라" : ReachedShrine ? "영적 산문을 지나 수문장을 찾아라" : "산신각에 올라 기도를 올려라";

        public void Configure(PassPlayerMovement movement, PassHealth vitality, PassBossController guardian, PassCamera cameraControl, PassEffects visuals, PassAudio sounds, PassHud display, PassWorldTransition transition)
        { player = movement; playerHealth = vitality; boss = guardian; view = cameraControl; effects = visuals; audioControl = sounds; hud = display; environment = transition; }
        public void ConfigureRefinement(PassShrineInteraction sanctuary, PassBossEntrance gate) { shrine = sanctuary; entrance = gate; }
        public void ConfigureGourd(PassGourdDrinking healingGourd) => gourd = healingGourd;
        private void Awake()
        {
            playerCombat = player.GetComponent<PassPlayerCombat>();
            energy = player.GetComponent<PassPlayerEnergy>();
            if (energy != null) energy.Configure(this);
            startPosition = checkpoint = player.transform.position;
            startRotation = checkpointRotation = player.transform.rotation;
            State = RunState.Intro;
            player.InputEnabled = false;
        }
        private void Start()
        {
            playerHealth.Died += OnDefeat; playerHealth.Damaged += OnPlayerDamaged; boss.Health.Died += OnVictory; player.Fell += OnFall;
        }
        private void Update()
        {
            var keyboard = Keyboard.current;
            if (IsTransitioning) return;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && (State == RunState.Playing || State == RunState.Paused)) TogglePause(true);
            if (State == RunState.Intro && keyboard != null && keyboard.enterKey.wasPressedThisFrame) BeginJourney();
            if ((State == RunState.Defeat || State == RunState.Victory) && keyboard != null && keyboard.rKey.wasPressedThisFrame) RetryCheckpoint();
            if (!IsRunning) return;
            Elapsed += Time.deltaTime;
            TryWakeBoss();
            // 절벽 아래에 착지해 진행이 끊기면 마지막으로 기도한 산신각으로 돌아간다.
            if (player.transform.position.y < checkpoint.y - checkpointFallDistance)
            { player.ResetToSpawn(); OnFall(); return; }
        }
        public void BeginJourney() => ChangeScreen(() => ResetRun(false));
        public void BeginBossChallenge() => ChangeScreen(() => ResetRun(true));
        private void ChangeScreen(System.Action action, bool playConfirm = true)
        {
            if (IsTransitioning) return;
            if (playConfirm && audioControl != null) audioControl.Confirm();
            if (screenFade == null) action(); else screenFade.Transition(action);
        }
        private void ResetRun(bool atBoss)
        {
            Time.timeScale = 1f; State = RunState.Playing; InEncounter = false; ReachedShrine = atBoss;
            BossHasDetectedPlayer = false;
            if (shrine != null) shrine.CancelPrayer();
            checkpointAtBoss = atBoss;
            Elapsed = 0f;
            if (entrance != null) entrance.ResetEntrance();
            boss.ResetEncounter(); playerHealth.Restore(); playerCombat.ResetHits();
            if (energy != null) energy.Restore();
            if (gourd != null) gourd.ResetForJourney();
            if (environment != null) environment.SetSpiritWorld(atBoss);
            checkpoint = atBoss ? arenaCheckpoint : startPosition;
            checkpointRotation = atBoss ? Quaternion.LookRotation(Vector3.ProjectOnPlane(boss.transform.position - checkpoint, Vector3.up)) : startRotation;
            player.SetCheckpoint(checkpoint, checkpointRotation); player.Teleport(checkpoint, checkpointRotation); player.InputEnabled = true;
            view.LockBoss(false); view.SnapBehind();
            hud.Notify(atBoss ? "원형 돌바닥으로 들어가 수문장에게 도전하세요" : "등불을 따라 산신각으로 올라가세요", 4f);
            if (atBoss) BeginEncounter();
        }
        public void RestAtShrine(Vector3 position, Quaternion rotation)
        {
            if (!IsRunning || InEncounter) return;
            ReachedShrine = true; checkpoint = position; checkpointRotation = rotation;
            checkpointAtBoss = false;
            player.SetCheckpoint(checkpoint, checkpointRotation); playerHealth.Restore();
            if (energy != null) energy.Restore();
            if (gourd != null) gourd.Refill();
            audioControl.Bell(); effects.Impact(player.transform.position, new Color(0.35f, 1f, 0.8f), 3f);
            hud.Notify("산신각의 가호 · 생명력 회복 · 호리병 충전", 4f);
        }
        public void BeginEncounter()
        {
            if (InEncounter || State != RunState.Playing) return;
            if (IsTransitioning) EnterEncounter(); else ChangeScreen(EnterEncounter, false);
        }
        private void EnterEncounter()
        {
            InEncounter = true;
            if (environment != null) environment.SetSpiritWorld(true);
            if (entrance != null && !entrance.HasPlayed)
            {
                State = RunState.Cinematic; player.InputEnabled = false; playerHealth.Invulnerable = true;
                if (gourd != null) gourd.CancelDrinking();
                player.AnimationControl.PlayIdle(); entrance.PlayPresentation();
            }
            else CompleteBossPresentation();
        }
        public void CompleteBossPresentation()
        {
            if (!InEncounter) return;
            if (IsTransitioning) FinishBossPresentation(); else ChangeScreen(FinishBossPresentation, false);
        }
        private void FinishBossPresentation()
        {
            if (entrance != null) entrance.RestoreGameplayCameras();
            State = RunState.Playing; player.InputEnabled = true; playerHealth.Invulnerable = false;
            TryWakeBoss();
            view.SnapBehind(); view.RefreshCursor();
            hud.Notify("천하대장군 · 산문의 수문장", 4f);
        }
        private void TryWakeBoss()
        {
            if (!InEncounter || State != RunState.Playing || BossHasDetectedPlayer) return;
            if (bossDetectionFloor != null)
            {
                // 바닥의 실제 월드 크기를 읽어 원형 바닥을 다시 넓혀도 인식 영역이 함께 바뀐다.
                Bounds bounds = bossDetectionFloor.bounds;
                Vector3 offset = player.transform.position - bounds.center;
                float x = offset.x / Mathf.Max(.01f, bounds.extents.x);
                float z = offset.z / Mathf.Max(.01f, bounds.extents.z);
                if (x * x + z * z > 1f || Mathf.Abs(offset.y) > bossDetectionHeightTolerance) return;
            }
            // 한 번 인식한 뒤에는 원 밖으로 나가도 전투를 계속한다. 재도전할 때만 초기화한다.
            BossHasDetectedPlayer = true;
            boss.WakeUp();
        }
        public void ShowNotice(string message, float duration) => hud.Notify(message, duration);
        public void TogglePause() => TogglePause(false);
        public void TogglePause(bool useDrumSound)
        {
            if (State != RunState.Playing && State != RunState.Paused) return;
            if (IsTransitioning) return;
            if (useDrumSound && audioControl != null) audioControl.MenuBack();
            ChangeScreen(ApplyPause, !useDrumSound);
        }
        private void ApplyPause()
        {
            bool paused = State == RunState.Playing;
            State = paused ? RunState.Paused : RunState.Playing; Time.timeScale = paused ? 0f : 1f;
            player.InputEnabled = !paused && (shrine == null || !shrine.IsPraying);
        }
        public void RetryCheckpoint()
        { ChangeScreen(ResetCheckpoint); }
        private void ResetCheckpoint()
        {
            Time.timeScale = 1f; State = RunState.Playing; InEncounter = false;
            BossHasDetectedPlayer = false;
            if (shrine != null) shrine.CancelPrayer();
            if (entrance != null) entrance.ResetEntrance();
            boss.ResetEncounter(); playerHealth.Restore();
            if (energy != null) energy.Restore();
            if (gourd != null) { if (ReachedShrine) gourd.Refill(); else gourd.ResetForJourney(); }
            if (environment != null) environment.SetSpiritWorld(checkpointAtBoss);
            player.Teleport(checkpoint, checkpointRotation); player.InputEnabled = true; view.LockBoss(false); view.SnapBehind();
            hud.Notify(ReachedShrine ? "산신각의 가호로 다시 일어났습니다" : "산길 입구로 돌아왔습니다", 3f);
            if (checkpointAtBoss) BeginEncounter();
        }
        private void OnPlayerDamaged(float damage)
        {
            view.AddShake(0.9f); audioControl.Hurt(); hud.FlashDamage();
            effects.Hit(player.transform.position + Vector3.up, -player.transform.forward, false, playerHealth.LastDamageWasHeavy, true);
            if (!playerHealth.IsDead)
            {
                if (playerHealth.LastDamageWasHeavy) player.AnimationControl.PlayHeavyHit();
                else player.AnimationControl.PlayHit();
            }
        }
        private void OnDefeat()
        {
            if (gourd != null) gourd.CancelDrinking();
            player.InputEnabled = false; player.AnimationControl.PlayHeavyHit(true); audioControl.Defeat();
            ChangeScreen(() => { State = RunState.Defeat; Time.timeScale = 1f; }, false);
        }
        private void OnVictory()
        {
            if (gourd != null) gourd.CancelDrinking();
            player.InputEnabled = false; player.AnimationControl.PlayIdle(); audioControl.Victory();
            ChangeScreen(() => { State = RunState.Victory; Time.timeScale = 1f; }, false);
        }
        private void OnFall() { playerHealth.TakeDamage(fallDamage); view.SnapBehind(); hud.Notify(ReachedShrine ? "산신각으로 돌아왔습니다" : "산길 입구로 돌아왔습니다", 2f); }
        private void OnDestroy()
        {
            Time.timeScale = 1f;
            if (playerHealth != null) { playerHealth.Died -= OnDefeat; playerHealth.Damaged -= OnPlayerDamaged; }
            if (boss != null && boss.Health != null) boss.Health.Died -= OnVictory;
            if (player != null) player.Fell -= OnFall;
        }
    }
}
