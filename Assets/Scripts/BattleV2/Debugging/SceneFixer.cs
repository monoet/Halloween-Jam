#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using BattleV2.AnimationSystem.Runtime;
using BattleV2.Execution.TimedHits;
using BattleV2.UI;

namespace BattleV2.Debugging
{
    public static class SceneFixer
    {
        [MenuItem("BattleV2/Fix Scene Setup")]
        public static void FixScene()
        {
            var installer = Object.FindObjectOfType<AnimationSystemInstaller>();
            if (installer == null)
            {
                Debug.LogError("SceneFixer: AnimationSystemInstaller not found!");
                return;
            }

            Undo.RecordObject(installer, "Fix Scene Setup");

            // 1. Fix BasicTimedHitRunner
            var basicRunner = Object.FindObjectOfType<BasicTimedHitRunner>();
            if (basicRunner == null)
            {
                var go = new GameObject("BasicTimedHitRunner");
                go.transform.SetParent(installer.transform);
                basicRunner = go.AddComponent<BasicTimedHitRunner>();
                Undo.RegisterCreatedObjectUndo(go, "Create BasicTimedHitRunner");
                Debug.Log("SceneFixer: Created BasicTimedHitRunner.");
            }

            // Assign via reflection because field is private
            var basicField = typeof(AnimationSystemInstaller).GetField("basicTimedHitRunner", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (basicField != null)
            {
                basicField.SetValue(installer, basicRunner);
                Debug.Log("SceneFixer: Assigned BasicTimedHitRunner to Installer.");
            }

            // 2. Fix Ks1TimedHitRunner
            var ks1Runner = Object.FindObjectOfType<Ks1TimedHitRunner>();
            if (ks1Runner == null)
            {
                var go = new GameObject("Ks1TimedHitRunner");
                go.transform.SetParent(installer.transform);
                ks1Runner = go.AddComponent<Ks1TimedHitRunner>();
                Undo.RegisterCreatedObjectUndo(go, "Create Ks1TimedHitRunner");
                Debug.Log("SceneFixer: Created Ks1TimedHitRunner.");
            }

            var ks1Field = typeof(AnimationSystemInstaller).GetField("ks1TimedHitRunner", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (ks1Field != null)
            {
                ks1Field.SetValue(installer, ks1Runner);
                Debug.Log("SceneFixer: Assigned Ks1TimedHitRunner to Installer.");
            }
            }
    }
}
#endif
