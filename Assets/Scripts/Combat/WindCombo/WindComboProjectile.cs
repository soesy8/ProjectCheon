using System.Collections.Generic;
using UnityEngine;

namespace Cheon.Combat
{
    public class WindComboProjectile : MonoBehaviour
    {
        private WindComboEffects effects;
        private Transform owner, visual;
        private Vector3 origin, direction;
        private float distance = .7f, range, width, speed, damage;
        private LayerMask layers;
        private readonly HashSet<WindComboTarget> hitTargets = new HashSet<WindComboTarget>();

        public void Initialize(WindComboEffects source, Transform attacker, Vector3 start, Vector3 forward,
            float reach, float breadth, float flightTime, float power, LayerMask collisionLayers, bool visible)
        {
            effects = source; owner = attacker; origin = start + Vector3.up * .95f; direction = forward.normalized;
            range = reach; width = breadth; damage = power; layers = collisionLayers;
            distance = Mathf.Min(distance, range);
            speed = (range - distance) / Mathf.Max(.01f, flightTime);
            transform.position = origin + direction * distance;
            if (visible)
            {
                var child = new GameObject("CloudCrescent", typeof(MeshFilter), typeof(MeshRenderer));
                visual = child.transform;
                visual.SetParent(transform, false);
                visual.rotation = WindComboEffects.CloudRotation(direction);
                // 평면의 오른쪽 끝이 판정의 선두다. 그림을 뒤로 옮겨 4.5m 바깥으로 나가지 않게 한다.
                visual.localPosition = -direction * .8f;
                visual.localScale = new Vector3(1.6f, width / Mathf.Cos(28f * Mathf.Deg2Rad), 1);
                child.GetComponent<MeshFilter>().sharedMesh = source.EffectMesh;
                var renderer = child.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = source.CrescentMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            Sweep(0, distance);
        }

        private void Update()
        {
            float nextDistance = Mathf.Min(range, distance + speed * Time.deltaTime);
            if (Sweep(distance, nextDistance)) return;
            distance = nextDistance;
            transform.position = origin + direction * distance;
            if (distance >= range - .0001f) Finish();
        }

        private bool Sweep(float from, float to)
        {
            Quaternion rotation = Quaternion.LookRotation(direction);
            Vector3 center = origin + direction * ((from + to) * .5f);
            var candidates = Physics.OverlapBox(center, new Vector3(width * .5f, .5f, Mathf.Max(.005f, (to - from) * .5f)),
                rotation, layers, QueryTriggerInteraction.Ignore);
            float wallDistance = to;
            // 벽과 적을 먼저 구분하여 벽 뒤의 적은 같은 프레임에도 맞지 않게 한다.
            foreach (Collider candidate in candidates)
            {
                if (candidate.transform.IsChildOf(owner) || candidate.GetComponentInParent<WindComboTarget>() != null) continue;
                Vector3 point = candidate.ClosestPoint(origin + direction * from);
                wallDistance = Mathf.Min(wallDistance, Mathf.Max(from, Vector3.Dot(point - origin, direction)));
            }
            foreach (Collider candidate in candidates)
            {
                var target = candidate.GetComponentInParent<WindComboTarget>();
                if (target == null || candidate.transform.IsChildOf(owner) || hitTargets.Contains(target)) continue;
                Vector3 point = candidate.ClosestPoint(origin + direction * from);
                if (Vector3.Dot(point - origin, direction) > wallDistance + .001f) continue;
                hitTargets.Add(target);
                target.ReceiveHit(damage, direction, true);
                if (effects != null) effects.Impact(point, true);
            }
            if (wallDistance < to)
            {
                transform.position = origin + direction * wallDistance;
                Finish();
                return true;
            }
            return false;
        }

        private void Finish()
        {
            if (effects != null) effects.Residue(transform.position, direction);
            Destroy(gameObject);
        }
    }
}
