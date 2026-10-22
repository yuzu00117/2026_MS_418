using UnityEngine;

namespace MiraiSozo.Options
{
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

        private void OnEnable()
        {
            GameOptions.InstanceChanged += Connect;
            Connect(GameOptions.Instance);
        }

        private void Connect(GameOptions options)
        {
            if (_options != null) _options.AudioChanged -= Apply;
            _options = options;
            if (_options != null) _options.AudioChanged += Apply;
            Apply();
        }

        private void OnDisable()
        {
            GameOptions.InstanceChanged -= Connect;
            if (_options != null) _options.AudioChanged -= Apply;
            _options = null;
            if (_source != null) _source.volume = _baseVolume;
        }

        public void Apply()
        {
            if (_source == null) return;
            if (_options == null) { _source.volume = _baseVolume; return; }
            var values = _options.Values;
            float volume = _category == OptionAudioCategory.BGM ? values.bgmVolume :
                _category == OptionAudioCategory.Voice ? values.voiceVolume : values.seVolume;
            _source.volume = _baseVolume * volume;
        }
    }
}
