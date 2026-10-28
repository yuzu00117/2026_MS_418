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
            var property = serializedObject.FindProperty(path);
            EditorGUI.BeginChangeCheck();
            int selectedIndex = EditorGUILayout.Popup(label, property.enumValueIndex, choices);
            if (EditorGUI.EndChangeCheck())
                property.enumValueIndex = selectedIndex;
        }

        private static string GetStateLabel(EchoState state) => state switch
        {
            EchoState.Hidden => "非表示", EchoState.Revealing => "表示を展開中",
            EchoState.Active => "表示中", EchoState.Hiding => "消去中", _ => "不明"
        };
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
            DrawEnum("_settings._scanMode", "スキャン方式", new[] { "プレイヤーに追従", "ソナー（定期再展開）", "一回だけ" });
            DrawRangeDisplay();
            bool isRangeVisible = serializedObject.FindProperty("_rangeDisplay._isEnabled").boolValue;
            int scanMode = serializedObject.FindProperty("_settings._scanMode").enumValueIndex;
            if (scanMode == (int)EchoScanMode.Once)
            {
                DrawField("_settings._onceStockCapacity", "連続で打てる回数（ストック）");
                DrawField("_settings._onceRechargeSeconds", "ストック1回復までの時間（秒）");
                EditorGUILayout.HelpBox("ON・切り替えのたびに、発信した位置・向きから帯状の波を1つ伝播します。ストックがある間は連続で打て、打つたびに1減り、指定した秒数ごとに1回復します。当たるのは先端から「波の帯幅」までの帯の中だけで、帯が通り過ぎた場所は当たっていない扱いになります。全部の波の帯が最大半径を抜けきるまでが起動中で、その後は自動でOFFに戻ります。停止時の消し方が「手前から順番」なら、OFFにしても伝播中の波は最後まで進みます。", MessageType.Info);
                if (serializedObject.FindProperty("_settings._duration").floatValue <= 0)
                    EditorGUILayout.HelpBox("伝播時間が0（即時）のため、波は一瞬で通り過ぎ、何にも当たりません。", MessageType.Warning);
            }
            else if (scanMode == (int)EchoScanMode.Sonar)
            {
                DrawField("_settings._pulseInterval", "再展開間隔 n（秒）");
                DrawField("_settings._pulseHoldDuration", "展開後の残存時間 m（秒）");
                EditorGUILayout.HelpBox("開始直後に1波、その後n秒ごとに現在地から再展開します。各波の位置・向きは固定され、伝播完了後m秒残ります。同時64波を超える設定では間隔nを補正します。", MessageType.Info);
            }
            else
            {
                DrawField("_settings._followWaveInterval", "拡張波を繰り返す間隔（秒）");
                if (serializedObject.FindProperty("_settings._followWaveInterval").floatValue > 0)
                {
                    EditorGUILayout.HelpBox("展開しきった後、指定した間隔ごとに中心から拡張波を表示します。広がる速さは伝播時間と同じで、波の帯が端を抜けきるまで描きます。「拡張波の色」「波の帯幅」で描き、検出範囲や専用タグの表示は変えません。", MessageType.Info);
                    if (!isRangeVisible)
                        EditorGUILayout.HelpBox("「スキャン範囲を表示」がOFFのため、拡張波は表示されません。", MessageType.Warning);
                    if (serializedObject.FindProperty("_settings._duration").floatValue <= 0)
                        EditorGUILayout.HelpBox("伝播時間が0（即時）のため、拡張波は一瞬で端へ届き、見えません。", MessageType.Warning);
                }
            }

            DrawEnum("_settings._offMode", "停止時の消し方", new[] { "即時", "手前から順番" });
            DrawEnum("_settings._timeMode", "時間の基準", new[] { "ゲーム時間", "実時間" });
            DrawField("_settings._isEnabledOnStart", "開始時に有効");
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("エコー地点からの遮蔽", EditorStyles.boldLabel);
            DrawField("_settings._shadow._isEnabled", "影になる部分を強調しない");
            if (serializedObject.FindProperty("_settings._shadow._isEnabled").boolValue)
            {
                DrawField("_settings._shadow._resolution", "影の解像度（1方向）");
                DrawField("_settings._shadow._bias", "表面の誤判定を防ぐ距離（m）");
                EditorGUILayout.HelpBox("専用タグのない物体は遮断します。タグがある場合はタグアセットの遮断設定だけを使用します。除外ルートは対象外です。", MessageType.Info);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("対象と表示", EditorStyles.boldLabel);
            DrawField("_settings._targetLayers", "対象レイヤー");
            DrawField("_excludedRoot", "除外するルート");
            DrawField("_settings._isAutoDiscoveryEnabled", "対象を自動収集");
            EditorGUILayout.HelpBox("表示と遮断の設定は専用タグのアセットで編集します。対象の専用タグ欄からアセットを選択してください。従来の管理元の一覧とUnityタグは描画判定に使用しません。", MessageType.Info);

            EditorGUILayout.Space();
            DrawField("_isGizmoVisible", "範囲をシーン画面に表示");
            DrawField("_onStateChanged", "状態変更時の通知");
            serializedObject.ApplyModifiedProperties();
            if (GUILayout.Button("セットアップを検証"))
                foreach (var selected in targets)
                    EchoSetupValidator.Validate((EchoController)selected);
            DrawRuntimeStatus();
        }

        // スキャン方式と見た目を続けて調整できるよう、方式の直下に置く。
        void DrawRangeDisplay()
        {
            DrawField("_rangeDisplay._isEnabled", "スキャン範囲を表示");
            if (serializedObject.FindProperty("_rangeDisplay._isEnabled").boolValue)
            {
                EditorGUI.indentLevel++;
                DrawField("_rangeDisplay._isSurfaceVisible", "床・壁に範囲を表示");
                DrawField("_rangeDisplay._isBoundaryVisible", "空間の境界線を表示");
                DrawField("_rangeDisplay._fillColor", "範囲内の色");
                DrawField("_rangeDisplay._waveColor", "拡張波の色");
                DrawField("_rangeDisplay._eraseColor", "消去波の色");
                DrawField("_rangeDisplay._boundaryColor", "境界線の色");
                DrawField("_rangeDisplay._waveWidth", "波の帯幅（m）");
                EditorGUI.indentLevel--;
            }
            bool hasRemovableLegacy = false, hasPrefabLegacy = false;
            foreach (var selected in targets)
            {
                var controller = (EchoController)selected;
                foreach (Component legacy in new Component[] { controller.GetComponent<EchoRangeDisplay>(), controller.GetComponent<EchoRevealWorld>() })
                    if (legacy)
                    {
                        if (EchoComponentMigration.IsRemovable(legacy)) hasRemovableLegacy = true;
                        else hasPrefabLegacy = true;
                    }
            }
            if (hasRemovableLegacy)
                EchoComponentMigration.DrawLegacyNotice("旧EchoRangeDisplayまたはEchoRevealWorldが残っています。再生中は旧EchoRangeDisplayの値が優先されます。");
            else if (hasPrefabLegacy)
                EditorGUILayout.HelpBox("共有Prefab側に旧EchoRangeDisplayがあります。再生中はその値を範囲表示に引き継ぎます。外す場合はPrefabの担当者と相談してください。", MessageType.Info);
        }

        void DrawRuntimeStatus()
        {
            EditorGUILayout.LabelField("動作確認", EditorStyles.boldLabel);
            var controller = (EchoController)target;
            if (Application.isPlaying && controller.Settings.Shadow.IsEnabled)
            {
                var shadow = controller.ShadowMaps;
                EditorGUILayout.HelpBox($"影の発生地点：{shadow.OriginCount} ／ 遮断描画対象：{shadow.BlockerCount}\n影の描画回数：{shadow.DrawCalls} ／ 今回描き直した面：{shadow.UpdatedFaceCount} ／ CPU準備：{shadow.CpuMilliseconds:F2} ms", MessageType.Info);
            }

            if (controller.Settings.ScanMode == EchoScanMode.Once)
                EditorGUILayout.HelpBox($"ストック：{controller.OnceStock} / {controller.Settings.OnceStockCapacity} ／ 次の回復：{controller.OnceRechargeProgress * 100:F0}%", MessageType.Info);
            EditorGUILayout.HelpBox($"状態：{GetStateLabel(controller.CurrentState)} ／ スキャン要求：{(controller.IsScanRequested ? "有効" : "無効")}\n最新波の外側：{controller.CurrentOuterRadius:F2} m ／ 消去：{controller.CurrentEraseRadius:F2} m\n生存領域：{controller.Frames.Count} ／ 登録：{controller.RegisteredCount} ／ 候補：{controller.RenderItems.Count}", MessageType.Info);
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("開始"))
                    controller.SetScanEnabled(true);
                if (GUILayout.Button("停止"))
                    controller.SetScanEnabled(false);
                if (GUILayout.Button("切り替え"))
                    controller.ToggleScan();
                EditorGUILayout.EndHorizontal();
                if (GUILayout.Button("対象を再収集"))
                    controller.RefreshTargets();
            }

            if (Application.isPlaying)
                Repaint();
        }
    }
}
