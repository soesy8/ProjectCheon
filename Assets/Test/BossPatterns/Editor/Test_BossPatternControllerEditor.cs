using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Cheon.Tests.BossPatterns.Editor
{
    [CustomEditor(typeof(Test_BossPatternController))]
    public class Test_BossPatternControllerEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.Add(new HelpBox("패턴 수치는 Test_BossPatternSettings 빈 오브젝트에서 조절합니다.", HelpBoxMessageType.Info));
            root.Add(new PropertyField(serializedObject.FindProperty("settings"), "패턴 설정"));
            root.Add(new Button(() =>
            {
                var settings = serializedObject.FindProperty("settings").objectReferenceValue;
                if (settings != null) Selection.activeObject = settings;
            }) { text = "패턴 설정 오브젝트 선택" });
            var status = new Label(); root.Add(status);
            root.schedule.Execute(() =>
            {
                if (target is Test_BossPatternController enemy)
                    status.text = $"상태: {enemy.Phase}\n점프: {enemy.JumpsCompleted} / 엎어지기: {enemy.FallsCompleted} / 회전: {enemy.TurnsCompleted}";
            }).Every(100);
            return root;
        }
    }
}
