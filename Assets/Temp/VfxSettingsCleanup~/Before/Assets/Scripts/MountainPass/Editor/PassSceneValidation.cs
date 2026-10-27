using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cheon.Prototype.MountainPass.Editor
{
    // 필수 연결과 선택 연결을 구분해, 에셋을 교체한 뒤에도 실제 누락만 찾아낸다.
    public static class PassSceneValidation
    {
        private static readonly Dictionary<System.Type, string[]> Required = new Dictionary<System.Type, string[]>
        {
            { typeof(PassGameFlow), new[] { "player", "playerHealth", "boss", "view", "effects", "audioControl", "hud", "environment", "shrine", "entrance", "gourd" } },
            { typeof(PassPlayerMovement), new[] { "animationControl", "view" } },
            { typeof(PassAnimationController), new[] { "characterAnimator", "fanAttachment", "timingSettings", "playbackSettings", "reactionSettings" } },
            { typeof(PassFanAttackCombat), new[] { "animationControl", "boss", "game", "effects", "view", "audioControl", "profile", "damageSettings", "visualEffect" } },
            { typeof(PassBossPatternController), new[] { "settings", "visual", "player", "playerHealth", "game", "effects", "view", "audioControl" } },
            { typeof(PassGourdDrinking), new[] { "animationControl", "movement", "health", "game", "attachment", "drawClip", "drinkingClip" } },
            { typeof(PassGourdAttachment), new[] { "characterAnimator", "drinking", "gourd", "beltSocket", "handSocket", "cork" } },
            { typeof(PassGourdDrinkingPose), new[] { "animationControl", "drinking" } },
            { typeof(PassFanAttackVfx), new[] { "profile", "animationControl", "playerRoot", "particles", "windSector", "attack1WindSector" } },
            { typeof(PassRollDust), new[] { "profile", "animationControl", "playerRoot", "dust" } },
            { typeof(PassShrineInteraction), new[] { "game", "prayerPoint", "respawnPoint" } },
            { typeof(PassHud), new[] { "game", "font", "textSettings" } },
            { typeof(PassEffects), new[] { "ringMaterial" } }
        };
        [MenuItem("천/현재 씬 연결 검사")]
        public static void CheckCurrentScene()
        {
            var issues = CollectIssues(SceneManager.GetActiveScene());
            foreach (string issue in issues) Debug.LogError(issue);
            if (issues.Count == 0) Debug.Log("씬 연결 검사 완료: 필수 참조·스크립트·테스트 의존성에 문제가 없습니다.");
        }
        public static List<string> CollectIssues(Scene scene)
        {
            var issues = new List<string>();
            foreach (var transform in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)))
            {
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) > 0) issues.Add(transform.name + ": 스크립트 연결 누락");
                foreach (var behavior in transform.GetComponents<MonoBehaviour>())
                {
                    if (behavior == null) continue;
                    var serialized = new SerializedObject(behavior);
                    if (Required.TryGetValue(behavior.GetType(), out var fields))
                        foreach (string field in fields)
                        {
                            var value = serialized.FindProperty(field);
                            if (value == null || value.objectReferenceValue == null) issues.Add(transform.name + " / " + behavior.GetType().Name + ": " + (value != null ? value.displayName : field) + " 연결 누락");
                        }
                    var property = serialized.GetIterator();
                    while (property.Next(true))
                        if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == null && property.objectReferenceInstanceIDValue != 0)
                            issues.Add(transform.name + ": " + property.displayName + " 참조가 끊어졌습니다.");
                }
            }
            if (!string.IsNullOrEmpty(scene.path))
                foreach (string path in AssetDatabase.GetDependencies(scene.path, true).Where(p => p.StartsWith("Assets/Test/"))) issues.Add("테스트 폴더 의존성: " + path);
            return issues;
        }
    }
}
