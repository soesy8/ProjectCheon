using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Cheon.Testing
{
    // 한 판의 흐름만 담당한다. 전투 수치는 각 담당 컴포넌트에서 조절한다.
    public class Test_CheonSession : MonoBehaviour
    {
        public enum Chapter { Introduction, Training, Battle, Victory, Defeat }
        [SerializeField] private Test_CheonPlayer player;
        [SerializeField] private Test_CheonBoss boss;
        [SerializeField] private Test_CheonWorld world;
        [SerializeField] private Test_CheonHud hud;
        [SerializeField] private Test_CheonAudio sound;
        [SerializeField] private Vector3 shrinePosition = new Vector3(0, 0, -10);
        public Chapter CurrentChapter { get; private set; }
        public bool Paused { get; private set; }
        public bool CanMove => !Paused && (CurrentChapter == Chapter.Training || CurrentChapter == Chapter.Battle || CurrentChapter == Chapter.Victory);
        public bool InSpirit => world.SpiritAmount > 0.5f;
        public float BoundaryZ => world.BoundaryZ;
        public float BattleSeconds { get; private set; }
        public int DodgedAttacks { get; private set; }
        public string Notice { get; private set; }
        private float noticeUntil;
        private bool reachedGate;
        private bool wasRunningInBackground;

        private void Awake() { wasRunningInBackground = Application.runInBackground; Application.runInBackground = true; }
        private void OnDestroy() { Time.timeScale = 1; Application.runInBackground = wasRunningInBackground; }

        private void Start()
        {
            Time.timeScale = 1;
            CurrentChapter = Chapter.Introduction;
            boss.gameObject.SetActive(false);
            Notify("WASD로 이동 · 마우스 또는 J로 부채 공격", 5);
            hud.RefreshOverlay();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.enterKey.wasPressedThisFrame && CurrentChapter == Chapter.Introduction) BeginTraining();
                if (keyboard.escapeKey.wasPressedThisFrame && CurrentChapter != Chapter.Introduction && CurrentChapter != Chapter.Defeat) TogglePause();
                if (keyboard.rKey.wasPressedThisFrame && (CurrentChapter == Chapter.Defeat || CurrentChapter == Chapter.Victory || Paused)) Restart();
                if (keyboard.mKey.wasPressedThisFrame) sound.ToggleMute();
                if (keyboard.fKey.wasPressedThisFrame && CanMove && !InSpirit && Vector3.Distance(player.transform.position, shrinePosition) < 3)
                {
                    player.Restore();
                    Notify("수련의 숨결 · 생명과 기력을 회복했습니다.");
                }
            }
            if (!CanMove) return;
            if (CurrentChapter == Chapter.Battle) BattleSeconds += Time.deltaTime;
            Vector3 position = player.transform.position;
            if (CurrentChapter == Chapter.Training && position.z > BoundaryZ - 3 && !reachedGate)
            {
                reachedGate = true;
                Notify("장승 사이의 경계문을 통과하세요.", 5);
            }
            // 문을 실제로 통과해야 영계에 진입한다. 씬을 바꾸지 않아 이동이 끊기지 않는다.
            if (CurrentChapter == Chapter.Training && position.z > BoundaryZ + 1.2f)
            {
                CurrentChapter = Chapter.Battle;
                world.SetSpirit(true);
                boss.gameObject.SetActive(true);
                boss.BeginBattle();
                sound.PlayCue(Test_CheonAudio.Cue.Gate);
                Notify("타락한 수호장승 · 붉은 공격 예고를 보고 피하세요.", 6);
            }
            if (CurrentChapter == Chapter.Victory && position.z < BoundaryZ - 1.2f && InSpirit)
            {
                world.SetSpirit(false);
                Notify("수련지에 평온이 돌아왔습니다. R로 다시 도전할 수 있습니다.", 7);
            }
            if (Time.time > noticeUntil) Notice = "";
        }

        public void BeginTraining()
        {
            CurrentChapter = Chapter.Training;
            hud.RefreshOverlay();
            Notify("경계문 너머의 수호장승을 정화하세요.", 6);
        }

        public void TogglePause()
        {
            Paused = !Paused;
            Time.timeScale = Paused ? 0 : 1;
            hud.RefreshOverlay();
        }

        public void Win()
        {
            CurrentChapter = Chapter.Victory;
            player.Restore();
            sound.PlayCue(Test_CheonAudio.Cue.Victory);
            Notify("정화 완료 · 경계문으로 돌아가거나 R로 다시 수련하세요.", 15);
            hud.RefreshOverlay();
        }

        public void Lose()
        {
            CurrentChapter = Chapter.Defeat;
            sound.PlayCue(Test_CheonAudio.Cue.Hurt);
            hud.RefreshOverlay();
        }

        public void RecordDodge() { DodgedAttacks++; }
        public void Notify(string message, float duration = 3) { Notice = message; noticeUntil = Time.time + duration; }
        public void Restart()
        {
            Time.timeScale = 1;
            // Build Settings에 없는 독립 테스트 씬도 Editor에서는 바로 재시작할 수 있다.
            #if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(SceneManager.GetActiveScene().path, new LoadSceneParameters(LoadSceneMode.Single));
            UnityEditor.EditorApplication.delayCall += FocusGameView;
            #else
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            #endif
        }
        #if UNITY_EDITOR
        private static void FocusGameView()
        {
            var type = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
            if (type != null) UnityEditor.EditorWindow.GetWindow(type).Focus();
        }
        #endif
    }
}
