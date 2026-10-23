using UnityEditor;
using UnityEngine;

namespace MS.Toon.Editor
{
    /// <summary>
    /// ToonStyleProfile のインスペクタ。先頭に使い方を出してから標準の項目を描く。
    /// Profile は複製して使い分ける資産なので、何のための設定かを開いた時点で分かるようにする。
    /// </summary>
    [CustomEditor(typeof(ToonStyleProfile))]
    public class ToonStyleProfileEditor : UnityEditor.Editor
    {
        // 説明文はこのクラスの表示専用。文言を直すときはここだけ変えれば済むように定数にする
        private const string HelpText = "ゲーム全体のトゥーン調の絵作りの設定です。シーンの ToonStyleApplier に設定すると、Use Global Style がオンの MS/Toon マテリアル全部に反映されます。複製して値を変えると別のスタイルを作れます。Ramp が未設定のときは、各マテリアルのランプを使います。";

        public override void OnInspectorGUI()
        {
            // 説明は標準項目より上に出す。DrawDefaultInspector は Header/Tooltip など既存の表示をそのまま残すため、自前で描画しない
            EditorGUILayout.HelpBox(HelpText, MessageType.Info);
            DrawDefaultInspector();
        }
    }
}
