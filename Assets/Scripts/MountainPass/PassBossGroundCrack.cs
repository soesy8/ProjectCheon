using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Cheon.Prototype.MountainPass;

namespace Cheon.Prototype.MountainPass
{
    // 기존 지면 균열 패턴을 원형 연속 폭발로 표시한다. 실제 폭발 순간부터만 VFX를 만든다.
    public class PassBossGroundCrack : MonoBehaviour
    {
        private sealed class CrackCell
        {
            public Vector3 center;
            public float createdAt;
            public LineRenderer line;
            public Vector3[] ringPoints;
        }

        private readonly List<CrackCell> cells = new List<CrackCell>();
        private readonly Stack<CrackCell> availableCells = new Stack<CrackCell>();
        private PassBossPatternSettings settings;
        private PassBossPatternController owner;
        private PassPlayerMovement player;
        private PassBossImpactDust dust;
        private Vector3 origin, direction;
        private float age;
        private bool running, hitPlayer;
        private MaterialPropertyBlock properties;

        public bool AttackFinished { get; private set; } = true;
        public int StepsEmitted { get; private set; }
        public Vector3 Direction => direction;
        public Vector3 Origin => origin;

        public void Configure(PassBossPatternSettings configuration, PassBossPatternController actor, PassPlayerMovement target, PassBossImpactDust impactDust)
        { settings = configuration; owner = actor; player = target; dust = impactDust; properties = new MaterialPropertyBlock(); }

        public void Begin(Vector3 start, Vector3 forward)
        {
            Clear();
            if (!isActiveAndEnabled) return;
            origin = start; direction = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
            running = true; AttackFinished = false;
            Tick(0f);
        }

        public void Tick(float deltaTime)
        {
            if (!running && cells.Count == 0) return;
            age += deltaTime;
            if (running)
            {
                float interval = Mathf.Max(.01f, settings.crack.stepInterval);
                while (StepsEmitted < settings.crack.stepCount && age >= StepsEmitted * interval)
                {
                    float createdAt = StepsEmitted * interval;
                    Vector3 point = origin + direction * ((StepsEmitted + 1) * settings.crack.stepDistance);
                    StepsEmitted++;
                    if (!owner.FindPatternGround(point, out point)) continue;
                    var cell = availableCells.Count > 0 ? availableCells.Pop() : new CrackCell();
                    cell.center = point; cell.createdAt = createdAt;
                    cell.line = CreateExplosionRing(cell);
                    cells.Add(cell); dust.EmitExplosion(point, settings.crack.radius);
                    // 한 프레임에 활성 구간을 지나쳐도 새로 터진 지점의 타격은 한 번 확인한다.
                    CheckDamage(cell);
                    // 피해 처리 중 사망 이벤트가 폭발을 정리했다면 이번 갱신도 즉시 끝낸다.
                    if (!running) return;
                }
                foreach (var cell in cells)
                {
                    if (age - cell.createdAt <= settings.crack.activeDuration) CheckDamage(cell);
                    if (!running) return;
                }
                if (StepsEmitted >= settings.crack.stepCount && age >= (settings.crack.stepCount - 1) * interval + settings.crack.activeDuration)
                { running = false; AttackFinished = true; }
            }
            for (int cellIndex = cells.Count - 1; cellIndex >= 0; cellIndex--)
            {
                var cell = cells[cellIndex];
                if (cell.line == null) { cells.RemoveAt(cellIndex); continue; }
                float progress = Mathf.Clamp01((age - cell.createdAt) / settings.crack.effectLifetime);
                if (progress >= 1f)
                { ReturnCellToPool(cell); cells.RemoveAt(cellIndex); continue; }
                float expansion = Mathf.Lerp(.2f, 1f, Mathf.Clamp01(progress * 4f));
                for (int index = 0; index < cell.ringPoints.Length; index++)
                    cell.line.SetPosition(index, Vector3.Lerp(cell.center + Vector3.up * .035f, cell.ringPoints[index], expansion));
                cell.line.widthMultiplier = settings.crack.lineWidth * (1f - progress);
                Color tint = settings.crack.color; tint.a *= 1f - progress;
                properties.SetColor("_BaseColor", tint); cell.line.SetPropertyBlock(properties);
            }
        }

        private void CheckDamage(CrackCell cell)
        {
            if (hitPlayer) return;
            var body = player.Body;
            Vector3 difference = player.transform.position - cell.center;
            if (Mathf.Abs(difference.y) > body.height) return;
            difference.y = 0;
            if (difference.magnitude <= settings.crack.radius + body.radius && owner.DealPatternDamage(settings.crack.damage)) hitPlayer = true;
        }

        private LineRenderer CreateExplosionRing(CrackCell cell)
        {
            var line = cell.line;
            if (line == null)
            {
                var item = new GameObject("지면 폭발 충격파"); item.transform.SetParent(transform, false);
                line = item.AddComponent<LineRenderer>();
                cell.ringPoints = new Vector3[48];
            }
            line.useWorldSpace = true; line.positionCount = 48; line.loop = true;
            line.sharedMaterial = settings.crack.material; line.widthMultiplier = settings.crack.lineWidth;
            line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
            properties.SetColor("_BaseColor", settings.crack.color); line.SetPropertyBlock(properties);
            // 지면 탐색은 생성 시 한 번만 하고, 충격파 확장은 저장한 원형 점을 사용한다.
            for (int index = 0; index < line.positionCount; index++)
            {
                float angle = index * Mathf.PI * 2f / line.positionCount;
                Vector3 point = cell.center + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * settings.crack.radius;
                if (owner.FindPatternGround(point, out Vector3 ground)) point = ground;
                cell.ringPoints[index] = point + Vector3.up * .035f;
                line.SetPosition(index, Vector3.Lerp(cell.center + Vector3.up * .035f, cell.ringPoints[index], .2f));
            }
            line.enabled = true;
            line.gameObject.SetActive(true);
            return line;
        }

        private void ReturnCellToPool(CrackCell cell)
        {
            if (cell.line == null) return;
            cell.line.enabled = false;
            // 대기 중에는 오브젝트도 끄고, 다음 폭발에서 같은 충격파를 다시 활성화한다.
            cell.line.gameObject.SetActive(false);
            availableCells.Push(cell);
        }

        public void Clear()
        {
            foreach (var cell in cells) ReturnCellToPool(cell);
            cells.Clear(); running = hitPlayer = false; age = 0; StepsEmitted = 0; AttackFinished = true;
        }

        private void OnDisable() => Clear();
    }
}
