using UnityEngine;

namespace MS.Toon
{
    /// <summary>
    /// プロジェクト全体で使う既定のトゥーン設定。Player Settings の Preloaded Assets に登録して使う。
    /// 起動時に自動で読み込まれ、シーンに ToonStyleApplier が無くても共通設定を反映する。
    /// シーンの ToonStyleApplier(Profile 設定済み)があれば、そちらがシーン単位の上書きとして優先される。
    /// </summary>
    [CreateAssetMenu(fileName = "SO_ToonProjectSettings", menuName = "MS/Toon/Project Settings")]
    public class ToonProjectSettings : ScriptableObject
    {
        [Tooltip("プロジェクト全体の既定の見た目。シーンに ToonStyleApplier が無い、または無効なときに使われる")]
        [SerializeField] private ToonStyleProfile _defaultProfile;

        // 反映中のプロファイルの Changed 購読は static で1つだけ持ち、二重登録を防ぐ
        private static ToonStyleProfile s_subscribedProfile;

        /// <summary>登録済みの設定。実行時は Preloaded Assets の読み込み、エディタでは起動時の読み込み処理が設定する。</summary>
        public static ToonProjectSettings Instance { get; private set; }

        public ToonStyleProfile DefaultProfile => _defaultProfile;

        private void OnEnable()
        {
#if !UNITY_EDITOR
            // ビルドでは Preloaded Assets がここで読み込まれる。
            // エディタでは参照されただけの未登録アセットまで拾わないよう、登録リストから読む別処理に任せる
            Register(this);
#endif
        }

        private void OnValidate()
        {
            if (Instance == this)
            {
                RefreshGlobals();
            }
        }

        /// <summary>この設定を共通設定として使い、シェーダーへ反映し直す。null なら共通設定なしになる。</summary>
        public static void Register(ToonProjectSettings settings)
        {
            Instance = settings;
            RefreshGlobals();
        }

        /// <summary>
        /// 今使うべきプロファイルを返す。シーンの Applier(Profile 設定済み)を優先し、無ければプロジェクトの既定。
        /// </summary>
        public static ToonStyleProfile ResolveEffectiveProfile()
        {
            ToonStyleApplier applier = ToonStyleApplier.Active;
            if (applier != null && applier.isActiveAndEnabled && applier.Profile != null)
            {
                return applier.Profile;
            }
            return Instance != null ? Instance._defaultProfile : null;
        }

        /// <summary>今使うべきプロファイルをシェーダーのグローバル値へ反映する。どれも無ければ共通設定を無効にする。</summary>
        public static void RefreshGlobals()
        {
            ToonStyleProfile profile = ResolveEffectiveProfile();
            Subscribe(profile);
            if (profile != null)
            {
                profile.ApplyToGlobals();
            }
            else
            {
                ToonStyleProfile.ClearGlobals();
            }
        }

        private static void Subscribe(ToonStyleProfile profile)
        {
            if (s_subscribedProfile == profile)
            {
                return;
            }
            if (s_subscribedProfile != null)
            {
                s_subscribedProfile.Changed -= RefreshGlobals;
            }
            s_subscribedProfile = profile;
            if (profile != null)
            {
                profile.Changed += RefreshGlobals;
            }
        }
    }
}
