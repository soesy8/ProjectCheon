using UnityEngine;

namespace Cheon.MountainPass.Presentation
{
    // 보행 가능한 바닥에 지정한다. 지정하지 않은 산길과 전투장은 흙으로 처리한다.
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Cheon.Tests.CombatPresentation", "Assembly-CSharp", "Test_FootstepSurface")]
    public class PassFootstepSurface : MonoBehaviour
    {
        public enum GroundKind { Dirt, Stone }
        [SerializeField, InspectorName("발걸음 재질")] private GroundKind surface = GroundKind.Stone;
        public GroundKind Surface => surface;
    }
}
