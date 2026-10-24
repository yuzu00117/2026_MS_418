using System;
using Aura.Inspector.Editor;
using UnityEditor;
using UnityEngine;

namespace Aura.ObjectInfoDemo.Editor
{
    /// @brief WaveEmitter の Inspector。ふつうの Inspector と同じ順に全項目を出すが、
    /// ・ビームだけの欄（判定の半径、跳ね返り、点と線）は、「撃ち方」（Fire Shape）が「ビーム（点と線）」のときだけ出す
    /// ・扇だけの欄（扇の届く距離、扇の半角）は、「撃ち方」が「扇（近距離）」のときだけ出す
    /// ・離した後に戻るまでの時間（Bend Hold Seconds、Bend Return Seconds）は、Bend Release Mode が Hold Then Return のときだけ出す
    /// ・点のレイヤー（Point Layer）は、レイヤーを選ぶ欄で出す
    [CustomEditor(typeof(WaveEmitter))]
    [CanEditMultipleObjects]
    public sealed class WaveEmitterEditor : UnityEditor.Editor
    {
        private const string ScriptPropertyPath = "m_Script";
        private const string FireShapePropertyPath = "_fireShape";
        private const string BendReleaseModePropertyPath = "_bendReleaseMode";
        private const string PointLayerPropertyPath = "_pointLayer";

        /// @brief 「撃ち方」が「ビーム（点と線）」のときだけ出す項目
        private static readonly string[] BeamPropertyPaths =
        {
            "_radius",
            "_maxReflections",
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

        /// @brief 「撃ち方」が「扇（近距離）」のときだけ出す項目
        private static readonly string[] FanPropertyPaths =
        {
            "_fanRange",
            "_fanHalfAngle",
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
            bool isBeam = HasEnumValue(serializedObject, FireShapePropertyPath, (int)WaveFireShape.Beam);
            bool isFan = HasEnumValue(serializedObject, FireShapePropertyPath, (int)WaveFireShape.Fan);
            bool isHoldThenReturn = HasEnumValue(serializedObject, BendReleaseModePropertyPath, (int)BendReleaseMode.HoldThenReturn);

            SerializedProperty property = serializedObject.GetIterator();
            bool isEnteringChildren = true;
            while (property.NextVisible(isEnteringChildren))
            {
                isEnteringChildren = false;
                if (!IsPropertyVisible(property.propertyPath, isBeam, isFan, isHoldThenReturn))
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
        /// @param serializedObject WaveEmitter の SerializedObject
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
        /// @param isBeam 「撃ち方」が「ビーム（点と線）」か
        /// @param isFan 「撃ち方」が「扇（近距離）」か
        /// @param isHoldThenReturn Bend Release Mode が Hold Then Return か
        /// @return 出すなら true
        public static bool IsPropertyVisible(string propertyPath, bool isBeam, bool isFan, bool isHoldThenReturn)
        {
            if (!isBeam && Array.IndexOf(BeamPropertyPaths, propertyPath) >= 0)
            {
                return false;
            }
            if (!isFan && Array.IndexOf(FanPropertyPaths, propertyPath) >= 0)
            {
                return false;
            }
            return isHoldThenReturn || Array.IndexOf(HoldThenReturnPropertyPaths, propertyPath) < 0;
        }
    }
}
