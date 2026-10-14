using UnityEngine;

namespace Aura.Wave
{
    /// @brief 波の種類（属性）を表すアセット。電磁波、超音波などを 1 つずつ作る
    /// 受け手の WaveReceiver は、このアセットを参照して反応する波を決める
    [CreateAssetMenu(fileName = "WaveType", menuName = "Aura/Wave/Wave Type")]
    public sealed class WaveType : ScriptableObject
    {
        [Tooltip("画面（HUD など）に出す名前。空なら、このアセットのファイル名を使う")]
        [SerializeField] private string _displayName;                   ///< 画面に出す名前。空ならアセットの名前

        [Tooltip("波の線の色。ビームは先へ行くほど半透明になる。アルファ（不透明度）も効く")]
        [SerializeField] private Color _color = Color.cyan;             ///< 波の見た目の色

        [Header("見た目（当たり判定には影響しない）")]
        [Tooltip("見た目の波の、山から山までの長さ（m）。短いほど細かく揺れる。0.01 以上")]
        [SerializeField, Min(0.01f)] private float _wavelength = 0.5f;  ///< 見た目の波長（m）

        [Tooltip("見た目の波の揺れ幅（m）。中心から片側までの幅。0 なら真っすぐな線になる")]
        [SerializeField, Min(0.0f)] private float _amplitude = 0.08f;   ///< 見た目の振幅（m）

        [Tooltip("見た目の波の模様が流れる速さ（m/s）。正なら先へ、負なら手元へ流れる。0 なら止まって見える\n" +
                 "波そのものが飛ぶ速さは、WaveEmitter の Pulse Speed で決める")]
        [SerializeField] private float _scrollSpeed = 6.0f;             ///< 見た目の波が流れる速さ（m/s）

        [Tooltip("波の線の太さ（m）")]
        [SerializeField, Min(0.001f)] private float _lineWidth = 0.04f; ///< 見た目の線の太さ（m）

        /// @brief 画面に出す名前
        public string DisplayName => string.IsNullOrEmpty(_displayName) ? name : _displayName;

        /// @brief 波の見た目の色
        public Color Color => _color;

        /// @brief 見た目の波長（m）
        public float Wavelength => _wavelength;

        /// @brief 見た目の振幅（m）
        public float Amplitude => _amplitude;

        /// @brief 見た目の波が流れる速さ（m/s）
        public float ScrollSpeed => _scrollSpeed;

        /// @brief 見た目の線の太さ（m）
        public float LineWidth => _lineWidth;
    }
}
