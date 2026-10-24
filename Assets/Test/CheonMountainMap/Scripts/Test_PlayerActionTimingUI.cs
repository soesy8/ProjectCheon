using System.Globalization;
using Cheon.Testing.PlayerFan;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Cheon.Testing.MountainMap
{
    public class Test_PlayerActionTimingUI : MonoBehaviour
    {
        [SerializeField] private Test_PlayerFanAnimationController animationControl;
        [SerializeField] private TMP_FontAsset koreanFont;
        [SerializeField] private TMP_Settings textSettings;
        [SerializeField] private bool startExpanded = true;
        private RectTransform canvas;
        private GameObject content;
        private TMP_Text currentState, feedback, explanation;
        private UnityEngine.UI.Image progressFill;
        private readonly UnityEngine.UI.Slider[] sliders = new UnityEngine.UI.Slider[8];
        private readonly TMP_InputField[] inputs = new TMP_InputField[8];
        private readonly TMP_Text[] labels = new TMP_Text[8];
        private readonly UnityEngine.UI.Button[] actionButtons = new UnityEngine.UI.Button[7];
        private int selectedAction;
        private bool dirty;
        private static readonly string[] States = { "ATTACK1", "ATTACK2", "ATTACK3", "DRAW", "SHEATHE", "HIT", "ROLL" };
        private static readonly string[] Captions = { "일반 이동 캔슬", "회피 캔슬", "공격 1로 전환", "공격 2로 전환", "공격 3으로 전환", "발도 / 납도로 전환", "자동 대기 복귀", "전환 블렌드 (초)" };
        private Test_PlayerActionTimings.ActionTiming Selected => animationControl.RuntimeTimings.Get(States[selectedAction]);

        public void Configure(Test_PlayerFanAnimationController controller, TMP_FontAsset font, TMP_Settings settings)
        {
            animationControl = controller;
            koreanFont = font;
            textSettings = settings;
        }

        private void Start()
        {
            if (animationControl == null || animationControl.RuntimeTimings == null)
            {
                Debug.LogError("플레이어의 전환 타이밍 설정 에셋을 연결하세요.", this);
                enabled = false;
                return;
            }
            canvas = Test_PlayerTestUi.Canvas(transform, koreanFont, textSettings, 10);
            Test_PlayerTestUi.EnsureEventSystem(transform);
            var panel = Test_PlayerTestUi.Panel("Test_TimingPanel", canvas, Vector2.one,
                new Vector2(-20f, -20f), new Vector2(440f, 828f), true);
            Test_PlayerTestUi.Button("Test_TogglePanel", panel.transform, koreanFont, "액션 전환 타이밍  [Tab]", 16f, 12f, 408f, TogglePanel);
            content = Test_PlayerTestUi.Rect("Test_Content", panel.transform, 0f, 52f, 440f, 776f).gameObject;
            currentState = Test_PlayerTestUi.Text("Test_CurrentAction", content.transform, koreanFont, "", 18f, 5f, 404f, 54f, 17f, Test_PlayerTestUi.Paper);
            Test_PlayerTestUi.Text("Test_SelectLabel", content.transform, koreanFont, "설정할 출발 액션", 18f, 67f, 404f, 23f, 14f, Test_PlayerTestUi.Muted);
            for (int i = 0; i < States.Length; i++)
            {
                int index = i;
                actionButtons[i] = Test_PlayerTestUi.Button("Test_Select_" + States[i], content.transform, koreanFont,
                    Test_PlayerActionTimings.Label(States[i]), 18f + i % 3 * 138f, 96f + i / 3 * 40f, 128f, () => SelectAction(index));
            }
            var track = Test_PlayerTestUi.Rect("Test_ProgressTrack", content.transform, 18f, 218f, 404f, 6f).gameObject.AddComponent<UnityEngine.UI.Image>();
            track.color = new Color(0.18f, 0.30f, 0.31f);
            track.raycastTarget = false;
            progressFill = Test_PlayerTestUi.Rect("Test_ProgressFill", track.transform, 0f, 0f, 1f, 6f).gameObject.AddComponent<UnityEngine.UI.Image>();
            progressFill.color = Test_PlayerTestUi.Jade;
            progressFill.raycastTarget = false;
            for (int i = 0; i < sliders.Length; i++)
            {
                int field = i;
                float y = 238f + i * 46f;
                labels[i] = Test_PlayerTestUi.Text("Test_Label_" + i, content.transform, koreanFont, Captions[i], 18f, y, 310f, 23f, 15f, Test_PlayerTestUi.Paper);
                sliders[i] = Test_PlayerTestUi.Slider("Test_TimingSlider_" + i, content.transform, 18f, y + 25f, 302f, i == 7 ? 0.4f : 1f);
                sliders[i].onValueChanged.AddListener(value => SetValue(field, value));
                inputs[i] = Test_PlayerTestUi.Number("Test_TimingInput_" + i, content.transform, koreanFont, 344f, y + 6f, 78f, value =>
                {
                    if (Test_PlayerTestUi.TryNumber(value, out float number)) SetValue(field, field == 7 ? number : number / 100f);
                    RefreshFields();
                });
            }
            explanation = Test_PlayerTestUi.Text("Test_Explanation", content.transform, koreanFont,
                "%는 재생 구간 기준입니다. 0 = 즉시, 100 = 끝.\n일반 이동과 회피는 각 캔슬 시점부터 허용됩니다.\n자동 복귀를 앞당기면 캔슬 시점도 함께 제한됩니다.",
                18f, 610f, 404f, 66f, 13f, Test_PlayerTestUi.Muted);
            Test_PlayerTestUi.Button("Test_SaveTimings", content.transform, koreanFont, "현재 설정 저장", 18f, 684f, 196f, SaveTimings);
            Test_PlayerTestUi.Button("Test_ReloadTimings", content.transform, koreanFont, "저장값 불러오기", 226f, 684f, 196f, ReloadTimings);
            feedback = Test_PlayerTestUi.Text("Test_SaveStatus", content.transform, koreanFont,
                "조절 즉시 적용 · 저장하면 다음 실행에도 유지", 18f, 727f, 404f, 42f, 13f, Test_PlayerTestUi.Jade);
            SelectAction(0);
            content.SetActive(startExpanded);
            ResizePanel();
        }

        private void Update()
        {
            if (canvas == null) return;
            if (!Test_PlayerTestUi.IsTyping && Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame) TogglePanel();
            float progress = animationControl.IsActionPlaying ? animationControl.PlaybackProgress : 0f;
            currentState.text = $"현재: {Test_PlayerActionTimings.Label(animationControl.SelectedState)}  {progress * 100f:0}%\n이동 {(animationControl.CanMove ? "가능" : "잠김")} · 회피 {(animationControl.CanDodge ? "가능" : "잠김")} · 공격 {(animationControl.CanPlayAttack(1) || animationControl.CanPlayAttack(2) || animationControl.CanPlayAttack(3) ? "가능" : "잠김")}";
            progressFill.rectTransform.sizeDelta = new Vector2(404f * progress, 6f);
            bool active = animationControl.SelectedState == States[selectedAction];
            for (int i = 0; i < labels.Length; i++)
            {
                labels[i].color = active && i < 7 && progress >= Value(i) ? Test_PlayerTestUi.Jade : Test_PlayerTestUi.Paper;
            }
        }

        private void SelectAction(int index)
        {
            selectedAction = index;
            for (int i = 0; i < actionButtons.Length; i++)
                actionButtons[i].GetComponent<UnityEngine.UI.Image>().color = i == index ? new Color(0.16f, 0.43f, 0.37f) : new Color(0.11f, 0.23f, 0.24f);
            bool equipment = animationControl.UsesUpperBodyEquipment && (index == 3 || index == 4);
            labels[0].text = equipment ? "하체 이동 유지 (상체만 발도 / 납도)" : Captions[0];
            sliders[0].interactable = inputs[0].interactable = !equipment;
            explanation.text = equipment ?
                "발도·납도 중에는 하체 이동이 항상 가능합니다.\n회피와 공격의 허용 시점은 각각 %로 조절합니다.\n상체 동작은 하체 이동과 독립적으로 재생됩니다." :
                "%는 재생 구간 기준입니다. 0 = 즉시, 100 = 끝.\n일반 이동과 회피는 각 캔슬 시점부터 허용됩니다.\n자동 복귀를 앞당기면 캔슬 시점도 함께 제한됩니다.";
            RefreshFields();
        }

        private float Value(int field) => field switch
        {
            0 => Selected.moveCancel, 1 => Selected.dodgeCancel, 2 => Selected.attack1Cancel,
            3 => Selected.attack2Cancel, 4 => Selected.attack3Cancel, 5 => Selected.equipmentCancel,
            6 => Selected.autoReturn, _ => Selected.blendDuration
        };

        private void SetValue(int field, float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return;
            switch (field)
            {
                case 0: Selected.moveCancel = value; break;
                case 1: Selected.dodgeCancel = value; break;
                case 2: Selected.attack1Cancel = value; break;
                case 3: Selected.attack2Cancel = value; break;
                case 4: Selected.attack3Cancel = value; break;
                case 5: Selected.equipmentCancel = value; break;
                case 6: Selected.autoReturn = value; break;
                case 7: Selected.blendDuration = value; break;
            }
            Selected.Validate();
            dirty = true;
            feedback.text = "변경됨 · 실행 중 적용 / 유지하려면 저장하세요.";
            RefreshFields();
        }

        private void RefreshFields()
        {
            for (int i = 0; i < sliders.Length; i++)
            {
                float value = Value(i);
                sliders[i].SetValueWithoutNotify(value);
                if (!inputs[i].isFocused)
                    inputs[i].SetTextWithoutNotify((i == 7 ? value : value * 100f).ToString(i == 7 ? "0.00" : "0.0", CultureInfo.InvariantCulture));
                if (i == 0 && !inputs[i].interactable) inputs[i].SetTextWithoutNotify("유지");
            }
        }

        private void TogglePanel()
        {
            content.SetActive(!content.activeSelf);
            ResizePanel();
        }

        private void ResizePanel() => ((RectTransform)content.transform.parent).sizeDelta = new Vector2(440f, content.activeSelf ? 828f : 58f);

        private void SaveTimings()
        {
#if UNITY_EDITOR
            animationControl.RuntimeTimings.Validate();
            UnityEditor.EditorUtility.CopySerialized(animationControl.RuntimeTimings, animationControl.SavedTimings);
            UnityEditor.EditorUtility.SetDirty(animationControl.SavedTimings);
            UnityEditor.AssetDatabase.SaveAssetIfDirty(animationControl.SavedTimings);
            dirty = false;
            feedback.text = "저장 완료 · 다음 Play에서도 이 값을 사용합니다.";
#else
            feedback.text = "설정 에셋 저장은 Unity 에디터에서 사용할 수 있습니다.";
#endif
        }

        private void ReloadTimings()
        {
            animationControl.ReloadSavedTimings();
            dirty = false;
            RefreshFields();
            feedback.text = "마지막 저장값을 불러왔습니다.";
        }

        private void OnEnable() { if (canvas != null) canvas.gameObject.SetActive(true); }
        private void OnDisable()
        {
            if (canvas != null) canvas.gameObject.SetActive(false);
            if (dirty) Debug.Log("전환 타이밍의 저장하지 않은 변경은 이번 실행에만 적용됩니다.", this);
        }
    }
}
