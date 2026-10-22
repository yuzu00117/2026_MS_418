using UnityEngine;

namespace MiraiSozo.Options
{
    /// <summary>演出からPlayを呼ぶ。軽減100%なら揺れを発生させない。</summary>
    [DefaultExecutionOrder(100)]
    public sealed class OptionCameraShake : MonoBehaviour
    {
        private const float ShakeFrequency = 45f;
        private const float HorizontalFrequencyRatio = 1.13f;
        private const float VerticalFrequencyRatio = 1.71f;
        private const float DefaultStrength = 0.15f;
        private const float DefaultDuration = 0.8f;
        private const float MinimumDuration = 0.01f;
        private float _remaining;
        private float _duration;
        private float _strength;
        private Vector3 _lastOffset;

        public void Play(float strength = DefaultStrength, float duration = DefaultDuration)
        {
            _strength = Mathf.Max(0f, strength);
            _duration = _remaining = Mathf.Max(MinimumDuration, duration);
        }

        private void Update()
        {
            // 前フレームの加算を取り除いてからCameraBehaviorに基本姿勢を計算させる。
            transform.position -= _lastOffset;
            _lastOffset = Vector3.zero;
        }

        private void LateUpdate()
        {
            // メニュー中のプレビューも確認できるよう、演出だけ非スケール時間を使う。
            if (_remaining <= 0f) return;
            _remaining = Mathf.Max(0f, _remaining - Time.unscaledDeltaTime);
            float reduction = GameOptions.Instance == null ? 0f : GameOptions.Instance.Values.shakeReduction;
            float phase = Time.unscaledTime * ShakeFrequency;
            var noise = new Vector3(Mathf.Sin(phase * HorizontalFrequencyRatio), Mathf.Sin(phase * VerticalFrequencyRatio), 0f);
            _lastOffset = transform.TransformDirection(noise) * (_strength * (1f - reduction) * _remaining / _duration);
            transform.position += _lastOffset;
        }

        private void OnDisable()
        {
            transform.position -= _lastOffset;
            _lastOffset = Vector3.zero;
        }
    }
}
