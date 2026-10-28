using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace Echo.Echolocation.Editor
{
    public sealed class EchoTagBatchWindow : EditorWindow
    {
        private string _unityTag = "Untagged";
        private EchoTagDefinition _definition;
        private bool _isSelectionOnly;
        private bool _isActiveSceneOnly = true;
        private bool _hasInactive = true;
        private bool _canReplace;
        private EchoRevealWorld _world;
        private readonly List<GameObject> _candidates = new List<GameObject>();
        private Vector2 _scroll;
        [MenuItem("Tools/エコーロケーション/03 対象に専用タグを付与", false, 103)]
        public static void Open() => GetWindow<EchoTagBatchWindow>("対象へのタグ付与");
        public static void OpenFor(EchoTagDefinition definition, EchoRevealWorld world)
        {
            var window = GetWindow<EchoTagBatchWindow>("対象へのタグ付与");
            window._definition = definition;
            window._world = world;
            window._isActiveSceneOnly = true;
            window._candidates.Clear();
        }
        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Omata_N配下のロード済みシーンだけを対象にします。検索用のUnityタグから対象を探し、エコー専用タグを付けるツールです。Unityタグは変更しません。プレビュー後に適用し、シーン保存は手動で行ってください。", MessageType.Info);
            EditorGUI.BeginChangeCheck();
            _unityTag = EditorGUILayout.TagField("検索するUnityタグ", _unityTag);
            _definition = (EchoTagDefinition)EditorGUILayout.ObjectField("付与する専用タグ", _definition, typeof(EchoTagDefinition), false);
            _isActiveSceneOnly = EditorGUILayout.Toggle("現在のシーンだけ", _isActiveSceneOnly);
            _isSelectionOnly = EditorGUILayout.Toggle("選択した階層だけ", _isSelectionOnly);
            _hasInactive = EditorGUILayout.Toggle("非アクティブも含む", _hasInactive);
            _canReplace = EditorGUILayout.Toggle("既存の専用タグを置換", _canReplace);

            if (EditorGUI.EndChangeCheck()) _candidates.Clear();
            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                if (GUILayout.Button("対象をプレビュー")) Preview();
                EditorGUILayout.LabelField("対象数", _candidates.Count.ToString());
                _scroll = EditorGUILayout.BeginScrollView(_scroll);
                foreach (var candidate in _candidates) EditorGUILayout.ObjectField(candidate, typeof(GameObject), true);
                EditorGUILayout.EndScrollView();
                using (new EditorGUI.DisabledScope(!_definition || _candidates.Count == 0))
                    if (GUILayout.Button("プレビュー対象に適用（Undo対応）"))
                    { Apply(_candidates, _definition, _world, _canReplace); _candidates.Clear(); }
            }
        }
        private void Preview()
        {
            _candidates.Clear();
            foreach (var transform in FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                var candidate = transform.gameObject;
                if (!candidate.scene.IsValid() || !candidate.scene.path.StartsWith("Assets/_Sandbox/Omata_N/") ||
                    (_isActiveSceneOnly && candidate.scene != UnityEngine.SceneManagement.SceneManager.GetActiveScene()) ||
                    (!_hasInactive && !candidate.activeInHierarchy) || !candidate.CompareTag(_unityTag) ||
                    (!_canReplace && candidate.TryGetComponent<EchoTarget>(out var existing) && existing.Definition)) continue;
                if (_isSelectionOnly)
                {
                    bool isSelected = false;
                    foreach (var selected in Selection.transforms) if (transform.IsChildOf(selected)) { isSelected = true; break; }
                    if (!isSelected) continue;
                }
                _candidates.Add(candidate);
            }
        }
        public static void Apply(IReadOnlyList<GameObject> candidates, EchoTagDefinition definition, EchoRevealWorld world, bool canReplace)
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Assign echo dedicated tags");
            foreach (var candidate in candidates)
            {
                if (!candidate || !candidate.scene.path.StartsWith("Assets/_Sandbox/Omata_N/")) continue;
                var target = candidate.GetComponent<EchoTarget>();
                if (target && target.Definition && !canReplace) continue;
                if (!target) target = Undo.AddComponent<EchoTarget>(candidate);
                Undo.RecordObject(target, "Assign echo tag"); target.Definition = definition;
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                EditorSceneManager.MarkSceneDirty(candidate.scene);
            }
            Undo.CollapseUndoOperations(group);
        }
    }
}
