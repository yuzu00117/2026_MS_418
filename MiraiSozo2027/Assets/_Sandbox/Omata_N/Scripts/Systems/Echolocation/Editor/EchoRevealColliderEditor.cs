using UnityEditor;
using UnityEngine;
namespace Echo.Echolocation.Editor
{
    [CustomEditor(typeof(EchoRevealCollider)), CanEditMultipleObjects]
    public sealed class EchoRevealColliderEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_mode"), new GUIContent("当たり判定のモード"), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_originalColliders"), new GUIContent("制御する元のコライダー"), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_occupantLayers"), new GUIContent("消去を保留する対象レイヤー"), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_patchMaterial"), new GUIContent("部分コライダーの物理マテリアル"), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_hasPenetrationGuard"), new GUIContent("重なっている間は表示と衝突を保持"), true);
            serializedObject.ApplyModifiedProperties();
        }
    }
}
