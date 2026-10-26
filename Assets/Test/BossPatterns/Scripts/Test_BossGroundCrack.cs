using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Cheon.Prototype.MountainPass;

namespace Cheon.Tests.BossPatterns
{
    // 기존 지면 균열 패턴을 원형 연속 폭발로 표시한다. 실제 폭발 순간부터만 VFX를 만든다.
    public class Test_BossGroundCrack : MonoBehaviour
    {
        private sealed class CrackCell
        {
            public Vector3 center;
            public float createdAt;
            public LineRenderer line;
            public Vector3[] ringPoints;
        }

        private readonly List<CrackCell> cells = new List<CrackCell>();
        private Test_BossPatternSettings settings;
        private Test_BossPatternController owner;
        private PassPlayerMovement player;
        private Test_BossImpactDust dust;
        private Vector3 origin, direction;
        private float age;
        private bool running, hitPlayer;
        private MaterialPropertyBlock properties;

        public bool AttackFinished { get; private set; } = true;
        public int StepsEmitted { get; private set; }
        public Vector3 Direction => direction;
        public Vector3 Origin => origin;

        public void Configure(Test_BossPatternSettings configuration, Test_BossPatternController actor, PassPlayerMovement target, Test_BossImpactDust impactDust)
        { settings = configuration; owner = actor; player = target; dust = impactDust; properties = new MaterialPropertyBlock(); }

        public void Begin(Vector3 start, Vector3 forward)
        {
            Clear();
            origin = start; direction = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
            running = true; AttackFinished = false;
            Tick(0f);
        }

        public void Tick(float deltaTime)
        {
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
                    var cell = new CrackCell { center = point, createdAt = createdAt };
                    cell.line = CreateExplosionRing(cell);
                    cells.Add(cell); dust.EmitExplosion(point, settings.crack.radius);
                    // 한 프레임에 활성 구간을 지나쳐도 새로 터진 지점의 타격은 한 번 확인한다.
                    CheckDamage(cell);
                }
                foreach (var cell in cells)
                    if (age - cell.createdAt <= settings.crack.activeDuration) CheckDamage(cell);
                if (StepsEmitted >= settings.crack.stepCount && age >= (settings.crack.stepCount - 1) * interval + settings.crack.activeDuration)
                { running = false; AttackFinished = true; }
            }
            foreach (var cell in cells)
            {
                if (cell.line == null) continue;
                float progress = Mathf.Clamp01((age - cell.createdAt) / settings.crack.effectLifetime);
                if (progress >= 1f)
                { cell.line.gameObject.SetActive(false); Destroy(cell.line.gameObject); cell.line = null; continue; }
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
            var body = player.GetComponent<CharacterController>();
            Vector3 difference = player.transform.position - cell.center;
            if (Mathf.Abs(difference.y) > body.height) return;
            difference.y = 0;
            if (difference.magnitude <= settings.crack.radius + body.radius && owner.DealPatternDamage(settings.crack.damage)) hitPlayer = true;
        }

        private LineRenderer CreateExplosionRing(CrackCell cell)
        {
            var item = new GameObject("Test_ActiveGroundExplosion"); item.transform.SetParent(transform, false);
            var line = item.AddComponent<LineRenderer>();
            line.useWorldSpace = true; line.positionCount = 48; line.loop = true;
            line.sharedMaterial = settings.crack.material; line.widthMultiplier = settings.crack.lineWidth;
            line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
            properties.SetColor("_BaseColor", settings.crack.color); line.SetPropertyBlock(properties);
            cell.ringPoints = new Vector3[line.positionCount];
            // 지면 탐색은 생성 시 한 번만 하고, 충격파 확장은 저장한 원형 점을 사용한다.
            for (int index = 0; index < line.positionCount; index++)
            {
                float angle = index * Mathf.PI * 2f / line.positionCount;
                Vector3 point = cell.center + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * settings.crack.radius;
                if (owner.FindPatternGround(point, out Vector3 ground)) point = ground;
                cell.ringPoints[index] = point + Vector3.up * .035f;
                line.SetPosition(index, Vector3.Lerp(cell.center + Vector3.up * .035f, cell.ringPoints[index], .2f));
            }
            return line;
        }

        public void Clear()
        {
            foreach (var cell in cells)
                if (cell.line != null) { cell.line.gameObject.SetActive(false); Destroy(cell.line.gameObject); }
            cells.Clear(); running = hitPlayer = false; age = 0; StepsEmitted = 0; AttackFinished = true;
        }
    }
}
