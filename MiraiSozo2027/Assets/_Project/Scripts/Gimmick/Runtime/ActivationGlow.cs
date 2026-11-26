using Aura.Inspector;
using UnityEngine;

namespace Aura.Gimmick
{
    /// @brief ActivationCounter が数えた数に応じて、見た目を段階的に光らせるギミック（タレットの波が当たった本数で光るクリスタルなど）
    /// 明るさは「数えた数 ÷ 必要な数」で決まり、そろったときに「そろったときの明るさ」になる
    /// 光らせる見た目のマテリアルは、Emission を ON にしておく（URP の Lit なら Emission にチェック、色は黒でよい）
    [DisallowMultipleComponent]
    public sealed class ActivationGlow : MonoBehaviour
    {
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");  ///< URP の Lit の発光色

        [InspectorLabel("数える部品")]
        [Tooltip("明るさを決める ActivationCounter。空なら、この GameObject に付いたものを使う")]
        [SerializeField] private ActivationCounter _counter;  ///< 明るさを決める ActivationCounter

        [InspectorLabel("光らせる見た目")]
        [Tooltip("発光色を変える Renderer。マテリアルの Emission を ON にしておく")]
        [SerializeField] private Renderer[] _renderers = new Renderer[0];  ///< 発光色を変える Renderer

        [InspectorLabel("光の色")]
        [Tooltip("発光の色。明るさはこの色に「明るさ」を掛けて決まる")]
        [SerializeField, ColorUsage(false, true)] private Color _glowColor = new Color(0.4f, 0.9f, 1.0f);  ///< 発光の色

        [InspectorLabel("0 のときの明るさ")]
        [Tooltip("1 つも数えていないときの明るさ。0 なら光らない")]
        [SerializeField, Min(0.0f)] private float _minIntensity = 0.1f;  ///< 1 つも数えていないときの明るさ

        [InspectorLabel("そろったときの明るさ")]
        [Tooltip("「必要な数」まで数えたときの明るさ。間は数に比例して段階的に明るくなる")]
        [SerializeField, Min(0.0f)] private float _maxIntensity = 6.0f;  ///< そろったときの明るさ

        [InspectorLabel("一緒に明るくする光")]
        [Tooltip("数に応じて強さを変える Light（空なら使わない）。0 のとき 0、そろったとき「光の最大の強さ」になる")]
        [SerializeField] private Light _light;  ///< 数に応じて強さを変える Light

        [InspectorLabel("光の最大の強さ")]
        [Tooltip("そろったときの Light の強さ")]
        [SerializeField, Min(0.0f)] private float _maxLightIntensity = 30.0f;  ///< そろったときの Light の強さ

        [InspectorLabel("明るさが変わる速さ")]
        [Tooltip("1 秒あたりに、0 からそろったときまでの何割ずつ明るさを変えるか。1 なら 1 秒で全部変わる")]
        [SerializeField, Min(0.01f)] private float _changeRate = 0.5f;  ///< 1 秒あたりに変える割合

        private MaterialPropertyBlock _block;
        private float _ratio = -1.0f;  ///< 今の明るさの割合（0〜1）。-1 はまだ塗っていない

        /// @brief 今の明るさの割合（0〜1）。数えた数に向かって少しずつ変わる
        public float Ratio => Mathf.Max(0.0f, _ratio);

        /// @brief 数えた数から決まる、目指す明るさの割合（0〜1）
        public float TargetRatio
        {
            get
            {
                if (_counter == null || _counter.RequiredCount <= 0)
                {
                    return 0.0f;
                }
                return Mathf.Clamp01((float)_counter.Count / _counter.RequiredCount);
            }
        }

        private void Awake()
        {
            if (_counter == null)
            {
                _counter = GetComponent<ActivationCounter>();
            }
            _block = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            _ratio = -1.0f;
        }

        private void Update()
        {
            float target = TargetRatio;
            float next = _ratio < 0.0f ? target : Mathf.MoveTowards(_ratio, target, _changeRate * Time.deltaTime);
            if (Mathf.Approximately(next, _ratio))
            {
                return;
            }
            _ratio = next;
            Apply(_ratio);
        }

        private void Apply(float ratio)
        {
            Color emission = _glowColor * Mathf.Lerp(_minIntensity, _maxIntensity, ratio);
            for (int i = 0; i < _renderers.Length; ++i)
            {
                Renderer target = _renderers[i];
                if (target == null)
                {
                    continue;
                }
                target.GetPropertyBlock(_block);
                _block.SetColor(EmissionColorId, emission);
                target.SetPropertyBlock(_block);
            }
            if (_light != null)
            {
                _light.intensity = _maxLightIntensity * ratio;
                _light.enabled = ratio > 0.0f;
            }
        }
    }
}
