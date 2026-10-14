// 検出ロジックとは独立した範囲表示の日本語Inspector。
using UnityEditor;
using UnityEngine;

namespace Echo.Echolocation.Editor
{
    [CustomEditor(typeof(EchoRangeDisplay)), CanEditMultipleObjects]
    public sealed class EchoRangeDisplayEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            void Field(string path, string label) => EditorGUILayout.PropertyField(serializedObject.FindProperty(path), new GUIContent(label));
            EditorGUILayout.HelpBox("床・壁への薄い色と波の先端、空間の境界線で範囲を示します。タグ色の強調表示とは独立しています。", MessageType.Info);
            Field("_isSurfaceVisible", "床・壁に範囲を表示");
            Field("_isBoundaryVisible", "空間の境界線を表示");
            Field("_fillColor", "範囲内の色");
            Field("_waveColor", "拡張波の色");
            Field("_eraseColor", "消去波の色");
            Field("_boundaryColor", "境界線の色");
            Field("_waveWidth", "波の帯幅（m）");
            serializedObject.ApplyModifiedProperties();
        }
    }
}
