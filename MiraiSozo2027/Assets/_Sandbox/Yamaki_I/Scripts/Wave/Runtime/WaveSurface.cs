using Aura.Inspector;
using UnityEngine;

namespace Aura.Wave
{
    /// @brief 波が当たったときに跳ね返すか止めるかを、物ごとに決める。鏡や吸音材の壁のコライダーか、その親に付ける
    /// WaveEmitter の Reflection Mode が None のときは使わない。コンポーネントを無効にすると、無いものとして扱う
    [DisallowMultipleComponent]
    public sealed class WaveSurface : MonoBehaviour
    {
        [InspectorLabel("当たった波の扱い")]
        [Tooltip("波が当たったときの扱い。WaveEmitter の「反射のしかた」が「跳ね返らない」のときは使わない（どの物でも止まる）\n" +
                 "跳ね返す: 跳ね返す（鏡、金属の板など）。「反射のしかた」が「反射板でだけ跳ね返る」でも跳ね返る\n" +
                 "止める: 吸い込んで止める（布、吸音材など）。「反射のしかた」が「どの物でも跳ね返る」でも跳ね返らない\n" +
                 "この GameObject と子のコライダーに効く。子にも WaveSurface があれば、当たったコライダーにいちばん近いものが効く\n" +
                 "WaveReceiver と同じ GameObject に付けると、受け手は波を受けたうえで、ここの扱いで跳ね返すか止める\n" +
                 "跳ね返すかは波が当たった瞬間（受け手に渡す前）の値で決まる。受け手の「起動したときの処理」などで変えると、次に当たる波から効く\n" +
                 "コンポーネントを無効にすると、無いものとして扱う（親に WaveSurface があれば、そちらが効く）")]
        [SerializeField] private WaveSurfaceResponse _response = WaveSurfaceResponse.Reflect;  ///< 波が当たったときの扱い

        /// @brief 波が当たったときの扱い。実行中に変えてもよい（次に当たる波から効く）
        public WaveSurfaceResponse Response
        {
            get => _response;
            set => _response = value;
        }

        /// @brief 跳ね返すか。true で Reflect、false で Absorb にする
        /// UnityEvent（受け手の On Activated など）から、鏡を出したり消したりするのに使う
        public bool IsReflective
        {
            get => _response == WaveSurfaceResponse.Reflect;
            set => _response = value ? WaveSurfaceResponse.Reflect : WaveSurfaceResponse.Absorb;
        }

        // 中身はないが、これがあると Inspector に有効と無効のチェックボックスが出る
        private void OnEnable()
        {
        }
    }
}
