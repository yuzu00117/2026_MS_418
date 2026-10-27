using Aura.Inspector;
using UnityEngine;

namespace Aura.ObjectInfo
{
    /// @brief 素材の種類（金属、コンクリート、ガラスなど）。シートの O6「波を跳ね返すか止めるか」と O7「エコーをさえぎるか通すか」
    /// 物に付けた Substance が、これを選ぶ。値を直すと、この素材を選んだ物すべてに効く
    [CreateAssetMenu(fileName = "SO_Substance_New", menuName = "Aura/Object Info/Substance Type")]
    public sealed class SubstanceType : ScriptableObject
    {
        [InspectorLabel("画面に出す名前")]
        [Tooltip("画面（HUD など）に出す名前。空なら、このアセットのファイル名を使う")]
        [SerializeField] private string _displayName;                ///< 画面に出す名前。空ならアセットの名前

        [InspectorLabel("当たった波の扱い")]
        [Tooltip("波（ビーム）が当たったときの扱い。波の種類によらず同じ\n" +
                 "止める: 止める（コンクリート、吸音材など）\n" +
                 "跳ね返す: 跳ね返す（鏡、金属など）\n" +
                 "通す: 波を当てたまま、向こうへ真っすぐ進む（電線など）。扇でも、この物は向こうの物をさえぎらない")]
        [SerializeField] private SubstanceWaveResponse _waveResponse; ///< 波が当たったときの扱い

        [InspectorLabel("エコーの扱い")]
        [Tooltip("エコーロケーションの扱い\n" +
                 "さえぎる: さえぎる。この物の向こうは見つけられない（壁など）\n" +
                 "通す: 通す。この物の向こうも見つけられる（ガラスなど）")]
        [SerializeField] private SubstanceEchoResponse _echoResponse; ///< エコーロケーションの扱い

        /// @brief 画面に出す名前
        public string DisplayName => string.IsNullOrEmpty(_displayName) ? name : _displayName;

        /// @brief 波が当たったときの扱い
        public SubstanceWaveResponse WaveResponse => _waveResponse;

        /// @brief エコーロケーションの扱い
        public SubstanceEchoResponse EchoResponse => _echoResponse;

        /// @brief 波を跳ね返すか
        public bool IsWaveReflective => _waveResponse == SubstanceWaveResponse.Reflect;

        /// @brief 波を通すか（当てたまま、向こうへ真っすぐ進む）
        public bool IsWavePassThrough => _waveResponse == SubstanceWaveResponse.PassThrough;

        /// @brief エコーをさえぎるか
        public bool IsEchoBlocking => _echoResponse == SubstanceEchoResponse.Block;
    }
}
