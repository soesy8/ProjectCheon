using System.Collections.Generic;
using UnityEngine;

namespace Cheon.Tests.BossPatterns
{
    // 순간이동과 잡몹 점프를 동기화하고, 마지막 착지를 기준으로 본체 타격을 예약한다.
    public class Test_BossLinkedAttack : MonoBehaviour
    {
        private enum LinkStage { None, Teleporting, LeadIn, StartingJumps, WaitingForLastLanding, WaitingForBossFall }
        private readonly List<Test_BossPatternController> participants = new List<Test_BossPatternController>();
        private readonly List<Vector3> teleportTargets = new List<Vector3>();
        private Test_BossPatternSettings settings;
        private Test_BossPatternController boss;
        private LinkStage stage;
        private float elapsed, nextJumpAt, jumpStartDeadline, lastLandingDeadline;
        private int participantIndex, lastLandingsBefore, bossFallsBefore;

        public bool IsActive => stage != LinkStage.None;
        public bool CanStartBossHit => IsActive && Time.time >= BossHitAt;
        public float BossHitAt { get; private set; } = float.PositiveInfinity;
        public float ReadyAt { get; private set; }
        public int CompletedCount { get; private set; }
        public int JumpsOrdered { get; private set; }

        public void Configure(Test_BossPatternSettings configuration, Test_BossPatternController owner)
        { settings = configuration; boss = owner; }

        public bool TryBegin()
        {
            // 본체 쿨타임은 연계 시작 전에 충족시켜 마지막 착지 이후 추가 대기를 없앤다.
            if (IsActive || !settings.linkedAttack.enabled || Time.time < ReadyAt || Time.time < boss.FallReadyAt) return false;
            participants.Clear(); teleportTargets.Clear();
            foreach (var enemy in settings.Enemies)
            {
                if (enemy.CanJoinLinkedAttack) participants.Add(enemy);
                if (participants.Count >= settings.linkedAttack.participants) break;
            }
            if (participants.Count < settings.linkedAttack.participants) { participants.Clear(); return false; }
            foreach (var enemy in participants)
                if (!enemy.ReserveForLinkedAttack(this)) { ReleaseParticipants(); return false; }
            for (int index = 0; index < participants.Count; index++)
            {
                float side = index % 2 == 0 ? -1f : 1f;
                Vector3 desired = boss.transform.position + boss.transform.right * settings.linkedAttack.teleportSideDistance * side * (index / 2 + 1);
                if (!participants[index].TryResolveTeleportDestination(desired, out Vector3 target)) { ReleaseParticipants(); return false; }
                foreach (var previous in teleportTargets)
                    if (Vector3.ProjectOnPlane(previous - target, Vector3.up).magnitude < participants[index].Body.radius * 2f + settings.collision.clearance)
                    { ReleaseParticipants(); return false; }
                teleportTargets.Add(target);
            }
            participantIndex = JumpsOrdered = 0; BossHitAt = float.PositiveInfinity;
            SetStage(LinkStage.Teleporting); boss.EnterLinkedAttack();
            for (int index = 0; index < participants.Count; index++)
                if (!participants[index].BeginLinkedTeleport(this, teleportTargets[index])) { Cancel(); return false; }
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (!IsActive) return;
            foreach (var enemy in participants)
                if (enemy == null || !enemy.IsAlive || !enemy.isActiveAndEnabled) { Cancel(); return; }
            elapsed += deltaTime;
            switch (stage)
            {
                case LinkStage.Teleporting:
                    foreach (var enemy in participants)
                    {
                        if (enemy.TeleportVfx.IsTeleporting) return;
                        if (!enemy.TeleportVfx.Succeeded) { Cancel(); return; }
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
                            else Cancel();
                        }
                        else
                        {
                            // 착지나 후딜레이를 기다리지 않고 같은 전조를 0.5초씩 어긋나게 시작한다.
                            nextJumpAt = Time.time + settings.linkedAttack.jumpInterval;
                            jumpStartDeadline = nextJumpAt + settings.linkedAttack.startTimeout;
                        }
                    }
                    else if (Time.time >= jumpStartDeadline) Cancel();
                    break;
                case LinkStage.WaitingForLastLanding:
                    var last = participants[participants.Count - 1];
                    if (last.JumpLandings > lastLandingsBefore)
                    {
                        BossHitAt = last.LastJumpLandedAt + settings.linkedAttack.attackGap;
                        SetStage(LinkStage.WaitingForBossFall);
                    }
                    else if (Time.time >= lastLandingDeadline) Cancel();
                    break;
                case LinkStage.WaitingForBossFall:
                    if (boss.FallsCompleted > bossFallsBefore)
                    {
                        CompletedCount++; ReadyAt = Time.time + settings.linkedAttack.cooldown;
                        stage = LinkStage.None; BossHitAt = float.PositiveInfinity; ReleaseParticipants();
                    }
                    break;
            }
        }

        private void SetStage(LinkStage next) { stage = next; elapsed = 0; }
        private void Cancel()
        {
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
            ReleaseParticipants(); ReadyAt = 0; CompletedCount = JumpsOrdered = 0;
        }
    }
}
