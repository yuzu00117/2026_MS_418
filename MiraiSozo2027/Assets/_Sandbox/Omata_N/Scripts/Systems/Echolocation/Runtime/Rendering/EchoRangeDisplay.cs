// 旧シーンの互換用。範囲表示はEchoControllerの設定で行う。
using UnityEngine;

namespace Echo.Echolocation
{
    /// <summary>統合前のシーンの値を、再生中だけControllerの範囲表示へ引き継ぐ。統合ツールで削除できる。</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(EchoController)), AddComponentMenu("")]
    public sealed class EchoRangeDisplay : MonoBehaviour
    {
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("showSurface")]
        private bool _isSurfaceVisible = true;
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("showBoundary")]
        private bool _isBoundaryVisible = true;
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("fillColor")]
        private Color _fillColor = new Color(.05f, .7f, 1, .09f);
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("waveColor")]
        private Color _waveColor = new Color(.05f, .9f, 1, .85f);
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("eraseColor")]
        private Color _eraseColor = new Color(1, .45f, .08f, .85f);
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("boundaryColor")]
        private Color _boundaryColor = new Color(.1f, .8f, 1, .32f);
        [SerializeField, Min(.01f)]
        [UnityEngine.Serialization.FormerlySerializedAs("waveWidth")]
        private float _waveWidth = .3f;

        /// <summary>このコンポーネントの値をControllerの範囲表示へ書き込む。</summary>
        public void CopyTo(EchoRangeDisplaySettings settings)
        {
            settings.IsEnabled = enabled;
            settings.IsSurfaceVisible = _isSurfaceVisible;
            settings.IsBoundaryVisible = _isBoundaryVisible;
            settings.FillColor = _fillColor;
            settings.WaveColor = _waveColor;
            settings.EraseColor = _eraseColor;
            settings.BoundaryColor = _boundaryColor;
            settings.WaveWidth = _waveWidth;
            settings.Validate();
        }

        void OnEnable()
        {
            // 共有Prefabなど移行前の資産でも従来どおり表示する。編集中は保存値を書き換えない。
            if (Application.isPlaying && TryGetComponent<EchoController>(out var controller))
                CopyTo(controller.RangeDisplay);
        }

        void OnDisable()
        {
            if (Application.isPlaying && TryGetComponent<EchoController>(out var controller))
                controller.RangeDisplay.IsEnabled = false;
        }
    }
}
