using UnityEngine;

namespace Echo.Echolocation
{
    /// <summary>名前を変更しても参照が維持されるエコー専用分類。</summary>
    [CreateAssetMenu(menuName = "エコーロケーション/専用タグ", fileName = "SO_EchoTag")]
    public sealed class EchoTagDefinition : ScriptableObject
    {
        [SerializeField] private string _displayName;
        [SerializeField, TextArea] private string _description;
        [SerializeField] private bool _isBlocking = true;
        [SerializeField] private bool _hasAssetSettings;
        [SerializeField] private EchoTagColorRule _settings = new EchoTagColorRule();
        public bool IsBlocking { get => _isBlocking; set => _isBlocking = value; }
        public bool HasAssetSettings => _hasAssetSettings;
        public EchoTagColorRule Settings
        {
            get { _settings ??= new EchoTagColorRule(); _settings.Definition = this; return _settings; }
        }
        public void Configure(EchoTagColorRule settings, bool isBlocking)
        { _settings = settings.Copy(); _settings.Definition = this; _isBlocking = isBlocking; _hasAssetSettings = true; }
        private void OnValidate() => Settings.Validate();
        public string DisplayName => string.IsNullOrEmpty(_displayName) ? name : _displayName;
        public string Description => _description;
    }
}
