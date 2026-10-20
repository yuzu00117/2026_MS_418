using Aura.Inspector;
using UnityEngine;

namespace Aura.ObjectInfoDemo
{
    /// @brief ReactionTrigger の状態をレンダラーの色で見せる（任意で付ける見た目の部品）
    /// 溜まるほど Charged Color に近づき、起動で Active Color、反応しない種類のビームで Rejected Color になる
    public sealed class ReactionFeedback : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");  ///< URP のシェーダーの色
        private static readonly int ColorId = Shader.PropertyToID("_Color");          ///< Built-in のシェーダーの色

        [InspectorLabel("対象の受け手")]
        [Tooltip("状態を色で見せる ReactionTrigger。空なら、この GameObject か親から探す")]
        [SerializeField] private ReactionTrigger _trigger;                     ///< 状態を見る受け手。空なら親から探す

        [InspectorLabel("色を変えるレンダラー")]
        [Tooltip("色を変えるレンダラー。マテリアルの色（_BaseColor か _Color）を上書きする\n" +
                 "空なら、この GameObject と子のレンダラーすべての色を変える")]
        [SerializeField] private Renderer[] _renderers;                        ///< 色を変えるレンダラー。空なら子から探す

        [InspectorLabel("溜まる前の色")]
        [Tooltip("何も溜まっていないときの色。溜まるほど「溜まりきる直前の色」に近づく")]
        [SerializeField] private Color _idleColor = new Color(0.35f, 0.35f, 0.38f);  ///< 何も受けていないときの色

        [InspectorLabel("溜まりきる直前の色")]
        [Tooltip("溜まりきる直前の色。溜まり具合に合わせて、「溜まる前の色」からこの色へ変わっていく")]
        [SerializeField] private Color _chargedColor = new Color(1.0f, 0.85f, 0.3f); ///< 溜まりきる直前の色

        [InspectorLabel("起動中の色")]
        [Tooltip("起動している間の色")]
        [SerializeField] private Color _activeColor = new Color(0.3f, 1.0f, 0.45f);  ///< 起動しているときの色

        [InspectorLabel("違う種類のときの色")]
        [Tooltip("反応しない種類のビームが当たっている間の色。外してから「違う種類の色の時間（秒）」で元に戻る。起動している間でも、この色が優先される")]
        [SerializeField] private Color _rejectedColor = new Color(1.0f, 0.2f, 0.2f); ///< 違う種類のビームが当たったときの色

        [InspectorLabel("違う種類の色の時間（秒）")]
        [Tooltip("反応しない種類のビームが最後に当たってから、「違う種類のときの色」を出し続ける時間（秒）。0 なら出さない")]
        [SerializeField, Min(0.0f)] private float _rejectedFlashTime = 0.2f;  ///< 違う種類の色を出す時間（秒）

        private MaterialPropertyBlock _propertyBlock;
        private float _rejectedUntil;  ///< 違う種類の色を出し終える時刻

        private void Awake()
        {
            if (_trigger == null)
            {
                _trigger = GetComponentInParent<ReactionTrigger>();
            }
            if (_renderers == null || _renderers.Length == 0)
            {
                _renderers = GetComponentsInChildren<Renderer>();
            }
            _propertyBlock = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            if (_trigger != null)
            {
                _trigger.BeamRejected += HandleBeamRejected;
            }
        }

        private void OnDisable()
        {
            if (_trigger != null)
            {
                _trigger.BeamRejected -= HandleBeamRejected;
            }
        }

        private void LateUpdate()
        {
            if (_trigger == null)
            {
                return;
            }

            Color color;
            if (Time.time < _rejectedUntil)
            {
                color = _rejectedColor;
            }
            else if (_trigger.IsActive)
            {
                color = _activeColor;
            }
            else
            {
                color = Color.Lerp(_idleColor, _chargedColor, _trigger.ChargeRatio);
            }
            ApplyColor(color);
        }

        private void HandleBeamRejected(InfoBeamHit hit)
        {
            _rejectedUntil = Time.time + _rejectedFlashTime;
        }

        private void ApplyColor(Color color)
        {
            foreach (Renderer target in _renderers)
            {
                if (target == null)
                {
                    continue;
                }
                target.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor(BaseColorId, color);
                _propertyBlock.SetColor(ColorId, color);
                target.SetPropertyBlock(_propertyBlock);
            }
        }
    }
}
