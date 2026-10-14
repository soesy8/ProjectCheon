using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Cheon.Testing
{
    // 짧게 나타나는 전투 연출을 한곳에서 갱신하고 수명이 끝나면 정리한다.
    public class Test_CheonEffects : MonoBehaviour
    {
        public static readonly Color Jade = new Color(.35f, 1, .85f);
        public static readonly Color Gold = new Color(1, .78f, .35f);
        public static readonly Color Red = new Color(1, .23f, .3f);
        [SerializeField] private Material effectMaterial;
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private Test_CheonCamera view;
        private readonly List<Effect> active = new List<Effect>();
        private Mesh disk, ring;
        private MaterialPropertyBlock properties;
        private class Effect
        {
            public GameObject item;
            public Renderer renderer;
            public Color color;
            public Vector3 velocity;
            public float age, life, radius;
            public int kind;
            public Test_CheonBoss boss;
            public Test_CheonPlayer player;
            public float damage;
            public bool hit;
            public TMP_Text label;
        }

        private void Awake()
        {
            disk = CircleMesh(0);
            ring = CircleMesh(.92f);
            properties = new MaterialPropertyBlock();
        }

        private Mesh CircleMesh(float inner)
        {
            const int count = 80;
            Vector3[] vertices = new Vector3[count * 2];
            int[] triangles = new int[count * 6];
            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2 / count;
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                vertices[i * 2] = direction * inner;
                vertices[i * 2 + 1] = direction;
                int next = (i + 1) % count;
                int t = i * 6;
                triangles[t] = i * 2; triangles[t + 1] = next * 2 + 1; triangles[t + 2] = i * 2 + 1;
                triangles[t + 3] = i * 2; triangles[t + 4] = next * 2; triangles[t + 5] = next * 2 + 1;
            }
            Mesh mesh = new Mesh { name = "Test_EffectCircle" };
            mesh.vertices = vertices; mesh.triangles = triangles; mesh.RecalculateNormals();
            return mesh;
        }

        private Effect MeshEffect(string title, Vector3 position, Mesh mesh, Color color, float life, float radius, int kind)
        {
            GameObject item = new GameObject(title, typeof(MeshFilter), typeof(MeshRenderer));
            item.transform.SetParent(transform);
            item.transform.position = position + Vector3.up * .065f;
            item.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = item.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = effectMaterial;
            Effect effect = new Effect { item = item, renderer = renderer, color = color, life = life, radius = radius, kind = kind };
            item.transform.localScale = Vector3.one * radius;
            active.Add(effect);
            Paint(effect, color);
            return effect;
        }

        public void Ring(Vector3 position, float radius, Color color, float life)
        { MeshEffect("Test_Ripple", position, ring, color, life, radius, 0); }

        public GameObject Telegraph(Vector3 position, float radius, float life)
        {
            MeshEffect("Test_DangerBoundary", position, ring, Red, life, radius, 2);
            return MeshEffect("Test_DangerCircle", position, disk, new Color(1, .12f, .25f, .2f), life, radius, 1).item;
        }

        public GameObject Lane(Vector3 from, Vector3 to, float width, float life)
        {
            Vector3 direction = to - from;
            var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(item.GetComponent<Collider>());
            item.name = "Test_DangerLane";
            item.transform.SetParent(transform);
            item.transform.position = (from + to) / 2 + Vector3.up * .06f;
            item.transform.rotation = Quaternion.LookRotation(direction);
            item.transform.localScale = new Vector3(width, .035f, direction.magnitude + 2);
            var renderer = item.GetComponent<Renderer>(); renderer.sharedMaterial = effectMaterial;
            var effect = new Effect { item = item, renderer = renderer, color = new Color(1, .16f, .24f, .3f), life = life, kind = 1 };
            active.Add(effect); Paint(effect, effect.color);
            return item;
        }

        public void Slash(Vector3 position, Vector3 forward, float radius, Color color)
        {
            GameObject item = new GameObject("Test_FanArc", typeof(LineRenderer));
            item.transform.SetParent(transform);
            var line = item.GetComponent<LineRenderer>();
            line.sharedMaterial = effectMaterial;
            line.widthMultiplier = .16f;
            line.positionCount = 23;
            line.useWorldSpace = true;
            for (int i = 0; i < line.positionCount; i++)
                line.SetPosition(i, position + Quaternion.AngleAxis(-75 + i * 150f / 22, Vector3.up) * forward * radius);
            var effect = new Effect { item = item, renderer = line, color = color, life = .24f, kind = 2 };
            active.Add(effect); Paint(effect, color);
        }

        public void Burst(Vector3 position, Color color, int count, float life)
        {
            for (int i = 0; i < count; i++)
            {
                var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(item.GetComponent<Collider>());
                item.name = "Test_SpiritSpark";
                item.transform.SetParent(transform);
                item.transform.position = position;
                item.transform.localScale = Vector3.one * Random.Range(.055f, .14f);
                item.transform.rotation = Random.rotation;
                var renderer = item.GetComponent<Renderer>(); renderer.sharedMaterial = effectMaterial;
                var effect = new Effect { item = item, renderer = renderer, color = color, life = life * Random.Range(.65f, 1.15f), velocity = Random.onUnitSphere * Random.Range(2, 6) + Vector3.up * 1.5f, kind = 3 };
                active.Add(effect); Paint(effect, color);
            }
        }

        public void Talisman(Vector3 position, Vector3 direction, Test_CheonBoss boss, float damage)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(item.GetComponent<Collider>());
            item.name = "Test_FlyingTalisman";
            item.transform.SetParent(transform);
            item.transform.position = position;
            item.transform.localScale = new Vector3(.32f, .5f, .08f);
            var renderer = item.GetComponent<Renderer>(); renderer.sharedMaterial = effectMaterial;
            var effect = new Effect { item = item, renderer = renderer, color = Gold, life = 2, velocity = direction * 16, kind = 4, boss = boss, damage = damage };
            active.Add(effect); Paint(effect, Gold);
        }

        public void Seal(Vector3 position, float radius)
        {
            Ring(position, radius, Gold, 1.2f);
            Ring(position, radius * .7f, Jade, 1.4f);
            for (int i = 0; i < 8; i++)
            {
                Vector3 offset = Quaternion.AngleAxis(i * 45, Vector3.up) * Vector3.forward * radius * .84f;
                var symbol = MeshEffect("Test_SealStroke", position + offset, disk, Gold, 1.1f, .35f, 0);
                symbol.item.transform.localScale = new Vector3(.13f, 1, .6f);
                symbol.item.transform.rotation = Quaternion.Euler(0, i * 45, 0);
                symbol.kind = 2;
            }
            Burst(position + Vector3.up * .1f, Jade, 30, .85f);
        }

        public void Shockwave(Vector3 position, Test_CheonPlayer player, float damage)
        {
            var effect = MeshEffect("Test_Shockwave", position, ring, Red, 1.2f, 9, 5);
            effect.player = player; effect.damage = damage;
        }

        public void DamageNumber(Vector3 position, int amount)
        {
            if (font == null) return;
            GameObject item = new GameObject("Test_Damage", typeof(TextMeshPro));
            item.transform.SetParent(transform); item.transform.position = position;
            var label = item.GetComponent<TextMeshPro>();
            label.font = font; label.fontSize = 5; label.text = amount.ToString();
            label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.sizeDelta = new Vector2(4, 1);
            active.Add(new Effect { item = item, label = label, life = .75f, color = Gold, velocity = Vector3.up * 1.5f, kind = 6 });
        }

        public void Shake(float strength) { if (view != null) view.AddShake(strength); }
        private void Paint(Effect effect, Color color) { if (effect.renderer == null) return; properties.SetColor("_BaseColor", color); effect.renderer.SetPropertyBlock(properties); }

        private void Update()
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                Effect effect = active[i];
                if (effect.item == null) { active.RemoveAt(i); continue; }
                effect.age += Time.deltaTime;
                float progress = effect.age / effect.life;
                if (progress >= 1) { Destroy(effect.item); active.RemoveAt(i); continue; }
                Color color = effect.color; color.a *= 1 - progress;
                if (effect.kind == 0) effect.item.transform.localScale = Vector3.one * effect.radius * Mathf.Lerp(.65f, 1.15f, progress);
                if (effect.kind == 1) color.a = effect.color.a * Mathf.Lerp(.4f, 1.8f, progress);
                if (effect.kind == 3 || effect.kind == 6) effect.item.transform.position += effect.velocity * Time.deltaTime;
                if (effect.kind == 4)
                {
                    if (effect.boss != null && effect.boss.gameObject.activeInHierarchy && !effect.boss.Defeated)
                    {
                        Vector3 target = effect.boss.transform.position + Vector3.up * 2;
                        effect.velocity = (target - effect.item.transform.position).normalized * 18;
                        if (Vector3.Distance(target, effect.item.transform.position) < 1.1f)
                        {
                            effect.boss.TakeDamage(effect.damage);
                            Destroy(effect.item); active.RemoveAt(i); continue;
                        }
                    }
                    effect.item.transform.position += effect.velocity * Time.deltaTime;
                    effect.item.transform.Rotate(0, 0, Time.deltaTime * 420);
                }
                if (effect.kind == 5)
                {
                    float radius = progress * effect.radius;
                    effect.item.transform.localScale = Vector3.one * radius;
                    if (!effect.hit && Mathf.Abs(Vector3.Distance(effect.item.transform.position, effect.player.transform.position) - radius) < .65f)
                    { effect.player.TakeDamage(effect.damage); effect.hit = true; }
                }
                if (effect.label != null)
                { effect.label.color = color; effect.item.transform.rotation = view.transform.rotation; }
                else Paint(effect, color);
            }
        }

        private void OnDestroy() { Destroy(disk); Destroy(ring); }
    }
}
