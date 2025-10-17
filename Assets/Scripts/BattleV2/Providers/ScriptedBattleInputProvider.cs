using System.Collections.Generic;
using BattleV2.Actions;
using BattleV2.Charge;
using BattleV2.Core;
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
                onSelected?.Invoke(new BattleSelection(fallback, 0, ResolveProfile(context, fallback)));
                return;
            }

            var action = GetNextAction(context.AvailableActions);
            if (action == null)
            {
                BattleLogger.Warn("ScriptedProvider", "Playlist action not available; using fallback index 0.");
                action = context.AvailableActions[0];
            }

            BattleLogger.Log("ScriptedProvider", $"Auto-selecting {action.id} (step {cursor}).");
            onSelected?.Invoke(new BattleSelection(action, 0, ResolveProfile(context, action)));
        }

        private BattleActionData GetNextAction(IReadOnlyList<BattleActionData> available)
        {
            if (cursor >= playlist.Count)
            {
                if (!loopPlaylist)
                {
                    cursor = playlist.Count - 1;
                }
                else
                {
                    cursor = 0;
                }
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

        private ChargeProfile ResolveProfile(BattleActionContext context, BattleActionData action)
        {
            var catalog = context?.Context?.Catalog;
            var impl = catalog != null ? catalog.Resolve(action) : null;
            return impl != null ? impl.ChargeProfile : defaultChargeProfile;
        }

        private void OnEnable()
        {
            cursor = 0;
        }
    }
}
