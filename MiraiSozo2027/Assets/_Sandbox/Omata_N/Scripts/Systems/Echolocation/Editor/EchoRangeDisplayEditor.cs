// 旧範囲表示コンポーネントのInspector。新方式はControllerの「範囲表示」で設定する。
using UnityEditor;

namespace Echo.Echolocation.Editor
{
    [CustomEditor(typeof(EchoRangeDisplay)), CanEditMultipleObjects]
    public sealed class EchoRangeDisplayEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI() => EchoComponentMigration.DrawLegacyNotice(
            "旧方式の互換コンポーネントです。範囲表示はEchoControllerの「範囲表示」で設定します。再生中はこのコンポーネントの値をControllerへ引き継ぎます。");
    }
}
