using UnityEngine;

namespace Cheon.Prototype.MountainPass
{
    // 직접 타격과 바람 판정은 파생 전투 컴포넌트가 담당하며, 공통 연결과 명중 수만 공유한다.
    [DefaultExecutionOrder(-15)]
    public abstract class PassPlayerCombat : MonoBehaviour
    {
        [InspectorName("플레이어 동작 제어기")]
        [SerializeField] protected PassAnimationController animationControl;
        [InspectorName("보스 제어기")]
        [SerializeField] protected PassBossController boss;
        [InspectorName("게임 진행 제어기")]
        [SerializeField] protected PassGameFlow game;
        [InspectorName("공통 시각 효과 제어기")]
        [SerializeField] protected PassEffects effects;
        [InspectorName("게임 카메라 제어기")]
        [SerializeField] protected PassCamera view;
        [InspectorName("효과음 제어기")]
        [SerializeField] protected PassAudio audioControl;
        public int HitsLanded { get; protected set; }
        public void Configure(PassAnimationController animation, PassBossController enemy, PassGameFlow flow,
            PassEffects visuals, PassCamera cameraControl, PassAudio sounds)
        { animationControl = animation; boss = enemy; game = flow; effects = visuals; view = cameraControl; audioControl = sounds; }
        protected abstract void LateUpdate();
        public virtual void ResetHits() => HitsLanded = 0;
    }
}
