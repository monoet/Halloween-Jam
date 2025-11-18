using System.Collections.Generic;
using BattleV2.Actions;
using BattleV2.Charge;
using BattleV2.Core;
using BattleV2.Execution.TimedHits;
using UnityEngine;

namespace BattleV2.Providers
{
    /// <summary>
    /// Deterministic provider that plays through a predefined playlist of actions.
    /// Useful for smoke testing sequences without manual input.
    /// </summary>
    [CreateAssetMenu(menuName = "Battle/Input Provider/Scripted")]
    public class ScriptedBattleInputProvider : ScriptableObject, IBattleInputProvider
    {
        [SerializeField] private List<BattleActionData> playlist = new();
        [SerializeField] private bool loopPlaylist = true;
        [SerializeField] private ChargeProfile defaultChargeProfile;

        private int cursor;

        public void RequestAction(BattleActionContext context, System.Action<BattleSelection> onSelected, System.Action onCancel)
        {
            if (context == null || context.AvailableActions == null || context.AvailableActions.Count == 0)
            {
                BattleLogger.Warn("ScriptedProvider", "No available actions; cancelling.");
                onCancel?.Invoke();
                return;
            }

            if (playlist.Count == 0)
            {
                BattleLogger.Warn("ScriptedProvider", "Playlist empty; falling back to first available action.");
                var fallback = context.AvailableActions[0];
                ResolveProfiles(context, fallback, out var chargeProfile, out var timedProfile, out var basicFallbackProfile, out var fallbackRunnerKind);
                onSelected?.Invoke(new BattleSelection(fallback, 0, chargeProfile, timedProfile, basicTimedHitProfile: basicFallbackProfile, runnerKind: fallbackRunnerKind));
                return;
            }

            var action = GetNextAction(context.AvailableActions);
            if (action == null)
            {
                BattleLogger.Warn("ScriptedProvider", "Playlist action not available; using fallback index 0.");
                action = context.AvailableActions[0];
            }

            ResolveProfiles(context, action, out var selectedChargeProfile, out var selectedTimedProfile, out var basicSelectedProfile, out var selectedRunnerKind);

            BattleLogger.Log("ScriptedProvider", $"Auto-selecting {action.id} (step {cursor}).");
            onSelected?.Invoke(new BattleSelection(action, 0, selectedChargeProfile, selectedTimedProfile, basicTimedHitProfile: basicSelectedProfile, runnerKind: selectedRunnerKind));
        }

        private BattleActionData GetNextAction(IReadOnlyList<BattleActionData> available)
        {
            if (cursor >= playlist.Count)
            {
                cursor = loopPlaylist ? 0 : playlist.Count - 1;
            }

            if (playlist.Count == 0)
            {
                return null;
            }

            var desired = playlist[cursor];
            cursor++;

            if (desired == null)
            {
                return null;
            }

            for (int i = 0; i < available.Count; i++)
            {
                var candidate = available[i];
                if (candidate != null && candidate.id == desired.id)
                {
                    return candidate;
                }
            }

            return null;
        }

        private void ResolveProfiles(
            BattleActionContext context,
            BattleActionData action,
            out ChargeProfile chargeProfile,
            out Ks1TimedHitProfile timedProfile,
            out BasicTimedHitProfile basicProfile,
            out TimedHitRunnerKind runnerKind)
        {
            var catalog = context?.Context?.Catalog;
            var impl = catalog != null ? catalog.Resolve(action) : null;

            chargeProfile = defaultChargeProfile;
            timedProfile = null;
            basicProfile = null;
            runnerKind = TimedHitRunnerKind.Default;

            if (impl != null)
            {
                if (impl.ChargeProfile != null)
                {
                    chargeProfile = impl.ChargeProfile;
                }

                if (impl is ITimedHitAction timedHitAction)
                {
                    timedProfile = timedHitAction.TimedHitProfile;
                }

                if (impl is IBasicTimedHitAction basicTimedAction && basicTimedAction.BasicTimedHitProfile != null)
                {
                    basicProfile = basicTimedAction.BasicTimedHitProfile;
                    runnerKind = TimedHitRunnerKind.Basic;
                }
            }

            if (chargeProfile == null)
            {
                chargeProfile = defaultChargeProfile != null
                    ? defaultChargeProfile
                    : ChargeProfile.CreateRuntimeDefault();
            }
        }

        private void OnEnable()
        {
            cursor = 0;
        }
    }
}
