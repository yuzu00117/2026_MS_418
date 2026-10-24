using System;
using Aura.Inspector.Editor;
using UnityEditor;
using UnityEngine;

namespace Aura.ObjectInfoDemo.Editor
{
    /// @brief WaveEmitter の Inspector。ふつうの Inspector と同じ順に全項目を出すが、
    /// ・古い形の「出せるビーム」（Sources と Source Index）は、一覧に入っているときだけ、新しい形にする案内と一緒に出す
    ///   そのときは、新しい形の欄（ビームの情報、入力で波の種類を切り替える）を隠す
    /// ・ビームだけの欄（判定の半径、跳ね返り、点と線）は、「撃ち方」（Fire Shape）が「ビーム（点と線）」のときだけ出す
    /// ・扇だけの欄（扇の届く距離、扇の半角）は、「撃ち方」が「扇（近距離）」のときだけ出す
    /// ・離した後に戻るまでの時間（Bend Hold Seconds、Bend Return Seconds）は、Bend Release Mode が Hold Then Return のときだけ出す
    /// ・点のレイヤー（Point Layer）は、レイヤーを選ぶ欄で出す
    [CustomEditor(typeof(WaveEmitter))]
    [CanEditMultipleObjects]
    public sealed class WaveEmitterEditor : UnityEditor.Editor
    {
        private const string ScriptPropertyPath = "m_Script";
        private const string SourcesPropertyPath = "_sources";
        private const string FireShapePropertyPath = "_fireShape";
        private const string SourceListMessage =
            "古い形（「出せるビーム」の一覧）で動いている。\n" +
            "新しい形にするには、撃ち手の本体に BeamSourceInfo を 1 つ付けて、この一覧を空にする（「ビームの情報」は空なら自動で探す）。";

        /// @brief 古い形の「出せるビーム」に入っているときだけ出す項目
        private static readonly string[] SourceListPropertyPaths =
        {
            "_sources",
            "_sourceIndex",
        };

        /// @brief 古い形の「出せるビーム」が空のときだけ出す項目（新しい形の欄）
        private static readonly string[] SingleSourcePropertyPaths =
        {
            "_source",
            "_canSwitchWaveKindByInput",
        };
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
            // 複数を選んで一覧の数が違うときは、どちらの形の物もあるかもしれないので、両方の欄を出す
            SerializedProperty sources = serializedObject.FindProperty(SourcesPropertyPath);
            bool hasSourceList = sources.hasMultipleDifferentValues || sources.arraySize > 0;
            bool hasSingleSource = sources.hasMultipleDifferentValues || sources.arraySize == 0;

            SerializedProperty property = serializedObject.GetIterator();
            bool isEnteringChildren = true;
            while (property.NextVisible(isEnteringChildren))
            {
                isEnteringChildren = false;
                if (!IsPropertyVisible(property.propertyPath, isBeam, isFan, isHoldThenReturn)
                    || !IsSourcePropertyVisible(property.propertyPath, hasSourceList, hasSingleSource))
                {
                    continue;
                }
                if (property.propertyPath == SourcesPropertyPath)
                {
                    EditorGUILayout.HelpBox(SourceListMessage, MessageType.Info);
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

        /// @brief ビームの情報まわりの項目を Inspector に出すか（古い形と新しい形の出し分け）
        /// @param propertyPath 項目の SerializedProperty.propertyPath
        /// @param hasSourceList 古い形の「出せるビーム」に入っている物があるか
        /// @param hasSingleSource 古い形の「出せるビーム」が空の物（新しい形の物）があるか
        /// @return 出すなら true。ビームの情報まわりでない項目は、いつも true
        public static bool IsSourcePropertyVisible(string propertyPath, bool hasSourceList, bool hasSingleSource)
        {
            if (!hasSourceList && Array.IndexOf(SourceListPropertyPaths, propertyPath) >= 0)
            {
                return false;
            }
            return hasSingleSource || Array.IndexOf(SingleSourcePropertyPaths, propertyPath) < 0;
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
