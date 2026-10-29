using UnityEditor;
namespace Echo.Echolocation.Editor
{
    [CustomEditor(typeof(EchoRevealCollider)), CanEditMultipleObjects]
    public sealed class EchoRevealColliderEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI() => EditorGUILayout.HelpBox(
            "旧方式の互換コンポーネントです。新方式はControllerの表示範囲で接触部分表示を選択します。ベイク・読み戻し・Collider制御は不要です。旧状態APIは非稼働を返します。",
            MessageType.Info);
    }
}
