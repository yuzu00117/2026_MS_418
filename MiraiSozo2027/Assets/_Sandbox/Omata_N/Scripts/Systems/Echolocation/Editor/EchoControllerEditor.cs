// 単位と日本語説明を持つInspector。設定のシリアライズはUnity標準のUndoに任せる。
using UnityEditor;
using UnityEngine;

namespace Echo.Echolocation.Editor
{
    [CustomEditor(typeof(EchoController)), CanEditMultipleObjects]
    public sealed class EchoControllerEditor : UnityEditor.Editor
    {
        void DrawField(string path, string label) => EditorGUILayout.PropertyField(serializedObject.FindProperty(path), new GUIContent(label), true);
        void DrawEnum(string path, string label, string[] choices)
        {
            var p = serializedObject.FindProperty(path);
            EditorGUI.BeginChangeCheck();
            int v = EditorGUILayout.Popup(label, p.enumValueIndex, choices);
            if (EditorGUI.EndChangeCheck())
                p.enumValueIndex = v;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.LabelField("基本", EditorStyles.boldLabel);
            DrawField("_originTransform", "スキャン中心");
            DrawField("_directionTransform", "正面の基準");
            DrawField("_targetCamera", "描画カメラ");
            DrawField("_settings._radius", "最大半径（m）");
            DrawField("_settings._angleDegrees", "全開き角（度）");
            DrawField("_settings._duration", "伝播時間（秒）");
            DrawEnum("_settings._scanMode", "スキャン方式", new[] { "プレイヤーに追従", "ソナー（定期再展開）" });
            if (serializedObject.FindProperty("_settings._scanMode").enumValueIndex == 1)
            {
                DrawField("_settings._pulseInterval", "再展開間隔 n（秒）");
                DrawField("_settings._pulseHoldDuration", "展開後の残存時間 m（秒）");
                EditorGUILayout.HelpBox("ON直後に1波、その後n秒ごとに現在地から再展開します。各波の位置・向きは固定され、伝播完了後m秒残ります。同時64波を超える設定では間隔nを補正します。", MessageType.Info);
            }

            DrawEnum("_settings._offMode", "OFF時の消し方", new[] { "即時", "手前から順番" });
            DrawEnum("_settings._timeMode", "時間の基準", new[] { "ゲーム時間", "実時間" });
            DrawField("_settings._isEnabledOnStart", "開始時にON");
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("エコー地点からの遮蔽", EditorStyles.boldLabel);
            DrawField("_settings._shadow._isEnabled", "影になる部分を強調しない");
            if (serializedObject.FindProperty("_settings._shadow._isEnabled").boolValue)
            {
                DrawField("_settings._shadow._layers", "遮断物のレイヤー");
                DrawField("_settings._shadow._isBlockingByDefault", "未登録タグは遮断する");
                DrawField("_settings._shadow._rules", "タグと遮断設定");
                DrawField("_settings._shadow._resolution", "影の解像度（1方向）");
                DrawField("_settings._shadow._bias", "表面の誤判定を防ぐ距離（m）");
                EditorGUILayout.HelpBox("各エコーの発生地点から6方向の影を計算します。遮断しないタグは距離を減らさず通過し、その先の遮断物で止まります。Renderer自身のタグを使用します。遮断する面自体は強調できます。除外ルート配下は遮断しません。", MessageType.Info);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("対象と色", EditorStyles.boldLabel);
            DrawField("_settings._targetLayers", "対象レイヤー");
            DrawField("_excludedRoot", "除外するルート");
            DrawField("_settings._isAutoDiscoveryEnabled", "対象を自動収集");
            DrawField("_settings._tagColorRules", "タグと表示色");
            DrawEnum("_settings._occlusionMode", "壁越し表示", new[] { "表示する", "深度で隠す" });
            DrawField("_isGizmoVisible", "範囲をScene Viewに表示");
            DrawField("_onStateChanged", "状態変更時の通知");
            serializedObject.ApplyModifiedProperties();
            if (GUILayout.Button("セットアップを検証"))
                foreach (var t in targets)
                    EchoSetupValidator.Validate((EchoController)t);
            EditorGUILayout.LabelField("動作確認", EditorStyles.boldLabel);
            var c = (EchoController)target;
            if (Application.isPlaying && c.Settings.Shadow.IsEnabled)
            {
                var shadow = c.ShadowMaps;
                EditorGUILayout.HelpBox($"影の発生地点：{shadow.OriginCount} ／ 遮断Renderer：{shadow.BlockerCount}\n影のDraw：{shadow.DrawCalls} ／ CPU準備：{shadow.CpuMilliseconds:F2} ms", MessageType.Info);
            }

            EditorGUILayout.HelpBox($"状態：{c.CurrentState} ／ ON要求：{c.IsScanRequested}\n最新波の外側：{c.CurrentOuterRadius:F2} m ／ 消去：{c.CurrentEraseRadius:F2} m\n生存領域：{c.Frames.Count} ／ 登録：{c.RegisteredCount} ／ 候補：{c.RenderItems.Count}", MessageType.Info);
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("ON"))
                    c.SetScanEnabled(true);
                if (GUILayout.Button("OFF"))
                    c.SetScanEnabled(false);
                if (GUILayout.Button("切り替え"))
                    c.ToggleScan();
                EditorGUILayout.EndHorizontal();
                if (GUILayout.Button("対象を再収集"))
                    c.RefreshTargets();
            }

            if (Application.isPlaying)
                Repaint();
        }
    }
}
