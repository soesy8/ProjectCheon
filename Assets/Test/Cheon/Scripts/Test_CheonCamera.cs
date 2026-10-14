using UnityEngine;

namespace Cheon.Testing
{
    [RequireComponent(typeof(Camera))]
    public class Test_CheonCamera : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private Transform boss;
        [SerializeField] private Test_CheonSession session;
        [SerializeField] private Vector3 offset = new Vector3(10.5f, 16.1f, -16.1f);
        [SerializeField] private float followSpeed = 5;
        [SerializeField, Min(1)] private float explorationSize = 8.4f;
        [SerializeField, Range(1, 2)] private float battleDistanceMultiplier = 1.2f;
        private Camera view;
        private float shake;
        private Vector3 focus;
        private float distanceMultiplier = 1;
        private void Awake() { view = GetComponent<Camera>(); view.orthographicSize = explorationSize; focus = player.position + Vector3.forward * 3; Snap(); }
        private void Snap() { transform.position = focus + offset * distanceMultiplier; transform.rotation = Quaternion.LookRotation(-offset); }
        private void LateUpdate()
        {
            bool battle = session.CurrentChapter == Test_CheonSession.Chapter.Battle;
            Vector3 target = player.position + Vector3.forward * 3;
            if (battle) target = Vector3.Lerp(player.position, boss.position, .32f) + Vector3.forward;
            focus = Vector3.Lerp(focus, target, 1 - Mathf.Exp(-followSpeed * Time.deltaTime));
            // 직교 카메라는 위치만 가까워져도 크기가 바뀌지 않으므로 시야와 실제 거리를 함께 전환한다.
            float targetMultiplier = battle ? battleDistanceMultiplier : 1;
            distanceMultiplier = Mathf.Lerp(distanceMultiplier, targetMultiplier, Time.deltaTime * 2);
            Snap();
            shake = Mathf.MoveTowards(shake, 0, Time.deltaTime * 1.4f);
            if (!session.Paused) transform.position += Random.insideUnitSphere * shake;
            view.orthographicSize = Mathf.Lerp(view.orthographicSize, explorationSize * targetMultiplier, Time.deltaTime * 2);
        }
        public void AddShake(float amount) { shake = Mathf.Max(shake, amount); }
    }
}
