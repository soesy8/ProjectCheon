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
            [Tooltip("직접 타격 피해 = Base Damage × Direct Weight")]
            [InspectorName("직접 타격 피해 비율 (0~1)")]
            [Range(0, 1)] public float directWeight = .7f;
            [Tooltip("원격 바람 피해 = Base Damage × Wind Weight. 합계는 자동 정규화하지 않습니다.")]
            [InspectorName("원거리 바람 피해 비율 (0~1)")]
            [Range(0, 1)] public float windWeight = .3f;
        }

        [Header("1타 / 2타 / 3타 피해 배분")]
        [InspectorName("1타 설정")]
        public AttackDamage attack1 = new AttackDamage();
        [InspectorName("2타 설정")]
        public AttackDamage attack2 = new AttackDamage { baseDamage = 28, directWeight = 1, windWeight = 0 };
        [InspectorName("3타 설정")]
        public AttackDamage attack3 = new AttackDamage { baseDamage = 40 };

        public AttackDamage GetAttack(int attack) => attack == 1 ? attack1 : attack == 2 ? attack2 : attack3;

        public float GetDamage(int attack, bool wind)
        {
            var settings = GetAttack(attack);
            return Mathf.Max(0, settings.baseDamage) * Mathf.Clamp01(wind ? settings.windWeight : settings.directWeight);
        }
    }
}
