using System;
using Aura.Inspector.Editor;
using UnityEditor;
using UnityEngine;

namespace Aura.ObjectInfoDemo.Editor
{
    /// @brief InfoBeamEmitter の Inspector。ふつうの Inspector と同じ順に全項目を出すが、
    /// ・点と線の欄（Point Spacing など）は、Beam Shape が Point Chain のときだけ出す
    /// ・離した後に戻るまでの時間（Bend Hold Seconds、Bend Return Seconds）は、Bend Release Mode が Hold Then Return のときだけ出す
    /// ・点のレイヤー（Point Layer）は、レイヤーを選ぶ欄で出す
    [CustomEditor(typeof(InfoBeamEmitter))]
    [CanEditMultipleObjects]
    public sealed class InfoBeamEmitterEditor : UnityEditor.Editor
    {
        private const string ScriptPropertyPath = "m_Script";
        private const string BeamShapePropertyPath = "_beamShape";
        private const string BendReleaseModePropertyPath = "_bendReleaseMode";
        private const string PointLayerPropertyPath = "_pointLayer";

        /// @brief Beam Shape が Point Chain のときだけ出す項目
        private static readonly string[] PointChainPropertyPaths =
        {
            "_pointSpacing",
            "_bendMode",
            "_bendRange",
            "_bendReleaseMode",
            "_bendHoldSeconds",
            "_bendReturnSeconds",
            "_isAimLockedWhileHeld",
            "_pointRadius",
            "_pointLayer",
            "_pointPrefab",
            "_isBodyLineVisible",
        };

        /// @brief Bend Release Mode が Hold Then Return のときだけ出す項目
        private static readonly string[] HoldThenReturnPropertyPaths =
        {
            "_bendHoldSeconds",
            "_bendReturnSeconds",
        };

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            bool isPointChain = HasEnumValue(serializedObject, BeamShapePropertyPath, (int)BeamShape.PointChain);
            bool isHoldThenReturn = HasEnumValue(serializedObject, BendReleaseModePropertyPath, (int)BendReleaseMode.HoldThenReturn);

            SerializedProperty property = serializedObject.GetIterator();
            bool isEnteringChildren = true;
            while (property.NextVisible(isEnteringChildren))
            {
                isEnteringChildren = false;
                if (!IsPropertyVisible(property.propertyPath, isPointChain, isHoldThenReturn))
                {
                    continue;
                }
                if (property.propertyPath == PointLayerPropertyPath)
                {
                    DrawLayerField(property);
                    continue;
                }
                // スクリプトの欄は、ふつうの Inspector と同じく変えられないようにする
                using (new EditorGUI.DisabledScope(property.propertyPath == ScriptPropertyPath))
                {
                    EditorGUILayout.PropertyField(property, true);
                }
            }
            serializedObject.ApplyModifiedProperties();
        }

        /// @brief レイヤーの番号を持つ項目を、レイヤーを選ぶ欄で出す
        private static void DrawLayerField(SerializedProperty property)
        {
            // BeginProperty で囲み、プレハブの上書きの印と、右クリックの Revert などを、ふつうの欄と同じく使えるようにする
            GUIContent label = InspectorLabelUtility.GetLabel(property);
            Rect rect = EditorGUILayout.GetControlRect();
            label = EditorGUI.BeginProperty(rect, label, property);
            EditorGUI.BeginChangeCheck();
            int layer = EditorGUI.LayerField(rect, label, property.intValue);
            if (EditorGUI.EndChangeCheck())
            {
                property.intValue = layer;
            }
            EditorGUI.EndProperty();
        }

        /// @brief その選択肢の項目が、指定した値か。複数を選んで値が違うときは、その値の物があるかもしれないので true
        /// @param serializedObject InfoBeamEmitter の SerializedObject
        /// @param propertyPath 選択肢の項目の SerializedProperty.propertyPath
        /// @param value 比べる値（選択肢の番号）
        /// @return 指定した値なら true
        public static bool HasEnumValue(SerializedObject serializedObject, string propertyPath, int value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyPath);
            return property.hasMultipleDifferentValues || property.intValue == value;
        }

        /// @brief その項目を Inspector に出すか
        /// @param propertyPath 項目の SerializedProperty.propertyPath
        /// @param isPointChain Beam Shape が Point Chain か
        /// @param isHoldThenReturn Bend Release Mode が Hold Then Return か
        /// @return 出すなら true
        public static bool IsPropertyVisible(string propertyPath, bool isPointChain, bool isHoldThenReturn)
        {
            if (Array.IndexOf(PointChainPropertyPaths, propertyPath) >= 0 && !isPointChain)
            {
                return false;
            }
            return isHoldThenReturn || Array.IndexOf(HoldThenReturnPropertyPaths, propertyPath) < 0;
        }
    }
}
