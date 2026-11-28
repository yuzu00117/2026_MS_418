using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MS.Toon.Editor
{
    /// <summary>
    /// ToonStyleApplier のインスペクタ。先頭に使い方と切り替え用のプルダウンを出してから標準の項目を描く。
    /// 複数選択での一括変更にも対応し、同じ設定を複数のシーンオブジェクトへ揃えやすくする。
    /// </summary>
    [CustomEditor(typeof(ToonStyleApplier)), CanEditMultipleObjects]
    public class ToonStyleApplierEditor : UnityEditor.Editor
    {
        // 説明文はこのクラスの表示専用。文言を直すときはここだけ変えれば済むように定数にする
        private const string HelpText = "シーンに1つだけ置きます。Profile に入れたものが現在のスタイルになります。Profiles に候補を入れておくと、下の「スタイル切り替え」から選んで切り替えられます。このコンポーネントを無効にすると、各マテリアル自身の値に戻ります。マテリアルに個別の設定ファイルが登録されている場合は、そのファイルで上書きした項目(輪郭線・影など)だけファイルの値で描き、上書きしていない項目はここの共通設定に従います。";

        private const string DuplicateText = "シーンに ToonStyleApplier が複数あります。このコンポーネントは無視されます(有効なのは別のオブジェクトのものです)。不要なものを削除してください。";

        private const string SwitchLabel = "スタイル切り替え";
        private const string OutsideCandidatesLabel = "(候補外)";
        private const string EmptySlotLabel = "(未設定)";
        private const string UndoName = "Switch Toon Style";

        private SerializedProperty _profileProperty;
        private SerializedProperty _profilesProperty;

        private void OnEnable()
        {
            // フィールド名は ToonStyleApplier の [SerializeField] と一致させること
            _profileProperty = serializedObject.FindProperty("_profile");
            _profilesProperty = serializedObject.FindProperty("_profiles");
        }

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
            serializedObject.Update();
            DrawProfileSwitcher();
            DrawDefaultInspector();
        }

        // Profiles の候補から1つ選んで、一発で Profile を切り替えるプルダウン。
        // 候補の一覧は先頭の選択対象のものを使う(複数選択で候補が違う場合は先頭に合わせる)。
        private void DrawProfileSwitcher()
        {
            if (_profilesProperty == null || _profilesProperty.arraySize == 0)
            {
                return;
            }

            var candidates = new List<ToonStyleProfile>();
            var labels = new List<string>();
            for (int i = 0; i < _profilesProperty.arraySize; i++)
            {
                ToonStyleProfile candidate = _profilesProperty.GetArrayElementAtIndex(i).objectReferenceValue as ToonStyleProfile;
                candidates.Add(candidate);
                // 同じ名前が並ぶと見分けられないので番号を付ける
                labels.Add($"{i}: {(candidate != null ? candidate.name : EmptySlotLabel)}");
            }

            int selected = candidates.IndexOf(_profileProperty.objectReferenceValue as ToonStyleProfile);
            if (selected < 0)
            {
                // 現在の Profile が候補に無いときは、そのことが分かる項目を末尾に足して選んでおく
                labels.Add(OutsideCandidatesLabel);
                selected = labels.Count - 1;
            }

            EditorGUI.showMixedValue = _profileProperty.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            int next = EditorGUILayout.Popup(SwitchLabel, selected, labels.ToArray());
            EditorGUI.showMixedValue = false;
            if (!EditorGUI.EndChangeCheck() || next >= candidates.Count)
            {
                return;
            }

            ApplyToTargets(candidates[next]);
        }

        // SetProfile を通して切り替えるのは、シェーダーのグローバル値への反映まで即座に行うため
        private void ApplyToTargets(ToonStyleProfile profile)
        {
            foreach (Object t in targets)
            {
                ToonStyleApplier applier = t as ToonStyleApplier;
                if (applier == null)
                {
                    continue;
                }
                Undo.RecordObject(applier, UndoName);
                applier.SetProfile(profile);
                PrefabUtility.RecordPrefabInstancePropertyModifications(applier);
                EditorUtility.SetDirty(applier);
            }
            serializedObject.Update();
        }
    }
}
