// 長押し・揺れ・有効化直後の誤操作を防ぐ、コントロールごとの状態。
namespace Echo.Echolocation
{
    /// <summary>押下と再受付を別の閾値で判定する。</summary>
    public sealed class EchoInputEdgeDetector
    {
        private bool _isArmed;
        /// <summary>現在値で初期化する。すでに押されている入力は発火させない。</summary>
        public void Synchronize(float value, float release) => _isArmed = value <= release;
        /// <summary>新しい押下の瞬間だけtrueを返す。</summary>
        public bool Evaluate(float value, float press, float release)
        {
            if (!_isArmed)
            {
                if (value <= release)
                    _isArmed = true;
                return false;
            }

            if (value < press)
                return false;
            _isArmed = false;
            return true;
        }
    }
}
