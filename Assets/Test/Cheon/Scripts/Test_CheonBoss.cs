using UnityEngine;

namespace Cheon.Testing
{
    public class Test_CheonBoss : MonoBehaviour
    {
        private enum Action { Waiting, Telegraph, Striking, Recovering, Stunned, Dead }
        [SerializeField] private Test_CheonSession session;
        [SerializeField] private Test_CheonPlayer player;
        [SerializeField] private Test_CheonEffects effects;
        [SerializeField] private Test_CheonAudio sound;
        [SerializeField] private Transform model;
        [SerializeField] private Transform club;
        [SerializeField] private float maxHealth = 680;
        [SerializeField] private float moveSpeed = 2.6f;
        [SerializeField] private float slamDamage = 24;
        [SerializeField] private float sweepDamage = 28;
        [SerializeField] private float chargeDamage = 30;
        [Header("영계 이동 경계")]
        [SerializeField] private Vector2 horizontalBounds = new Vector2(-9.5f, 9.5f);
        [SerializeField] private Vector2 depthBounds = new Vector2(3, 15);
        public float Health { get; private set; }
        public float HealthRatio => Health / maxHealth;
        public bool Enraged => HealthRatio <= .5f;
        public bool Defeated => action == Action.Dead;
        public string Intent { get; private set; } = "잠든 수호장승";
        private Action action;
        private float timer, duration, stunUntil;
        private int pattern, attackIndex;
        private Vector3 target, chargeStart, chargeEnd;
        private bool chargeHit;
        private GameObject telegraph;
        private Vector3 modelHome;
        private bool announcedRage;

        private void Awake() { modelHome = model.localPosition; Health = maxHealth; }
        public void BeginBattle() { Health = maxHealth; action = Action.Waiting; timer = 1.8f; Intent = "깨어나는 수호장승"; }

        private void Update()
        {
            if (session.CurrentChapter != Test_CheonSession.Chapter.Battle || session.Paused) return;
            timer -= Time.deltaTime;
            if (!announcedRage && Enraged)
            {
                announcedRage = true;
                effects.Burst(transform.position + Vector3.up * 2, Test_CheonEffects.Red, 28, .9f);
                session.Notify("광폭화 · 내려찍기 뒤에 충격파가 이어집니다!", 5);
            }
            if (action == Action.Stunned)
            {
                Intent = "봉인됨 · 공격 기회";
                model.localRotation = Quaternion.Euler(12, 0, Mathf.Sin(Time.time * 22) * 3);
                if (Time.time >= stunUntil) { action = Action.Recovering; timer = .6f; }
                return;
            }
            Vector3 direction = player.transform.position - transform.position;
            direction.y = 0;
            if (action == Action.Waiting)
            {
                Intent = "다가오는 발소리";
                if (direction.magnitude > 5) transform.position += direction.normalized * moveSpeed * Time.deltaTime;
                if (direction.sqrMagnitude > .1f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 4);
                if (timer <= 0) StartPattern();
            }
            else if (action == Action.Telegraph)
            {
                float progress = 1 - timer / duration;
                model.localRotation = Quaternion.Euler(-18 * progress, 0, 0);
                club.localRotation = Quaternion.Euler(-90 * progress, 0, -18);
                if (timer <= 0) Strike();
            }
            else if (action == Action.Striking && pattern == 2)
            {
                transform.position = Vector3.Lerp(chargeStart, chargeEnd, Mathf.Clamp01(1 - timer / duration));
                if (!chargeHit && Vector3.Distance(transform.position, player.transform.position) < 2)
                { player.TakeDamage(chargeDamage); chargeHit = true; }
                if (timer <= 0) Recover();
            }
            else if (action == Action.Recovering)
            {
                Intent = "틈이 열렸다 · 부채 연계";
                model.localRotation = Quaternion.Slerp(model.localRotation, Quaternion.identity, Time.deltaTime * 6);
                club.localRotation = Quaternion.Slerp(club.localRotation, Quaternion.Euler(0, 0, -18), Time.deltaTime * 6);
                if (timer <= 0) { action = Action.Waiting; timer = Enraged ? .75f : 1.1f; }
            }
            model.localPosition = modelHome + Vector3.up * Mathf.Sin(Time.time * 3) * .06f;
            Vector3 bounded = transform.position;
            bounded.x = Mathf.Clamp(bounded.x, horizontalBounds.x, horizontalBounds.y);
            bounded.z = Mathf.Clamp(bounded.z, depthBounds.x, depthBounds.y);
            transform.position = bounded;
        }

        private void StartPattern()
        {
            pattern = attackIndex++ % 3;
            action = Action.Telegraph;
            target = player.transform.position;
            if (pattern == 0)
            {
                Intent = Enraged ? "내려찍기 + 충격파" : "내려찍기 · 원 밖으로 회피";
                duration = Enraged ? .95f : 1.35f;
                telegraph = effects.Telegraph(target, 3.1f, duration);
            }
            else if (pattern == 1)
            {
                Intent = "휘두르기 · 거리를 벌리세요";
                duration = Enraged ? .9f : 1.15f;
                target = transform.position;
                telegraph = effects.Telegraph(target, 5.2f, duration);
            }
            else
            {
                Intent = "돌진 · 옆으로 회피";
                duration = Enraged ? .8f : 1.2f;
                chargeStart = transform.position;
                Vector3 direction = (target - chargeStart).normalized;
                direction.y = 0;
                chargeEnd = chargeStart + direction * 11;
                chargeEnd.x = Mathf.Clamp(chargeEnd.x, horizontalBounds.x + .5f, horizontalBounds.y - .5f);
                chargeEnd.z = Mathf.Clamp(chargeEnd.z, depthBounds.x, depthBounds.y);
                transform.rotation = Quaternion.LookRotation(direction);
                telegraph = effects.Lane(chargeStart, chargeEnd, 2.6f, duration);
            }
            timer = duration;
        }

        private void Strike()
        {
            if (telegraph != null) Destroy(telegraph);
            sound.PlayCue(Test_CheonAudio.Cue.Slam);
            effects.Shake(.3f);
            club.localRotation = Quaternion.Euler(35, 0, -18);
            if (pattern == 2) { action = Action.Striking; duration = .55f; timer = duration; chargeHit = false; return; }
            float radius = pattern == 0 ? 3.1f : 5.2f;
            effects.Ring(target, radius, Test_CheonEffects.Red, .55f);
            effects.Burst(target + Vector3.up * .2f, Test_CheonEffects.Gold, 20, .7f);
            if (Vector3.Distance(player.transform.position, target) < radius + .35f) player.TakeDamage(pattern == 0 ? slamDamage : sweepDamage);
            if (pattern == 0 && Enraged) effects.Shockwave(target, player, 20);
            Recover();
        }

        private void Recover() { action = Action.Recovering; timer = Enraged ? 1.05f : 1.65f; }

        public void TakeDamage(float amount, float stunSeconds = 0)
        {
            if (session.CurrentChapter != Test_CheonSession.Chapter.Battle || Defeated) return;
            Health = Mathf.Max(0, Health - amount);
            effects.DamageNumber(transform.position + Vector3.up * 4, Mathf.RoundToInt(amount));
            effects.Burst(transform.position + Vector3.up * 1.9f, Test_CheonEffects.Gold, 8, .35f);
            sound.PlayCue(Test_CheonAudio.Cue.Hit);
            if (Health <= 0)
            {
                action = Action.Dead;
                if (telegraph != null) Destroy(telegraph);
                model.localRotation = Quaternion.Euler(0, 0, 78);
                effects.Seal(transform.position, 6);
                session.Win();
            }
            else if (stunSeconds > .5f)
            {
                if (telegraph != null) Destroy(telegraph);
                action = Action.Stunned;
                stunUntil = Time.time + stunSeconds;
            }
        }
    }
}
