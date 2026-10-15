using UnityEngine;

namespace MiraiSozo.Options
{
    public enum OptionAudioCategory { SE, BGM, Voice }

    /// <summary>AudioSourceごとの元音量を保ちながら、カテゴリー音量を掛ける。</summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class OptionAudioChannel : MonoBehaviour
    {
        [SerializeField] private OptionAudioCategory _category;
        private AudioSource _source;
        private GameOptions _options;
        private float _baseVolume;
        public OptionAudioCategory Category { get => _category; set { _category = value; Apply(); } }
        public float BaseVolume { get => _baseVolume; set { _baseVolume = Mathf.Clamp01(value); Apply(); } }

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _baseVolume = _source.volume;
        }

        private void OnEnable() => Connect();
        private void Update() { if (_options == null) Connect(); }

        private void Connect()
        {
            if (_options == GameOptions.Instance) return;
            if (_options != null) _options.Changed -= Apply;
            _options = GameOptions.Instance;
            if (_options != null) _options.Changed += Apply;
            Apply();
        }

        private void OnDisable()
        {
            if (_options != null) _options.Changed -= Apply;
            _options = null;
            if (_source != null) _source.volume = _baseVolume;
        }

        public void Apply()
        {
            if (_source == null || GameOptions.Instance == null) return;
            var values = GameOptions.Instance.Values;
            float volume = _category == OptionAudioCategory.BGM ? values.bgmVolume :
                _category == OptionAudioCategory.Voice ? values.voiceVolume : values.seVolume;
            _source.volume = _baseVolume * volume;
        }
    }
}
