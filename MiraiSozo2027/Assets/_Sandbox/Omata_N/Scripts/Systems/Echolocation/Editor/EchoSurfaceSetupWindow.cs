using UnityEditor;
namespace Echo.Echolocation.Editor
{
    public sealed class EchoSurfaceSetupWindow : EditorWindow
    {
        [MenuItem("Tools/エコーロケーション/04 接触部分表示とグリッチ", false, 104)]
        public static void Open() => GetWindow<EchoSurfaceSetupWindow>("部分表示とグリッチ");
        private void OnGUI() => EditorGUILayout.HelpBox(
            "① Controllerと対象に同じ専用タグを設定\n② 元マテリアル表示を有効化し接触部分表示を選択\n③ タグの輪郭グリッチを有効にし「表示されている間」を選択\n④ 通常の輪郭線はOFFのまま幅・色・強度・速度を調整\n\n全体表示＋表示開始時のみなら表示開始時だけ全体の輪郭が乱れます。「常時」なら常に。Colliderとベイクデータは不要です。",
            MessageType.Info);
    }
}
