using UnityEditor;
using UnityEngine;

namespace BattleV2.Diagnostics.Editor
{
    [CustomEditor(typeof(BattleDebugTogglesBehaviour))]
    internal sealed class BattleDebugTogglesBehaviourEditor : UnityEditor.Editor
    {
        private SerializedProperty enableEG;
        private SerializedProperty enableMS;
        private SerializedProperty enableRTO;
        private SerializedProperty enableSS;
        private SerializedProperty enableEX;
        private SerializedProperty enableAPLogs;
        private SerializedProperty enableAPFeature;
        private SerializedProperty persistToPlayerPrefs;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private SerializedProperty enableCpTraceLogs;
        private SerializedProperty enableAnimTraceLogs;
        private SerializedProperty enableLocomotionTraceLogs;
        private SerializedProperty enableFlowTraceLogs;
#endif

        private bool showChannels = true;
        private bool showDiagnostics = true;
        private bool showExperimental = true;
        private bool showPersistence = true;

        private void OnEnable()
        {
            enableEG = serializedObject.FindProperty("enableEG");
            enableMS = serializedObject.FindProperty("enableMS");
            enableRTO = serializedObject.FindProperty("enableRTO");
            enableSS = serializedObject.FindProperty("enableSS");
            enableEX = serializedObject.FindProperty("enableEX");
            enableAPLogs = serializedObject.FindProperty("enableAPLogs");
            enableAPFeature = serializedObject.FindProperty("enableAPFeature");
            persistToPlayerPrefs = serializedObject.FindProperty("persistToPlayerPrefs");

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            enableCpTraceLogs = serializedObject.FindProperty("enableCpTraceLogs");
            enableAnimTraceLogs = serializedObject.FindProperty("enableAnimTraceLogs");
            enableLocomotionTraceLogs = serializedObject.FindProperty("enableLocomotionTraceLogs");
            enableFlowTraceLogs = serializedObject.FindProperty("enableFlowTraceLogs");
#endif
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            showChannels = EditorGUILayout.Foldout(showChannels, "Enable Channels", true);
            if (showChannels)
            {
                EditorGUILayout.PropertyField(enableEG);
                EditorGUILayout.PropertyField(enableMS);
                EditorGUILayout.PropertyField(enableRTO);
                EditorGUILayout.PropertyField(enableSS);
                EditorGUILayout.PropertyField(enableEX);
                EditorGUILayout.PropertyField(enableAPLogs);
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            showDiagnostics = EditorGUILayout.Foldout(showDiagnostics, "Diagnostics Logs (Dev Only)", true);
            if (showDiagnostics)
            {
                EditorGUILayout.PropertyField(enableCpTraceLogs);
                EditorGUILayout.PropertyField(enableAnimTraceLogs);
                EditorGUILayout.PropertyField(enableLocomotionTraceLogs);
                EditorGUILayout.PropertyField(enableFlowTraceLogs);
            }
#endif

            showExperimental = EditorGUILayout.Foldout(showExperimental, "Experimental Feature Flags", true);
            if (showExperimental)
            {
                EditorGUILayout.PropertyField(enableAPFeature);
            }

            showPersistence = EditorGUILayout.Foldout(showPersistence, "Persistence (PlayerPrefs)", true);
            if (showPersistence)
            {
                EditorGUILayout.PropertyField(persistToPlayerPrefs);
            }

            serializedObject.ApplyModifiedProperties();

            if (GUILayout.Button("Apply Debug Toggles"))
            {
                ((BattleDebugTogglesBehaviour)target).Apply();
            }
        }
    }
}
