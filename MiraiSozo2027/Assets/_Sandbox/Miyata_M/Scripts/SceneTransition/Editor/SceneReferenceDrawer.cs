using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SceneFlow.EditorTools
{
    /// <summary>
    /// SceneReference の Inspector 表示。シーンアセットをドラッグで指定でき、
    /// Build Settings に未登録なら警告と追加ボタンを出す。
    /// </summary>
    [CustomPropertyDrawer(typeof(SceneReference))]
    public class SceneReferenceDrawer : PropertyDrawer
    {
        private const string AssetFieldName = "_sceneAsset";
        private const float ButtonHeightRatio = 1f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight;
            float space = EditorGUIUtility.standardVerticalSpacing;
            return IsMissingFromBuildSettings(property) ? (line + space) * 3 : line;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var assetProp = property.FindPropertyRelative(AssetFieldName);
            float line = EditorGUIUtility.singleLineHeight;
            float space = EditorGUIUtility.standardVerticalSpacing;

            var fieldRect = new Rect(position.x, position.y, position.width, line);
            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.PropertyField(fieldRect, assetProp, label);
            EditorGUI.EndProperty();

            if (!IsMissingFromBuildSettings(property)) return;

            var helpRect = new Rect(position.x, fieldRect.yMax + space, position.width, line * 2);
            EditorGUI.HelpBox(helpRect, "Build Settings に登録されていません。このままでは遷移できません。", MessageType.Warning);

            var buttonRect = new Rect(position.x, helpRect.yMax + space, position.width, line * ButtonHeightRatio);
            if (GUI.Button(buttonRect, "Build Settings に追加"))
            {
                AddToBuildSettings(AssetDatabase.GetAssetPath(assetProp.objectReferenceValue));
            }
        }

        private static bool IsMissingFromBuildSettings(SerializedProperty property)
        {
            var asset = property.FindPropertyRelative(AssetFieldName).objectReferenceValue;
            if (asset == null) return false;
            string path = AssetDatabase.GetAssetPath(asset);
            return !EditorBuildSettings.scenes.Any(s => s.path == path);
        }

        private static void AddToBuildSettings(string path)
        {
            var list = EditorBuildSettings.scenes.ToList();
            list.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
