using UnityEngine;
using BattleV2.Core;

namespace BattleV2.Diagnostics
{
    /// <summary>
    /// Simple helper to toggle P2-lite diagnostics flags from the scene (temporary use).
    /// Remove or disable in production builds.
    /// </summary>
    public sealed class P2LiteTogglesSetter : MonoBehaviour
    {
        [Header("Diagnostics")]
        [SerializeField] private bool devFlowTrace = true;
        [SerializeField] private bool enableResolveShadow = true;
        [SerializeField] private bool enableReqLog = true;

        [Header("Flip control")]
        [SerializeField] private bool useP2LiteResolve = true;
        [SerializeField] private bool onlyForEnemies = true;
        [SerializeField] private string filterAttackerId = string.Empty; // Optional: name or instanceId as string.

        private void Awake()
        {
            BattleDiagnostics.DevFlowTrace = devFlowTrace;
            BattleDiagnostics.EnableP2LiteResolveShadow = enableResolveShadow;
            BattleDiagnostics.EnableP2LiteReqLog = enableReqLog;
            BattleDiagnostics.UseP2LiteResolve = useP2LiteResolve;
            BattleDiagnostics.P2LiteOnlyForEnemies = onlyForEnemies;
            BattleDiagnostics.P2LiteFilterAttackerId = filterAttackerId ?? string.Empty;
        }
    }
}
