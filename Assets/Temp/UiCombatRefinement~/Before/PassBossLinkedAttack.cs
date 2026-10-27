using System.Collections.Generic;
using UnityEngine;

namespace Cheon.Prototype.MountainPass
{
    // 순간이동과 잡몹 점프를 동기화하고, 마지막 착지를 기준으로 본체 타격을 예약한다.
    public class PassBossLinkedAttack : MonoBehaviour
    {
        private enum LinkStage { None, Preparing, Teleporting, LeadIn, StartingJumps, WaitingForLastLanding, WaitingForBossFall }
        private readonly List<PassBossPatternController> participants = new List<PassBossPatternController>();
        private readonly List<Vector3> teleportTargets = new List<Vector3>();
        private PassBossPatternSettings settings;
        private PassBossPatternController boss;
        private LinkStage stage;
        private float elapsed, nextJumpAt, jumpStartDeadline, lastLandingDeadline, bossFallDeadline;
        private int participantIndex, lastLandingsBefore, bossFallsBefore;

        public bool IsActive => stage != LinkStage.None;
        public bool CanStartBossHit => IsActive && Time.time >= BossHitAt;
        public float BossHitAt { get; private set; } = float.PositiveInfinity;
        public float ReadyAt { get; private set; }
        public int CompletedCount { get; private set; }
        public int JumpsOrdered { get; private set; }
        public int CancelledCount { get; private set; }
        public string LastCancellationReason { get; private set; } = "";
        public bool IsPreparing => stage == LinkStage.Preparing;

        public void Configure(PassBossPatternSettings configuration, PassBossPatternController owner)
        { settings = configuration; boss = owner; }

        public bool TryBegin()
        {
            if (IsActive || !settings.linkedAttack.enabled || Time.time < ReadyAt || !boss.IsAlive ||
                boss.Phase != PassBossPatternController.PatternPhase.Idle) return false;
            participants.Clear(); teleportTargets.Clear();
            foreach (var enemy in settings.Enemies)
            {
                if (enemy.CanJoinLinkedAttack) participants.Add(enemy);
                if (participants.Count >= settings.linkedAttack.participants) break;
            }
            if (participants.Count < settings.linkedAttack.participants) { participants.Clear(); return false; }
            foreach (var enemy in participants)
                if (!enemy.ReserveForLinkedAttack(this)) { ReleaseParticipants(); return false; }
            // 현재 동작은 끝까지 재생시키고 다음 개별 공격만 막아 참가 시점을 맞춘다.
            participantIndex = JumpsOrdered = 0; BossHitAt = float.PositiveInfinity;
            SetStage(LinkStage.Preparing); boss.EnterLinkedAttack();
            return true;
        }

        private bool StartTeleports()
        {
            teleportTargets.Clear();
            for (int index = 0; index < participants.Count; index++)
            {
                float side = index % 2 == 0 ? -1f : 1f;
                Vector3 desired = boss.transform.position + boss.transform.right * settings.linkedAttack.teleportSideDistance * side * (index / 2 + 1);
                if (!participants[index].TryResolveTeleportDestination(desired, out Vector3 target)) return false;
                foreach (var previous in teleportTargets)
                {
                    if (Vector3.ProjectOnPlane(previous - target, Vector3.up).magnitude < participants[index].Body.radius * 2f + settings.collision.clearance)
                        return false;
                }
                teleportTargets.Add(target);
            }
            SetStage(LinkStage.Teleporting);
            for (int index = 0; index < participants.Count; index++)
                if (!participants[index].BeginLinkedTeleport(this, teleportTargets[index])) return false;
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (!IsActive) return;
            if (!settings.linkedAttack.enabled) { Cancel("협동 공격 사용 해제"); return; }
            foreach (var enemy in participants)
                if (enemy == null || !enemy.IsAlive || !enemy.isActiveAndEnabled) { Cancel("참가 잡몹 사망·비활성화"); return; }
            elapsed += deltaTime;
            switch (stage)
            {
                case LinkStage.Preparing:
                    bool ready = Time.time >= boss.FallReadyAt;
                    foreach (var enemy in participants) ready &= enemy.ReadyForLinkedTeleport(this);
                    if (ready)
                    { if (!StartTeleports()) Cancel("안전한 순간이동 지점 없음"); }
                    else if (elapsed >= settings.linkedAttack.preparationTimeout) Cancel("참가자 준비 시간 초과");
                    break;
                case LinkStage.Teleporting:
                    if (elapsed > settings.linkedAttack.startTimeout + settings.linkedAttack.teleportDuration * 2f)
                    { Cancel("순간이동 시간 초과"); break; }
                    foreach (var enemy in participants)
                    {
                        if (enemy.TeleportVfx.IsTeleporting) return;
                        if (!enemy.TeleportVfx.Succeeded) { Cancel("순간이동 취소"); return; }
                    }
                    SetStage(LinkStage.LeadIn);
                    break;
                case LinkStage.LeadIn:
                    if (elapsed >= settings.linkedAttack.windup)
                    {
                        nextJumpAt = Time.time; jumpStartDeadline = Time.time + settings.linkedAttack.startTimeout;
                        SetStage(LinkStage.StartingJumps);
                    }
                    break;
                case LinkStage.StartingJumps:
                    if (Time.time < nextJumpAt) break;
                    var next = participants[participantIndex];
                    int landingsBefore = next.JumpLandings;
                    if (next.StartLinkedJump(this))
                    {
                        participantIndex++; JumpsOrdered++;
                        if (participantIndex == participants.Count)
                        {
                            lastLandingsBefore = landingsBefore; bossFallsBefore = boss.FallsCompleted;
                            lastLandingDeadline = Time.time + settings.jump.windup + settings.jump.maximumFlight + settings.linkedAttack.startTimeout;
                            if (boss.StartLinkedFall(this)) SetStage(LinkStage.WaitingForLastLanding);
                            else Cancel("본체 엎어지기 준비 실패");
                        }
                        else
                        {
                            // 착지나 후딜레이를 기다리지 않고 같은 전조를 0.5초씩 어긋나게 시작한다.
                            nextJumpAt = Time.time + settings.linkedAttack.jumpInterval;
                            jumpStartDeadline = nextJumpAt + settings.linkedAttack.startTimeout;
                        }
                    }
                    else if (Time.time >= jumpStartDeadline) Cancel("잡몹 점프 시작 시간 초과");
                    break;
                case LinkStage.WaitingForLastLanding:
                    var last = participants[participants.Count - 1];
                    if (last.JumpLandings > lastLandingsBefore)
                    {
                        BossHitAt = last.LastJumpLandedAt + settings.linkedAttack.attackGap;
                        bossFallDeadline = BossHitAt + settings.fall.fallDuration + settings.fall.holdDuration +
                            settings.fall.riseDuration + settings.fall.recovery + settings.linkedAttack.startTimeout;
                        SetStage(LinkStage.WaitingForBossFall);
                    }
                    else if (Time.time >= lastLandingDeadline) Cancel("마지막 잡몹 착지 시간 초과");
                    break;
                case LinkStage.WaitingForBossFall:
                    if (boss.FallsCompleted > bossFallsBefore)
                    {
                        CompletedCount++; ReadyAt = Time.time + settings.linkedAttack.cooldown;
                        stage = LinkStage.None; BossHitAt = float.PositiveInfinity; ReleaseParticipants();
                    }
                    else if (Time.time >= bossFallDeadline) Cancel("본체 엎어지기 완료 시간 초과");
                    break;
            }
        }

        private void SetStage(LinkStage next) { stage = next; elapsed = 0; }
        private void Cancel(string reason)
        {
            CancelledCount++; LastCancellationReason = reason;
            stage = LinkStage.None; BossHitAt = float.PositiveInfinity;
            ReadyAt = Time.time + settings.linkedAttack.cooldown;
            boss.CancelLinkedFall(this); ReleaseParticipants(); boss.LeaveLinkedAttack();
        }
        private void ReleaseParticipants()
        {
            foreach (var enemy in participants) if (enemy != null) enemy.ReleaseLinkedAttack(this);
            participants.Clear(); teleportTargets.Clear();
        }
        public void Clear()
        {
            stage = LinkStage.None; BossHitAt = float.PositiveInfinity;
            if (boss != null) boss.CancelLinkedFall(this);
            ReleaseParticipants(); ReadyAt = 0; CompletedCount = JumpsOrdered = CancelledCount = 0; LastCancellationReason = "";
        }
    }
}
