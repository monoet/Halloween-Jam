using BattleV2.Orchestration;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace BattleV2.Orchestration.Editor
{
    [CustomEditor(typeof(EncounterSpawnPattern))]
    public class EncounterSpawnPatternEditor : UnityEditor.Editor
    {
        private const float HandleSize = 0.75f;
        private const float SphereSize = 0.2f;
        private static readonly Color PreviewColor = new Color(0.85f, 0.93f, 1f);

        private SerializedProperty dimensionProp;
        private SerializedProperty layoutsProp;
        private ReorderableList layoutsList;
        private int previewLayoutIndex = -1;

        private EncounterSpawnPattern Pattern => (EncounterSpawnPattern)target;

        private void OnEnable()
        {
            dimensionProp = serializedObject.FindProperty("dimension");
            layoutsProp = serializedObject.FindProperty("layouts");

            layoutsList = new ReorderableList(serializedObject, layoutsProp, true, true, true, true)
            {
                drawHeaderCallback = DrawLayoutsHeader,
                drawElementCallback = DrawLayoutElement,
                elementHeightCallback = LayoutElementHeight,
                onAddCallback = OnAddLayout
            };

            SceneView.duringSceneGui += DuringSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DuringSceneGUI;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawDimensionField();
            EditorGUILayout.Space();
            layoutsList.DoLayoutList();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawDimensionField()
        {
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(dimensionProp);
            if (!EditorGUI.EndChangeCheck())
            {
                return;
            }

            EncounterSpawnPattern.SpawnDimension newDimension =
                (EncounterSpawnPattern.SpawnDimension)dimensionProp.enumValueIndex;

            foreach (Object targetObject in targets)
            {
                if (targetObject is EncounterSpawnPattern pattern)
                {
                    Undo.RecordObject(pattern, "Change Spawn Dimension");
                    pattern.SetDimension(newDimension);
                    EditorUtility.SetDirty(pattern);
                }
            }

            serializedObject.Update();
        }

        private void DrawLayoutsHeader(Rect rect)
        {
            EditorGUI.LabelField(rect, "Layouts");
        }

        private void DrawLayoutElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            if (index >= layoutsProp.arraySize)
            {
                return;
            }

            SerializedProperty layoutProp = layoutsProp.GetArrayElementAtIndex(index);
            SerializedProperty sizeProp = layoutProp.FindPropertyRelative("size");
            SerializedProperty offsetsProp = layoutProp.FindPropertyRelative("offsets");

            float lineHeight = EditorGUIUtility.singleLineHeight;
            float verticalSpacing = EditorGUIUtility.standardVerticalSpacing;

            Rect sizeRect = new Rect(rect.x, rect.y, rect.width * 0.5f, lineHeight);
            Rect previewButtonRect = new Rect(rect.x + rect.width - 70f, rect.y, 70f, lineHeight);
            Rect offsetsRect = new Rect(rect.x, rect.y + lineHeight + verticalSpacing, rect.width,
                EditorGUI.GetPropertyHeight(offsetsProp, includeChildren: true));

            EditorGUI.PropertyField(sizeRect, sizeProp);

            bool isPreviewing = previewLayoutIndex == index;
            string buttonLabel = isPreviewing ? "Hide" : "Preview";
            if (GUI.Button(previewButtonRect, buttonLabel))
            {
                previewLayoutIndex = isPreviewing ? -1 : index;
                SceneView.RepaintAll();
            }

            EditorGUI.PropertyField(offsetsRect, offsetsProp, includeChildren: true);
        }

        private float LayoutElementHeight(int index)
        {
            if (index >= layoutsProp.arraySize)
            {
                return EditorGUIUtility.singleLineHeight;
            }

            SerializedProperty layoutProp = layoutsProp.GetArrayElementAtIndex(index);
            SerializedProperty offsetsProp = layoutProp.FindPropertyRelative("offsets");
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float verticalSpacing = EditorGUIUtility.standardVerticalSpacing;
            return lineHeight + verticalSpacing + EditorGUI.GetPropertyHeight(offsetsProp, includeChildren: true);
        }

        private void OnAddLayout(ReorderableList list)
        {
            layoutsProp.arraySize++;
            SerializedProperty newLayout = layoutsProp.GetArrayElementAtIndex(layoutsProp.arraySize - 1);
            newLayout.FindPropertyRelative("size").intValue = 1;
            SerializedProperty offsets = newLayout.FindPropertyRelative("offsets");
            offsets.arraySize = 1;
            offsets.GetArrayElementAtIndex(0).vector3Value = Vector3.zero;
        }

        private void DuringSceneGUI(SceneView sceneView)
        {
            if (previewLayoutIndex < 0 || previewLayoutIndex >= layoutsProp.arraySize)
            {
                return;
            }

            serializedObject.Update();

            SerializedProperty layoutProp = layoutsProp.GetArrayElementAtIndex(previewLayoutIndex);
            SerializedProperty offsetsProp = layoutProp.FindPropertyRelative("offsets");

            Handles.color = PreviewColor;
            bool changed = false;

            for (int i = 0; i < offsetsProp.arraySize; i++)
            {
                SerializedProperty offsetProp = offsetsProp.GetArrayElementAtIndex(i);
                Vector3 offsetValue = offsetProp.vector3Value;

                if (Pattern.Is2D)
                {
                    offsetValue.y = 0f;
                }

                float size = HandleUtility.GetHandleSize(offsetValue);
                Handles.SphereHandleCap(0, offsetValue, Quaternion.identity, SphereSize * size, EventType.Repaint);
                Handles.Label(offsetValue + Vector3.up * 0.2f, $"#{i}", EditorStyles.miniBoldLabel);

                EditorGUI.BeginChangeCheck();
                Vector3 newValue = Pattern.Is2D
                    ? Handles.Slider2D(offsetValue, Vector3.up, Vector3.right, Vector3.forward,
                        HandleSize * size, Handles.CircleHandleCap, 0f)
                    : Handles.PositionHandle(offsetValue, Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(Pattern, "Move Spawn Offset");
                    if (Pattern.Is2D)
                    {
                        newValue.y = 0f;
                    }
                    offsetProp.vector3Value = newValue;
                    changed = true;
                }
            }

            if (changed)
            {
                serializedObject.ApplyModifiedProperties();
                Pattern.SetDimension(Pattern.Dimension); // enforce constraints and mark dirty
                EditorUtility.SetDirty(Pattern);
            }
        }
    }
}
