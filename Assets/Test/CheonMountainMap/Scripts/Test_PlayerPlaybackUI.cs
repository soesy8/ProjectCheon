using System.Globalization;
using Cheon.Testing.PlayerFan;
using TMPro;
using UnityEngine;

namespace Cheon.Testing.MountainMap
{
    public class Test_PlayerPlaybackUI : MonoBehaviour
    {
        [SerializeField] private Test_PlayerFanAnimationController animationControl;
        [SerializeField] private TMP_FontAsset koreanFont;
        [SerializeField] private TMP_Settings textSettings;
        private RectTransform canvas;
        private TMP_Text feedback;
        private readonly TMP_Text[] labels = new TMP_Text[2];
        private readonly TMP_InputField[] speedInputs = new TMP_InputField[2];
        private readonly TMP_InputField[] startInputs = new TMP_InputField[2];
        private readonly TMP_InputField[] endInputs = new TMP_InputField[2];
        private readonly UnityEngine.UI.Slider[] speedSliders = new UnityEngine.UI.Slider[2];
        private bool dirty;

        public void Configure(Test_PlayerFanAnimationController controller, TMP_FontAsset font, TMP_Settings settings)
        {
            animationControl = controller;
            koreanFont = font;
            textSettings = settings;
        }

        private void Start()
        {
            if (animationControl == null || animationControl.SavedPlaybackSettings == null)
            {
                Debug.LogError("플레이어의 재생 수치 설정 에셋을 연결하세요.", this);
                enabled = false;
                return;
            }
            canvas = Test_PlayerTestUi.Canvas(transform, koreanFont, textSettings, 10);
            Test_PlayerTestUi.EnsureEventSystem(transform);
            var panel = Test_PlayerTestUi.Panel("Test_PlaybackPanel", canvas, Vector2.up,
                new Vector2(20f, -432f), new Vector2(316f, 356f), true);
            Test_PlayerTestUi.Text("Test_PlaybackTitle", panel.transform, koreanFont, "구르기 / 달리기 재생", 16f, 12f, 284f, 28f, 20f, Test_PlayerTestUi.Jade);
            Test_PlayerTestUi.Text("Test_PlaybackHint", panel.transform, koreanFont, "속도 배율 / 원본 FBX 프레임", 16f, 42f, 284f, 23f, 13f, Test_PlayerTestUi.Muted);
            for (int i = 0; i < 2; i++)
            {
                int index = i;
                int playbackIndex = i + 4;
                string name = i == 0 ? "Roll" : "Run";
                float y = 72f + i * 92f;
                labels[i] = Test_PlayerTestUi.Text("Test_PlaybackLabel_" + name, panel.transform, koreanFont, "", 16f, y, 202f, 26f, 14f, Test_PlayerTestUi.Paper);
                speedInputs[i] = Test_PlayerTestUi.Number("Test_PlaybackSpeed_" + name, panel.transform, koreanFont, 222f, y, 78f, value =>
                {
                    if (Test_PlayerTestUi.TryNumber(value, out float speed)) SetSpeed(index, speed);
                });
                Test_PlayerTestUi.Text("Test_StartLabel_" + name, panel.transform, koreanFont, "시작 F", 16f, y + 31f, 48f, 26f, 14f, Test_PlayerTestUi.Paper);
                startInputs[i] = Test_PlayerTestUi.Number("Test_PlaybackStart_" + name, panel.transform, koreanFont, 64f, y + 31f, 66f, value =>
                {
                    if (!int.TryParse(value, out int frame)) return;
                    animationControl.SetPlaybackFrameRange(playbackIndex, frame, animationControl.GetPlaybackEndFrame(playbackIndex));
                    MarkChanged();
                });
                Test_PlayerTestUi.Text("Test_EndLabel_" + name, panel.transform, koreanFont, "종료 F", 174f, y + 31f, 48f, 26f, 14f, Test_PlayerTestUi.Paper);
                endInputs[i] = Test_PlayerTestUi.Number("Test_PlaybackEnd_" + name, panel.transform, koreanFont, 222f, y + 31f, 78f, value =>
                {
                    if (!int.TryParse(value, out int frame)) return;
                    animationControl.SetPlaybackFrameRange(playbackIndex, animationControl.GetPlaybackStartFrame(playbackIndex), frame);
                    MarkChanged();
                });
                startInputs[i].contentType = endInputs[i].contentType = TMP_InputField.ContentType.IntegerNumber;
                speedSliders[i] = Test_PlayerTestUi.Slider("Test_PlaybackSlider_" + name, panel.transform, 16f, y + 62f, 284f, 3f);
                speedSliders[i].minValue = 0.05f;
                speedSliders[i].onValueChanged.AddListener(value => SetSpeed(index, value));
            }
            Test_PlayerTestUi.Button("Test_SavePlayback", panel.transform, koreanFont, "재생 수치 저장", 16f, 258f, 136f, SavePlayback);
            Test_PlayerTestUi.Button("Test_ReloadPlayback", panel.transform, koreanFont, "저장값 불러오기", 164f, 258f, 136f, ReloadPlayback);
            feedback = Test_PlayerTestUi.Text("Test_PlaybackFeedback", panel.transform, koreanFont,
                "조절 즉시 적용 · 저장하면 다음 실행에도 유지", 16f, 302f, 284f, 42f, 13f, Test_PlayerTestUi.Jade);
        }

        private void Update()
        {
            if (canvas == null) return;
            for (int i = 0; i < 2; i++)
            {
                int index = i + 4;
                float speed = i == 0 ? animationControl.rollSpeed : animationControl.run2Speed;
                labels[i].text = $"{(i == 0 ? "구르기" : "달리기")} · 0~{animationControl.GetPlaybackMaximumFrame(index)}F · 속도 ×";
                speedSliders[i].SetValueWithoutNotify(speed);
                SyncInput(speedInputs[i], speed.ToString("0.00", CultureInfo.InvariantCulture));
                SyncInput(startInputs[i], animationControl.GetPlaybackStartFrame(index).ToString());
                SyncInput(endInputs[i], animationControl.GetPlaybackEndFrame(index).ToString());
            }
        }

        private static void SyncInput(TMP_InputField input, string value)
        {
            if (!input.isFocused && input.text != value) input.SetTextWithoutNotify(value);
        }

        private void SetSpeed(int index, float value)
        {
            float speed = Mathf.Clamp(value, 0.05f, 3f);
            if (index == 0) animationControl.rollSpeed = speed;
            else animationControl.run2Speed = speed;
            MarkChanged();
        }

        private void MarkChanged()
        {
            dirty = true;
            feedback.text = "변경됨 · 다음 실행에도 유지하려면 저장하세요.";
        }

        private void SavePlayback()
        {
#if UNITY_EDITOR
            var settings = animationControl.SavedPlaybackSettings;
            animationControl.CopyPlaybackTo(settings);
            UnityEditor.EditorUtility.SetDirty(settings);
            UnityEditor.AssetDatabase.SaveAssetIfDirty(settings);
            dirty = false;
            feedback.text = "저장 완료 · 다음 Play에서도 이 값을 사용합니다.";
#else
            feedback.text = "설정 에셋 저장은 Unity 에디터에서 사용할 수 있습니다.";
#endif
        }

        private void ReloadPlayback()
        {
            animationControl.ReloadSavedPlaybackSettings();
            dirty = false;
            feedback.text = "마지막 저장값을 불러왔습니다.";
        }

        private void OnEnable() { if (canvas != null) canvas.gameObject.SetActive(true); }
        private void OnDisable()
        {
            if (canvas != null) canvas.gameObject.SetActive(false);
            if (dirty) Debug.Log("재생 수치의 저장하지 않은 변경은 이번 실행에만 적용됩니다.", this);
        }
    }
}
