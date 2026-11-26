using System.Collections.Generic;
using UnityEngine;

namespace MiraiSozo.Options
{
    /// <summary>メニューの効果音を鳴らす。音量はオプションのSE音量に従い、鳴らす音はOptionMenuThemeで設定する。</summary>
    public sealed class OptionMenuSound : MonoBehaviour
    {
        // これより小さい振幅は無音とみなし、再生前に切り取る（約-60dB）。
        private const float SilenceThreshold = 0.001f;
        private AudioSource _source;
        // 元の音と、先頭の無音を切り取った音の対応。同じ音は一度だけ切り取る。
        private readonly Dictionary<AudioClip, AudioClip> _trimmed = new Dictionary<AudioClip, AudioClip>();

        /// <summary>
        /// 効果音用のAudioSourceを作る。メニューを閉じた後も鳴り終わるよう、
        /// 閉じると非表示になるCanvasではなく、常に有効なオブジェクトの下に置く。
        /// </summary>
        public static OptionMenuSound Create(Transform parent)
        {
            var root = new GameObject("Menu Sound");
            root.transform.SetParent(parent, false);
            var source = root.AddComponent<AudioSource>();
            source.playOnAwake = false;
            // ポーズでAudioListenerが止められても鳴らす。
            source.ignoreListenerPause = true;
            root.AddComponent<OptionAudioChannel>().Category = OptionAudioCategory.SE;
            var sound = root.AddComponent<OptionMenuSound>();
            sound._source = source;
            sound.Prepare();
            return sound;
        }

        public void PlayTabSwitch() => Play(OptionMenuTheme.Current.TabSwitchSound, 1f);

        /// <summary>メニュー・オプション画面・確認画面の開閉と、画面の切り替えで鳴らす。</summary>
        public void PlayWindow() => Play(OptionMenuTheme.Current.WindowSound, 1f);

        /// <summary>説明ウィンドウが現れたときに、ウィンドウの音を小さく鳴らす。</summary>
        public void PlayDescription() => Play(OptionMenuTheme.Current.WindowSound, OptionMenuTheme.Current.DescriptionSoundVolume);

        // 初めて鳴らすときに読み込みと切り取りで遅れないよう、作成時に準備しておく。
        private void Prepare()
        {
            var theme = OptionMenuTheme.Current;
            GetPlayableClip(theme.TabSwitchSound);
            GetPlayableClip(theme.WindowSound);
        }

        private void Play(AudioClip clip, float volume)
        {
            var playable = GetPlayableClip(clip);
            if (playable == null || _source == null) return;
            _source.PlayOneShot(playable, OptionMenuTheme.Current.SoundVolume * volume);
        }

        private AudioClip GetPlayableClip(AudioClip clip)
        {
            if (clip == null) return null;
            if (_trimmed.TryGetValue(clip, out var trimmed)) return trimmed;
            // インポート設定で事前読み込みが無効な音も、ここで読み込んでおく。
            if (clip.loadState != AudioDataLoadState.Loaded) clip.LoadAudioData();
            trimmed = TrimLeadingSilence(clip);
            _trimmed[clip] = trimmed;
            return trimmed;
        }

        // 先頭の無音を切り取った複製を作る。音のデータを読めない設定（ストリーミングなど）の場合は元の音を使う。
        private static AudioClip TrimLeadingSilence(AudioClip clip)
        {
            if (clip.loadState != AudioDataLoadState.Loaded || clip.loadType == AudioClipLoadType.Streaming) return clip;
            int channels = clip.channels;
            var data = new float[clip.samples * channels];
            if (!clip.GetData(data, 0)) return clip;
            int start = 0;
            while (start < data.Length && Mathf.Abs(data[start]) < SilenceThreshold) start++;
            int startFrame = start / channels;
            if (startFrame == 0 || startFrame >= clip.samples) return clip;
            int frames = clip.samples - startFrame;
            var trimmedData = new float[frames * channels];
            System.Array.Copy(data, startFrame * channels, trimmedData, 0, trimmedData.Length);
            var trimmed = AudioClip.Create(clip.name, frames, channels, clip.frequency, false);
            trimmed.SetData(trimmedData, 0);
            return trimmed;
        }

        private void OnDestroy()
        {
            foreach (var pair in _trimmed)
                if (pair.Value != pair.Key) Destroy(pair.Value);
            _trimmed.Clear();
        }
    }
}
