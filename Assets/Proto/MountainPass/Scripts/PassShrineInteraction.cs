using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Cheon.Prototype.MountainPass
{
    public class PassShrineInteraction : MonoBehaviour
    {
        [SerializeField] private PassGameFlow game;
        [SerializeField] private Transform prayerPoint;
        [SerializeField] private Transform respawnPoint;
        [SerializeField, Min(0.5f)] private float interactionRadius = 2.5f;
        [SerializeField, Min(0.1f)] private float prayerDuration = 1.2f;
        public bool IsPraying { get; private set; }
        public Vector3 Position => prayerPoint != null ? prayerPoint.position : transform.position;
        public bool CanInteract
        {
            get
            {
                if (game == null || !game.IsRunning || game.InEncounter || IsPraying) return false;
                Vector3 difference = game.Player.position - Position;
                return Mathf.Abs(difference.y) < 1f && new Vector2(difference.x, difference.z).magnitude <= interactionRadius;
            }
        }
        public void Configure(PassGameFlow flow, Transform altar, Transform spawn) { game = flow; prayerPoint = altar; respawnPoint = spawn; }
        private void Update()
        { if (CanInteract && Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame) Interact(); }
        public void Interact() { if (CanInteract) StartCoroutine(Pray()); }
        private IEnumerator Pray()
        {
            IsPraying = true;
            var movement = game.Player.GetComponent<PassPlayerMovement>();
            movement.InputEnabled = false;
            movement.AnimationControl.PlayIdle();
            game.ShowNotice("산신각에 기도를 올립니다", prayerDuration + 0.4f);
            yield return new WaitForSeconds(prayerDuration);
            if (game.IsRunning) game.RestAtShrine(respawnPoint.position, respawnPoint.rotation);
            movement.InputEnabled = game.IsRunning;
            IsPraying = false;
        }
    }
}
