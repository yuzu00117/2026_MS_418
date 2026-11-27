using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MS.Toon.Editor
{
    /// <summary>
    /// マテリアルと「個別の設定ファイル(ToonStyleProfile)」の紐づけを管理する。
    /// マテリアルは ScriptableObject への参照を持てないため、GUID を文字列タグに保存し、
    /// 設定ファイルの値をマテリアルのプロパティへ書き込んでおく(実行時はマテリアルの値だけで描ける)。
    /// 設定ファイルを編集したら、登録先のマテリアルへ自動で書き戻す。
    /// </summary>
    [InitializeOnLoad]
    public static class ToonMaterialProfileLink
    {
        private const string MaterialSearchFilter = "t:Material";
        private const string UndoName = "個別の設定ファイルを登録";
        private const float GlobalToggleOn = 1f;

        private static readonly int UseGlobalOutlineId = Shader.PropertyToID("_UseGlobalOutline");
        private static readonly int UseGlobalStyleId = Shader.PropertyToID("_UseGlobalStyle");

        // プロファイル GUID -> 登録先マテリアルのパス。全マテリアルを毎回調べると重いので、初回だけ作って使い回す
        private static Dictionary<string, HashSet<string>> s_cache;
        // 連続した編集(スライダーのドラッグなど)を1回の書き戻しにまとめるための待ち行列
        private static readonly HashSet<ToonStyleProfile> s_pending = new HashSet<ToonStyleProfile>();
        private static bool s_flushScheduled;

        static ToonMaterialProfileLink()
        {
            ToonStyleProfile.AnyChanged -= OnProfileChanged;
            ToonStyleProfile.AnyChanged += OnProfileChanged;
        }

        /// <summary>マテリアルに登録されている設定ファイル。未登録・見つからないときは null。</summary>
        public static ToonStyleProfile GetProfile(Material material)
        {
            string guid = GetProfileGuid(material);
            if (string.IsNullOrEmpty(guid))
            {
                return null;
            }
            string path = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<ToonStyleProfile>(path);
        }

        /// <summary>マテリアルに保存されている GUID。設定ファイルが消えていても GUID は残る。</summary>
        public static string GetProfileGuid(Material material)
        {
            return material == null ? string.Empty : material.GetTag(ToonStyleProfile.MaterialTagKey, false, string.Empty);
        }

        /// <summary>
        /// 設定ファイルを登録し、その値をマテリアルへ書き込む。null を渡すと登録を外し、共通設定に戻す。
        /// </summary>
        public static void Register(Material material, ToonStyleProfile profile)
        {
            if (material == null)
            {
                return;
            }

            Undo.RecordObject(material, UndoName);
            string oldGuid = GetProfileGuid(material);
            string newGuid = profile != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(profile)) : string.Empty;

            material.SetOverrideTag(ToonStyleProfile.MaterialTagKey, newGuid);
            if (profile != null)
            {
                profile.WriteTo(material);
            }
            else
            {
                // 登録を外したら、全体の見た目(Applier)に従う状態へ戻すのが自然なため両方オンにする
                material.SetFloat(UseGlobalOutlineId, GlobalToggleOn);
                material.SetFloat(UseGlobalStyleId, GlobalToggleOn);
            }

            EditorUtility.SetDirty(material);
            // 紐づけ一覧はディスク上のタグから作り直すため、登録はすぐ保存しておく
            AssetDatabase.SaveAssetIfDirty(material);
            UpdateCache(AssetDatabase.GetAssetPath(material), oldGuid, newGuid);
        }

        private static void OnProfileChanged(ToonStyleProfile profile)
        {
            if (profile == null)
            {
                return;
            }
            s_pending.Add(profile);
            if (s_flushScheduled)
            {
                return;
            }
            s_flushScheduled = true;
            // OnValidate の中ではアセットの読み込みが制限されるため、次のエディタ更新まで遅らせる。
            // delayCall はエディタが裏にあると呼ばれないことがあるため、毎回呼ばれる update を使う
            EditorApplication.update += Flush;
        }

        private static void Flush()
        {
            EditorApplication.update -= Flush;
            s_flushScheduled = false;
            var profiles = new List<ToonStyleProfile>(s_pending);
            s_pending.Clear();

            foreach (ToonStyleProfile profile in profiles)
            {
                if (profile == null)
                {
                    continue;
                }
                string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(profile));
                if (string.IsNullOrEmpty(guid))
                {
                    continue;
                }
                foreach (Material material in FindMaterials(guid))
                {
                    if (profile.WriteTo(material))
                    {
                        EditorUtility.SetDirty(material);
                    }
                }
            }
        }

        private static IEnumerable<Material> FindMaterials(string profileGuid)
        {
            EnsureCache();
            if (!s_cache.TryGetValue(profileGuid, out HashSet<string> paths))
            {
                yield break;
            }
            foreach (string path in paths)
            {
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                // 一覧作成後にタグが変わっている場合があるため、実物で確かめてから書く
                if (material != null && GetProfileGuid(material) == profileGuid)
                {
                    yield return material;
                }
            }
        }

        private static void EnsureCache()
        {
            if (s_cache != null)
            {
                return;
            }
            s_cache = new Dictionary<string, HashSet<string>>();
            foreach (string guid in AssetDatabase.FindAssets(MaterialSearchFilter))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                // 全マテリアルを読み込むと重いため、ファイルにタグ名が無いものは読まずに飛ばす
                if (!path.EndsWith(".mat") || !File.Exists(path) || !File.ReadAllText(path).Contains(ToonStyleProfile.MaterialTagKey))
                {
                    continue;
                }
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                AddToCache(path, GetProfileGuid(material));
            }
        }

        private static void UpdateCache(string path, string oldGuid, string newGuid)
        {
            if (s_cache == null)
            {
                return;
            }
            if (!string.IsNullOrEmpty(oldGuid) && s_cache.TryGetValue(oldGuid, out HashSet<string> oldPaths))
            {
                oldPaths.Remove(path);
            }
            AddToCache(path, newGuid);
        }

        private static void AddToCache(string path, string profileGuid)
        {
            if (string.IsNullOrEmpty(profileGuid))
            {
                return;
            }
            if (!s_cache.TryGetValue(profileGuid, out HashSet<string> paths))
            {
                paths = new HashSet<string>();
                s_cache.Add(profileGuid, paths);
            }
            paths.Add(path);
        }

        /// <summary>一覧を捨て、次に必要になったとき作り直させる。</summary>
        internal static void InvalidateCache()
        {
            s_cache = null;
        }

        // マテリアルの追加・削除・移動・外部での変更(git pull など)があったら一覧を作り直す
        private sealed class MaterialPostprocessor : AssetPostprocessor
        {
            private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
            {
                if (ContainsMaterial(imported) || ContainsMaterial(deleted) || ContainsMaterial(moved))
                {
                    InvalidateCache();
                }
            }

            private static bool ContainsMaterial(string[] paths)
            {
                foreach (string path in paths)
                {
                    if (path.EndsWith(".mat"))
                    {
                        return true;
                    }
                }
                return false;
            }
        }
    }
}
