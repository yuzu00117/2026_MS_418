using Aura.Wave;
using UnityEditor;
using UnityEngine;

namespace Aura.ObjectInfo.Editor
{
    /// @brief [ShowWaveTypeAsset] を付けた欄を描く。ふつうの欄（選択肢やチェックボックス）の横に、
    /// 実際に使われる WaveType のアセットを、変えられない欄で薄く出す（クリックすると Project ウィンドウで場所が光る）
    /// 名前（[InspectorLabel] の日本語）は、外側の描き方から渡されたものをそのまま使う
    [CustomPropertyDrawer(typeof(ShowWaveTypeAssetAttribute))]
    public sealed class ShowWaveTypeAssetDrawer : PropertyDrawer
    {
        private const float Spacing = 4.0f;            ///< 欄どうしの間（ピクセル）
        private const float ArrowWidth = 16.0f;        ///< 「→」の幅（ピクセル）
        private const float ToggleWidth = 16.0f;       ///< チェックボックスの幅（ピクセル）
        private const float PopupWidthRatio = 0.45f;   ///< 選択肢の欄に使う割合。残りをアセットの欄に使う
        private const float MinAssetWidth = 60.0f;     ///< アセットの欄がこれより狭くなるときは、アセットを出さない（ピクセル）
        private const string ArrowTooltip =
            "右は、実際に使われる波の種類のアセット（SO_WaveTypeInfo の枠から引いたもの）。ここでは変えられない\n" +
            "クリックすると、Project ウィンドウでアセットの場所が光る。色などを変えるときは、そのアセットを直す";
        private const string MissingInfoText = "（SO_WaveTypeInfo が無い）";

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            label = EditorGUI.BeginProperty(position, label, property);
            Rect fieldRect = EditorGUI.PrefixLabel(position, label);
            // 名前の後ろの欄は、字下げを 0 にして描く（配列の中でも、欄が右へずれないようにする）
            int indentLevel = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            bool isBool = property.propertyType == SerializedPropertyType.Boolean;
            float valueWidth = isBool ? ToggleWidth : fieldRect.width * PopupWidthRatio;
            var valueRect = new Rect(fieldRect.x, fieldRect.y, valueWidth, EditorGUIUtility.singleLineHeight);
            // 名前を付けずに、ふつうの欄（選択肢・チェックボックス）を描く
            EditorGUI.PropertyField(valueRect, property, GUIContent.none);

            var arrowRect = new Rect(valueRect.xMax + Spacing, fieldRect.y, ArrowWidth, EditorGUIUtility.singleLineHeight);
            float assetX = arrowRect.xMax + Spacing;
            var assetRect = new Rect(assetX, fieldRect.y, fieldRect.xMax - assetX, EditorGUIUtility.singleLineHeight);
            if (assetRect.width >= MinAssetWidth && TryGetKind(property, out WaveKind kind))
            {
                DrawAsset(arrowRect, assetRect, kind);
            }

            EditorGUI.indentLevel = indentLevel;
            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        /// @brief 出すアセットの種類を決める
        /// @return 決まれば true。複数を選んで種類が違うときは false（どれか 1 つに決まらないので出さない）
        private bool TryGetKind(SerializedProperty property, out WaveKind outKind)
        {
            var showAttribute = (ShowWaveTypeAssetAttribute)attribute;
            if (showAttribute.HasFixedKind)
            {
                outKind = showAttribute.Kind;
                return true;
            }
            outKind = WaveKind.Electromagnetic;
            if (property.propertyType != SerializedPropertyType.Enum || property.hasMultipleDifferentValues)
            {
                return false;
            }
            outKind = (WaveKind)property.intValue;
            return true;
        }

        /// @brief 「→」と、その種類のアセットを変えられない欄で描く
        private static void DrawAsset(Rect arrowRect, Rect assetRect, WaveKind kind)
        {
            EditorGUI.LabelField(arrowRect, new GUIContent("→", ArrowTooltip));
            if (WaveTypeInfo.Instance == null)
            {
                EditorGUI.LabelField(assetRect, new GUIContent(MissingInfoText, ArrowTooltip));
                return;
            }
            // 変えられない欄でも、クリックすると Project ウィンドウで場所が光る（スクリプトの欄と同じ）
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUI.ObjectField(assetRect, WaveTypeInfo.GetWaveType(kind), typeof(WaveType), false);
            }
        }
    }
}
