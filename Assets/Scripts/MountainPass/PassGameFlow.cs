using UnityEngine;
using UnityEngine.InputSystem;

namespace Cheon.Prototype.MountainPass
{
    [DefaultExecutionOrder(-40)]
    public class PassGameFlow : MonoBehaviour
    {
        public enum RunState { Intro, Playing, Paused, Defeat, Victory, Cinematic }
        [SerializeField] private PassPlayerMovement player;
        [SerializeField] private PassHealth playerHealth;
        [SerializeField] private PassBossController boss;
        [SerializeField] private PassCamera view;
        [SerializeField] private PassEffects effects;
        [SerializeField] private PassAudio audioControl;
        [SerializeField] private PassHud hud;
        [SerializeField] private PassWorldTransition environment;
        [SerializeField] private PassShrineInteraction shrine;
        [SerializeField] private PassBossEntrance entrance;
        [SerializeField] private Vector3 arenaCheckpoint = new Vector3(-15f, 37.2f, -43f);
        private Vector3 startPosition, checkpoint;
        private Quaternion startRotation, checkpointRotation;
        public RunState State { get; private set; }
        public bool IsRunning => State == RunState.Playing;
        public bool InEncounter { get; private set; }
        public bool ReachedShrine { get; private set; }
        public float Elapsed { get; private set; }
        public PassHealth PlayerHealth => playerHealth;
        public PassBossController Boss => boss;
        public PassCamera View => view;
        public PassShrineInteraction Shrine => shrine;
        public PassBossEntrance Entrance => entrance;
        public Transform Player => player.transform;
        public Vector3 ObjectivePosition => InEncounter ? boss.transform.position : ReachedShrine && entrance != null ? entrance.transform.position : shrine != null ? shrine.Position : startPosition;
        public string ObjectiveTitle => InEncounter ? "장승 수문장을 쓰러뜨려라" : ReachedShrine ? "영적 산문을 지나 수문장을 찾아라" : "산신각에 올라 기도를 올려라";

        public void Configure(PassPlayerMovement movement, PassHealth vitality, PassBossController guardian, PassCamera cameraControl, PassEffects visuals, PassAudio sounds, PassHud display, PassWorldTransition transition)
        { player = movement; playerHealth = vitality; boss = guardian; view = cameraControl; effects = visuals; audioControl = sounds; hud = display; environment = transition; }
        public void ConfigureRefinement(PassShrineInteraction sanctuary, PassBossEntrance gate) { shrine = sanctuary; entrance = gate; }
        private void Awake()
        {
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
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && (State == RunState.Playing || State == RunState.Paused)) TogglePause();
            if (State == RunState.Intro && keyboard != null && keyboard.enterKey.wasPressedThisFrame) BeginJourney();
            if ((State == RunState.Defeat || State == RunState.Victory) && keyboard != null && keyboard.rKey.wasPressedThisFrame) RetryCheckpoint();
            if (!IsRunning) return;
            Elapsed += Time.deltaTime;
            // 절벽 아래에 착지해 진행이 끊기면 마지막으로 기도한 산신각으로 돌아간다.
            if (player.transform.position.y < checkpoint.y - 12f)
            { player.ResetToSpawn(); OnFall(); return; }
        }
        public void BeginJourney() => ResetRun(false);
        public void BeginBossChallenge() => ResetRun(true);
        private void ResetRun(bool atBoss)
        {
            Time.timeScale = 1f; State = RunState.Playing; InEncounter = false; ReachedShrine = atBoss;
            Elapsed = 0f;
            if (entrance != null) entrance.ResetEntrance();
            boss.ResetEncounter(); playerHealth.Restore(); player.GetComponent<PassPlayerCombat>().ResetHits();
            if (environment != null) environment.SetSpiritWorld(atBoss);
            checkpoint = atBoss ? arenaCheckpoint : startPosition;
            checkpointRotation = atBoss ? Quaternion.LookRotation(Vector3.ProjectOnPlane(boss.transform.position - checkpoint, Vector3.up)) : startRotation;
            player.SetCheckpoint(checkpoint, checkpointRotation); player.Teleport(checkpoint, checkpointRotation); player.InputEnabled = true;
            view.LockBoss(false); view.SnapBehind();
            hud.Notify(atBoss ? "산문의 수문장이 깨어납니다" : "등불을 따라 산신각으로 올라가세요", 4f);
            if (atBoss) BeginEncounter();
        }
        public void RestAtShrine(Vector3 position, Quaternion rotation)
        {
            if (!IsRunning || InEncounter) return;
            ReachedShrine = true; checkpoint = position; checkpointRotation = rotation;
            player.SetCheckpoint(checkpoint, checkpointRotation); playerHealth.Restore();
            audioControl.Bell(); effects.Impact(player.transform.position, new Color(0.35f, 1f, 0.8f), 3f);
            hud.Notify("산신각의 가호 · 생명력이 회복되었습니다", 4f);
        }
        public void BeginEncounter()
        {
            if (InEncounter || !IsRunning) return;
            InEncounter = true;
            if (environment != null) environment.SetSpiritWorld(true);
            if (entrance != null && !entrance.HasPlayed)
            {
                State = RunState.Cinematic; player.InputEnabled = false; playerHealth.Invulnerable = true;
                player.AnimationControl.PlayIdle(); entrance.PlayPresentation();
            }
            else CompleteBossPresentation();
        }
        public void CompleteBossPresentation()
        {
            if (!InEncounter) return;
            State = RunState.Playing; player.InputEnabled = true; playerHealth.Invulnerable = false;
            boss.WakeUp(); view.LockBoss(true);
            hud.Notify("천하대장군 · 산문의 수문장", 4f);
        }
        public void ShowNotice(string message, float duration) => hud.Notify(message, duration);
        public void TogglePause()
        {
            if (State != RunState.Playing && State != RunState.Paused) return;
            bool paused = State == RunState.Playing;
            State = paused ? RunState.Paused : RunState.Playing; Time.timeScale = paused ? 0f : 1f; player.InputEnabled = !paused;
        }
        public void RetryCheckpoint()
        {
            Time.timeScale = 1f; State = RunState.Playing; InEncounter = false;
            if (entrance != null) entrance.ResetEntrance();
            boss.ResetEncounter(); playerHealth.Restore();
            if (environment != null) environment.SetSpiritWorld(checkpoint.y > 34f);
            player.Teleport(checkpoint, checkpointRotation); player.InputEnabled = true; view.LockBoss(false); view.SnapBehind();
            hud.Notify(ReachedShrine ? "산신각의 가호로 다시 일어났습니다" : "산길 입구로 돌아왔습니다", 3f);
            if (checkpoint.y > 34f) BeginEncounter();
        }
        private void OnPlayerDamaged(float damage)
        {
            view.AddShake(0.9f); audioControl.Hurt(); hud.FlashDamage();
            if (!playerHealth.IsDead) player.AnimationControl.PlayHit();
        }
        private void OnDefeat() { State = RunState.Defeat; player.InputEnabled = false; player.AnimationControl.PlayHit(); }
        private void OnVictory() { State = RunState.Victory; player.InputEnabled = false; player.AnimationControl.PlayIdle(); audioControl.Bell(); }
        private void OnFall() { playerHealth.TakeDamage(20f); view.SnapBehind(); hud.Notify(ReachedShrine ? "산신각으로 돌아왔습니다" : "산길 입구로 돌아왔습니다", 2f); }
        private void OnDestroy()
        {
            Time.timeScale = 1f;
            if (playerHealth != null) { playerHealth.Died -= OnDefeat; playerHealth.Damaged -= OnPlayerDamaged; }
            if (boss != null && boss.Health != null) boss.Health.Died -= OnVictory;
            if (player != null) player.Fell -= OnFall;
        }
    }
}
