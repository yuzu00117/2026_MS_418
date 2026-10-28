using UnityEditor;
namespace Echo.Echolocation.Editor
{
    [CustomEditor(typeof(EchoRevealWorld)), CanEditMultipleObjects]
    public sealed class EchoRevealWorldEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI() => EchoComponentMigration.DrawLegacyNotice(
            "旧方式の互換コンポーネントで、現在は何もしません。接触部分表示は専用タグの表示範囲で選択します。");
    }
}
