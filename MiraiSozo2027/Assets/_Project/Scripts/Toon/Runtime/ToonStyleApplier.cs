using UnityEngine;

namespace MS.Toon
{
    /// <summary>
    /// シーン単位で共通設定(ToonProjectSettings の既定)を上書きする。
    /// シーンに1つ置く。無効にする・外すと、プロジェクトの共通設定に戻る。エディット中もプロファイル変更が即座に Scene ビューへ反映される。
    /// 個別の設定ファイルが登録されたマテリアルは、そのファイルで上書きしていない項目だけこの値に従う。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class ToonStyleApplier : MonoBehaviour
    {
        [Tooltip("現在適用するプロファイル。未設定ならプロジェクトの共通設定を使う。個別の設定ファイルが登録されたマテリアルは、上書きしていない項目だけこの設定に従う")]
        [SerializeField] private ToonStyleProfile _profile;

        /// <summary>現在有効な1つ。マテリアルのインスペクタが共通値を表示するために参照する。</summary>
        public static ToonStyleApplier Active { get; private set; }

        public ToonStyleProfile Profile => _profile;

        /// <summary>有効だが Active ではない(=別の Applier が先に有効化されている)ときに true。</summary>
        public bool IsDuplicate => isActiveAndEnabled && IsActiveExistingOther();

        private void OnEnable()
        {
            // コンパイル後は static が初期化されるので、OnEnable の再実行で Active が復元される
            if (IsActiveExistingOther())
            {
                Debug.LogWarning($"ToonStyleApplier が重複しています: '{name}' は反映されません。シーン内の Applier は1つにしてください。", this);
                return;
            }
            Active = this;
            Apply();
        }

        private void OnDisable()
        {
            // 重複側が反映し直すと Active の設定まで巻き込むため、触らない
            if (!IsActive())
            {
                return;
            }
            // 先に Active を外してから反映し直すと、プロジェクトの共通設定に戻る
            Active = null;
            ToonProjectSettings.RefreshGlobals();
        }

        private void OnValidate()
        {
            if (isActiveAndEnabled && IsActive())
            {
                Apply();
            }
        }

        // Unity の == で比較し、破棄済みオブジェクトを指す static 参照を null として扱う
        private bool IsActiveExistingOther()
        {
            return Active != null && Active != this;
        }

        private bool IsActive()
        {
            return Active == this;
        }

        public void SetProfile(ToonStyleProfile profile)
        {
            _profile = profile;
            Apply();
        }

        private void Apply()
        {
            if (!IsActive())
            {
                return;
            }
            // Profile が未設定ならプロジェクトの共通設定が使われる(判定は ToonProjectSettings にまとめてある)
            ToonProjectSettings.RefreshGlobals();
        }
    }
}
