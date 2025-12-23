using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BattleV2.Actions;
using BattleV2.Core;
using BattleV2.Execution;
using BattleV2.Execution.TimedHits;
using BattleV2.Orchestration.Events;
using BattleV2.Providers;
using BattleV2.Targeting;
using BattleV2.Targeting.Policies;
using UnityEngine;
using BattleV2.AnimationSystem.Runtime;
using BattleV2.Marks;
using System.Linq;

namespace BattleV2.Orchestration.Services
{
    public interface IEnemyTurnCoordinator
    {
        Task ExecuteAsync(EnemyTurnContext context);
    }

    public readonly struct EnemyTurnContext
    {
        public EnemyTurnContext(
            BattleManagerV2 manager,
            CombatantState attacker,
            CombatantState player,
            CombatContext combatContext,
            IReadOnlyList<CombatantState> allies,
            IReadOnlyList<CombatantState> enemies,
            float averageSpeed,
            BattleStateController stateController,
            Action<CombatantState> advanceTurn,
            Action stopTurnService,
            Func<bool> tryResolveBattleEnd,
            Action refreshCombatContext,
            CancellationToken token,
            uint battleSeed,
            int executionId,
            int attackerTurnCounter)
        {
            ExecutionId = executionId;
            AttackerTurnCounter = attackerTurnCounter;
            BattleSeed = battleSeed;
            Manager = manager;
            Attacker = attacker;
            Player = player;
            CombatContext = combatContext;
            Allies = allies ?? Array.Empty<CombatantState>();
            Enemies = enemies ?? Array.Empty<CombatantState>();
            AverageSpeed = averageSpeed;
            StateController = stateController;
            AdvanceTurn = advanceTurn ?? (_ => { });
            StopTurnService = stopTurnService ?? (() => { });
            TryResolveBattleEnd = tryResolveBattleEnd ?? (() => false);
            RefreshCombatContext = refreshCombatContext ?? (() => { });
            Token = token;
        }

        public int ExecutionId { get; }
        public int AttackerTurnCounter { get; }
        public uint BattleSeed { get; }
        public BattleManagerV2 Manager { get; }
        public CombatantState Attacker { get; }
        public CombatantState Player { get; }
        public CombatContext CombatContext { get; }
        public IReadOnlyList<CombatantState> Allies { get; }
        public IReadOnlyList<CombatantState> Enemies { get; }
        public float AverageSpeed { get; }
        public BattleStateController StateController { get; }
        public Action<CombatantState> AdvanceTurn { get; }
        public Action StopTurnService { get; }
        public Func<bool> TryResolveBattleEnd { get; }
        public Action RefreshCombatContext { get; }
        public CancellationToken Token { get; }
    }

    public sealed class EnemyTurnCoordinator : IEnemyTurnCoordinator
    {
        private readonly ActionCatalog actionCatalog;
        private readonly ICombatantActionValidator actionValidator;
        private readonly ITargetingCoordinator targetingCoordinator;
        private readonly IActionPipeline actionPipeline;
        private readonly ITriggeredEffectsService triggeredEffects;
        private readonly IBattleAnimOrchestrator animOrchestrator;
        private readonly IBattleEventBus eventBus;
        private readonly BattleV2.Core.Services.ICombatSideService sideService;
        private readonly IFallbackActionResolver fallbackActionResolver;
        private readonly BattleV2.Marks.MarkInteractionProcessor markProcessor;

        public EnemyTurnCoordinator(
            ActionCatalog actionCatalog,
            ICombatantActionValidator actionValidator,
            ITargetingCoordinator targetingCoordinator,
            IActionPipeline actionPipeline,
            ITriggeredEffectsService triggeredEffects,
            IBattleAnimOrchestrator animOrchestrator,
            IBattleEventBus eventBus,
            BattleV2.Core.Services.ICombatSideService sideService,
            IFallbackActionResolver fallbackActionResolver = null,
            BattleV2.Marks.MarkInteractionProcessor markProcessor = null)
        {
            this.actionCatalog = actionCatalog;
            this.actionValidator = actionValidator;
            this.targetingCoordinator = targetingCoordinator;
            this.actionPipeline = actionPipeline;
            this.triggeredEffects = triggeredEffects;
            this.animOrchestrator = animOrchestrator;
            this.eventBus = eventBus;
            this.sideService = sideService ?? new BattleV2.Core.Services.CombatSideService();
            this.fallbackActionResolver = fallbackActionResolver;
            this.markProcessor = markProcessor;
        }

        public async Task ExecuteAsync(EnemyTurnContext context)
        {
            var attacker = context.Attacker;
            if (attacker == null || !attacker.IsAlive)
            {
                context.AdvanceTurn(attacker);
                context.StateController?.Set(BattleState.AwaitingAction);
                return;
            }

            context.StateController?.Set(BattleState.Resolving);

            var target = context.Player;
            if (target == null || target.IsDead())
            {
                context.StateController?.Set(BattleState.Defeat);
                context.StopTurnService();
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (BattleDiagnostics.DevFlowTrace)
            {
                string source = "Empty";
                string ids = "[]";

                if (attacker != null && attacker.ActionLoadout != null)
                {
                    bool assetHasIds = attacker.ActionLoadout.ActionIds != null && attacker.ActionLoadout.ActionIds.Count > 0;
                    if (assetHasIds)
                    {
                        source = "Asset";
                    }
                    else
                    {
                        source = attacker.AllowLegacyActionFallback ? "AssetEmpty->LegacyFallback" : "AssetEmpty->None";
                    }
                }
                else if (attacker != null && attacker.AllowedActionIds != null && attacker.AllowedActionIds.Count > 0)
                {
                    source = "Legacy";
                }

                var effectiveIds = attacker != null ? attacker.AllowedActionIds : null;
                if (effectiveIds != null && effectiveIds.Count > 0)
                {
                    var parts = new string[Mathf.Min(effectiveIds.Count, 10)];
                    for (int i = 0; i < parts.Length; i++)
                    {
                        parts[i] = effectiveIds[i] ?? "(null)";
                    }
                    ids = $"[{string.Join(",", parts)}{(effectiveIds.Count > parts.Length ? ",..+" + (effectiveIds.Count - parts.Length) : string.Empty)}]";
                }

                BattleDiagnostics.Log(
                    "BATTLEFLOW",
                    $"LOADOUT_EFFECTIVE actor={attacker?.DisplayName ?? "(null)"}#{(attacker != null ? attacker.GetInstanceID() : 0)} source={source} ids={ids}",
                    attacker);
            }
#endif

            var combatContext = context.CombatContext;
            var available = actionCatalog?.BuildAvailableFor(attacker, combatContext);
            if (available == null || available.Count == 0)
            {
                Debug.LogWarning($"[EnemyTurnCoordinator] Enemy {attacker.name} has no actions.");
                context.AdvanceTurn(attacker);
                context.StateController?.Set(BattleState.AwaitingAction);
                return;
            }

            bool strictAllowedIds = attacker != null && attacker.ActionLoadout != null;
            available = (System.Collections.Generic.IReadOnlyList<BattleActionData>)BattleV2.Orchestration.Services.ActionAvailabilityService.FilterAllowedIds(
                available,
                attacker != null ? attacker.AllowedActionIds : null,
                strictAllowedIds,
                attacker);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (BattleDiagnostics.DevFlowTrace)
            {
                string availableIds;
                if (available == null || available.Count == 0)
                {
                    availableIds = "[]";
                }
                else
                {
                    var parts = new string[Mathf.Min(available.Count, 10)];
                    for (int i = 0; i < parts.Length; i++)
                    {
                        parts[i] = available[i]?.id ?? "(null)";
                    }
                    availableIds = $"[{string.Join(",", parts)}{(available.Count > parts.Length ? ",..+" + (available.Count - parts.Length) : string.Empty)}]";
                }

                 BattleDiagnostics.Log(
                     "BATTLEFLOW",
                    $"ENEMY_ACTION_POOL exec={context.ExecutionId} actor={attacker?.DisplayName ?? "(null)"}#{(attacker != null ? attacker.GetInstanceID() : 0)} allowedFilter={(attacker != null && attacker.AllowedActionIds != null && attacker.AllowedActionIds.Count > 0)} available={availableIds}",
                     attacker);
             }
#endif

            BattleActionData actionData = null;
            IAction implementation = null;
            for (int i = 0; i < available.Count; i++)
            {
                var candidate = available[i];
                if (actionValidator.TryValidate(candidate, attacker, combatContext, 0, out implementation))
                {
                    actionData = candidate;
                    break;
                }
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (BattleDiagnostics.DevFlowTrace)
            {
                BattleDiagnostics.Log(
                    "BATTLEFLOW",
                    $"ENEMY_ACTION_PICK exec={context.ExecutionId} actor={attacker?.DisplayName ?? "(null)"}#{(attacker != null ? attacker.GetInstanceID() : 0)} action={(actionData != null ? actionData.id : "(none)")}",
                    attacker);
            }
#endif

            if (actionData == null || implementation == null)
            {
                Debug.LogWarning($"[EnemyTurnCoordinator] Enemy {attacker.name} has no valid actions.");
                context.AdvanceTurn(attacker);
                context.StateController?.Set(BattleState.AwaitingAction);
                return;
            }

            BasicTimedHitProfile basicProfile = null;
            TimedHitRunnerKind runnerKind = TimedHitRunnerKind.Default;
            if (implementation is IBasicTimedHitAction basicTimedAction && basicTimedAction.BasicTimedHitProfile != null)
            {
                basicProfile = basicTimedAction.BasicTimedHitProfile;
                runnerKind = TimedHitRunnerKind.Basic;
            }

            var selection = new BattleSelection(
                actionData,
                0,
                implementation.ChargeProfile,
                null,
                basicTimedHitProfile: basicProfile,
                runnerKind: runnerKind);

            await RunEnemyActionAsync(
                context,
                attacker,
                selection,
                implementation);
        }

        private async Task RunEnemyActionAsync(
            EnemyTurnContext context,
            CombatantState attacker,
            BattleSelection selection,
            IAction implementation,
            bool allowFallback = true)
        {
            try
            {
                var intent = TargetingIntent.FromAction(selection.Action);
                bool resourcesCharged = false;

                // IMPORTANT: orientar listas desde la perspectiva del atacante.
                // sameSide   = aliados del atacante (su escuadrón)
                // opponents  = oponentes del atacante (objetivos ofensivos)
                IReadOnlyList<CombatantState> sameSide = context.Enemies ?? Array.Empty<CombatantState>();
                IReadOnlyList<CombatantState> opponents = context.Allies ?? Array.Empty<CombatantState>();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (BattleDiagnostics.DevFlowTrace)
                {
                    bool selfInOpponents = attacker != null && opponents.Contains(attacker);
                    bool selfInSameSide = attacker != null && sameSide.Contains(attacker);
                    bool attackerIsEnemy = attacker != null && attacker.IsEnemy;
                    int sameSideAlive = CountAlive(sameSide);
                    int opponentsAlive = CountAlive(opponents);
                    int selfIndexOpponents = selfInOpponents ? IndexOf(opponents, attacker) : -1;
                    string sampleSameSide = FormatSample(sameSide, attacker, IndexOf(sameSide, attacker));
                    string sampleOpponents = FormatSample(opponents, attacker, selfIndexOpponents);
                    BattleDiagnostics.Log(
                        "BATTLEFLOW",
                        $"TARGET_LISTS exec={context.ExecutionId} attacker={attacker?.DisplayName ?? "(null)"}#{(attacker != null ? attacker.GetInstanceID() : 0)} sameSideN={sameSide.Count} opponentsN={opponents.Count} sameSideAliveN={sameSideAlive} opponentsAliveN={opponentsAlive} selfInOpponents={selfInOpponents} selfIdxOpponents={selfIndexOpponents} selfInSameSide={selfInSameSide} sampleSameSide={sampleSameSide} sampleOpponents={sampleOpponents}",
                        attacker);
                    if (selfInOpponents)
                    {
                        BattleDiagnostics.Log(
                            "BATTLEFLOW",
                            $"WARN_TARGET_LISTS_SELF_IN_OPPONENTS exec={context.ExecutionId} attacker={attacker?.DisplayName ?? "(null)"}#{(attacker != null ? attacker.GetInstanceID() : 0)} selfIdxOpponents={selfIndexOpponents} sampleOpponents={sampleOpponents}",
                            attacker);
                    }
                    // Heurística sencilla: solo gritar si la mayoría absoluta parecen same-side (evitar falsos positivos).
                    if (attackerIsEnemy && opponents.Count >= 2)
                    {
                        int sameTeamInOpponents = 0;
                        for (int i = 0; i < opponents.Count; i++)
                        {
                            var opp = opponents[i];
                            if (opp != null && opp.IsEnemy == attacker.IsEnemy)
                            {
                                sameTeamInOpponents++;
                            }
                        }
                        bool likelySwap = sameTeamInOpponents == opponents.Count ||
                                          (opponents.Count >= 3 && sameTeamInOpponents >= opponents.Count - 1);
                        if (attacker != null && sameTeamInOpponents > 0 && likelySwap)
                        {
                            BattleDiagnostics.Log(
                                "BATTLEFLOW",
                                $"WARN_TARGET_LISTS_OPPONENTS_LOOK_LIKE_SAMESIDE exec={context.ExecutionId} attacker={attacker.DisplayName}#{attacker.GetInstanceID()} sameTeamCount={sameTeamInOpponents}/{opponents.Count} sampleOpponents={sampleOpponents}",
                                attacker);
                        }
                    }
                }
#endif

                var action = selection.Action;
                bool isOffensiveSingle = attacker != null &&
                                         attacker.IsEnemy &&
                                         action != null &&
                                         action.targetAudience == TargetAudience.Enemies &&
                                         action.targetShape == TargetShape.Single;

                IReadOnlyList<CombatantState> alliesForTargeting = sameSide;
                IReadOnlyList<CombatantState> enemiesForTargeting = opponents;
                List<CombatantState> candidatesAlive = null;
                CombatantState picked = null;
                TargetPickResult pick = default;

                if (isOffensiveSingle &&
                    enemiesForTargeting != null)
                {
                    candidatesAlive = CollectAliveTargets(enemiesForTargeting);

                    if (candidatesAlive.Count == 0)
                    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        if (BattleDiagnostics.DevFlowTrace)
                        {
                            BattleDiagnostics.Log(
                                "BATTLEFLOW",
                                $"NOOP_NO_VALID_TARGET exec={context.ExecutionId} attacker={attacker.DisplayName}#{attacker.GetInstanceID()} action={action.id} reason=NoOpponentsAlive consumeTurn=true",
                                attacker);
                        }
#endif
                        context.AdvanceTurn(attacker);
                        context.StateController?.Set(BattleState.AwaitingAction);
                        await BattlePacingUtility.DelayGlobalAsync("EnemyTurn", attacker, context.Token);
                        return;
                    }

                    if (candidatesAlive.Count > 1)
                    {
                        // Default: offensive actions should never self-target unless a strategy/estado explícito lo permita.
                        bool allowSelfTarget = false;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        if (BattleDiagnostics.DevFlowTrace)
                        {
                            BattleDiagnostics.Log(
                                "BATTLEFLOW",
                                $"AI_TARGET_CANDIDATES exec={context.ExecutionId} attacker={attacker.DisplayName}#{attacker.GetInstanceID()} action={action.id} audience={action.targetAudience} shape=Single candidates={EnemyTargetingDebug.FormatCandidates(candidatesAlive)} allowSelfTarget={allowSelfTarget}",
                                attacker);
                        }
#endif

                        var filtered = allowSelfTarget
                            ? candidatesAlive.ToList()
                            : candidatesAlive.Where(c => c != attacker).ToList();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        if (BattleDiagnostics.DevFlowTrace)
                        {
                            BattleDiagnostics.Log(
                                "BATTLEFLOW",
                                $"AI_TARGET_FILTER exec={context.ExecutionId} allowSelfTarget={allowSelfTarget} beforeN={candidatesAlive.Count} afterN={filtered.Count} filtered={EnemyTargetingDebug.FormatCandidates(filtered)}",
                                attacker);
                        }
#endif

                        if (filtered.Count > 0)
                        {
                            int spawnInstanceId = attacker.SpawnInstanceId != 0 ? attacker.SpawnInstanceId : attacker.GetInstanceID();
                            uint actionHash = unchecked((uint)EnemyTargetingDebug.StableHash(action.id));
                            int seed = EnemyTargetingDebug.MixSeed(
                                context.BattleSeed,
                                unchecked((uint)spawnInstanceId),
                                unchecked((uint)context.AttackerTurnCounter),
                                actionHash);

                            var policy = EnemyTargetingPolicyRegistry.Get("RandomAlive");
                            var policyContext = new TargetingContext(
                                context.ExecutionId,
                                attacker,
                                action.id,
                                TargetShape.Single,
                                filtered,
                                seed);

                            pick = policy.PickTarget(policyContext);
                            picked = pick.Picked;
                            if (!allowSelfTarget && picked == attacker)
                            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                                if (BattleDiagnostics.DevFlowTrace)
                                {
                                    BattleDiagnostics.Log(
                                        "BATTLEFLOW",
                                        $"WARN_SELF_TARGET_PICKED exec={context.ExecutionId} attacker={attacker.DisplayName}#{attacker.GetInstanceID()} action={action.id} pickedSelfWhileAllowSelfTargetFalse seed={seed}",
                                        attacker);
                                }
#endif
                                picked = filtered.FirstOrDefault(c => c != attacker);
                            }

                            if (picked != null)
                            {
                                enemiesForTargeting = ReorderFirst(enemiesForTargeting, picked);
                            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                            if (BattleDiagnostics.DevFlowTrace)
                            {
                                string pickedStr = picked != null
                                    ? $"{picked.DisplayName}#{picked.GetInstanceID()}"
                                    : "(null)";
                                BattleDiagnostics.Log(
                                    "BATTLEFLOW",
                                    $"AI_TARGET_PICK exec={context.ExecutionId} battleSeed={context.BattleSeed} spawnId={spawnInstanceId} turnIdx={context.AttackerTurnCounter} action={action.id} actionHash={actionHash} shape=Single policy={policy.Id} seed={seed} rollIdx={pick.Index} roll01={pick.Roll01:0.0000} candidates={EnemyTargetingDebug.FormatCandidates(filtered)} picked={pickedStr}",
                                    attacker);
                            }
#endif
                        }
                        else
                        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                            if (BattleDiagnostics.DevFlowTrace)
                            {
                                BattleDiagnostics.Log(
                                    "BATTLEFLOW",
                                    $"WARN_NO_VALID_TARGET exec={context.ExecutionId} attacker={attacker.DisplayName}#{attacker.GetInstanceID()} action={action.id} reason=FilteredEmpty allowSelfTarget={allowSelfTarget}",
                                    attacker);
                            }
#endif
                            if (BattleDiagnostics.DevFlowTrace)
                            {
                                BattleDiagnostics.Log(
                                    "BATTLEFLOW",
                                    $"NOOP_NO_VALID_TARGET exec={context.ExecutionId} attacker={attacker.DisplayName}#{attacker.GetInstanceID()} action={action.id} reason=FilteredEmpty consumeTurn=true",
                                    attacker);
                            }
                            context.AdvanceTurn(attacker);
                            context.StateController?.Set(BattleState.AwaitingAction);
                            await BattlePacingUtility.DelayGlobalAsync("EnemyTurn", attacker, context.Token);
                            return;
                        }
                    }
                }

                // Use targeting coordinator to resolve based on action side/scope.
                  var resolution = await targetingCoordinator.ResolveAsync(
                      attacker,
                      selection.Action,
                      intent,
                      TargetSourceType.Auto,
                      context.Player,
                      alliesForTargeting,
                      enemiesForTargeting);

                // Guardrail: never allow self-target for offensive actions by default.
                if (attacker != null &&
                    action != null &&
                    action.targetAudience == TargetAudience.Enemies &&
                    resolution.Targets != null &&
                    resolution.Targets.Contains(attacker))
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    if (BattleDiagnostics.DevFlowTrace)
                    {
                        BattleDiagnostics.Log(
                            "BATTLEFLOW",
                            $"WARN_RESOLVE_RETURNED_SELF exec={context.ExecutionId} attacker={attacker.DisplayName}#{attacker.GetInstanceID()} action={action.id} audience={action.targetAudience} shape={action.targetShape} resolvedCount={resolution.Targets.Count}",
                            attacker);
                        BattleDiagnostics.Log(
                            "BATTLEFLOW",
                            $"NOOP_SELF_TARGET_DENIED exec={context.ExecutionId} attacker={attacker.DisplayName}#{attacker.GetInstanceID()} action={action.id} audience={action.targetAudience} shape={action.targetShape} consumeTurn=true",
                            attacker);
                    }
#endif
                    context.AdvanceTurn(attacker);
                    context.StateController?.Set(BattleState.AwaitingAction);
                    await BattlePacingUtility.DelayGlobalAsync("EnemyTurn", attacker, context.Token);
                    return;
                }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (BattleDiagnostics.DevFlowTrace &&
                    attacker != null &&
                    action != null &&
                    action.targetShape == TargetShape.Single)
                {
                    if (resolution.Targets != null && resolution.Targets.Count > 1)
                    {
                        BattleDiagnostics.Log(
                            "BATTLEFLOW",
                            $"WARN_TARGET_SHAPE_SINGLE_GOT_MULTI exec={context.ExecutionId} actor={attacker.DisplayName}#{attacker.GetInstanceID()} action={action.id} resolvedCount={resolution.Targets.Count}",
                            attacker);
                    }

                    if (candidatesAlive != null && candidatesAlive.Count > 0)
                    {
                        // Note: picked is only set when we ran the policy branch.
                        // If resolved differs from picked, log it for visibility.
                        var resolvedPrimary = resolution.Targets != null && resolution.Targets.Count > 0 ? resolution.Targets[0] : null;
                        if (picked != null && resolvedPrimary != null && picked != resolvedPrimary)
                        {
                            BattleDiagnostics.Log(
                                "BATTLEFLOW",
                                $"WARN_PICK_MISMATCH exec={context.ExecutionId} actor={attacker.DisplayName}#{attacker.GetInstanceID()} action={action.id} picked={picked.DisplayName}#{picked.GetInstanceID()} resolved={resolvedPrimary.DisplayName}#{resolvedPrimary.GetInstanceID()}",
                                attacker);
                        }
                    }
                }
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (BattleDiagnostics.DevFlowTrace)
                {
                    string setIds;
                    if (resolution.TargetSet.IsGroup)
                    {
                        setIds = resolution.TargetSet.Ids != null ? $"[{string.Join(",", resolution.TargetSet.Ids)}]" : "[]";
                    }
                    else
                    {
                        setIds = $"[{resolution.TargetSet.SingleId}]";
                    }

                    string targetsStr;
                    if (resolution.Targets == null || resolution.Targets.Count == 0)
                    {
                        targetsStr = "[]";
                    }
                    else
                    {
                        var parts = new string[Mathf.Min(resolution.Targets.Count, 6)];
                        for (int i = 0; i < parts.Length; i++)
                        {
                            var t = resolution.Targets[i];
                            parts[i] = t != null ? $"{t.DisplayName}#{t.GetInstanceID()}" : "(null)";
                        }
                        targetsStr = $"[{string.Join(",", parts)}{(resolution.Targets.Count > parts.Length ? ",..+" + (resolution.Targets.Count - parts.Length) : string.Empty)}]";
                    }

                    BattleDiagnostics.Log(
                        "BATTLEFLOW",
                        $"TARGET_RESOLVE exec={context.ExecutionId} actor={attacker?.DisplayName ?? "(null)"}#{(attacker != null ? attacker.GetInstanceID() : 0)} action={selection.Action?.id ?? "(null)"} shape={selection.Action?.targetShape} setGroup={resolution.TargetSet.IsGroup} setIds={setIds} targets={targetsStr}",
                        attacker);
                }
#endif

                if (resolution.Targets.Count == 0)
                {
                    if (allowFallback && TryResolveFallback(context, attacker, out var fallbackSelection, out var fallbackImpl))
                    {
                        await RunEnemyActionAsync(context, attacker, fallbackSelection, fallbackImpl, allowFallback: false);
                        return;
                    }

                    context.AdvanceTurn(attacker);
                    context.StateController?.Set(BattleState.AwaitingAction);
                    await BattlePacingUtility.DelayGlobalAsync("EnemyTurn", attacker, context.Token);
                    return;
                }

                var enrichedSelection = selection.WithTargets(resolution.TargetSet);
                var primaryTarget = resolution.Targets != null && resolution.Targets.Count > 0
                    ? resolution.Targets[0]
                    : null;
                if (primaryTarget != null)
                {
                    var targetTransform = primaryTarget.transform;
                    if (targetTransform != null)
                    {
                        enrichedSelection = enrichedSelection.WithTargetTransform(targetTransform);
                    }
                }
                if (enrichedSelection.TimedHitProfile != null)
                {
                    enrichedSelection = enrichedSelection.WithTimedHitHandle(new TimedHitExecutionHandle(enrichedSelection.TimedHitResult, context.ExecutionId));
                }

                var snapshot = new ExecutionSnapshot(context.Allies, context.Enemies, resolution.Targets);

                var playbackTask = animOrchestrator != null
                    ? animOrchestrator.PlayAsync(new ActionPlaybackRequest(attacker, enrichedSelection, resolution.Targets, context.AverageSpeed, enrichedSelection.AnimationRecipeId))
                    : Task.CompletedTask;

                var defeatCandidates = CollectDeathCandidates(resolution.Targets);

                var judgmentSeed = System.HashCode.Combine(attacker != null ? attacker.GetInstanceID() : 0, enrichedSelection.Action != null ? enrichedSelection.Action.id.GetHashCode() : 0, resolution.Targets.Count);
                var resourcesPre = BattleV2.Execution.ResourceSnapshot.FromCombatant(attacker);
                var resourcesPost = BattleV2.Execution.ResourceSnapshot.FromCombatant(attacker);
                var judgment = BattleV2.Execution.ActionJudgment.FromSelection(enrichedSelection, attacker, enrichedSelection.CpCharge, judgmentSeed, resourcesPre, resourcesPost);

                var request = new ActionRequest(
                    context.ExecutionId,
                    context.Manager,
                    attacker,
                    resolution.Targets,
                    enrichedSelection,
                    implementation,
                    context.CombatContext,
                    judgment);

                var result = await actionPipeline.Run(request);
                if (!result.Success)
                {
                    context.AdvanceTurn(attacker);
                    context.StateController?.Set(BattleState.AwaitingAction);
                    await BattlePacingUtility.DelayGlobalAsync("EnemyTurn", attacker, context.Token);
                    return;
                }

                resourcesPost = BattleV2.Execution.ResourceSnapshot.FromCombatant(attacker);
                if (resourcesCharged)
                {
#if UNITY_EDITOR
                    Debug.Assert(false, $"[CP/SP] Duplicate charge: action={enrichedSelection.Action?.id ?? "null"} actor={attacker?.name ?? "(null)"}");
#endif
                    Debug.LogWarning($"[CP/SP] Duplicate charge detected: action={enrichedSelection.Action?.id ?? "null"} actor={attacker?.name ?? "(null)"}");
                }
                resourcesCharged = true;
                var judgmentWithCosts = judgment.WithPostCost(resourcesPost);
                var timedGrade = BattleV2.Execution.ActionJudgment.ResolveTimedGrade(result.TimedResult);
                judgment = judgmentWithCosts.WithTimedGrade(timedGrade);

                triggeredEffects?.Schedule(
                    context.ExecutionId,
                    attacker,
                    enrichedSelection,
                    result.TimedResult,
                    snapshot,
                    context.CombatContext);

                if (playbackTask != null)
                {
                    try
                    {
                        await playbackTask;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[EnemyTurnCoordinator] Enemy playback failed: {ex}");
                    }
                }

                markProcessor?.Process(attacker, enrichedSelection, judgment, resolution.Targets, context.ExecutionId, context.AttackerTurnCounter);
                context.RefreshCombatContext();

                PublishDefeatEvents(defeatCandidates, attacker);

                bool battleEnded = context.TryResolveBattleEnd();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (BattleDiagnostics.DevCpTrace)
                {
                    BattleDiagnostics.Log(
                        "CPTRACE",
                        $"TURN_CLOSE_PUBLISH exec={context.ExecutionId} actor={attacker?.DisplayName ?? "(null)"}#{(attacker != null ? attacker.GetInstanceID() : 0)} action={enrichedSelection.Action?.id ?? "(null)"} cp={enrichedSelection.CpCharge} isTriggered=false",
                        attacker);
                }
                if (BattleDiagnostics.DevFlowTrace)
                {
                    BattleDiagnostics.Log(
                        "BATTLEFLOW",
                        $"TURN_CLOSE_PUBLISH exec={context.ExecutionId} actor={attacker?.DisplayName ?? "(null)"}#{(attacker != null ? attacker.GetInstanceID() : 0)} action={enrichedSelection.Action?.id ?? "(null)"} cp={enrichedSelection.CpCharge} isTriggered=false",
                        attacker);
                }
#endif
                eventBus?.Publish(new ActionCompletedEvent(context.ExecutionId, attacker, enrichedSelection.WithTimedResult(result.TimedResult), resolution.Targets, isTriggered: false, judgment: judgment));

                if (battleEnded)
                {
                    await BattlePacingUtility.DelayGlobalAsync("EnemyTurn", attacker, context.Token);
                    return;
                }

                context.StateController?.Set(BattleState.AwaitingAction);
                await BattlePacingUtility.DelayGlobalAsync("EnemyTurn", attacker, context.Token);
            }
            catch (Exception ex)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (BattleDiagnostics.DevCpTrace)
                {
                    BattleDiagnostics.Log(
                        "CPTRACE",
                        $"EXCEPTION exec={context.ExecutionId} where=EnemyTurnCoordinator exType={(ex != null ? ex.GetType().Name : "(null)")} exMsg={(ex != null ? ex.Message : "(null)")}",
                        attacker);
                }
#endif
                Debug.LogError($"[EnemyTurnCoordinator] Enemy action error: {ex}");
                context.AdvanceTurn(attacker);
                context.StateController?.Set(BattleState.AwaitingAction);
                await BattlePacingUtility.DelayGlobalAsync("EnemyTurn", attacker, context.Token);
            }
        }

        private static List<CombatantState> CollectAliveTargets(IReadOnlyList<CombatantState> list)
        {
            if (list == null || list.Count == 0)
            {
                return new List<CombatantState>(0);
            }

            var alive = new List<CombatantState>(list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                var c = list[i];
                if (c != null && c.IsAlive)
                {
                    alive.Add(c);
                }
            }

            return alive;
        }

        private static int CountAlive(IReadOnlyList<CombatantState> list)
        {
            if (list == null || list.Count == 0) return 0;
            int alive = 0;
            for (int i = 0; i < list.Count; i++)
            {
                var c = list[i];
                if (c != null && c.IsAlive) alive++;
            }
            return alive;
        }

        private static string FormatSample(IReadOnlyList<CombatantState> list, CombatantState attacker, int attackerIndex)
        {
            if (list == null || list.Count == 0) return "[]";
            int take = Math.Min(list.Count, 3);
            var parts = new string[take];
            for (int i = 0; i < take; i++)
            {
                var c = list[i];
                bool isAttacker = c != null && attacker != null && c == attacker;
                parts[i] = c != null ? $"{c.DisplayName}#{c.GetInstanceID()}{(isAttacker ? "*ATTACKER*" : string.Empty)}" : "(null)";
            }
            string suffix = list.Count > take ? $",..+{list.Count - take}" : string.Empty;
            // Si el atacante está fuera del sample, forzamos visibilidad en el último slot.
            if (attackerIndex >= take && attackerIndex >= 0 && attacker != null)
            {
                parts[take - 1] = $"{attacker.DisplayName}#{attacker.GetInstanceID()}*ATTACKER*@idx={attackerIndex}";
            }
            return $"[{string.Join(",", parts)}{suffix}]";
        }

        private static int IndexOf(IReadOnlyList<CombatantState> list, CombatantState target)
        {
            if (list == null || list.Count == 0 || target == null) return -1;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == target) return i;
            }
            return -1;
        }

        private static IReadOnlyList<CombatantState> ReorderFirst(IReadOnlyList<CombatantState> list, CombatantState first)
        {
            if (list == null || list.Count <= 1 || first == null)
            {
                return list;
            }

            var reordered = new List<CombatantState>(list.Count);
            reordered.Add(first);
            for (int i = 0; i < list.Count; i++)
            {
                var c = list[i];
                if (c == null || c == first)
                {
                    continue;
                }
                reordered.Add(c);
            }

            return reordered;
        }

        private bool TryResolveFallback(EnemyTurnContext context, CombatantState attacker, out BattleSelection selection, out IAction implementation)
        {
            selection = default;
            implementation = null;

            if (fallbackActionResolver == null || attacker == null)
            {
                return false;
            }

            if (!fallbackActionResolver.TryResolve(attacker, context.CombatContext, out selection) || selection.Action == null)
            {
                return false;
            }

            if (!actionValidator.TryValidate(selection.Action, attacker, context.CombatContext, selection.CpCharge, out implementation))
            {
                return false;
            }

            return true;
        }

        private static List<CombatantState> CollectDeathCandidates(IReadOnlyList<CombatantState> targets)
        {
            var result = new List<CombatantState>();
            if (targets == null)
            {
                return result;
            }

            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (target == null || !target.IsAlive)
                {
                    continue;
                }

                if (!result.Contains(target))
                {
                    result.Add(target);
                }
            }

            return result;
        }

        private void PublishDefeatEvents(List<CombatantState> candidates, CombatantState killer)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return;
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                var combatant = candidates[i];
                if (combatant != null && combatant.IsDead())
                {
                    eventBus?.Publish(new CombatantDefeatedEvent(combatant, killer));
                }
            }
        }
    }
}
