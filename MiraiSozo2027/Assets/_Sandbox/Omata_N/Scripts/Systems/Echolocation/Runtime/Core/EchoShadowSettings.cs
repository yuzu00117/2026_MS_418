// エコーの発生地点から見た遮蔽。透過は距離減衰を伴わない二値の設定。
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Echo.Echolocation
{
    [Serializable]
    public sealed class EchoShadowSettings
    {
        [UnityEngine.SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("Enabled")]
        private bool _isEnabled = true;
        public bool IsEnabled { get => _isEnabled; set => _isEnabled = value; }

        [UnityEngine.SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("Layers")]
        private LayerMask _layers = ~0;
        public LayerMask Layers { get => _layers; set => _layers = value; }

        [UnityEngine.SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("DefaultBlocks")]
        private bool _isBlockingByDefault = true;
        public bool IsBlockingByDefault { get => _isBlockingByDefault; set => _isBlockingByDefault = value; }

        [Tooltip("1方向の影解像度。各発生地点で6方向を描画します。")]
        [UnityEngine.SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("Resolution")]
        private int _resolution = 512;
        public int Resolution { get => _resolution; set => _resolution = value; }

        [Range(.001f, .2f), Tooltip("自分自身の面を影と誤判定しないための距離許容値（m）。大きいと薄い壁から漏れます。")]
        [UnityEngine.SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("Bias")]
        private float _bias = .03f;
        public float Bias { get => _bias; set => _bias = value; }

        [UnityEngine.SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("Rules")]
        private List<EchoBlockingRule> _rules = new List<EchoBlockingRule>();
        public List<EchoBlockingRule> Rules { get => _rules; set => _rules = value; }

        public void Validate()
        {
            Resolution = Mathf.Clamp(Mathf.ClosestPowerOfTwo(Resolution), 128, 1024);
            Bias = float.IsFinite(Bias) ? Mathf.Clamp(Bias, .001f, .2f) : .03f;
            Rules ??= new List<EchoBlockingRule>();
        }

        /// <summary>Renderer自身のタグで照合する。重複時は有効な先頭行を優先する。</summary>
        public bool IsBlockingTag(string tag)
        {
            foreach (var rule in Rules)
                if (rule != null && rule.IsEnabled && rule.TagName == tag)
                    return rule.IsBlocking;
            return IsBlockingByDefault;
        }

        public EchoShadowSettings Copy()
        {
            var copy = (EchoShadowSettings)MemberwiseClone();
            copy.Rules = new List<EchoBlockingRule>();
            if (Rules != null)
                foreach (var r in Rules)
                    if (r != null)
                        copy.Rules.Add(new EchoBlockingRule { IsEnabled = r.IsEnabled, TagName = r.TagName, IsBlocking = r.IsBlocking });
            copy.Validate();
            return copy;
        }
    }
}
