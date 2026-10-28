using UnityEditor;
namespace Echo.Echolocation.Editor
{
    [CustomEditor(typeof(EchoRevealWorld)), CanEditMultipleObjects]
    public sealed class EchoRevealWorldEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI() => EditorGUILayout.HelpBox(
            "旧方式の互換コンポーネントです。新方式はControllerの表示範囲で接触部分表示を選択します。ベイク・読み戻し・Collider制御は不要です。輪郭のグリッチは管理元のタグと表示設定で調整してください。旧状態APIは非稼働を返します。",
            MessageType.Info);
    }
}
