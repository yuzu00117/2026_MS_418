using UnityEditor;
using UnityEngine;

namespace MS.Toon.Editor
{
    /// <summary>
    /// ToonStyleApplier のインスペクタ。先頭に使い方を出してから標準の項目を描く。
    /// 複数選択での一括変更にも対応し、同じ設定を複数のシーンオブジェクトへ揃えやすくする。
    /// </summary>
    [CustomEditor(typeof(ToonStyleApplier)), CanEditMultipleObjects]
    public class ToonStyleApplierEditor : UnityEditor.Editor
    {
        // 説明文はこのクラスの表示専用。文言を直すときはここだけ変えれば済むように定数にする
        private const string HelpText = "シーンに1つだけ置きます。Profile に入れたものが現在のスタイルになります。Profiles は切り替え用の候補で、コンポーネントの右上メニュー(⋮)の Cycle Profile で順に切り替えられます。このコンポーネントを無効にすると、各マテリアル自身の値に戻ります。";

        private const string DuplicateText = "シーンに ToonStyleApplier が複数あります。このコンポーネントは無視されます(有効なのは別のオブジェクトのものです)。不要なものを削除してください。";

        public override void OnInspectorGUI()
        {
            // 複数選択時は1つでも重複があれば警告する(どれが無視されるかは対象ごとに異なるため)
            foreach (Object t in targets)
            {
                ToonStyleApplier applier = t as ToonStyleApplier;
                if (applier != null && applier.IsDuplicate)
                {
                    EditorGUILayout.HelpBox(DuplicateText, MessageType.Error);
                    break;
                }
            }

            // 説明は標準項目より上に出す。DrawDefaultInspector は Header/Tooltip など既存の表示をそのまま残すため、自前で描画しない
            EditorGUILayout.HelpBox(HelpText, MessageType.Info);
            DrawDefaultInspector();
        }
    }
}
