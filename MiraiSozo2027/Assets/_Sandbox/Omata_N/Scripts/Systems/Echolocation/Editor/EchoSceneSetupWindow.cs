using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Echo.Echolocation.Editor
{
    /// <summary>新規シーンの設定順序と、個別ツールへの入口。</summary>
    public sealed class EchoSceneSetupWindow : EditorWindow
    {
        private EchoController _controller;
        private Camera _camera;
        private Transform _origin;
        private EchoTagDefinition _definition;
        private Vector2 _scroll;
        [MenuItem("Tools/エコーロケーション/00 新しいシーンの設定ガイド", false, 100)]
        public static void Open() => GetWindow<EchoSceneSetupWindow>("エコー設定ガイド");
        [MenuItem("Tools/エコーロケーション/02 発信元と表示ルールを設定", false, 102)]
        public static void OpenSceneSettings() => Open();
        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            var scene = SceneManager.GetActiveScene();
            EditorGUILayout.LabelField("設定するシーン", scene.name);
            EditorGUILayout.HelpBox("上から順に設定してください。シーンは先にOmata_N配下へ保存します。別のシーンへ移ったら、そのシーンの発信元・カメラ・対象を設定します。プレイヤー操作はチーム側の仕組みを使用してください。", MessageType.Info);
            bool canEdit = !Application.isPlaying && scene.path.StartsWith("Assets/_Sandbox/Omata_N/");
            if (!canEdit) EditorGUILayout.HelpBox("再生を停止し、現在のシーンをOmata_N配下へ保存してください。", MessageType.Warning);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("1. 描画機能を準備", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("エコーを画面に描く準備です。同じRendererを使用していて設定済みならスキップできます。", MessageType.None);
            if (GUILayout.Button("描画準備ツールを開く")) EchoRendererSetupWindow.Open();
            using (new EditorGUI.DisabledScope(!canEdit))
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("2. 発信元とカメラを設定", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("Controllerはエコーの発信と描画範囲を管理します。発信位置にはプレイヤーなどのTransformを指定します。", MessageType.None);
                _controller = (EchoController)EditorGUILayout.ObjectField("このシーンのController", _controller, typeof(EchoController), true);
                _camera = (Camera)EditorGUILayout.ObjectField("描画するカメラ", _camera, typeof(Camera), true);
                _origin = (Transform)EditorGUILayout.ObjectField("発信位置・方向", _origin, typeof(Transform), true);
                bool hasSceneObjects = _camera && _origin && _camera.gameObject.scene == scene && _origin.gameObject.scene == scene && (!_controller || _controller.gameObject.scene == scene);
                using (new EditorGUI.DisabledScope(!hasSceneObjects))
                    if (GUILayout.Button("発信元を作成／選択した発信元を設定")) ConfigureScene();
                if (_controller && _controller.gameObject.scene != scene)
                    EditorGUILayout.HelpBox("別シーンのControllerが選ばれています。このシーンのものを指定してください。", MessageType.Warning);
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("3. 専用タグと表示ルールを作成", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("専用タグは対象の種類を表すアセットです。Controllerの表示ルールと対象オブジェクトで同じタグを使います。色・輪郭・元マテリアルの表示設定はControllerのInspectorで編集します。", MessageType.None);
                _definition = (EchoTagDefinition)EditorGUILayout.ObjectField("専用タグ", _definition, typeof(EchoTagDefinition), false);
                if (GUILayout.Button("専用タグアセットを新規作成")) CreateTag();
                using (new EditorGUI.DisabledScope(!_controller || _controller.gameObject.scene != scene || !_definition))
                    if (GUILayout.Button("このタグの表示ルールを追加してInspectorを開く")) AddRule();
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("4. 対象に専用タグを付与", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("Unityタグで対象を検索し、上で作った専用タグを付与します。元表示の部分表示には接触部分表示を指定します。", MessageType.None);
                if (GUILayout.Button("対象へのタグ付与ツールを開く"))
                    EchoTagBatchWindow.OpenFor(_definition, _controller && _controller.gameObject.scene == scene ? _controller.GetComponent<EchoRevealWorld>() : null);
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("5. 接触部分表示の準備（使用する場合のみ）", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("接触部分表示は届いた表面をGPUで表示します。ベイクやCollider追加は不要です。輪郭グリッチは管理元のタグと表示設定で調整します。", MessageType.None);
                if (GUILayout.Button("部分表示の準備ツールを開く")) EchoSurfaceSetupWindow.Open();
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("6. 保存して動作確認", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("シーンを保存して再生します。チーム入力からエコーを発信し、表示・遮蔽・当たり判定を確認してください。表示されない場合は専用タグと描画Featureを確認します。入力連携はこのツールでは作成しません。", MessageType.None);
                if (GUILayout.Button("現在のシーンを保存")) EditorSceneManager.SaveScene(scene);
            }
            EditorGUILayout.EndScrollView();
        }
        private void ConfigureScene()
        {
            if (!_controller)
            {
                foreach (var existing in FindObjectsByType<EchoController>(FindObjectsInactive.Include))
                    if (existing.gameObject.scene == SceneManager.GetActiveScene())
                    { _controller = existing; ShowNotification(new GUIContent("既存の発信元を選択しました。確認してもう一度設定してください。")); return; }
                var owner = new GameObject("EchoController");
                SceneManager.MoveGameObjectToScene(owner, SceneManager.GetActiveScene());
                Undo.RegisterCreatedObjectUndo(owner, "Create echo controller");
                _controller = Undo.AddComponent<EchoController>(owner);
            }
            Undo.RecordObject(_controller, "Configure echo controller");
            _controller.TargetCamera = _camera;
            _controller.OriginTransform = _origin;
            _controller.DirectionTransform = _origin;

            PrefabUtility.RecordPrefabInstancePropertyModifications(_controller);
            EditorSceneManager.MarkSceneDirty(_controller.gameObject.scene);
            Selection.activeObject = _controller;
        }
        private void CreateTag()
        {
            const string folder = "Assets/_Sandbox/Omata_N/Settings/EchoTags";
            EchoSurfaceBaker.EnsureFolder(folder);
            _definition = CreateInstance<EchoTagDefinition>();
            AssetDatabase.CreateAsset(_definition, AssetDatabase.GenerateUniqueAssetPath(folder + "/SO_EchoTag.asset"));
            AssetDatabase.SaveAssetIfDirty(_definition);
            Selection.activeObject = _definition;
        }
        private void AddRule()
        {
            bool hasRule = false;
            foreach (var rule in _controller.Settings.TagColorRules)
                hasRule |= rule != null && rule.Definition == _definition;
            if (!hasRule)
            {
                Undo.RecordObject(_controller, "Add echo display rule");
                _controller.Settings.TagColorRules.Add(new EchoTagColorRule { Definition = _definition });
                PrefabUtility.RecordPrefabInstancePropertyModifications(_controller);
                EditorSceneManager.MarkSceneDirty(_controller.gameObject.scene);
            }
            Selection.activeObject = _controller;
        }
    }
}
