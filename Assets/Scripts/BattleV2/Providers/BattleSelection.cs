using BattleV2.Actions;
using BattleV2.Charge;
using BattleV2.Execution.TimedHits;
using BattleV2.Targeting;
using UnityEngine;

namespace BattleV2.Providers
{
    public readonly struct BattleSelection
    {
        public BattleSelection(
            BattleActionData action,
            int cpCharge = 0,
            ChargeProfile chargeProfile = null,
            Ks1TimedHitProfile timedHitProfile = null,
            TimedHitResult? timedHitResult = null,
            TargetSet? targets = null,
            TimedHitExecutionHandle timedHitHandle = null,
            string animationRecipeId = null,
            Transform targetTransform = null,
            BasicTimedHitProfile basicTimedHitProfile = null,
            TimedHitRunnerKind runnerKind = TimedHitRunnerKind.Default)
        {
            Action = action;
            CpCharge = cpCharge;
            ChargeProfile = chargeProfile;
            TimedHitProfile = timedHitProfile;
            TimedHitResult = timedHitResult;
            Targets = targets;
            TimedHitHandle = timedHitHandle;
            AnimationRecipeId = animationRecipeId;
            TargetTransform = targetTransform;
            BasicTimedHitProfile = basicTimedHitProfile;
            RunnerKind = runnerKind;
        }

        public BattleActionData Action { get; }
        public int CpCharge { get; }
        public ChargeProfile ChargeProfile { get; }
        public Ks1TimedHitProfile TimedHitProfile { get; }
        public TimedHitResult? TimedHitResult { get; }
        public TargetSet? Targets { get; }
        public TimedHitExecutionHandle TimedHitHandle { get; }
        public string AnimationRecipeId { get; }
        public Transform TargetTransform { get; }
        public BasicTimedHitProfile BasicTimedHitProfile { get; }
        public TimedHitRunnerKind RunnerKind { get; }

        public BattleSelection WithTargets(TargetSet? targets)
        {
            return new BattleSelection(Action, CpCharge, ChargeProfile, TimedHitProfile, TimedHitResult, targets, TimedHitHandle, AnimationRecipeId, TargetTransform, BasicTimedHitProfile, RunnerKind);
        }

        public BattleSelection WithTimedResult(TimedHitResult? timedHitResult)
        {
            return new BattleSelection(Action, CpCharge, ChargeProfile, TimedHitProfile, timedHitResult, Targets, TimedHitHandle, AnimationRecipeId, TargetTransform, BasicTimedHitProfile, RunnerKind);
        }

        public BattleSelection WithTimedHitHandle(TimedHitExecutionHandle handle)
        {
            return new BattleSelection(Action, CpCharge, ChargeProfile, TimedHitProfile, TimedHitResult, Targets, handle, AnimationRecipeId, TargetTransform, BasicTimedHitProfile, RunnerKind);
        }

        public BattleSelection WithAnimationRecipeId(string recipeId)
        {
            return new BattleSelection(Action, CpCharge, ChargeProfile, TimedHitProfile, TimedHitResult, Targets, TimedHitHandle, recipeId, TargetTransform, BasicTimedHitProfile, RunnerKind);
        }

        public BattleSelection WithTargetTransform(Transform targetTransform)
        {
            return new BattleSelection(Action, CpCharge, ChargeProfile, TimedHitProfile, TimedHitResult, Targets, TimedHitHandle, AnimationRecipeId, targetTransform, BasicTimedHitProfile, RunnerKind);
        }
    }
}
