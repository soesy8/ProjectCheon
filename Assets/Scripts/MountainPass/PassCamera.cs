using UnityEngine;
using UnityEngine.InputSystem;

namespace Cheon.Prototype.MountainPass
{
    [RequireComponent(typeof(Camera)), DefaultExecutionOrder(100)]
    public class PassCamera : MonoBehaviour
    {
        [InspectorName("플레이어")]
        [SerializeField] private Transform player;
        [InspectorName("보스 제어기")]
        [SerializeField] private Transform boss;
        [InspectorName("게임 진행 제어기")]
        [SerializeField] private PassGameFlow game;
        [InspectorName("기본 카메라 거리 (m)")]
        [SerializeField, Range(4f, 14f)] private float distance = 7.5f;
        [InspectorName("카메라 상하 각도 (도)")]
        [SerializeField, Range(-75f, 88f)] private float pitch = 28f;
        [InspectorName("카메라 최소 상하 각도 (도)")]
        [SerializeField, Range(-75f, 0f)] private float minimumPitch = -65f;
        [InspectorName("카메라 최대 상하 각도 (도)")]
        [SerializeField, Range(45f, 88f)] private float maximumPitch = 85f;
        [InspectorName("마우스 시점 감도")]
        [SerializeField, Min(.01f)] private float sensitivity = .15f;
        [Tooltip("자유 시점의 상하 입력 감도 비율입니다. 회전 가능 범위는 그대로 유지합니다.")]
        [InspectorName("마우스 상하 감도 비율")]
        [SerializeField, Range(.01f, 1f)] private float verticalSensitivityMultiplier = .25f;
        [InspectorName("마우스 최대 회전 속도 (도/초)")]
        [SerializeField, Min(1f)] private float maximumMouseAngularSpeed = 720f;
        [InspectorName("플레이어 이동 추적 Lerp 속도")]
        [SerializeField, Min(1f)] private float followSpeed = 10f;
        [Header("보스 주시")]
        [Tooltip("장승 중심이 이 영역 안에 있으면 추적 회전을 멈춥니다. 상하 회전은 기본 시점과 플레이어 표시를 우선합니다.")]
        [InspectorName("중앙 추적 사각형 (화면 비율)")]
        [SerializeField] private Rect lockDeadZone = new Rect(.44f, .45f, .12f, .2f);
        [InspectorName("주시 회전 Lerp 속도")]
        [SerializeField, Min(.1f)] private float lockFollowSpeed = 10f;
        [InspectorName("최대 좌우 추적 속도 (도/초)")]
        [SerializeField, Min(1f)] private float maximumYawSpeed = 160f;
        [InspectorName("최대 상하 추적 속도 (도/초)")]
        [SerializeField, Min(1f)] private float maximumPitchSpeed = 60f;
        [Tooltip("평소에는 기본 상하 각도에서 이 범위 안으로만 보정합니다. 큰 점프에서 두 대상을 담기 어려울 때만 더 보정합니다.")]
        [InspectorName("기본 시점의 상하 보정 범위 (도)")]
        [SerializeField, Range(0f, 30f)] private float lockPitchAllowance = 20f;
        [InspectorName("주시 최소 내려다보기 각도 (도)")]
        [SerializeField, Range(0f, 30f)] private float minimumLockPitch = 5f;
        [InspectorName("플레이어·장승 중심 화면 여백")]
        [SerializeField, Range(.02f, .2f)] private float framingMargin = .05f;
        [Tooltip("장승 전신 대신 몸체 중심과 플레이어를 기준으로, 꼭 필요한 경우에만 기본 거리에서 추가로 물러납니다.")]
        [InspectorName("최대 추가 후퇴 거리 (m)")]
        [SerializeField, Range(0f, 15f)] private float maximumFramingRetreat = 12f;
        [InspectorName("점프 상승 선행 보정 시간 (초)")]
        [SerializeField, Range(0f, .3f)] private float framingLookAhead = .12f;
        [InspectorName("근접 시 자동 구도 보정 중지 거리 (m)")]
        [SerializeField, Min(0f)] private float minimumBossFramingDistance = 4f;
        [InspectorName("거리 보정 Lerp 속도")]
        [SerializeField, Min(.1f)] private float framingSpeed = 10f;
        [InspectorName("기본 거리 복귀 Lerp 속도")]
        [SerializeField, Min(.1f)] private float framingReturnSpeed = 5f;
        [InspectorName("최대 거리 보정 속도 (m/초)")]
        [SerializeField, Min(.1f)] private float maximumFramingSpeed = 12f;

        private float yaw, orbitPitch, lookPitch, shake, extraDistance, collisionDistance;
        private Vector3 focus;
        private Vector3 previousBossCenter;
        private bool hasBossSample;
        private Camera viewCamera;
        private CharacterController playerBody;
        private Renderer[] playerRenderers;
        private readonly RaycastHit[] hits = new RaycastHit[32];
        private bool wasObstructed;
        private bool applicationFocused = true;
        private int ignoreMouseUntilFrame;
        public bool BossLocked { get; private set; }
        public float Pitch => pitch;
        public bool BossFramingAllowed => BossLocked && player != null && boss != null &&
            Vector3.ProjectOnPlane(boss.position - player.position, Vector3.up).sqrMagnitude >=
            minimumBossFramingDistance * minimumBossFramingDistance;

        public void Configure(Transform target, Transform enemy, PassGameFlow flow)
        {
            player = target; boss = enemy; game = flow;
            GetComponent<Camera>().orthographic = false;
            GetComponent<Camera>().fieldOfView = 60f;
            SnapBehind();
        }
        private void Start() => SnapBehind();
        public void SnapBehind()
        {
            if (player == null) return;
            viewCamera = GetComponent<Camera>();
            playerBody = player.GetComponent<CharacterController>();
            playerRenderers = player.GetComponentsInChildren<Renderer>(true);
            yaw = player.eulerAngles.y;
            orbitPitch = lookPitch = pitch;
            focus = player.position + Vector3.up * 1.35f;
            extraDistance = 0f; collisionDistance = distance;
            wasObstructed = hasBossSample = false;
            Vector3 position = GetOrbitPosition(0f);
            transform.SetPositionAndRotation(ResolveCollision(position, 0f), Quaternion.Euler(lookPitch, yaw, 0f));
        }
        public void LockBoss(bool locked)
        {
            if (BossLocked == locked) return;
            BossLocked = locked;
            hasBossSample = false;
            // 토글할 때 기본 pitch를 현재 주시 각도로 덮어쓰지 않아 원래 구도로 복귀할 수 있다.
        }
        public void AddShake(float amount) => shake = Mathf.Max(shake, amount);
        public void RefreshCursor()
        {
            bool locked = applicationFocused && game != null && game.IsRunning;
            if (locked && Cursor.lockState != CursorLockMode.Locked) ignoreMouseUntilFrame = Time.frameCount + 2;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
        private void OnApplicationFocus(bool focused) { applicationFocused = focused; RefreshCursor(); }
        private void OnDisable() { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        private void LateUpdate()
        {
            if (player == null) return;
            RefreshCursor();
            if (game != null && game.State == PassGameFlow.RunState.Paused) return;
            float delta = Time.deltaTime;
            if (delta <= 0f) return;
            bool running = game != null && game.IsRunning;
            bool canLockBoss = game != null && game.InEncounter && game.BossHasDetectedPlayer && boss != null && game.Boss != null &&
                game.Boss.Health != null && !game.Boss.Health.IsDead;
            bool typing = PassHud.IsTyping;
            if (!canLockBoss) LockBoss(false);
            if (running && !typing && canLockBoss && Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
                LockBoss(!BossLocked);
            if (running && applicationFocused && !typing && !BossLocked && Cursor.lockState == CursorLockMode.Locked && Mouse.current != null && Time.frameCount > ignoreMouseUntilFrame)
            {
                ApplyMouseMotion(Mouse.current.delta.ReadValue(), delta);
            }

            // 플레이어 이동만 한 번 보간하고, 회전은 같은 반지름의 궤도에 적용해 거리와 높이를 유지한다.
            focus = Vector3.Lerp(focus, player.position + Vector3.up * 1.35f, 1f - Mathf.Exp(-followSpeed * delta));
            float desiredOrbitPitch = BossLocked ? Mathf.Clamp(pitch, minimumLockPitch, 80f) : pitch;
            orbitPitch = FollowAngle(orbitPitch, desiredOrbitPitch, followSpeed, maximumPitchSpeed, delta);
            float desiredPitch = pitch, desiredRetreat = 0f;
            Bounds playerBounds = default;
            if (BossFramingAllowed)
            {
                Vector3 bossCenter = GetBossFocusPoint();
                yaw = FollowAngle(yaw, GetDesiredYaw(bossCenter), lockFollowSpeed, maximumYawSpeed, delta);
                desiredPitch = GetDesiredPitch(bossCenter);
                playerBounds = GetPlayerBounds();
                // 상승을 조금 먼저 읽어 거리 보정을 일찍 시작한다. 큰 이동에도 보정량과 속도는 제한한다.
                float risingLead = hasBossSample ? Mathf.Clamp((bossCenter.y - previousBossCenter.y) / delta * framingLookAhead, 0f, 2f) : 0f;
                previousBossCenter = bossCenter; hasBossSample = true;
                FindFraming(bossCenter + Vector3.up * risingLead, playerBounds, ref desiredPitch, out desiredRetreat);
            }
            float framingResponse = desiredRetreat > extraDistance ? framingSpeed : framingReturnSpeed;
            float smoothedRetreat = Mathf.Lerp(extraDistance, desiredRetreat, 1f - Mathf.Exp(-framingResponse * delta));
            extraDistance = Mathf.MoveTowards(extraDistance, smoothedRetreat, maximumFramingSpeed * delta);
            Vector3 position = ResolveCollision(GetOrbitPosition(extraDistance), delta);
            if (BossFramingAllowed)
            {
                // 보정 중에도 플레이어가 아래로 잘리지 않도록 현재 거리에서 허용되는 상하 각도를 사용한다.
                GetPitchRange(position, playerBounds, GetBossFocusPoint(), false, out float lower, out float upper);
                if (lower <= upper) desiredPitch = Mathf.Clamp(desiredPitch, lower, upper);
            }
            lookPitch = FollowAngle(lookPitch, desiredPitch, BossLocked ? lockFollowSpeed : followSpeed, maximumPitchSpeed, delta);
            shake = Mathf.MoveTowards(shake, 0f, delta * 2f);
            Vector3 tremor = Random.insideUnitSphere * shake * .12f;
            transform.SetPositionAndRotation(position + tremor, Quaternion.Euler(lookPitch, yaw, 0f));
        }

        private void ApplyMouseMotion(Vector2 motion, float delta)
        {
            // 포커스 복귀나 커서 잠금 직후 큰 입력값이 들어와도 한 프레임에 급회전하지 않는다.
            Vector2 scaledMotion = new Vector2(motion.x, motion.y * verticalSensitivityMultiplier) * sensitivity;
            Vector2 turn = Vector2.ClampMagnitude(scaledMotion, maximumMouseAngularSpeed * Mathf.Min(delta, .05f));
            yaw += turn.x;
            pitch = Mathf.Clamp(pitch - turn.y, minimumPitch, maximumPitch);
        }

        public bool IsViewObstruction(Collider obstacle)
        {
            if (obstacle == null || player != null && obstacle.transform.IsChildOf(player)) return false;
            // 보스와 소환 장승은 시야를 잠깐 가려도 카메라를 플레이어 앞으로 밀어내지 않는다.
            return obstacle.GetComponentInParent<PassBossController>() == null;
        }

        private static float FollowAngle(float current, float target, float response, float maximumSpeed, float delta)
        {
            float smoothed = Mathf.LerpAngle(current, target, 1f - Mathf.Exp(-response * delta));
            return Mathf.MoveTowardsAngle(current, smoothed, maximumSpeed * delta);
        }

        private Vector3 GetOrbitPosition(float retreat)
        {
            Quaternion orbit = Quaternion.Euler(orbitPitch, yaw, 0f);
            return focus - orbit * Vector3.forward * (distance + retreat) + orbit * Vector3.right * .35f;
        }

        private float GetDesiredYaw(Vector3 target)
        {
            // 바로 머리 위를 지나는 점프에서는 수평 방향이 뒤집혀도 카메라를 급회전시키지 않는다.
            if (Vector3.ProjectOnPlane(target - player.position, Vector3.up).sqrMagnitude < 2.25f) return yaw;
            Vector3 local = Quaternion.Inverse(Quaternion.Euler(lookPitch, yaw, 0f)) * (target - transform.position);
            float tangent = Mathf.Tan(viewCamera.fieldOfView * Mathf.Deg2Rad * .5f) * viewCamera.aspect;
            float viewportX = .5f + local.x / (2f * Mathf.Max(.001f, local.z) * tangent);
            if (local.z > 0f && viewportX >= lockDeadZone.xMin && viewportX <= lockDeadZone.xMax) return yaw;
            float edge = Mathf.Clamp(viewportX, lockDeadZone.xMin, lockDeadZone.xMax);
            Vector3 toward = target - transform.position;
            float heading = Mathf.Atan2(toward.x, toward.z) * Mathf.Rad2Deg;
            return heading - Mathf.Atan((edge * 2f - 1f) * tangent) * Mathf.Rad2Deg;
        }

        private float GetDesiredPitch(Vector3 target)
        {
            Vector3 local = Quaternion.Inverse(Quaternion.Euler(lookPitch, yaw, 0f)) * (target - transform.position);
            float tangent = Mathf.Tan(viewCamera.fieldOfView * Mathf.Deg2Rad * .5f);
            float viewportY = .5f + local.y / (2f * Mathf.Max(.001f, local.z) * tangent);
            float targetPitch = lookPitch;
            if (local.z > 0f && (viewportY < lockDeadZone.yMin || viewportY > lockDeadZone.yMax))
            {
                float edge = Mathf.Clamp(viewportY, lockDeadZone.yMin, lockDeadZone.yMax);
                Vector3 level = Quaternion.Inverse(Quaternion.Euler(0f, yaw, 0f)) * (target - transform.position);
                targetPitch = (Mathf.Atan((edge * 2f - 1f) * tangent) - Mathf.Atan2(level.y, level.z)) * Mathf.Rad2Deg;
            }
            return Mathf.Clamp(targetPitch, Mathf.Max(minimumLockPitch, pitch - lockPitchAllowance), Mathf.Min(80f, pitch + lockPitchAllowance));
        }

        private void FindFraming(Vector3 bossCenter, Bounds playerBounds, ref float targetPitch, out float retreat)
        {
            retreat = 0f;
            Vector3 level = Quaternion.Inverse(Quaternion.Euler(0f, yaw, 0f)) * (bossCenter - GetOrbitPosition(0f));
            // 옆이나 뒤로 크게 이동한 대상은 회전으로 따라간다. 화면에 억지로 넣기 위해 후퇴하지 않는다.
            bool includeBoss = level.z > 1f && Mathf.Abs(Mathf.Atan2(level.x, level.z) * Mathf.Rad2Deg) < 60f;
            GetPitchRange(GetOrbitPosition(0f), playerBounds, bossCenter, includeBoss, out float lower, out float upper);
            if (lower > upper && includeBoss)
            {
                float near = 0f, far = maximumFramingRetreat;
                // 각도·높이 후보를 교체하지 않고, 필요한 궤도 반지름만 연속적으로 찾는다.
                for (int i = 0; i < 8; i++)
                {
                    float middle = (near + far) * .5f;
                    GetPitchRange(GetOrbitPosition(middle), playerBounds, bossCenter, true, out float min, out float max);
                    if (min <= max) far = middle; else near = middle;
                }
                retreat = far;
                GetPitchRange(GetOrbitPosition(retreat), playerBounds, bossCenter, true, out lower, out upper);
            }
            if (lower > upper)
            {
                targetPitch = upper;
                GetPitchRange(GetOrbitPosition(retreat), playerBounds, bossCenter, false, out lower, out upper);
            }
            if (lower <= upper) targetPitch = Mathf.Clamp(targetPitch, lower, upper);
        }

        private void GetPitchRange(Vector3 position, Bounds playerBounds, Vector3 bossCenter, bool includeBoss, out float lower, out float upper)
        {
            Quaternion inverse = Quaternion.Inverse(Quaternion.Euler(0f, yaw, 0f));
            float minimumElevation = 90f, maximumElevation = -90f;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = playerBounds.center + Vector3.Scale(playerBounds.extents,
                    new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                IncludeElevation(inverse * (corner - position), ref minimumElevation, ref maximumElevation);
            }
            if (includeBoss)
            {
                // 장승의 끝부분 대신 몸체 중심 주변을 담아 큰 점프에서도 과도한 줌아웃을 피한다.
                IncludeElevation(inverse * (bossCenter + Vector3.up * .35f - position), ref minimumElevation, ref maximumElevation);
                IncludeElevation(inverse * (bossCenter - Vector3.up * .35f - position), ref minimumElevation, ref maximumElevation);
            }
            float halfAngle = Mathf.Atan(Mathf.Tan(viewCamera.fieldOfView * Mathf.Deg2Rad * .5f) * (1f - framingMargin * 2f)) * Mathf.Rad2Deg;
            lower = Mathf.Max(minimumLockPitch, -halfAngle - minimumElevation);
            upper = Mathf.Min(80f, halfAngle - maximumElevation);
        }

        private static void IncludeElevation(Vector3 local, ref float minimum, ref float maximum)
        {
            float elevation = Mathf.Atan2(local.y, Mathf.Max(.01f, local.z)) * Mathf.Rad2Deg;
            minimum = Mathf.Min(minimum, elevation); maximum = Mathf.Max(maximum, elevation);
        }

        private Bounds GetPlayerBounds()
        {
            Bounds bounds = playerBody != null && playerBody.enabled ? playerBody.bounds : new Bounds(player.position + Vector3.up, new Vector3(1f, 2f, 1f));
            if (playerRenderers != null)
                foreach (Renderer model in playerRenderers)
                    if (model != null && model.enabled && model.gameObject.activeInHierarchy && (model is MeshRenderer || model is SkinnedMeshRenderer))
                        bounds.Encapsulate(model.bounds);
            bounds.Expand(.2f);
            return bounds;
        }

        private Vector3 ResolveCollision(Vector3 position, float delta)
        {
            Vector3 offset = position - focus;
            float length = offset.magnitude;
            if (length < .001f) return position;
            Vector3 direction = offset / length;
            int count = Physics.SphereCastNonAlloc(focus, .22f, direction, hits, length, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (!IsViewObstruction(hits[i].collider)) continue;
                length = Mathf.Min(length, Mathf.Max(.5f, hits[i].distance - .12f));
            }
            // 벽 관통 방지는 즉시 적용하되, 장애물이 사라진 뒤에는 원래 거리로 부드럽게 돌아간다.
            if (length < offset.magnitude - .01f)
            { collisionDistance = length; wasObstructed = true; }
            else if (wasObstructed && delta > 0f)
            {
                collisionDistance = Mathf.Min(length, Mathf.Lerp(collisionDistance, length, 1f - Mathf.Exp(-followSpeed * delta)));
                if (length - collisionDistance < .01f) wasObstructed = false;
            }
            else collisionDistance = length;
            return focus + direction * collisionDistance;
        }

        private Vector3 GetBossFocusPoint()
        {
            if (game != null && game.Boss is PassBossPatternController guardian && guardian.Body != null)
                return guardian.Body.transform.TransformPoint(guardian.Body.center);
            return boss.position + Vector3.up * 5.5f;
        }

        private void OnValidate()
        {
            lockDeadZone.width = Mathf.Clamp(lockDeadZone.width, .1f, .8f);
            lockDeadZone.height = Mathf.Clamp(lockDeadZone.height, .1f, .8f);
            lockDeadZone.x = Mathf.Clamp(lockDeadZone.x, Mathf.Max(.05f, .5f - lockDeadZone.width), Mathf.Min(.5f, .95f - lockDeadZone.width));
            lockDeadZone.y = Mathf.Clamp(lockDeadZone.y, Mathf.Max(.05f, .5f - lockDeadZone.height), Mathf.Min(.5f, .95f - lockDeadZone.height));
        }
    }
}
