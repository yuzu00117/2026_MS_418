using UnityEngine;

namespace Aura.Wave
{
    /// @brief WaveReceiver の状態をレンダラーの色で見せる（任意で付ける見た目の部品）
    /// 溜まるほど Charged Color に近づき、起動で Active Color、違う種類の波で一瞬 Rejected Color になる
    public sealed class WaveReceiverFeedback : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");  ///< URP のシェーダーの色
        private static readonly int ColorId = Shader.PropertyToID("_Color");          ///< Built-in のシェーダーの色

        [Tooltip("状態を色で見せる WaveReceiver。空なら、この GameObject か親から探す")]
        [SerializeField] private WaveReceiver _receiver;                       ///< 状態を見る受け手。空なら親から探す

        [Tooltip("色を変えるレンダラー。マテリアルの色（_BaseColor か _Color）を上書きする\n" +
                 "空なら、この GameObject と子のレンダラーすべての色を変える")]
        [SerializeField] private Renderer[] _renderers;                        ///< 色を変えるレンダラー。空なら子から探す

        [Tooltip("エネルギーが 0 のときの色。溜まるほど Charged Color に近づく")]
        [SerializeField] private Color _idleColor = new Color(0.35f, 0.35f, 0.38f);  ///< 何も受けていないときの色

        [Tooltip("エネルギーが溜まりきる直前の色。溜まり具合に合わせて、Idle Color からこの色へ変わっていく")]
        [SerializeField] private Color _chargedColor = new Color(1.0f, 0.85f, 0.3f); ///< 溜まりきる直前の色

        [Tooltip("受け手が起動している間の色")]
        [SerializeField] private Color _activeColor = new Color(0.3f, 1.0f, 0.45f);  ///< 起動しているときの色

        [Tooltip("反応しない種類の波が当たったときに出す色。単発なら当たってから Rejected Flash Time の間だけ出る。ビームなら当てている間ずっと出る\n" +
                 "起動している間でも、この色が優先される")]
        [SerializeField] private Color _rejectedColor = new Color(1.0f, 0.2f, 0.2f); ///< 違う種類の波が当たったときの色

        [Tooltip("反応しない種類の波が最後に当たってから、Rejected Color を出し続ける時間（秒）。ビームは当てている間ずっと出て、外してからこの時間で元に戻る。0 なら出さない")]
        [SerializeField, Min(0.0f)] private float _rejectedFlashTime = 0.2f;  ///< 違う種類の色を出す時間（秒）

        private MaterialPropertyBlock _propertyBlock;
        private float _rejectedUntil;  ///< 違う種類の色を出し終える時刻

        private void Awake()
        {
            if (_receiver == null)
            {
                _receiver = GetComponentInParent<WaveReceiver>();
            }
            if (_renderers == null || _renderers.Length == 0)
            {
                _renderers = GetComponentsInChildren<Renderer>();
            }
            _propertyBlock = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            if (_receiver != null)
            {
                _receiver.WaveRejected += HandleWaveRejected;
            }
        }

        private void OnDisable()
        {
            if (_receiver != null)
            {
                _receiver.WaveRejected -= HandleWaveRejected;
            }
        }

        private void LateUpdate()
        {
            if (_receiver == null)
            {
                return;
            }

            Color color;
            if (Time.time < _rejectedUntil)
            {
                color = _rejectedColor;
            }
            else if (_receiver.IsActive)
            {
                color = _activeColor;
            }
            else
            {
                color = Color.Lerp(_idleColor, _chargedColor, _receiver.EnergyRatio);
            }
            ApplyColor(color);
        }

        private void HandleWaveRejected(WaveHit hit)
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
