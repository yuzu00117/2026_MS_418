using System;
using UnityEditor;
using UnityEngine;
namespace Echo.Echolocation.Editor
{
    [CustomEditor(typeof(EchoTagDefinition)), CanEditMultipleObjects]
    public sealed class EchoTagDefinitionEditor : UnityEditor.Editor
    {
        private const string SettingsPath = "_settings";
        private const string IrradiatedPath = "_settings._irradiated";
        private const string MaterializationPath = "_settings._materialization";
        private const string HiddenPath = SettingsPath + "._isNormalRenderingHidden";
        private const string FillName = "_fill";
        private const string OutlineName = "_outline";
        private const string OriginalName = "_original";
        // 照射中と実体化中で見出しをそろえ、同じ番号が同じ表示の設定だと分かるようにする。
        private const string FillTitle = "① 塗りつぶしによる強調表示";
        private const string OutlineTitle = "② アウトラインによる強調表示";
        private const string OriginalTitle = "③ 元のマテリアルによる表示";
        private const string CopyAllLabel = "実体化しているときの表示をコピー";
        private const string CopyFillLabel = "実体化しているときの①をコピー";
        private const string CopyOutlineLabel = "実体化しているときの②をコピー";
        private const string CopyOriginalLabel = "実体化しているときの③をコピー";

        private void DrawField(string path, string label) => EditorGUILayout.PropertyField(serializedObject.FindProperty(path), new GUIContent(label), true);
        private bool GetBool(string path) => serializedObject.FindProperty(path).boolValue;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            DrawField("_displayName", "表示名"); DrawField("_description", "説明");
            EditorGUILayout.Space();
            DrawField("_isBlocking", "エコーロケーションを遮断する");
            DrawField(SettingsPath + "._contactResolution", "全体接触判定の解像度");
            DrawField(SettingsPath + "._surfaceMemoryResolution", "接触部分の残存精度");
            EditorGUILayout.HelpBox("残存精度は対象を立体的に分割する記録解像度です。高いほど境界が細かくなり、記録メモリが増えます。部分残存を使用する対象だけに確保します。", MessageType.Info);
            EditorGUILayout.Space();
            DrawHiddenSection();
            EditorGUILayout.Space();
            DrawMaterializedSection();
            if (EditorGUI.EndChangeCheck()) serializedObject.FindProperty("_hasAssetSettings").boolValue = true;
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawHiddenSection()
        {
            DrawField(HiddenPath, "通常の描画を隠す");
            if (!GetBool(HiddenPath)) return;
            EditorGUI.indentLevel++;
            DrawMaterializationConditions();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("照射中の表示", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("透明な間、エコーが当たっている部分の表示です。", MessageType.None);
            DrawIrradiatedEffect(FillName, FillTitle, CopyFillLabel, rule => rule.CopyMaterializedFillToIrradiated());
            DrawIrradiatedEffect(OutlineName, OutlineTitle, CopyOutlineLabel, rule => rule.CopyMaterializedOutlineToIrradiated());
            DrawIrradiatedEffect(OriginalName, OriginalTitle, CopyOriginalLabel, rule => rule.CopyMaterializedOriginalToIrradiated());
            if (GUILayout.Button(CopyAllLabel))
                CopyFromMaterialized(CopyAllLabel, rule => rule.CopyMaterializedDisplayToIrradiated());
            EditorGUI.indentLevel--;
        }

        private void DrawMaterializationConditions()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("実体化の条件", EditorStyles.boldLabel);
            DrawField(MaterializationPath + "._requiredSeconds", "実体化までの照射時間（秒）");
            DrawField(MaterializationPath + "._interruptionMode", "照射が途切れたとき");
            var interruption = (EchoIrradiationInterruptionMode)serializedObject.FindProperty(MaterializationPath + "._interruptionMode").enumValueIndex;
            if (interruption == EchoIrradiationInterruptionMode.Decay)
                DrawField(MaterializationPath + "._decaySpeed", "減る速さ（秒/秒）");
            DrawField(MaterializationPath + "._persistenceMode", "実体化した後");
            var persistence = (EchoMaterializedPersistenceMode)serializedObject.FindProperty(MaterializationPath + "._persistenceMode").enumValueIndex;
            if (persistence == EchoMaterializedPersistenceMode.RevertAfterIdle)
                DrawField(MaterializationPath + "._revertSeconds", "透明に戻るまでの時間（秒）");
            DrawField(MaterializationPath + "._isColliderDisabledWhileTransparent", "透明な間は当たり判定を無効にする");
            EditorGUILayout.EndVertical();
        }

        private void DrawMaterializedSection()
        {
            const string Notice = "実体化しているとき、エコーが当たっている部分の表示です。実体化中は透明化が解除され通常の描画に戻るため、③は表示されません。";
            const string NoticeWithoutHidden = Notice + "通常の描画を隠さないため、最初から実体化しています。";
            EditorGUILayout.LabelField("実体化しているときの表示", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(GetBool(HiddenPath) ? Notice : NoticeWithoutHidden, MessageType.None);
            DrawEffect(SettingsPath, FillName, FillTitle);
            DrawEffect(SettingsPath, OutlineName, OutlineTitle);
            DrawEffect(SettingsPath, OriginalName, OriginalTitle);
        }

        private void DrawEffect(string settingsPath, string name, string title)
        {
            BeginEffect(settingsPath, name, title);
            EndEffect();
        }

        // 照射中の表示だけが、シームレス移行と実体化しているときの表示からのコピーを持つ。
        private void DrawIrradiatedEffect(string name, string title, string copyLabel, Action<EchoTagColorRule> copy)
        {
            if (BeginEffect(IrradiatedPath, name, title)) DrawSeamlessTransition(IrradiatedPath + "." + name, name);
            // 「表示する」もコピー対象のため、オフの間もボタンを出す。
            if (GUILayout.Button(copyLabel)) CopyFromMaterialized(copyLabel, copy);
            EndEffect();
        }

        // 表示が有効ならtrue。続けて項目を描けるよう、枠は閉じずに返す。
        private bool BeginEffect(string settingsPath, string name, string title)
        {
            string path = settingsPath + "." + name;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            DrawField(path + "._isEnabled", "表示する");
            if (!GetBool(path + "._isEnabled")) return false;
            DrawCommon(path);
            DrawAppearance(settingsPath, name);
            return true;
        }

        private static void EndEffect()
        {
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        private void DrawCommon(string path)
        {
            DrawField(path + "._isThroughWalls", "壁越しに表示する");
            DrawField(path + "._displayScope", "表示範囲");
            DrawField(path + "._hasPersistence", "接触終了後も表示を残す");
            if (!GetBool(path + "._hasPersistence")) return;
            DrawField(path + "._persistenceSeconds", "残存時間（秒）");
        }

        // 照射中と実体化中で項目名をそろえ、同じ見た目の設定として扱えるようにする。
        private void DrawAppearance(string settingsPath, string name)
        {
            if (name == FillName) DrawField(settingsPath + "._color", "強調色 / 不透明度");
            if (name == OutlineName)
            { DrawField(settingsPath + "._outlineColor", "輪郭の色 / 不透明度"); DrawField(settingsPath + "._outlineWidth", "輪郭の幅（ピクセル）"); }
            if (name == OriginalName) DrawField(settingsPath + "._originalOpacity", "表示の不透明度");
        }

        // 移行先は実体化しているときの同じ番号の表示。実体化後に表示しない場合は消えていく。
        private void DrawSeamlessTransition(string path, string name)
        {
            DrawField(path + "._isSeamlessTransition", "実体化へシームレスに移行する");
            if (!GetBool(path + "._isSeamlessTransition")) return;
            string notice = name == OriginalName
                ? "実体化までの照射時間に合わせて、不透明度を通常の描画（1）へ近づけます。"
                : GetBool(SettingsPath + "." + name + "._isEnabled")
                    ? "実体化までの照射時間に合わせて、実体化しているときの色・幅へ近づけます。"
                    : "実体化しているときの表示がオフのため、照射時間に合わせて消えていきます。";
            EditorGUILayout.HelpBox(notice, MessageType.None);
        }

        private void CopyFromMaterialized(string undoName, Action<EchoTagColorRule> copy)
        {
            serializedObject.ApplyModifiedProperties();
            Undo.RecordObjects(targets, undoName);
            foreach (var selected in targets)
            {
                var definition = (EchoTagDefinition)selected;
                copy(definition.Settings);
                EditorUtility.SetDirty(definition);
            }
            serializedObject.Update();
            serializedObject.FindProperty("_hasAssetSettings").boolValue = true;
        }
    }
}
