using Cheon.Prototype.MountainPass;
using UnityEngine;

namespace Cheon.Tests.CombatPresentation
{
    // 원본 카메라의 입력·충돌 처리를 유지하고 테스트 씬에서만 얼굴의 화면 여백을 보완한다.
    [DefaultExecutionOrder(110), RequireComponent(typeof(Camera))]
    public class Test_BossFraming : MonoBehaviour
    {
        [SerializeField, InspectorName("게임 진행")] private PassGameFlow game;
        [SerializeField, InspectorName("기존 카메라")] private PassCamera cameraControl;
        [SerializeField, InspectorName("보스 얼굴")] private Renderer head;
        [SerializeField, InspectorName("근접 자유 시점 보조")] private bool assistCloseCombat = true;
        [SerializeField, Min(1), InspectorName("근접 보조 거리")] private float closeDistance = 12f;
        [SerializeField, Range(0, 20), InspectorName("최대 추가 후퇴 거리")] private float maximumRetreat = 14f;
        [SerializeField, Range(.05f, .25f), InspectorName("화면 위 여백")] private float topMargin = .14f;
        [SerializeField, Range(.05f, .3f), InspectorName("화면 아래 HUD 여백")] private float bottomMargin = .2f;
        [SerializeField, Min(.1f), InspectorName("구도 보정 속도")] private float response = 7f;
        private Camera view;
        private CharacterController playerBody;
        private readonly Vector3[] points = new Vector3[13];
        private readonly RaycastHit[] hits = new RaycastHit[32];
        private float retreat, pitchCorrection;
        public float CurrentRetreat => retreat;
        public Vector3 HeadPoint => head != null ? head.bounds.center : transform.position;

        public void Configure(PassGameFlow flow, PassCamera source, Renderer face)
        { game = flow; cameraControl = source; head = face; }

        private void Awake()
        {
            view = GetComponent<Camera>();
            if (game != null) playerBody = game.Player.GetComponent<CharacterController>();
        }

        private void LateUpdate()
        {
            if (game == null || head == null || cameraControl == null || !cameraControl.enabled || !view.enabled) return;
            if (game.State == PassGameFlow.RunState.Paused) return;
            if (!game.InEncounter || game.State != PassGameFlow.RunState.Playing || game.IsTransitioning)
            { retreat = pitchCorrection = 0; return; }
            float delta = Time.deltaTime;
            if (delta <= 0) return;
            Vector3 basePosition = transform.position;
            Vector3 baseAngles = transform.eulerAngles;
            float basePitch = Mathf.DeltaAngle(0, baseAngles.x);
            Quaternion yaw = Quaternion.Euler(0, baseAngles.y, 0);
            Vector3 forward = yaw * Vector3.forward;
            Vector3 toBoss = head.bounds.center - basePosition;
            bool near = Vector3.ProjectOnPlane(game.Player.position - game.Boss.transform.position, Vector3.up).magnitude < closeDistance;
            bool facing = Vector3.Dot(forward, Vector3.ProjectOnPlane(toBoss, Vector3.up).normalized) > .65f;
            bool active = cameraControl.BossLocked || (assistCloseCombat && near && facing);
            float desiredRetreat = 0, desiredPitch = basePitch;
            if (active)
            {
                int count = CollectPoints();
                // 거리를 늘릴 때마다 허용되는 상하 각도를 계산한다. 매 프레임 같은 기준으로 계산해 줌 왕복을 줄인다.
                for (int step = 0; step <= 28; step++)
                {
                    float candidate = maximumRetreat * step / 28f;
                    Vector3 position = basePosition - forward * candidate + Vector3.up * candidate * .08f;
                    bool fits = GetPitchInterval(position, yaw, count, out float lower, out float upper);
                    desiredRetreat = candidate;
                    desiredPitch = lower <= upper ? Mathf.Clamp(basePitch, lower, upper) : (lower + upper) * .5f;
                    if (fits) break;
                }
            }
            float blend = 1 - Mathf.Exp(-response * delta);
            retreat = Mathf.Lerp(retreat, desiredRetreat, blend);
            pitchCorrection = Mathf.Lerp(pitchCorrection, desiredPitch - basePitch, blend);
            Vector3 corrected = basePosition - forward * retreat + Vector3.up * retreat * .08f;
            Vector3 pivot = game.Player.position + Vector3.up * 1.35f;
            Vector3 offset = corrected - pivot;
            float length = offset.magnitude;
            if (length > .01f)
            {
                int count = Physics.SphereCastNonAlloc(pivot, .23f, offset / length, hits, length, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    Transform item = hits[i].transform;
                    if (item.IsChildOf(game.Player) || item.IsChildOf(game.Boss.transform)) continue;
                    length = Mathf.Min(length, Mathf.Max(.5f, hits[i].distance - .12f));
                }
                corrected = pivot + offset.normalized * length;
            }
            transform.SetPositionAndRotation(corrected, Quaternion.Euler(basePitch + pitchCorrection, baseAngles.y, 0));
        }

        private int CollectPoints()
        {
            Bounds player = playerBody != null ? playerBody.bounds : new Bounds(game.Player.position + Vector3.up, new Vector3(1, 2, 1));
            for (int i = 0; i < 8; i++)
                points[i] = player.center + Vector3.Scale(player.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            // 얼굴 Renderer는 넘어질 때도 모델과 함께 움직여 별도 월드 높이 상수가 필요 없다.
            Bounds face = head.bounds;
            points[8] = face.center + Vector3.up * (face.extents.y + 1.5f);
            points[9] = face.center - Vector3.up * face.extents.y;
            points[10] = face.center + Vector3.right * face.extents.x;
            points[11] = face.center - Vector3.right * face.extents.x;
            int count = 12;
            if (game.Boss is PassBossPatternController boss && boss.LandingReserved)
                points[count++] = boss.LandingPosition + Vector3.up * .2f;
            return count;
        }

        private bool GetPitchInterval(Vector3 position, Quaternion yaw, int count, out float lower, out float upper)
        {
            Quaternion inverse = Quaternion.Inverse(yaw);
            float minimum = 90, maximum = -90;
            float tangent = Mathf.Tan(view.fieldOfView * Mathf.Deg2Rad * .5f);
            bool horizontalFits = true;
            for (int i = 0; i < count; i++)
            {
                Vector3 local = inverse * (points[i] - position);
                if (local.z < .2f) { horizontalFits = false; continue; }
                float elevation = Mathf.Atan2(local.y, local.z) * Mathf.Rad2Deg;
                minimum = Mathf.Min(minimum, elevation); maximum = Mathf.Max(maximum, elevation);
                if (Mathf.Abs(local.x / local.z) > tangent * view.aspect * .87f) horizontalFits = false;
            }
            lower = Mathf.Max(-12, Mathf.Atan((bottomMargin * 2 - 1) * tangent) * Mathf.Rad2Deg - minimum);
            upper = Mathf.Min(55, Mathf.Atan((1 - topMargin * 2) * tangent) * Mathf.Rad2Deg - maximum);
            return horizontalFits && lower <= upper;
        }
    }
}
