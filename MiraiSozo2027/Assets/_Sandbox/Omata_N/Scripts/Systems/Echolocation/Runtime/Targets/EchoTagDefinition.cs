using UnityEngine;

namespace Echo.Echolocation
{
    /// <summary>名前を変更しても参照が維持されるエコー専用分類。</summary>
    [CreateAssetMenu(menuName = "Echo/Tag", fileName = "SO_EchoTag")]
    public sealed class EchoTagDefinition : ScriptableObject
    {
        [SerializeField] private string _displayName;
        [SerializeField, TextArea] private string _description;
        public string DisplayName => string.IsNullOrEmpty(_displayName) ? name : _displayName;
        public string Description => _description;
    }
}
