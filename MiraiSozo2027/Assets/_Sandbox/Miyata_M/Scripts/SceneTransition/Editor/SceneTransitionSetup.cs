using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SceneFlow.EditorTools
{
    /// <summary>
    /// 遷移用の Scene(Title / StageSelect / Game)と SceneTransition.prefab を生成する。
    /// メニュー: Tools/SceneTransition/Setup、またはバッチ -executeMethod SceneFlow.EditorTools.SceneTransitionSetup.Build
    /// 何度実行しても同じ結果になる(上書き生成)。
    /// </summary>
    public static class SceneTransitionSetup
    {
        private const string SceneDir = "Assets/_Sandbox/Miyata_M/Scenes/SceneTransition";
        private const string ResourcesDir = "Assets/_Sandbox/Miyata_M/Resources";
        private const string PrefabPath = ResourcesDir + "/SceneTransition.prefab";

        // テスト用シーン名(本番のシーンとは無関係。遷移先は SceneReference で指定する)
        private const string TitleName = "Scene_Title";
        private const string StageSelectName = "Scene_StageSelect";
        private const string GameName = "Scene_Game";

        [MenuItem("Tools/SceneTransition/Setup")]
        public static void Build()
        {
            Directory.CreateDirectory(SceneDir);
            Directory.CreateDirectory(ResourcesDir);

            BuildPrefab();

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

        // ---- ロード画面を含む常駐 prefab ----
        private static void BuildPrefab()
        {
            var root = new GameObject("SceneTransition");
            var canvas = CreateCanvas("LoadingCanvas", 100, root.transform);
            var group = canvas.gameObject.AddComponent<CanvasGroup>();
            var bg = CreateImage(canvas.transform, "Background", new Color(0, 0, 0, 0.95f));
            Stretch(bg.rectTransform);
            CreateLabel(canvas.transform, "Loading...", new Vector2(0, 40));
            var slider = CreateSlider(canvas.transform, new Vector2(0, -20));

            var transition = root.AddComponent<SceneTransition>();
            SetObject(transition, "_canvasGroup", group);
            SetObject(transition, "_progressBar", slider);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
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
            CreateEventSystem(); // ロード画面の prefab 側には EventSystem を持たせない
            EditorSceneManager.SaveScene(scene, ScenePath(name));
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

        private static Slider CreateSlider(Transform parent, Vector2 pos)
        {
            var root = new GameObject("ProgressBar", typeof(Slider));
            root.transform.SetParent(parent, false);
            var rt = (RectTransform)root.transform;
            rt.sizeDelta = new Vector2(800, 30);
            rt.anchoredPosition = pos;

            var bg = CreateImage(root.transform, "Background", new Color(0.3f, 0.3f, 0.3f));
            Stretch(bg.rectTransform);
            var fillArea = new GameObject("FillArea", typeof(RectTransform));
            fillArea.transform.SetParent(root.transform, false);
            Stretch((RectTransform)fillArea.transform);
            var fill = CreateImage(fillArea.transform, "Fill", Color.cyan);
            Stretch(fill.rectTransform);

            var slider = root.GetComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.interactable = false; // 表示専用
            slider.minValue = 0f;
            slider.maxValue = 1f;
            return slider;
        }

        private static void SetObject(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
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
