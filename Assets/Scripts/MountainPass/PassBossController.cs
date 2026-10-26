using UnityEngine;

namespace Cheon.Prototype.MountainPass
{
    [RequireComponent(typeof(PassHealth))]
    public class PassBossController : MonoBehaviour
    {
        public enum BossState { Dormant, Pursuing, Windup, Wave, Recovering, Defeated }
        [SerializeField] protected PassPlayerMovement player;
        [SerializeField] protected PassHealth playerHealth;
        [SerializeField] protected PassGameFlow game;
        [SerializeField] protected PassEffects effects;
        [SerializeField] protected PassCamera view;
        [SerializeField] protected PassAudio audioControl;
        [SerializeField] protected Transform visual;
        [SerializeField, Min(0.1f)] private float moveSpeed = 2.6f;
        [SerializeField, Min(1f)] private float slamRadius = 3.5f;
        [SerializeField, Min(0.2f)] private float windupDuration = 1.35f;
        [SerializeField, Min(1f)] private float slamDamage = 28f;
        [SerializeField, Min(1f)] private float waveDamage = 22f;
        private Vector3 home, visualPosition, slamPosition;
        private Quaternion homeRotation, visualRotation;
        private float timer, nextAttack, flash;
        private int attackCount;
        private bool waveAttack, wavePassedPlayer;
        private LineRenderer telegraph, wave;
        private Renderer[] renderers;
        private Color[][] originalColors;
        private MaterialPropertyBlock properties;
        public PassHealth Health { get; private set; }
        public BossState State { get; protected set; }
        public virtual bool Enraged => Health != null && Health.Fraction <= 0.5f;
        public virtual int AttacksPerformed => attackCount;
        public virtual string AttackLabel => State == BossState.Windup ? (waveAttack ? "귀기 파동 · 파동이 닿기 직전에 구르세요" : "천근 내려찍기 · 붉은 원에서 벗어나세요")
            : State == BossState.Wave ? "귀기 파동 · 구르기로 통과하세요" : State == BossState.Recovering ? "공격의 틈 · 부채 연격" : "";

        public void Configure(PassPlayerMovement target, PassHealth vitality, PassGameFlow flow, PassEffects visuals, PassCamera cameraControl, PassAudio sounds, Transform model)
        { player = target; playerHealth = vitality; game = flow; effects = visuals; view = cameraControl; audioControl = sounds; visual = model; }

        protected virtual void Awake()
        {
            Health = GetComponent<PassHealth>(); home = transform.position; homeRotation = transform.rotation;
            visualPosition = visual.localPosition; visualRotation = visual.localRotation;
            renderers = visual.GetComponentsInChildren<Renderer>(); properties = new MaterialPropertyBlock();
            originalColors = new Color[renderers.Length][];
            for (int i = 0; i < renderers.Length; i++)
            {
                var materials = renderers[i].sharedMaterials;
                originalColors[i] = new Color[materials.Length];
                for (int slot = 0; slot < materials.Length; slot++)
                    originalColors[i][slot] = materials[slot].HasProperty("_BaseColor") ? materials[slot].GetColor("_BaseColor") : Color.white;
            }
            Health.Damaged += OnDamaged; Health.Died += OnDefeated;
        }
        protected virtual void Start()
        {
            telegraph = effects.CreateRing("GuardianAttackWarning", new Color(1f, 0.28f, 0.12f));
            wave = effects.CreateRing("GuardianSpiritWave", new Color(1f, 0.55f, 0.2f));
            ResetEncounter();
        }
        public virtual void ResetEncounter()
        {
            Health.Restore(); Health.Invulnerable = true;
            transform.SetPositionAndRotation(home, homeRotation);
            visual.SetLocalPositionAndRotation(visualPosition, visualRotation);
            var collision = GetComponent<Collider>(); if (collision != null) collision.enabled = true;
            State = BossState.Dormant; timer = 0f; attackCount = 0; flash = 0f;
            if (telegraph != null) telegraph.enabled = false;
            if (wave != null) wave.enabled = false;
            Tint(Color.white);
        }
        public virtual void WakeUp()
        { if (State != BossState.Dormant) return; Health.Invulnerable = false; State = BossState.Pursuing; nextAttack = Time.time + 2f; audioControl.Bell(); }

        protected virtual void Update()
        {
            if (State == BossState.Defeated)
            {
                timer += Time.deltaTime;
                visual.localRotation = visualRotation * Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, 65f, Mathf.SmoothStep(0f, 1f, timer / 2f)));
                visual.localPosition = visualPosition - Vector3.up * Mathf.Min(0.8f, timer * 0.3f);
                return;
            }
            if (game == null || !game.IsRunning) return;
            flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime * 5f);
            Tint(Color.Lerp(Enraged ? new Color(1f, 0.72f, 0.65f) : Color.white, new Color(0.3f, 1.8f, 1.4f), flash));
            if (State == BossState.Dormant) return;
            timer += Time.deltaTime;
            Vector3 towardPlayer = player.transform.position - transform.position; towardPlayer.y = 0f;
            float distance = towardPlayer.magnitude;
            if (State == BossState.Pursuing)
            {
                Face(towardPlayer, 70f);
                if (distance > 7f)
                {
                    Vector3 destination = transform.position + towardPlayer.normalized * moveSpeed * Time.deltaTime * (Enraged ? 1.35f : 1f);
                    Vector3 fromHome = destination - home; fromHome.y = 0f;
                    if (fromHome.magnitude < 18f) transform.position = destination;
                }
                visual.localPosition = visualPosition + Vector3.up * Mathf.Sin(Time.time * 2f) * 0.04f;
                if (Time.time >= nextAttack && distance <= 13f) BeginAttack();
            }
            else if (State == BossState.Windup)
            {
                float duration = windupDuration * (Enraged ? 0.78f : 1f);
                float progress = Mathf.Clamp01(timer / duration);
                visual.localRotation = visualRotation * Quaternion.Euler(-12f * progress, 0f, 0f);
                effects.DrawRing(telegraph, waveAttack ? transform.position + Vector3.up * 0.15f : slamPosition, waveAttack ? 1.5f + progress : slamRadius, new Color(1f, 0.3f, 0.12f, 0.55f + progress * 0.45f), 0.06f + progress * 0.22f);
                if (timer >= duration) ResolveAttack();
            }
            else if (State == BossState.Wave)
            {
                float radius = timer * (Enraged ? 17f : 13.5f);
                effects.DrawRing(wave, transform.position + Vector3.up * 0.16f, radius, new Color(1f, 0.5f, 0.17f, 1f - radius / 23f), 0.27f);
                if (!wavePassedPlayer && radius >= distance - 0.8f)
                { wavePassedPlayer = true; if (Mathf.Abs(radius - distance) < 1.3f) DamagePlayer(waveDamage); }
                if (radius >= (Enraged ? 21f : 17f)) Recover();
            }
            else if (State == BossState.Recovering)
            {
                visual.localRotation = Quaternion.Slerp(visual.localRotation, visualRotation, Time.deltaTime * 5f);
                if (timer >= (Enraged ? 0.95f : 1.3f)) { State = BossState.Pursuing; nextAttack = Time.time + 0.55f; }
            }
        }
        private void Face(Vector3 direction, float speed)
        { if (direction.sqrMagnitude > 0.01f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), speed * Time.deltaTime); }
        private void BeginAttack()
        {
            waveAttack = attackCount % 3 == 2; attackCount++; timer = 0f; State = BossState.Windup;
            slamPosition = player.transform.position; slamPosition.y = home.y + 0.14f;
            Face(player.transform.position - transform.position, 3600f);
        }
        private void ResolveAttack()
        {
            telegraph.enabled = false; audioControl.Slam(); view.AddShake(0.8f);
            if (waveAttack) { State = BossState.Wave; timer = 0f; wavePassedPlayer = false; return; }
            Vector3 difference = player.transform.position - slamPosition; difference.y = 0f;
            if (difference.magnitude <= slamRadius + 0.25f) DamagePlayer(slamDamage);
            effects.Impact(slamPosition, new Color(1f, 0.35f, 0.12f), slamRadius + 1f);
            visual.localRotation = visualRotation * Quaternion.Euler(15f, 0f, 0f);
            Recover();
        }
        private void DamagePlayer(float damage)
        {
            // 구르기 초중반은 무적이며, 일어나는 후반에는 공격을 받을 수 있다.
            if (player.AnimationControl.IsDodging && player.AnimationControl.PlaybackProgress < 0.78f) return;
            if (Mathf.Abs(player.transform.position.y - home.y) < 4f) playerHealth.TakeDamage(damage);
        }
        private void Recover() { State = BossState.Recovering; timer = 0f; wave.enabled = false; }
        private void OnDamaged(float damage) { flash = 1f; }
        protected virtual void OnDefeated()
        {
            State = BossState.Defeated; timer = 0f;
            if (telegraph != null) telegraph.enabled = false;
            if (wave != null) wave.enabled = false;
            var collision = GetComponent<Collider>(); if (collision != null) collision.enabled = false;
            effects.Impact(transform.position, new Color(0.3f, 1f, 0.85f), 13f); audioControl.Bell();
        }
        private void Tint(Color color)
        {
            // 슬롯마다 원래 나무·눈·치아 색을 보존한 상태에서 피격과 격노 색만 곱한다.
            for (int i = 0; i < renderers.Length; i++)
                for (int slot = 0; slot < originalColors[i].Length; slot++)
                {
                    renderers[i].GetPropertyBlock(properties, slot);
                    properties.SetColor("_BaseColor", originalColors[i][slot] * color);
                    renderers[i].SetPropertyBlock(properties, slot);
                }
        }
        protected virtual void OnDestroy() { if (Health != null) { Health.Damaged -= OnDamaged; Health.Died -= OnDefeated; } }
    }
}
