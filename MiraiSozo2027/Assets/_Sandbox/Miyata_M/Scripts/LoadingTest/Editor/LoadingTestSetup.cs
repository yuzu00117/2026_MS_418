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

namespace LoadingTest.EditorTools
{
    /// <summary>
    /// ロード画面3方式の検証用 Scene / Prefab を生成する。
    /// メニュー: Tools/LoadingTest/Setup、またはバッチ -executeMethod LoadingTest.EditorTools.LoadingTestSetup.Build
    /// 何度実行しても同じ結果になる(上書き生成)。
    /// </summary>
    public static class LoadingTestSetup
    {
        private const string SceneDir = "Assets/_Sandbox/Miyata_M/Scenes/LoadingTest";
        private const string ResourcesDir = "Assets/_Sandbox/Miyata_M/Resources";
        private const string PrefabPath = ResourcesDir + "/PF_SceneLoader.prefab";

        // 方式A / B / C のシーン名(Build Settings へ登録する)
        private const string SceneA1 = "LT_A_Start";
        private const string SceneA2 = "LT_A_Next";
        private const string SceneBoot = "LT_B_Boot";
        private const string SceneTitle = "LT_B_Title";
        private const string SceneGame = "LT_B_Game";
        private const string SceneC1 = "LT_C_Start";
        private const string SceneC2 = "LT_C_Next";

        [MenuItem("Tools/LoadingTest/Setup")]
        public static void Build()
        {
            Directory.CreateDirectory(SceneDir);
            Directory.CreateDirectory(ResourcesDir);

            BuildA();
            BuildB();
            BuildC();
            RegisterBuildSettings(new[] { SceneA1, SceneA2, SceneBoot, SceneTitle, SceneGame, SceneC1, SceneC2 });
            AssetDatabase.SaveAssets();
            Debug.Log("LoadingTest: セットアップ完了");
        }

        // ---- 方式A: 各シーンにロードUI ----
        private static void BuildA()
        {
            BuildASceneWithLoader(SceneA1, "シーンA1(-> A2へ)", SceneA2);
            BuildASceneWithLoader(SceneA2, "シーンA2(-> A1へ)", SceneA1);
        }

        private static void BuildASceneWithLoader(string name, string label, string target)
        {
            var scene = NewScene();
            CreateCamera();
            var canvas = CreateCanvas("Canvas");
            CreateLabel(canvas.transform, label, new Vector2(0, 150));

            var loader = new GameObject("LoadingScreen").AddComponent<LoadingTest.A.LoadingScreen>();
            var button = CreateButton(canvas.transform, "ロード開始", new Vector2(0, 0));
            UnityEventTools.AddStringPersistentListener(button.onClick, loader.LoadScene, target);

            // ロードUIは最後に生成して最前面に置く(ボタンより手前に描画し、クリックも遮断する)
            var panel = CreateLoadingPanel(canvas.transform, out var slider);
            SetObject(loader, "_loadingPanel", panel);
            SetObject(loader, "_progressBar", slider);
            panel.SetActive(false);
            CreateEventSystem();
            Save(scene, name);
        }

        // ---- 方式B: 常駐 Boot + Additive ----
        private static void BuildB()
        {
            // Boot: ローダーとロードUIのみ。EventSystem もここに1つだけ置く
            var boot = NewScene();
            CreateBootCamera();
            var canvas = CreateCanvas("LoadingCanvas", 100);
            var group = canvas.gameObject.AddComponent<CanvasGroup>();
            var bg = CreateImage(canvas.transform, "Background", new Color(0, 0, 0, 0.9f));
            Stretch(bg.rectTransform);
            CreateLabel(canvas.transform, "Loading...", new Vector2(0, 40));
            var slider = CreateSlider(canvas.transform, new Vector2(0, -20));
            var loader = canvas.gameObject.AddComponent<LoadingTest.B.AdditiveSceneLoader>();
            SetObject(loader, "_loadingGroup", group);
            SetObject(loader, "_progressBar", slider);
            SetString(loader, "_firstScene", SceneTitle);
            CreateEventSystem();
            Save(boot, SceneBoot);

            BuildBGameScene(SceneTitle, "タイトル(-> Gameへ)", SceneGame, new Color(0.15f, 0.25f, 0.45f));
            BuildBGameScene(SceneGame, "ゲーム(-> Titleへ)", SceneTitle, new Color(0.45f, 0.25f, 0.15f));
        }

        private static void BuildBGameScene(string name, string label, string target, Color bgColor)
        {
            var scene = NewScene();
            CreateCamera(); // カメラはゲームシーン側に置く(同時に読み込まれるのは1シーンのみなので重複しない)
            var canvas = CreateCanvas("Canvas");
            var bg = CreateImage(canvas.transform, "Background", bgColor);
            Stretch(bg.rectTransform);
            CreateLabel(canvas.transform, label, new Vector2(0, 150));
            var button = CreateButton(canvas.transform, "ロード開始", new Vector2(0, 0));
            var load = button.gameObject.AddComponent<LoadingTest.B.AdditiveLoadButton>();
            SetString(load, "_targetScene", target);
            // EventSystem は Boot に1つだけ置く(重複警告回避)
            Save(scene, name);
        }

        // ---- 方式C: シングルトン ----
        private static void BuildC()
        {
            BuildSceneLoaderPrefab();

            BuildCScene(SceneC1, "シーンC1(-> C2へ)", SceneC2, new Color(0.2f, 0.4f, 0.2f));
            BuildCScene(SceneC2, "シーンC2(-> C1へ)", SceneC1, new Color(0.4f, 0.2f, 0.4f));
        }

        /// <summary>方式C の PF_SceneLoader.prefab を生成する(シーン遷移の Setup からも使う)。</summary>
        public static void BuildSceneLoaderPrefab()
        {
            Directory.CreateDirectory(ResourcesDir);
            var root = new GameObject("SceneLoader");
            var canvas = CreateCanvas("LoadingCanvas", 100, root.transform);
            var group = canvas.gameObject.AddComponent<CanvasGroup>();
            var bg = CreateImage(canvas.transform, "Background", new Color(0, 0, 0, 0.9f));
            Stretch(bg.rectTransform);
            CreateLabel(canvas.transform, "Loading...", new Vector2(0, 40));
            var slider = CreateSlider(canvas.transform, new Vector2(0, -20));
            var loader = root.AddComponent<LoadingTest.C.SceneLoader>();
            SetObject(loader, "_canvasGroup", group);
            SetObject(loader, "_progressBar", slider);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
        }

        private static void BuildCScene(string name, string label, string target, Color bgColor)
        {
            var scene = NewScene();
            CreateCamera();
            var canvas = CreateCanvas("Canvas");
            var bg = CreateImage(canvas.transform, "Background", bgColor);
            Stretch(bg.rectTransform);
            CreateLabel(canvas.transform, label, new Vector2(0, 150));
            var button = CreateButton(canvas.transform, "ロード開始", new Vector2(0, 0));
            var load = button.gameObject.AddComponent<LoadingTest.C.SceneLoaderButton>();
            SetString(load, "_targetScene", target);
            CreateEventSystem(); // 方式Cはローダー側に EventSystem を持たせない
            Save(scene, name);
        }

        // ---- 共通ヘルパー ----
        private static Scene NewScene()
        {
            return EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        private static void Save(Scene scene, string name)
        {
            EditorSceneManager.SaveScene(scene, $"{SceneDir}/{name}.unity");
        }

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

        private static void CreateBootCamera()
        {
            // ゲームシーンのカメラが0台になる入れ替え中の間も、背景を黒で確実に描画するための最小カメラ。
            // AudioListener はゲームシーン側と重複するため付けない。MainCamera タグも付けない
            var go = new GameObject("BootCamera", typeof(Camera));
            var cam = go.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.cullingMask = 0; // 何も描画しない
            cam.depth = -100f;   // ゲームシーンのカメラより常に後ろ
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
            rt.sizeDelta = new Vector2(400, 100);
            rt.anchoredPosition = pos;
            var text = CreateLabel(go.transform, label, Vector2.zero);
            text.color = Color.black;
            text.rectTransform.sizeDelta = rt.sizeDelta;
            return go.GetComponent<Button>();
        }

        private static GameObject CreateLoadingPanel(Transform parent, out Slider slider)
        {
            var panel = CreateImage(parent, "LoadingPanel", new Color(0, 0, 0, 0.9f));
            Stretch(panel.rectTransform);
            CreateLabel(panel.transform, "Loading...", new Vector2(0, 40));
            slider = CreateSlider(panel.transform, new Vector2(0, -20));
            return panel.gameObject;
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

        private static void SetString(Object target, string field, string value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // 既存の登録は維持し、未登録のものだけ末尾へ追加する
        private static void RegisterBuildSettings(string[] names)
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
