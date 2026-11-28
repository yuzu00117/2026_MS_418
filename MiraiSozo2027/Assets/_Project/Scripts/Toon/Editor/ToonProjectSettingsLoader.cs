using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MS.Toon.Editor
{
    /// <summary>
    /// エディタでは Preloaded Assets が自動で読み込まれないため、起動時・スクリプト再読み込み時に
    /// 登録リストから ToonProjectSettings を探して共通設定として反映する。
    /// </summary>
    [InitializeOnLoad]
    public static class ToonProjectSettingsLoader
    {
        private const string WarnNotRegistered = "ToonProjectSettings が Player Settings の Preloaded Assets に登録されていません。トゥーンの共通設定が反映されず、各マテリアル自身の値で描画されます。SO_ToonProjectSettings を選んで「Preloaded Assets に登録」を押してください。";

        static ToonProjectSettingsLoader()
        {
            // 起動直後はアセットの読み込みが終わっていないことがあるため、1フレーム後に行う
            EditorApplication.delayCall += Load;
        }

        /// <summary>登録リストを読み直して反映する。登録・解除した直後にも呼ぶ。</summary>
        public static void Load()
        {
            ToonProjectSettings settings = FindRegistered();
            if (settings == null)
            {
                Debug.LogWarning(WarnNotRegistered);
            }
            ToonProjectSettings.Register(settings);
        }

        public static ToonProjectSettings FindRegistered()
        {
            foreach (Object asset in PlayerSettings.GetPreloadedAssets())
            {
                if (asset is ToonProjectSettings settings)
                {
                    return settings;
                }
            }
            return null;
        }

        /// <summary>Preloaded Assets に追加する。同じ型の別アセットが登録済みなら、二重に効かないよう置き換える。</summary>
        public static void RegisterToPreloaded(ToonProjectSettings settings)
        {
            List<Object> assets = new List<Object>();
            foreach (Object asset in PlayerSettings.GetPreloadedAssets())
            {
                // 削除済みの空き枠と、別の ToonProjectSettings は外す
                if (asset != null && !(asset is ToonProjectSettings))
                {
                    assets.Add(asset);
                }
            }
            assets.Add(settings);
            PlayerSettings.SetPreloadedAssets(assets.ToArray());
            AssetDatabase.SaveAssets();
            Load();
        }
    }
}
