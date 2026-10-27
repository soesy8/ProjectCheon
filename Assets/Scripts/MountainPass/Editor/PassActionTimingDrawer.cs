using UnityEditor;
using UnityEngine;

namespace Cheon.Prototype.MountainPass.Editor
{
    // 저장되는 동작 식별자는 유지하고, 조절 화면에서는 한국어 동작 이름을 사용한다.
    [CustomPropertyDrawer(typeof(PassActionTimings.ActionTiming))]
    public class PassActionTimingDrawer : PropertyDrawer
    {
        private static readonly string[] States = { "ATTACK1", "ATTACK2", "ATTACK3", "DRAW", "SHEATHE", "HIT", "ROLL" };
        private static readonly GUIContent[] Names = { new GUIContent("공격 1"), new GUIContent("공격 2"), new GUIContent("공격 3"), new GUIContent("발도"), new GUIContent("납도"), new GUIContent("피격"), new GUIContent("구르기") };
        private static readonly string[] Fields = { "moveCancel", "dodgeCancel", "attack1Cancel", "attack2Cancel", "attack3Cancel", "equipmentCancel", "autoReturn", "blendDuration" };
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
            (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing) * (property.isExpanded ? Fields.Length + 2 : 1);
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            float step = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            position.height = EditorGUIUtility.singleLineHeight;
            var state = property.FindPropertyRelative("state");
            property.isExpanded = EditorGUI.Foldout(position, property.isExpanded, PassActionTimings.Label(state.stringValue), true);
            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                position.y += step;
                int selected = System.Array.IndexOf(States, state.stringValue);
                int next = EditorGUI.Popup(position, new GUIContent("동작 종류"), selected, Names);
                if (next >= 0 && next != selected) state.stringValue = States[next];
                foreach (string field in Fields)
                { position.y += step; EditorGUI.PropertyField(position, property.FindPropertyRelative(field)); }
                EditorGUI.indentLevel--;
            }
            EditorGUI.EndProperty();
        }
    }
}
