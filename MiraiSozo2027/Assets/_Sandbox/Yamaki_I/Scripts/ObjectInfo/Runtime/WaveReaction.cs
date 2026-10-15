using System;
using System.Collections.Generic;
using Aura.Inspector;
using Aura.Wave;
using UnityEngine;

namespace Aura.ObjectInfo
{
    /// @brief 波で動く物の情報。シートの O3「反応する波の種類」と O4「動くまでに当てる時間」
    /// 反応は波の種類だけで決まり、誰が出したビーム（ギミック・敵・プレイヤー）かは見ない
    /// 動かす処理は持たない。今ある WaveReceiver（Aura.Wave）は、この情報をまだ読まない
    [DisallowMultipleComponent]
    public sealed class WaveReaction : MonoBehaviour, IObjectInfo
    {
        [InspectorLabel("反応する波の種類")]
        [Tooltip("反応する波の種類（WaveType のアセット）。電磁波だけ、超音波だけ、両方、など。ここにない種類の波には反応しない\n" +
                 "空にすると、どの波にも反応しない。「どの波にも反応する」がオンなら使わない")]
        [SerializeField] private WaveType[] _reactiveTypes;                 ///< 反応する波の種類

        [InspectorLabel("どの波にも反応する")]
        [Tooltip("オンにすると、「反応する波の種類」に関係なく、どの種類の波にも反応する")]
        [SerializeField] private bool _canReactToAnyType;                   ///< true なら種類に関係なく反応する

        [InspectorLabel("動くまでの秒数（強さ 1）")]
        [Tooltip("動くまでに溜める量。強さ 1 のビームを当て続けたときの秒数で数える。例: 3 なら、強さ 1 のビームを 3 秒当てると動く\n" +
                 "強いビームほど早く動く（強さ 2 なら半分の時間）。0 なら、当たった瞬間に動く")]
        [SerializeField, Min(0.0f)] private float _requiredSeconds = 1.0f;  ///< 動くまでに溜める量（強さ 1 のビームで当てる秒数）

        /// @brief 反応する波の種類。Can React To Any Type がオンなら使わない
        public IReadOnlyList<WaveType> ReactiveTypes => _reactiveTypes ?? Array.Empty<WaveType>();

        /// @brief 種類に関係なく、どの波にも反応するか
        public bool CanReactToAnyType => _canReactToAnyType;

        /// @brief 動くまでに溜める量（強さ 1 のビームで当てる秒数）。強いビームほど早く溜まる。0 なら当たった瞬間に動く
        public float RequiredSeconds => _requiredSeconds;

        /// @brief その種類の波に反応するか。波の種類だけで決まる
        /// @param type 当たった波の種類
        /// @return 反応するなら true
        public bool CanReactTo(WaveType type)
        {
            if (_canReactToAnyType)
            {
                return true;
            }
            return type != null && _reactiveTypes != null && Array.IndexOf(_reactiveTypes, type) >= 0;
        }

        // 有効・無効が変わると ObjectInfoLookup の結果が変わるので、覚えた結果を捨ててもらう
        // （OnEnable があるので、Inspector に有効と無効のチェックボックスも出る。無効にすると、無いものとして扱う）
        private void OnEnable()
        {
            ObjectInfoCache.MarkDirty();
        }

        private void OnDisable()
        {
            ObjectInfoCache.MarkDirty();
        }
    }
}
