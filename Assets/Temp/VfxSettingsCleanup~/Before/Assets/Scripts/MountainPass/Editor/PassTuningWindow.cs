using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cheon.Prototype.MountainPass.Editor
{
    // 활성 씬에서 실제 사용하는 연결만 모아, 같은 수치를 서로 다른 곳에서 수정하는 혼동을 줄인다.
    public class PassTuningWindow : EditorWindow
    {
        private static readonly string[] Sections = { "플레이어 이동", "공격 동작·부채", "공격 피해", "호리병 회복", "보스 패턴", "강피격·기상·사망", "달리기·구르기 재생", "입력 취소·동작 전환", "공격 시각 효과", "구르기 먼지", "플레이어 카메라", "정상·보스 안개" };
        private Object[] targets;
        private UnityEditor.Editor inspector;
        private int section;
        private Vector2 scroll;
        [MenuItem("천/게임 수치 조절")]
        public static void Open() => GetWindow<PassTuningWindow>("게임 수치 조절");
        private void OnEnable() { EditorSceneManager.activeSceneChangedInEditMode += SceneChanged; RefreshTargets(); }
        private void OnDisable()
        {
            EditorSceneManager.activeSceneChangedInEditMode -= SceneChanged;
            if (inspector != null) DestroyImmediate(inspector);
        }
        private void SceneChanged(Scene previous, Scene next) => RefreshTargets();
        private void RefreshTargets()
        {
            var movement = Find<PassPlayerMovement>();
            var animation = movement != null ? movement.AnimationControl : null;
            targets = new Object[] { movement, animation, Find<PassFanAttackDamageSettings>(), Find<PassGourdDrinking>(), Find<PassBossPatternSettings>(),
                animation != null ? animation.ReactionSettings : null, animation != null ? animation.SavedPlaybackSettings : null,
                animation != null ? animation.SavedTimings : null, Find<PassFanAttackVfx>()?.Profile, Find<PassRollDust>()?.Profile,
                Find<PassCamera>(), Find<PassMountainMist>() };
            if (inspector != null) DestroyImmediate(inspector);
        }
        private static T Find<T>() where T : Component => SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).FirstOrDefault();
        private void OnGUI()
        {
            EditorGUILayout.LabelField("현재 씬", SceneManager.GetActiveScene().name);
            section = EditorGUILayout.Popup("조절할 항목", section, Sections);
            if (GUILayout.Button("현재 씬 연결 새로 확인")) RefreshTargets();
            if (GUILayout.Button("현재 씬 연결 검사")) PassSceneValidation.CheckCurrentScene();
            Object target = targets != null && section < targets.Length ? targets[section] : null;
            if (target == null) { EditorGUILayout.HelpBox("현재 씬에 해당 설정이 연결되어 있지 않습니다.", MessageType.Info); return; }
            using (new EditorGUI.DisabledScope(true)) EditorGUILayout.ObjectField("실제로 사용하는 설정", target, typeof(Object), true);
            if (GUILayout.Button("계층 또는 프로젝트에서 선택")) { Selection.activeObject = target; EditorGUIUtility.PingObject(target); }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            UnityEditor.Editor.CreateCachedEditor(target, null, ref inspector);
            inspector.OnInspectorGUI();
            EditorGUILayout.EndScrollView();
        }
    }
}
