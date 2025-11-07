using System.Collections.Generic;

namespace BattleV2.AnimationSystem
{
    public interface IOrchestratorDiagnosticsProvider
    {
        OrchestratorDiagnosticsSnapshot GetDiagnostics();
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public readonly struct OrchestratorDiagnosticsSnapshot
    {
        public OrchestratorDiagnosticsSnapshot(
            RouterDiagnosticsInfo routerDiagnostics,
            IReadOnlyDictionary<string, BattlePhase> sessionPhases,
            IReadOnlyCollection<string> phaseStrategies,
            IReadOnlyCollection<string> bindingProviders,
            IReadOnlyCollection<string> bindingIssues)
        {
            RouterDiagnostics = routerDiagnostics;
            SessionPhases = sessionPhases ?? (IReadOnlyDictionary<string, BattlePhase>)new Dictionary<string, BattlePhase>();
            PhaseStrategies = phaseStrategies ?? (IReadOnlyCollection<string>)System.Array.Empty<string>();
            BindingProviders = bindingProviders ?? (IReadOnlyCollection<string>)System.Array.Empty<string>();
            BindingIssues = bindingIssues ?? (IReadOnlyCollection<string>)System.Array.Empty<string>();
        }

        public RouterDiagnosticsInfo RouterDiagnostics { get; }
        public IReadOnlyDictionary<string, BattlePhase> SessionPhases { get; }
        public IReadOnlyCollection<string> PhaseStrategies { get; }
        public IReadOnlyCollection<string> BindingProviders { get; }
        public IReadOnlyCollection<string> BindingIssues { get; }
    }
#else
    public readonly struct OrchestratorDiagnosticsSnapshot
    {
    }
#endif
}
