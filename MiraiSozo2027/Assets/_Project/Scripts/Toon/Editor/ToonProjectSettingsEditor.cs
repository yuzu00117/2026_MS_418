using UnityEditor;

namespace MS.Toon.Editor
{
    /// <summary>
    /// ToonProjectSettings のインスペクタ。使い方と、Preloaded Assets への登録状態を出す。
    /// 登録し忘れると共通設定が効かないため、開いた時点で気付けるようにする。
    /// </summary>
    [CustomEditor(typeof(ToonProjectSettings))]
    public class ToonProjectSettingsEditor : UnityEditor.Editor
    {
        private const string HelpText =
            "プロジェクト全体の既定の見た目を決めます。起動時に自動で読み込まれます。\n" +
            "・Default Profile:既定で使う設定ファイル\n" +
            "・シーンに ToonStyleApplier があれば、そのシーンだけそちらが優先されます";

        private const string WarnNotRegistered = "Preloaded Assets に登録されていないため、この設定は使われていません。";
        private const string WarnOtherRegistered = "別の ToonProjectSettings が登録されています。この設定は使われていません。";
        private const string RegisterButton = "Preloaded Assets に登録";

        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(HelpText, MessageType.Info);

            var settings = (ToonProjectSettings)target;
            ToonProjectSettings registered = ToonProjectSettingsLoader.FindRegistered();
            if (registered != settings)
            {
                EditorGUILayout.HelpBox(registered == null ? WarnNotRegistered : WarnOtherRegistered, MessageType.Warning);
                if (UnityEngine.GUILayout.Button(RegisterButton))
                {
                    ToonProjectSettingsLoader.RegisterToPreloaded(settings);
                }
            }

            DrawDefaultInspector();
        }
    }
}
