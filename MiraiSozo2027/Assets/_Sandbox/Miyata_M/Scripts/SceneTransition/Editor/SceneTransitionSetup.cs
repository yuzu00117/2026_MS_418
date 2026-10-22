using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SceneFlow.EditorTools
{
    /// <summary>
    /// 遷移用の Scene(Title / StageSelect / Game)を生成する。ロード画面(SceneLoader.prefab)は 2006 の方式C のものを使う。
    /// メニュー: Tools/SceneTransition/Setup、またはバッチ -executeMethod SceneFlow.EditorTools.SceneTransitionSetup.Build
    /// 何度実行しても同じ結果になる(上書き生成)。
    /// </summary>
    public static class SceneTransitionSetup
    {
        private const string SceneDir = "Assets/_Sandbox/Miyata_M/Scenes/SceneTransition";

        // テスト用シーン名(本番のシーンとは無関係。遷移先は SceneReference で指定する)
        private const string TitleName = "Scene_Title";
        private const string StageSelectName = "Scene_StageSelect";
        private const string GameName = "Scene_Game";

        [MenuItem("Tools/SceneTransition/Setup")]
        public static void Build()
        {
            Directory.CreateDirectory(SceneDir);

            // ロード画面は遷移の外側(2006 の方式C)。SceneLoader.prefab が無ければ生成する
            LoadingTest.EditorTools.LoadingTestSetup.BuildSceneLoaderPrefab();

            // 遷移先をアセット参照で指定するため、先に空シーンを保存してアセットとして取り込む
            var names = new List<string> { TitleName, StageSelectName, GameName };
            foreach (var n in names) CreatePlaceholderScene(n);
            AssetDatabase.Refresh();

            BuildScene(TitleName, new Color(0.15f, 0.25f, 0.45f),
                (StageSelectName, "ステージ選択へ"));
            BuildScene(StageSelectName, new Color(0.2f, 0.4f, 0.2f),
                (GameName, "ゲーム開始"), (TitleName, "タイトルへ"));
            BuildScene(GameName, new Color(0.45f, 0.25f, 0.15f),
                (StageSelectName, "ステージ選択へ"), (TitleName, "タイトルへ"));

            RegisterBuildSettings(names);
            AssetDatabase.SaveAssets();
            Debug.Log("SceneTransition: セットアップ完了");
        }

        // ---- 遷移元シーン ----
        private static void CreatePlaceholderScene(string name)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath(name));
        }

        private static void BuildScene(string name, Color bgColor, params (string target, string label)[] buttons)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera();
            var canvas = CreateCanvas("Canvas");
            var bg = CreateImage(canvas.transform, "Background", bgColor);
            Stretch(bg.rectTransform);
            CreateLabel(canvas.transform, name, new Vector2(0, 200));

            float y = 40f;
            foreach (var (target, label) in buttons)
            {
                var button = CreateButton(canvas.transform, label, new Vector2(0, y));
                var comp = button.gameObject.AddComponent<SceneTransitionButton>();
                AssignScene(new SerializedObject(comp), "_target", ScenePath(target));
                y -= 130f;
            }
            // ボタン以外(クリア判定などのイベント)から遷移する例。Game から Title へ戻る
            if (name == GameName) AddTrigger(canvas.transform, "ClearTrigger", TitleName);

            CreateEventSystem(); // ロード画面の prefab 側には EventSystem を持たせない
            EditorSceneManager.SaveScene(scene, ScenePath(name));
        }

        // クリア判定の代わりにカウントダウンを置き、終了イベントを Trigger.Go に接続する(動作確認用)
        private static void AddTrigger(Transform canvas, string objectName, string targetScene)
        {
            var go = new GameObject(objectName);
            var trigger = go.AddComponent<SceneTransitionTrigger>();
            AssignScene(new SerializedObject(trigger), "_target", ScenePath(targetScene));

            var label = CreateLabel(canvas, "", new Vector2(0, 320));
            var countdown = go.AddComponent<CountdownEvent>();
            var so = new SerializedObject(countdown);
            so.FindProperty("_label").objectReferenceValue = label;
            so.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(countdown.OnFinished, trigger.Go);
            EditorUtility.SetDirty(countdown);
        }

        // SceneReference の各フィールドを設定する(Inspector でシーンをドラッグした状態と同じにする)
        private static void AssignScene(SerializedObject so, string field, string scenePath)
        {
            var prop = so.FindProperty(field);
            prop.FindPropertyRelative("_sceneAsset").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
            prop.FindPropertyRelative("_scenePath").stringValue = scenePath;
            prop.FindPropertyRelative("_sceneName").stringValue = Path.GetFileNameWithoutExtension(scenePath);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static string ScenePath(string name)
        {
            return $"{SceneDir}/{name}.unity";
        }

        // ---- 共通ヘルパー ----
        private static Canvas CreateCanvas(string name, int sortOrder = 0, Transform parent = null)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            if (parent != null) go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            return canvas;
        }

        private static void CreateCamera()
        {
            // Overlay Canvas だけだと "No cameras rendering" の警告が出るためカメラを置く
            var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            go.tag = "MainCamera";
            var cam = go.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
        }

        private static void CreateEventSystem()
        {
            // Input System 専用設定のため InputSystemUIInputModule を使う
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        private static Image CreateImage(Transform parent, string name, Color color)
        {
            var img = new GameObject(name, typeof(Image)).GetComponent<Image>();
            img.transform.SetParent(parent, false);
            img.color = color;
            return img;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static Text CreateLabel(Transform parent, string text, Vector2 pos)
        {
            var go = new GameObject("Label", typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.text = text;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = 48;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            var rt = t.rectTransform;
            rt.sizeDelta = new Vector2(900, 100);
            rt.anchoredPosition = pos;
            return t;
        }

        private static Button CreateButton(Transform parent, string label, Vector2 pos)
        {
            var go = new GameObject("Button", typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(500, 100);
            rt.anchoredPosition = pos;
            var text = CreateLabel(go.transform, label, Vector2.zero);
            text.color = Color.black;
            text.rectTransform.sizeDelta = rt.sizeDelta;
            return go.GetComponent<Button>();
        }

        // 既存の登録は維持し、未登録のものだけ末尾へ追加する
        private static void RegisterBuildSettings(List<string> names)
        {
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var n in names)
            {
                string path = $"{SceneDir}/{n}.unity";
                if (!list.Exists(s => s.path == path)) list.Add(new EditorBuildSettingsScene(path, true));
            }
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
