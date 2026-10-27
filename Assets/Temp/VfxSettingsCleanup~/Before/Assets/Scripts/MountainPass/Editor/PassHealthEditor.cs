using UnityEditor;
using UnityEngine;

namespace Cheon.Prototype.MountainPass.Editor
{
    [CustomEditor(typeof(PassHealth))]
    public class PassHealthEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var health = (PassHealth)target;
            if (health.TryGetComponent<PassBossPatternController>(out var boss))
            {
                EditorGUILayout.HelpBox("보스와 잡몹의 최대 체력·피격 간격은 보스 패턴 설정의 '전투 수치'에서 조절합니다.", MessageType.Info);
                var settings = new SerializedObject(boss).FindProperty("settings").objectReferenceValue;
                using (new EditorGUI.DisabledScope(true)) EditorGUILayout.ObjectField("실제 전투 수치 설정", settings, typeof(PassBossPatternSettings), true);
                if (settings != null && GUILayout.Button("보스 패턴 설정 선택")) Selection.activeObject = settings;
            }
            else DrawDefaultInspector();
            if (Application.isPlaying)
                using (new EditorGUI.DisabledScope(true)) EditorGUILayout.FloatField("현재 생명력", health.Current);
        }
    }
}
