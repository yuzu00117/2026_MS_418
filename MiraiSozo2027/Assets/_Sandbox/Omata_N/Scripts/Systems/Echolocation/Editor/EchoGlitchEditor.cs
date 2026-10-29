using UnityEditor;
namespace Echo.Echolocation.Editor
{
    [CustomEditor(typeof(EchoGlitch)), CanEditMultipleObjects]
    public sealed class EchoGlitchEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI() => EditorGUILayout.HelpBox(
            "この旧コンポーネントは描画に使用しません。エコーの管理元の「タグと表示設定」で、専用タグごとに「輪郭のグリッチを表示」を設定してください。以前の数値は移行用に保持されています。", MessageType.Info);
    }
}
