// エコーの発生地点から見た遮蔽。透過は距離減衰を伴わない二値の設定。
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Echo.Echolocation
{
    [Serializable]
    public sealed class EchoBlockingRule
    {
        [UnityEngine.SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("Enabled")]
        private bool _isEnabled = true;
        public bool IsEnabled { get => _isEnabled; set => _isEnabled = value; }

        [UnityEngine.SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("TagName")]
        private string _tagName = "Untagged";
        public string TagName { get => _tagName; set => _tagName = value; }

        [UnityEngine.SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("Blocks")]
        private bool _isBlocking = true;
        public bool IsBlocking { get => _isBlocking; set => _isBlocking = value; }
    }
}
