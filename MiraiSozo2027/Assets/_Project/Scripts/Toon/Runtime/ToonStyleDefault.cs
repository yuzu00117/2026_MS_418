using UnityEngine;

namespace MS.Toon
{
    /// <summary>
    /// シーンに ToonStyleApplier が無いときに、標準プロファイル(Resources/Toon/SO_ToonStyle_Main)を反映する。
    /// 各シーンへ Applier を置かずに済ませ、共有シーンの編集による衝突を避けるための仕組み。
    /// </summary>
    public static class ToonStyleDefault
    {
        /// <summary>Resources.Load に渡すパス。ファイルを移動・改名したらここも合わせること。</summary>
        public const string ResourcePath = "Toon/SO_ToonStyle_Main";

        private static ToonStyleProfile s_profile;
        private static ToonStyleProfile s_subscribedProfile;

        /// <summary>標準プロファイル。見つからなければ null。</summary>
        public static ToonStyleProfile Profile
        {
            get
            {
                if (s_profile == null)
                {
                    s_profile = Resources.Load<ToonStyleProfile>(ResourcePath);
                }
                return s_profile;
            }
        }

        /// <summary>有効な Applier が無く、標準プロファイルで描画しているときに true。</summary>
        public static bool IsInUse => !HasActiveApplier() && Profile != null;

        // シーン読み込み前に反映し、Applier の無いシーンでも最初のフレームから正しい見た目にする
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void OnRuntimeLoad()
        {
            ApplyIfNoApplier();
        }

#if UNITY_EDITOR
        // コンパイル直後は static ごと消えるため、エディタ起動・再コンパイルのたびに反映し直す。
        // シーン内の Applier と順番が前後しても、Applier があればこちらは何もせず、後から来た Applier は上書きするので結果は同じ。
        // delayCall はエディタが非アクティブだと走らないため、すぐに反映する
        [UnityEditor.InitializeOnLoadMethod]
        private static void OnEditorLoad()
        {
            ApplyIfNoApplier();
        }
#endif

        /// <summary>有効な Applier が無ければ標準プロファイルを反映する。Applier が外れたときにも呼ばれる。</summary>
        public static void ApplyIfNoApplier()
        {
            if (HasActiveApplier())
            {
                Unsubscribe();
                return;
            }
            ToonStyleProfile profile = Profile;
            if (profile == null)
            {
                Debug.LogWarning($"標準のトゥーン設定が見つかりません: Resources/{ResourcePath}。各マテリアルの値で描画します。");
                ToonStyleProfile.ClearGlobals();
                return;
            }
            Subscribe(profile);
            profile.ApplyToGlobals();
        }

        private static bool HasActiveApplier()
        {
            ToonStyleApplier applier = ToonStyleApplier.Active;
            return applier != null && applier.isActiveAndEnabled;
        }

        // 標準プロファイルを編集したとき、Applier が無いシーンにもすぐ反映されるようにする
        private static void Subscribe(ToonStyleProfile profile)
        {
            if (s_subscribedProfile == profile)
            {
                return;
            }
            Unsubscribe();
            profile.Changed += ApplyIfNoApplier;
            s_subscribedProfile = profile;
        }

        private static void Unsubscribe()
        {
            if (s_subscribedProfile != null)
            {
                s_subscribedProfile.Changed -= ApplyIfNoApplier;
            }
            s_subscribedProfile = null;
        }
    }
}
