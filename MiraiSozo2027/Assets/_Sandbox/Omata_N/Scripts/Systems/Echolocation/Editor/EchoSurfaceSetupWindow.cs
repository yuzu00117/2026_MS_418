using UnityEditor;
using UnityEngine;
namespace Echo.Echolocation.Editor
{
    /// <summary>部分表示対象のInspectorへ案内し、設定目的を説明する。</summary>
    public sealed class EchoSurfaceSetupWindow : EditorWindow
    {
        private EchoRevealState _state;
        [MenuItem("Tools/エコーロケーション/04 接触部分表示と衝突を準備", false, 104)]
        public static void Open() => GetWindow<EchoSurfaceSetupWindow>("部分表示と衝突の準備");
        private void OnGUI()
        {
            EditorGUILayout.HelpBox("このツールは接触部分表示の対象を選び、専用Inspectorへ案内します。ベイクはメッシュを小さな三角形へ分割し、表示範囲と当たり判定の単位を作る操作です。", MessageType.Info);
            _state = (EchoRevealState)EditorGUILayout.ObjectField("準備する対象", _state, typeof(EchoRevealState), true);
            EditorGUILayout.HelpBox(@"① タグ付与ツールで表示状態の管理元を指定
② Controllerの表示ルールで「接触部分表示」を選択
③ 対象のInspectorで「所有Rendererを分割ベイク」
④ 衝突も制御する場合だけEchoRevealColliderを追加し、元のCollider・表示部分の当たり判定モードを設定
⑤ シーンを保存

メッシュやスケールを変えたら再ベイクします。全体表示ではベイク不要です。", MessageType.None);
            using (new EditorGUI.DisabledScope(!_state))
                if (GUILayout.Button("対象の設定とベイク操作をInspectorで開く")) Selection.activeObject = _state;
        }
    }
}
