using UnityEditor;
namespace Echo.Echolocation.Editor
{
    public sealed class EchoSurfaceSetupWindow : EditorWindow
    {
        [MenuItem("Tools/エコーロケーション/04 接触部分表示の準備", false, 104)]
        public static void Open() => GetWindow<EchoSurfaceSetupWindow>("接触部分表示");
        private void OnGUI() => EditorGUILayout.HelpBox(
            "① 対象のEchoTargetに専用タグを設定\n② 専用タグで元マテリアル表示を有効化\n③ 表示範囲で「接触した部分」を選択\n\n波が届いた表面だけをGPUで表示します。Colliderとベイクデータは不要です。",
            MessageType.Info);
    }
}
