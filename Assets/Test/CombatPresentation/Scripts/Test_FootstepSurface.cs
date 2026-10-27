using UnityEngine;

namespace Cheon.Tests.CombatPresentation
{
    // 보행 가능한 바닥에 지정한다. 지정하지 않은 산길과 전투장은 흙으로 처리한다.
    public class Test_FootstepSurface : MonoBehaviour
    {
        public enum GroundKind { Dirt, Stone }
        [SerializeField, InspectorName("발걸음 재질")] private GroundKind surface = GroundKind.Stone;
        public GroundKind Surface => surface;
    }
}
