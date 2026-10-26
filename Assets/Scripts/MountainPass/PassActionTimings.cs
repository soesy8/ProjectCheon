using System;
using UnityEngine;

namespace Cheon.Prototype.MountainPass
{
    [CreateAssetMenu(menuName = "Cheon/Prototype/Player Action Timings")]
    public class PassActionTimings : ScriptableObject
    {
        [Serializable]
        public sealed class ActionTiming
        {
            public string state;
            [Range(0f, 1f)] public float moveCancel = 0.65f;
            [Range(0f, 1f)] public float dodgeCancel = 0.65f;
            [Range(0f, 1f)] public float attack1Cancel = 0.55f;
            [Range(0f, 1f)] public float attack2Cancel = 0.55f;
            [Range(0f, 1f)] public float attack3Cancel = 0.55f;
            [Range(0f, 1f)] public float equipmentCancel = 0.9f;
            [Range(0.05f, 1f)] public float autoReturn = 1f;
            [Range(0f, 0.4f)] public float blendDuration = 0.12f;

            public float AttackCancel(int index) => index == 1 ? attack1Cancel : index == 2 ? attack2Cancel : attack3Cancel;

            public void Validate()
            {
                autoReturn = Mathf.Clamp(autoReturn, 0.05f, 1f);
                // 자동 복귀보다 늦은 캔슬 시점은 도달할 수 없으므로 같은 구간 안으로 제한한다.
                moveCancel = Mathf.Clamp(moveCancel, 0f, autoReturn);
                dodgeCancel = Mathf.Clamp(dodgeCancel, 0f, autoReturn);
                attack1Cancel = Mathf.Clamp(attack1Cancel, 0f, autoReturn);
                attack2Cancel = Mathf.Clamp(attack2Cancel, 0f, autoReturn);
                attack3Cancel = Mathf.Clamp(attack3Cancel, 0f, autoReturn);
                equipmentCancel = Mathf.Clamp(equipmentCancel, 0f, autoReturn);
                blendDuration = Mathf.Clamp(blendDuration, 0f, 0.4f);
            }
        }

        // 현재 저장된 캔슬 설정을 새 설정 에셋의 기본값으로 사용한다.
        [SerializeField] private ActionTiming[] actions =
        {
            new ActionTiming { state = "ATTACK1", moveCancel = 0.60f, dodgeCancel = 0.45f },
            new ActionTiming { state = "ATTACK2", moveCancel = 0.55f, dodgeCancel = 0.32f, attack1Cancel = 0.60f, attack2Cancel = 0.60f, attack3Cancel = 0.25811943f },
            new ActionTiming { state = "ATTACK3", moveCancel = 0.80f, dodgeCancel = 0.75f, attack1Cancel = 0.70f, attack2Cancel = 0.70f, attack3Cancel = 0.70f, equipmentCancel = 1f },
            new ActionTiming { state = "DRAW", moveCancel = 0.96f, dodgeCancel = 0.96f, attack1Cancel = 0.96f, attack2Cancel = 0.96f, attack3Cancel = 0.96f, equipmentCancel = 0.45f },
            new ActionTiming { state = "SHEATHE", moveCancel = 0.96f, dodgeCancel = 0.96f, attack1Cancel = 0.96f, attack2Cancel = 0.96f, attack3Cancel = 0.96f, equipmentCancel = 0.45f },
            new ActionTiming { state = "HIT", moveCancel = 0.55f, dodgeCancel = 0.35f, attack1Cancel = 0.85f, attack2Cancel = 0.85f, attack3Cancel = 0.85f },
            new ActionTiming { state = "ROLL", moveCancel = 1f, dodgeCancel = 1f, attack1Cancel = 0.90f, attack2Cancel = 1f, attack3Cancel = 1f, equipmentCancel = 1f }
        };

        public ActionTiming Get(string state)
        {
            foreach (var action in actions)
                if (action.state == state) return action;
            return null;
        }

        public void Validate()
        {
            foreach (var action in actions) action.Validate();
        }

        private void OnValidate() => Validate();

        public static string Label(string state) => state switch
        {
            "ATTACK1" => "공격 1", "ATTACK2" => "공격 2", "ATTACK3" => "공격 3",
            "DRAW" => "발도", "SHEATHE" => "납도", "HIT" => "피격",
            "DRAWN_IDLE" => "발도 대기", "SHEATHED_IDLE" => "납도 대기", "ROLL" => "구르기", "RUN2" => "달리기", _ => state
        };
    }
}

