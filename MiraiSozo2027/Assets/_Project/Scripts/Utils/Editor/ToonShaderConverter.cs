using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// _Project 配下の Prefab が使うマテリアルを MS/Toon に一括変換する一時ツール。
// シェーダー移行(feature/toon-shader)のマージ前後にだけ使う想定。マージ後は削除してよい。
public static class ToonShaderConverter
{
    private const string ToonShaderName = "MS/Toon";
    private const string TargetRoot = "Assets/_Project";
    private const string OutputFolder = "Assets/_Project/Art/Materials";
    private const string MenuRoot = "Tools/MS Toon/";

    // これ以外のシェーダー(パーティクル・UI・TMP・独自シェーダー等)は意図が読めないので触らない。
    private static readonly HashSet<string> SupportedShaders = new HashSet<string>
    {
        "Universal Render Pipeline/Lit",
        "Universal Render Pipeline/Simple Lit",
        "Universal Render Pipeline/Unlit",
        "Universal Render Pipeline/Baked Lit",
        "Standard",
        "Standard (Specular setup)",
        "Unlit/Texture",
        "Unlit/Color",
        "Diffuse",
    };

    private sealed class Report
    {
        public int Prefabs;
        public int ChangedPrefabs;
        public int ConvertedInPlace;
        public int Created;
        public int Skipped;
        public int Warnings;
    }

    [MenuItem(MenuRoot + "Preview Project Prefab Materials")]
    private static void PreviewConversion()
    {
        Run(false);
    }

    [MenuItem(MenuRoot + "Convert Project Prefab Materials")]
    private static void ConvertAll()
    {
        bool isConfirmed = EditorUtility.DisplayDialog(
            "MS Toon 一括変換",
            "Assets/_Project 配下の Prefab のマテリアルを MS/Toon に変換します。\n" +
            "事前に Preview で対象を確認し、変更をコミットしてから実行してください。",
            "変換する", "キャンセル");
        if (!isConfirmed)
        {
            return;
        }
        Run(true);
    }

    private static void Run(bool applyChanges)
    {
        Shader toonShader = Shader.Find(ToonShaderName);
        if (toonShader == null)
        {
            Debug.LogError($"[ToonConverter] シェーダー '{ToonShaderName}' が見つかりません。feature/toon-shader の内容が取り込まれているか確認してください。");
            return;
        }

        string mode = applyChanges ? "変換" : "プレビュー";
        var report = new Report();
        // 同じ元マテリアルから複数の新規 .mat を作らないよう、元 -> 変換後を使い回す。
        var convertedCache = new Dictionary<Material, Material>();

        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { TargetRoot });
        // 新規 .mat を直後の Prefab 保存で参照するため、StartAssetEditing による保留はしない。
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            report.Prefabs++;
            ProcessPrefab(path, toonShader, applyChanges, convertedCache, report);
        }

        if (applyChanges)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        Debug.Log($"[ToonConverter] {mode}完了: Prefab {report.Prefabs} 件(変更{(applyChanges ? "" : "予定")} {report.ChangedPrefabs} 件) / " +
                  $"既存.matを直接変換 {report.ConvertedInPlace} / 新規.mat作成 {report.Created} / スキップ {report.Skipped} / 警告 {report.Warnings}");
    }

    private static void ProcessPrefab(string path, Shader toonShader, bool applyChanges,
        Dictionary<Material, Material> convertedCache, Report report)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        bool isChanged = false;
        try
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                bool isRendererChanged = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material converted = ConvertMaterial(materials[i], path, renderer.name, toonShader,
                        applyChanges, convertedCache, report);
                    if (converted != null && converted != materials[i])
                    {
                        materials[i] = converted;
                        isRendererChanged = true;
                    }
                    else if (converted != null)
                    {
                        // 既存 .mat をその場で変換した場合は Prefab 側の参照は変わらない。
                        isChanged = true;
                    }
                }
                if (isRendererChanged)
                {
                    renderer.sharedMaterials = materials;
                    isChanged = true;
                }
            }

            if (isChanged)
            {
                report.ChangedPrefabs++;
                if (applyChanges)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // 変換後のマテリアルを返す。対象外なら null。
    private static Material ConvertMaterial(Material source, string prefabPath, string rendererName, Shader toonShader,
        bool applyChanges, Dictionary<Material, Material> convertedCache, Report report)
    {
        if (source == null || source.shader == null)
        {
            return null;
        }
        if (source.shader == toonShader)
        {
            return null;
        }
        if (convertedCache.TryGetValue(source, out Material cached))
        {
            return cached;
        }

        string shaderName = source.shader.name;
        if (!SupportedShaders.Contains(shaderName))
        {
            report.Skipped++;
            Debug.Log($"[ToonConverter] スキップ(対象外シェーダー '{shaderName}'): {source.name} @ {prefabPath}");
            return null;
        }
        // MS/Toon は透過・アルファテスト未対応のため、見た目が壊れるものは手動確認に回す。
        if (IsTransparentOrCutout(source))
        {
            report.Skipped++;
            report.Warnings++;
            Debug.LogWarning($"[ToonConverter] 透過/アルファテストのためスキップ(手動確認): {source.name} @ {prefabPath} ({rendererName})");
            return null;
        }

        string sourcePath = AssetDatabase.GetAssetPath(source);
        bool isEditableMaterialFile = sourcePath.StartsWith(TargetRoot + "/") && sourcePath.EndsWith(".mat");

        if (isEditableMaterialFile)
        {
            report.ConvertedInPlace++;
            Debug.Log($"[ToonConverter] {(applyChanges ? "変換" : "変換予定")}(既存.mat): {sourcePath}");
            if (applyChanges)
            {
                MaterialSnapshot snapshot = MaterialSnapshot.Capture(source);
                Undo.RecordObject(source, "Convert to MS/Toon");
                source.shader = toonShader;
                snapshot.ApplyTo(source);
                EditorUtility.SetDirty(source);
            }
            convertedCache[source] = source;
            return source;
        }

        // FBX 内蔵・_Sandbox・Packages 内のマテリアルは他からも参照されうるため、書き換えず複製して差し替える。
        report.Created++;
        string newPath = AssetDatabase.GenerateUniqueAssetPath($"{OutputFolder}/M_{SanitizeName(source.name)}_Toon.mat");
        Debug.Log($"[ToonConverter] {(applyChanges ? "新規作成" : "新規作成予定")}: {newPath} (元: {source.name} / {sourcePath})");
        if (!applyChanges)
        {
            convertedCache[source] = source;
            return source;
        }

        EnsureFolder(OutputFolder);
        var created = new Material(toonShader) { name = Path.GetFileNameWithoutExtension(newPath) };
        MaterialSnapshot.Capture(source).ApplyTo(created);
        AssetDatabase.CreateAsset(created, newPath);
        convertedCache[source] = created;
        return created;
    }

    private static bool IsTransparentOrCutout(Material material)
    {
        if (material.HasProperty("_Surface") && material.GetFloat("_Surface") >= 1f)
        {
            return true;
        }
        if (material.HasProperty("_Mode") && material.GetFloat("_Mode") >= 1f)
        {
            return true;
        }
        if (material.HasProperty("_AlphaClip") && material.GetFloat("_AlphaClip") >= 1f)
        {
            return true;
        }
        return material.renderQueue >= 2450;
    }

    private static string SanitizeName(string name)
    {
        string trimmed = name.Replace(" (Instance)", "");
        foreach (char invalid in Path.GetInvalidFileNameChars())
        {
            trimmed = trimmed.Replace(invalid, '_');
        }
        return trimmed;
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
        {
            return;
        }
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }

    // シェーダー差し替えで値が失われないよう、先に取り出しておく。
    // URP/Lit は _BaseMap/_BaseColor、Standard 系は _MainTex/_Color を使うため両方に対応する。
    private struct MaterialSnapshot
    {
        private Texture _baseMap;
        private Vector2 _baseScale;
        private Vector2 _baseOffset;
        private Color _baseColor;
        private Texture _bumpMap;
        private float _bumpScale;
        private Texture _emissionMap;
        private Color _emissionColor;

        public static MaterialSnapshot Capture(Material m)
        {
            var s = new MaterialSnapshot();
            string mapProp = m.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";
            string colorProp = m.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";

            s._baseMap = m.HasProperty(mapProp) ? m.GetTexture(mapProp) : null;
            s._baseScale = m.HasProperty(mapProp) ? m.GetTextureScale(mapProp) : Vector2.one;
            s._baseOffset = m.HasProperty(mapProp) ? m.GetTextureOffset(mapProp) : Vector2.zero;
            s._baseColor = m.HasProperty(colorProp) ? m.GetColor(colorProp) : Color.white;
            s._bumpMap = m.HasProperty("_BumpMap") ? m.GetTexture("_BumpMap") : null;
            s._bumpScale = m.HasProperty("_BumpScale") ? m.GetFloat("_BumpScale") : 1f;
            s._emissionMap = m.HasProperty("_EmissionMap") ? m.GetTexture("_EmissionMap") : null;
            s._emissionColor = m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor") : Color.black;
            return s;
        }

        public void ApplyTo(Material target)
        {
            target.SetTexture("_BaseMap", _baseMap);
            target.SetTextureScale("_BaseMap", _baseScale);
            target.SetTextureOffset("_BaseMap", _baseOffset);
            target.SetColor("_BaseColor", _baseColor);

            bool hasNormal = _bumpMap != null;
            if (hasNormal)
            {
                target.SetTexture("_BumpMap", _bumpMap);
                target.SetFloat("_BumpScale", _bumpScale);
            }
            SetToggle(target, "_UseNormalMap", "_NORMALMAP", hasNormal);

            bool hasEmission = _emissionMap != null || _emissionColor.maxColorComponent > 0f;
            if (hasEmission)
            {
                if (_emissionMap != null)
                {
                    target.SetTexture("_EmissionMap", _emissionMap);
                }
                target.SetColor("_EmissionColor", _emissionColor);
            }
            SetToggle(target, "_UseEmission", "_EMISSION", hasEmission);
        }

        private static void SetToggle(Material target, string property, string keyword, bool isEnabled)
        {
            target.SetFloat(property, isEnabled ? 1f : 0f);
            if (isEnabled)
            {
                target.EnableKeyword(keyword);
            }
            else
            {
                target.DisableKeyword(keyword);
            }
        }
    }
}
