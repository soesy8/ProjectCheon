using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Cheon.Prototype.MountainPass.Editor
{
    // 별도 수치를 복제하지 않고 현재 씬이 참조하는 컴포넌트와 에셋을 직접 편집한다.
    public class PassTuningWindow : EditorWindow
    {
        private static readonly string[] Categories = { "전체", "이동·회피", "공격·부채", "피격·회복", "보스", "카메라", "환경·연출", "공통" };
        private sealed class Source
        {
            public string label;
            public SerializedObject serialized;
        }
        private sealed class Field
        {
            public Source source;
            public string path, label, searchText;
        }
        private sealed class Section
        {
            public int category;
            public string title;
            public bool expanded = true;
            public readonly List<Field> fields = new List<Field>();
        }
        private readonly Dictionary<Object, Source> sources = new Dictionary<Object, Source>();
        private readonly List<Section> sections = new List<Section>();
        private MonoBehaviour[] components;
        private PassAnimationController animationControl;
        [SerializeField] private int category;
        [SerializeField] private string search = "";
        private Vector2 scroll;
        private bool showSources;

        [MenuItem("천/게임 수치 조절")]
        public static void Open() => GetWindow<PassTuningWindow>("게임 수치 조절");
        private void OnEnable()
        {
            minSize = new Vector2(620f, 480f);
            EditorSceneManager.activeSceneChangedInEditMode += SceneChanged;
            EditorApplication.playModeStateChanged += PlayModeChanged;
            Undo.undoRedoPerformed += SynchronizeAnimation;
            RefreshTargets();
        }
        private void OnDisable()
        {
            EditorSceneManager.activeSceneChangedInEditMode -= SceneChanged;
            EditorApplication.playModeStateChanged -= PlayModeChanged;
            Undo.undoRedoPerformed -= SynchronizeAnimation;
            ReleaseSources();
        }
        private void ReleaseSources()
        {
            foreach (var source in sources.Values) source.serialized.Dispose();
            sources.Clear(); sections.Clear();
        }
        private void SceneChanged(Scene previous, Scene next) => RefreshTargets();
        private void PlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode || state == PlayModeStateChange.EnteredPlayMode) RefreshTargets();
        }
        private T Find<T>() where T : Component => components.OfType<T>().FirstOrDefault();
        private Section AddSection(int group, string title) { var section = new Section { category = group, title = title }; sections.Add(section); return section; }
        private Source GetSource(Object target, string label)
        {
            if (target == null) return null;
            if (!sources.TryGetValue(target, out var source))
            {
                source = new Source { label = label, serialized = new SerializedObject(target) };
                sources.Add(target, source);
            }
            return source;
        }
        private void AddFields(Section section, Object target, params string[] paths)
        {
            var source = GetSource(target, section.title);
            if (source == null) return;
            foreach (string path in paths)
            {
                var property = source.serialized.FindProperty(path);
                if (property == null) continue;
                if (property.propertyType == SerializedPropertyType.Generic && !property.isArray) property.isExpanded = true;
                string label = property.displayName;
                if (target is Transform) label = path == "m_LocalPosition" ? "장착 위치 (m)" : path == "m_LocalRotation" ? "장착 회전 (도)" : "크기 배율";
                var labels = new List<string> { label };
                var child = property.Copy(); var end = property.GetEndProperty();
                while (child.NextVisible(true) && !SerializedProperty.EqualContents(child, end)) labels.Add(child.displayName);
                section.fields.Add(new Field { source = source, path = path, label = label, searchText = string.Join(" ", labels) });
            }
        }
        private void AddAll(int group, string title, Object target)
        {
            if (target == null) return;
            var section = AddSection(group, title);
            var source = GetSource(target, title);
            var iterator = source.serialized.GetIterator();
            while (iterator.NextVisible(true))
            {
                if (iterator.depth != 0 || iterator.name.StartsWith("m_")) continue;
                var field = target.GetType().GetField(iterator.name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field == null || IsConnection(field.FieldType)) continue;
                AddFields(section, target, iterator.propertyPath);
            }
        }
        private static bool IsConnection(Type type)
        {
            if (type.IsArray) type = type.GetElementType();
            return typeof(Component).IsAssignableFrom(type) || type == typeof(GameObject) || typeof(ScriptableObject).IsAssignableFrom(type);
        }
        private T Reference<T>(Object target, string path) where T : Object
        {
            if (target == null) return null;
            using (var serialized = new SerializedObject(target)) return serialized.FindProperty(path)?.objectReferenceValue as T;
        }
        private void AddSocket(int group, string title, Object target, string path)
        { var socket = Reference<Transform>(target, path); if (socket != null) AddFields(AddSection(group, title), socket, "m_LocalPosition", "m_LocalRotation", "m_LocalScale"); }
        private void AddTiming(Section section, string state)
        {
            if (animationControl == null || animationControl.SavedTimings == null) return;
            var timing = animationControl.SavedTimings;
            var array = GetSource(timing, "동작별 입력 허용 시점").serialized.FindProperty("actions");
            for (int index = 0; index < array.arraySize; index++)
            {
                var item = array.GetArrayElementAtIndex(index);
                if (item.FindPropertyRelative("state").stringValue != state) continue;
                foreach (string name in new[] { "moveCancel", "dodgeCancel", "attack1Cancel", "attack2Cancel", "attack3Cancel", "equipmentCancel", "autoReturn", "blendDuration" })
                    AddFields(section, timing, item.propertyPath + "." + name);
                return;
            }
        }
        private void RefreshTargets()
        {
            ReleaseSources();
            components = SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true)).Where(component => component != null).ToArray();
            var movement = Find<PassPlayerMovement>();
            animationControl = movement != null ? movement.AnimationControl : null;
            var playback = animationControl != null ? animationControl.SavedPlaybackSettings : null;
            var profile = Find<PassFanAttackVfx>()?.Profile;
            var damage = Find<PassFanAttackDamageSettings>();
            var fan = Find<PassFanAttachment>();
            var gourd = Find<PassGourdAttachment>();

            AddFields(AddSection(1, "기본 이동·달리기"), movement, "moveSpeed", "sprintSpeed", "rotationSpeed", "gravity", "inputBufferSeconds", "respawnBelow");
            AddFields(AddSection(1, "달리기 애니메이션"), playback, "run2Speed", "run2StartFrame", "run2EndFrame");
            var dodge = AddSection(1, "구르기 이동·애니메이션·입력 전환");
            AddFields(dodge, movement, "dodgeDistance", "dodgeDecelerationPower", "dodgeSteering", "dodgeRecoverySteering", "dodgeRecoveryStartFrame");
            AddFields(dodge, playback, "rollSpeed", "rollStartFrame", "rollEndFrame"); AddTiming(dodge, "ROLL");
            AddAll(1, "구르기 흙먼지", Find<PassRollDust>()?.Profile);

            for (int attack = 1; attack <= 3; attack++)
            {
                string prefix = "attack" + attack;
                var section = AddSection(2, attack + "타 · 동작·피해·궤적" + (attack == 2 ? "" : "·바람"));
                AddFields(section, animationControl, prefix + "Speed", prefix + "StartFrame", prefix + "EndFrame");
                AddFields(section, damage, prefix + ".baseDamage", prefix + ".directWeight");
                if (attack != 2) AddFields(section, damage, prefix + ".windWeight");
                AddFields(section, profile, prefix); AddTiming(section, "ATTACK" + attack);
                if (attack != 2) AddFields(section, profile, attack == 1 ? "attack1Wind" : "wind");
            }
            AddFields(AddSection(2, "구름 원화·공격 흙먼지"), profile, "cloudSpiral", "dust", "dustMaterial");
            AddFields(AddSection(2, "공통 공격 판정"), Find<PassFanAttackCombat>(), "targetLayers", "maximumComboTurnAngle", "directionalTargetRange", "directionalTargetHalfAngle");
            var equipment = AddSection(2, "발도·납도 동작");
            AddFields(equipment, animationControl, "equipmentWhileMoving", "drawSpeed", "movingDrawSpeedMultiplier", "sheatheSpeed", "equipmentClipLength", "drawGrabTime", "drawOpenStart", "drawOpenEnd", "sheatheCloseStart", "sheatheCloseEnd", "sheatheStowTime");
            AddTiming(AddSection(2, "발도 입력 전환"), "DRAW");
            AddTiming(AddSection(2, "납도 입력 전환"), "SHEATHE");
            AddFields(AddSection(2, "부채 장착"), fan, "fanScale", "attachmentBlendDuration");
            AddSocket(2, "부채 오른손 장착 위치", fan, "handSocket"); AddSocket(2, "부채 허리 수납 위치", fan, "stowedSocket");

            AddAll(3, "플레이어 체력·피격 무적", movement != null ? movement.GetComponent<PassHealth>() : null);
            AddAll(3, "호리병 횟수·회복량·마시기 동작", Find<PassGourdDrinking>());
            AddAll(3, "호리병 장착·입 접촉 보정", gourd); AddAll(3, "마시기 상체 자세", Find<PassGourdDrinkingPose>());
            AddSocket(3, "호리병 왼손 장착 위치", gourd, "handSocket"); AddSocket(3, "호리병 허리 수납 위치", gourd, "beltSocket");
            AddAll(3, "산신각 기도·호리병 충전", Find<PassShrineInteraction>());
            AddAll(3, "강피격·기상·사망", animationControl != null ? animationControl.ReactionSettings : null);
            AddTiming(AddSection(3, "일반 피격 입력 전환"), "HIT");
            AddFields(AddSection(3, "낙하 피해·체크포인트"), Find<PassGameFlow>(), "fallDamage", "checkpointFallDistance");
            AddAll(4, "보스 체력·패턴·범위·피해", Find<PassBossPatternSettings>());
            AddAll(4, "보스 산문 통과 판정", Find<PassBossEntrance>());
            AddAll(5, "탐험·전투 카메라", Find<PassCamera>());
            AddAll(6, "정상·보스 전투 안개 전환", Find<PassMountainMist>());
            foreach (var fog in components.OfType<PassHeightFog>())
            {
                string title = fog.name switch { "HeightFog_VFX" => "산길 전체 안개", "ApproachMountainMist" => "정상 접근 안개", "RearMistCurtain" => "후방 안개", "BossAftermathMist" => "보스 전투 후 안개", _ => "공간 안개 · " + fog.name };
                AddAll(6, title, fog);
            }
            AddAll(6, "산문 환경·조명 전환", Find<PassWorldTransition>());
            AddAll(6, "계단 접근 조명", Find<PassStairAtmosphere>());
            AddFields(AddSection(7, "동작 공통 혼합 시간"), animationControl, "transitionDuration", "returnBlendDuration", "locomotionBlendDuration");
            AddAll(7, "효과음", Find<PassAudio>()); AddAll(7, "공통 파문 효과", Find<PassEffects>());
            AddAll(7, "프레임 제한", components.FirstOrDefault(component => component.GetType().Name == "FrameRateLimiter"));
            sections.RemoveAll(section => section.fields.Count == 0);
            Repaint();
        }
        private void SynchronizeAnimation()
        {
            if (!EditorApplication.isPlaying || animationControl == null) return;
            animationControl.ReloadSavedPlaybackSettings(); animationControl.ReloadSavedTimings();
        }
        private void ApplyChanges()
        {
            bool animationChanged = false;
            foreach (var source in sources.Values)
            {
                var serialized = source.serialized;
                if (serialized.targetObject == null || !serialized.hasModifiedProperties || !serialized.ApplyModifiedProperties()) continue;
                if (animationControl != null && (serialized.targetObject == animationControl.SavedPlaybackSettings || serialized.targetObject == animationControl.SavedTimings)) animationChanged = true;
                if (serialized.targetObject is Component component)
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                    if (!EditorApplication.isPlaying) EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
                }
            }
            if (animationChanged) SynchronizeAnimation();
        }
        private void SaveSettings()
        {
            ApplyChanges();
            foreach (var target in sources.Keys) if (target != null && EditorUtility.IsPersistent(target)) AssetDatabase.SaveAssetIfDirty(target);
            var scene = SceneManager.GetActiveScene();
            if (scene.isDirty) EditorSceneManager.SaveScene(scene);
            ShowNotification(new GUIContent("현재 씬과 연결된 설정 에셋을 저장했습니다."));
        }
        private bool Matches(string text) => string.IsNullOrWhiteSpace(search) || text.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        private void OnGUI()
        {
            float previousLabelWidth = EditorGUIUtility.labelWidth;
            try { EditorGUIUtility.labelWidth = Mathf.Clamp(position.width * .47f, 260f, 430f); DrawWindow(); }
            finally { EditorGUIUtility.labelWidth = previousLabelWidth; }
        }
        private void DrawWindow()
        {
            EditorGUILayout.LabelField("현재 씬", SceneManager.GetActiveScene().name);
            EditorGUI.BeginChangeCheck(); category = EditorGUILayout.Popup("카테고리", category, Categories);
            search = EditorGUILayout.TextField("항목 검색", search);
            if (EditorGUI.EndChangeCheck()) scroll = Vector2.zero;
            EditorGUILayout.HelpBox("같은 카테고리의 동작·피해·효과를 한 페이지에서 조절합니다. 실행 중 씬 수치 변경은 재생 종료 시 복원됩니다.", MessageType.Info);
            foreach (var source in sources.Values) if (source.serialized.targetObject != null) source.serialized.UpdateIfRequiredOrScript();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            int lastCategory = -1;
            foreach (var section in sections)
            {
                if (category != 0 && section.category != category) continue;
                bool titleMatches = Matches(section.title) || Matches(Categories[section.category]);
                if (!titleMatches && !section.fields.Any(field => Matches(field.searchText))) continue;
                if (lastCategory != section.category)
                { EditorGUILayout.Space(8f); EditorGUILayout.LabelField(Categories[section.category], EditorStyles.boldLabel); lastCategory = section.category; }
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    section.expanded = EditorGUILayout.Foldout(section.expanded, section.title, true);
                    if (!section.expanded && string.IsNullOrWhiteSpace(search)) continue;
                    using (new EditorGUI.IndentLevelScope())
                        foreach (var field in section.fields)
                        {
                            if (!titleMatches && !Matches(field.searchText) || field.source.serialized.targetObject == null) continue;
                            var property = field.source.serialized.FindProperty(field.path);
                            if (property == null) continue;
                            if (property.propertyType == SerializedPropertyType.Quaternion)
                            {
                                EditorGUI.BeginChangeCheck(); var euler = EditorGUILayout.Vector3Field(field.label, property.quaternionValue.eulerAngles);
                                if (EditorGUI.EndChangeCheck()) property.quaternionValue = Quaternion.Euler(euler);
                            }
                            else EditorGUILayout.PropertyField(property, new GUIContent(field.label), true);
                        }
                }
            }
            showSources = EditorGUILayout.Foldout(showSources, "실제 연결된 씬·설정 에셋 보기", true);
            if (showSources) using (new EditorGUI.DisabledScope(true))
                foreach (var source in sources.Values) EditorGUILayout.ObjectField(source.label, source.serialized.targetObject, typeof(Object), true);
            EditorGUILayout.EndScrollView(); ApplyChanges();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("연결 새로 확인")) RefreshTargets();
                if (GUILayout.Button("씬 연결 검사")) PassSceneValidation.CheckCurrentScene();
                using (new EditorGUI.DisabledScope(EditorApplication.isPlaying)) if (GUILayout.Button("씬·에셋 저장")) SaveSettings();
            }
        }
    }
}
