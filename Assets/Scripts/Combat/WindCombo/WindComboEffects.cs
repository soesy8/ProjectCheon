using System.Collections.Generic;
using UnityEngine;

namespace Cheon.Combat
{
    public class WindComboEffects : MonoBehaviour
    {
        [SerializeField] private Transform fan;
        [SerializeField] private Material ribbonMaterial;
        [SerializeField] private Material crescentMaterial;
        [SerializeField] private Material impactMaterial;
        [SerializeField] private Mesh effectMesh;
        [SerializeField] private bool showEffects = true;
        private readonly List<Visual> visuals = new List<Visual>();
        private Visual gather;
        private MaterialPropertyBlock properties;
        private class Visual
        {
            public Transform item;
            public Renderer renderer;
            public float age, life;
            public Vector3 size;
            public bool gather;
            public int swing;
        }

        public bool ShowEffects { get => showEffects; set { showEffects = value; if (!value) EndGather(); } }
        public Mesh EffectMesh => effectMesh;
        public Material CrescentMaterial => crescentMaterial;

        private void Awake() { properties = new MaterialPropertyBlock(); }

        private Visual Create(Material material, Vector3 position, Quaternion rotation, Vector3 size, float life, Transform parent = null)
        {
            var item = new GameObject("WindCloud", typeof(MeshFilter), typeof(MeshRenderer));
            item.transform.SetParent(parent);
            item.transform.SetPositionAndRotation(position, rotation);
            item.GetComponent<MeshFilter>().sharedMesh = effectMesh;
            var renderer = item.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var visual = new Visual { item = item.transform, renderer = renderer, size = size, life = life };
            item.transform.localScale = size;
            visuals.Add(visual);
            return visual;
        }

        // 텍스처의 가로축을 진행 방향, 세로축을 지면에 걸친 폭으로 맞춘다.
        public static Quaternion CloudRotation(Vector3 direction, float tilt = 28)
        {
            Vector3 side = Vector3.Cross(Vector3.up, direction).normalized;
            // 카메라 쪽으로 평면을 기울여 쿼터뷰에서 문양이 선처럼 눕지 않게 한다.
            if (Camera.main != null)
                tilt = Mathf.Abs(tilt) * (Vector3.Dot(side, Camera.main.transform.forward) >= 0 ? 1 : -1);
            Vector3 cloudUp = (side * Mathf.Cos(tilt * Mathf.Deg2Rad) + Vector3.up * Mathf.Sin(tilt * Mathf.Deg2Rad)).normalized;
            return Quaternion.LookRotation(Vector3.Cross(direction, cloudUp), cloudUp);
        }

        public void Slash(int stage)
        {
            if (!showEffects) return;
            var v = Create(ribbonMaterial, transform.position + Vector3.up * .9f + transform.forward * .95f,
                CloudRotation(transform.right, stage == 1 ? 24 : -28),
                new Vector3(stage == 1 ? 2.1f : 2.35f, .65f, 1), stage == 1 ? .14f : .16f, transform);
            v.swing = stage;
        }

        public void BeginGather()
        {
            if (!showEffects) return;
            gather = Create(crescentMaterial, fan.position + transform.forward * .45f,
                CloudRotation(transform.forward, 38), new Vector3(.8f, .75f, 1), .4f, transform);
            gather.gather = true;
        }

        public void UpdateGather(float progress)
        {
            if (gather == null || gather.item == null) return;
            gather.item.position = fan.position + transform.forward * .45f;
            gather.item.rotation = CloudRotation(transform.forward, 38);
            gather.item.localScale = gather.size * Mathf.Lerp(.45f, 1.1f, progress);
        }

        public void EndGather()
        {
            if (gather == null) return;
            if (gather.item != null) Destroy(gather.item.gameObject);
            visuals.Remove(gather);
            gather = null;
        }

        public void Launch(Vector3 origin, Vector3 direction, float range, float width, float flightTime, float damage, LayerMask layers)
        {
            var item = new GameObject("WindCrescentProjectile");
            item.AddComponent<WindComboProjectile>().Initialize(this, transform, origin, direction,
                range, width, flightTime, damage, layers, showEffects);
        }

        public void Impact(Vector3 position, bool strong)
        {
            if (!showEffects) return;
            Camera view = Camera.main;
            Create(impactMaterial, position, view != null ? view.transform.rotation : Quaternion.identity,
                Vector3.one * (strong ? 1.0f : .55f), .11f);
        }

        public void Residue(Vector3 position, Vector3 direction)
        {
            if (showEffects) Create(ribbonMaterial, position, CloudRotation(direction), new Vector3(.9f, .3f, 1), .15f);
        }

        private void Update()
        {
            for (int i = visuals.Count - 1; i >= 0; i--)
            {
                var v = visuals[i];
                if (v.item == null) { visuals.RemoveAt(i); continue; }
                if (v.gather) continue;
                v.age += Time.deltaTime;
                float ratio = Mathf.Clamp01(v.age / v.life);
                if (ratio >= 1) { Destroy(v.item.gameObject); visuals.RemoveAt(i); continue; }
                v.item.localScale = Vector3.Scale(v.size, new Vector3(Mathf.Lerp(.8f, 1.12f, ratio), Mathf.Lerp(.8f, 1.05f, ratio), 1));
                if (v.swing != 0)
                    v.item.localRotation = Quaternion.AngleAxis(Mathf.Lerp(v.swing == 1 ? 25 : -25, v.swing == 1 ? -25 : 25, ratio), Vector3.up)
                        * CloudRotation(Vector3.right, v.swing == 1 ? 24 : -28);
                properties.SetFloat("_Opacity", 1 - Mathf.SmoothStep(0, 1, (ratio - .45f) / .55f));
                v.renderer.SetPropertyBlock(properties);
            }
        }

        private void OnDisable()
        {
            foreach (var v in visuals) if (v.item != null) Destroy(v.item.gameObject);
            visuals.Clear();
            gather = null;
        }
    }
}
