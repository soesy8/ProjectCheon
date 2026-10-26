using System;
using UnityEngine;

namespace Cheon.Tests.FanAttackVfx
{
    // 모델이나 이펙트와 분리한 빈 오브젝트에서 피해 배분을 조절한다.
    public class Test_FanAttackDamageSettings : MonoBehaviour
    {
        [Serializable]
        public class AttackDamage
        {
            [Min(0)] public float baseDamage = 22;
            [Tooltip("직접 타격 피해 = Base Damage × Direct Weight")]
            [Range(0, 1)] public float directWeight = .7f;
            [Tooltip("원격 바람 피해 = Base Damage × Wind Weight. 합계는 자동 정규화하지 않습니다.")]
            [Range(0, 1)] public float windWeight = .3f;
        }

        [Header("1타 / 2타 / 3타 피해 배분")]
        public AttackDamage attack1 = new AttackDamage();
        public AttackDamage attack2 = new AttackDamage { baseDamage = 28, directWeight = 1, windWeight = 0 };
        public AttackDamage attack3 = new AttackDamage { baseDamage = 40 };

        public AttackDamage GetAttack(int attack) => attack == 1 ? attack1 : attack == 2 ? attack2 : attack3;

        public float GetDamage(int attack, bool wind)
        {
            var settings = GetAttack(attack);
            return Mathf.Max(0, settings.baseDamage) * Mathf.Clamp01(wind ? settings.windWeight : settings.directWeight);
        }
    }
}
