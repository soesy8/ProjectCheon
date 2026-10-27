using System;
using UnityEngine;

namespace Cheon.Prototype.MountainPass
{
    // 모델이나 이펙트와 분리한 빈 오브젝트에서 피해 배분을 조절한다.
    public class PassFanAttackDamageSettings : MonoBehaviour
    {
        [Serializable]
        public class AttackDamage
        {
            [InspectorName("기본 피해량")]
            [Min(0)] public float baseDamage = 22;
            [Tooltip("직접 타격 피해 = 기본 피해량 × 직접 타격 비율")]
            [InspectorName("직접 타격 피해 비율 (0~1)")]
            [Range(0, 1)] public float directWeight = .7f;
        }

        [Serializable]
        public class WindAttackDamage : AttackDamage
        {
            [Tooltip("원거리 바람 피해 = 기본 피해량 × 바람 피해 비율. 합계는 자동 정규화하지 않습니다.")]
            [InspectorName("원거리 바람 피해 비율 (0~1)")]
            [Range(0, 1)] public float windWeight = .3f;
        }

        [Header("1타 / 2타 / 3타 피해 배분")]
        [InspectorName("1타 설정")]
        public WindAttackDamage attack1 = new WindAttackDamage();
        [InspectorName("2타 설정")]
        public AttackDamage attack2 = new AttackDamage { baseDamage = 28, directWeight = 1 };
        [InspectorName("3타 설정")]
        public WindAttackDamage attack3 = new WindAttackDamage { baseDamage = 40 };

        public AttackDamage GetAttack(int attack) => attack == 1 ? attack1 : attack == 2 ? attack2 : attack3;

        public float GetDamage(int attack, bool wind)
        {
            var settings = GetAttack(attack);
            float weight = wind ? attack == 1 ? attack1.windWeight : attack == 3 ? attack3.windWeight : 0f : settings.directWeight;
            return Mathf.Max(0, settings.baseDamage) * Mathf.Clamp01(weight);
        }
    }
}
