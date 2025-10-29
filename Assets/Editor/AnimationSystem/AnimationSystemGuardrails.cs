#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BattleV2.AnimationSystem.Editor
{
    [InitializeOnLoad]
    public static class AnimationSystemGuardrails
    {
        private const string ManagerPath = "Assets/Scripts/BattleV2/Orchestration/BattleManagerV2.cs";
        private const string RequiredInvocation = "PlayAsync(";

        static AnimationSystemGuardrails()
        {
            ValidateBattleManagerContract();
        }

        private static void ValidateBattleManagerContract()
        {
            if (!File.Exists(ManagerPath))
            {
                return;
            }

            string contents = File.ReadAllText(ManagerPath);
            if (!contents.Contains(RequiredInvocation))
            {
                Debug.LogError("[AnimationSystemGuardrails] BattleManagerV2 must continue invoking PlayAsync on the orchestrator. Please restore the contract before committing.");
            }
        }
    }
}
#endif
