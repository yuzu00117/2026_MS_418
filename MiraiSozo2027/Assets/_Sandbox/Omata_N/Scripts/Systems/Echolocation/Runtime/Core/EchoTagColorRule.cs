// Inspectorのタグ・色一覧の1行を定義する。
using System;
using UnityEngine;

namespace Echo.Echolocation
{
    /// <summary>タグ完全一致による表示色。アルファは不透明度。</summary>
    [Serializable]
    public sealed class EchoTagColorRule
    {
        [Tooltip("この行を有効にします。")]
        [UnityEngine.SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("Enabled")]
        private bool _isEnabled = true;
        public bool IsEnabled { get => _isEnabled; set => _isEnabled = value; }

        [Tooltip("対象GameObjectのタグ。子へは自動継承しません。")]
        [UnityEngine.SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("TagName")]
        private string _tagName = "Untagged";
        public string TagName { get => _tagName; set => _tagName = value; }

        [Tooltip("RGBが強調色、Aが不透明度です。")]
        [UnityEngine.SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("Color")]
        private Color _color = new Color(0, 1, 1, .65f);
        public Color Color { get => _color; set => _color = value; }
    }
}
