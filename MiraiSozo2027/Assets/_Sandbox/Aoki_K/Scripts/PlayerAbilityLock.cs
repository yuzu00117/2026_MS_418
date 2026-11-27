using System.Collections.Generic;
using Aura.Echolocation;
using Aura.Kinesis;
using Aura.Wave;
using UnityEngine;

namespace Aura.PlayerControl
{
    /// <summary>
    /// プレイヤーの能力どうしを同時に使えないようにする。
    /// 組み合わせは Inspector の一覧で変えられ、どちらかを使っている間はもう片方を始められない（押しても無視する）
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerAbilityLock : MonoBehaviour
    {
        [Tooltip("同時に使えない能力の組み合わせ。どちらかを使っている間は、もう片方を始められない")]
        [SerializeField]
        private List<PlayerAbilityExclusion> _exclusions = new List<PlayerAbilityExclusion>
        {
            new PlayerAbilityExclusion(PlayerAbility.Echolocation, PlayerAbility.Phononkinesis),
            new PlayerAbilityExclusion(PlayerAbility.Echolocation, PlayerAbility.Wave)
        };

        private EchoController _echoController;
        private Phononkinesis _phononkinesis;
        private WaveEmitter _waveEmitter;

        /// <summary>同時に使えない能力の組み合わせ。中身は書き換えられる</summary>
        public List<PlayerAbilityExclusion> Exclusions => _exclusions;

        private void Awake()
        {
            _echoController = GetComponent<EchoController>();
            _phononkinesis = GetComponent<Phononkinesis>();
            _waveEmitter = GetComponent<WaveEmitter>();
        }

        /// <summary>指定した能力を今始められるか。同時に使えない相手の能力を使っている間は false</summary>
        public bool CanUse(PlayerAbility ability)
        {
            foreach (PlayerAbilityExclusion exclusion in _exclusions)
            {
                if (exclusion.TryGetPartner(ability, out PlayerAbility partner) && IsInUse(partner))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 指定した能力を使っているか。持っていない能力は使っていない扱いにする。
        /// 能力を増やす時は、ここに「使用中」の判定を足す
        /// </summary>
        public bool IsInUse(PlayerAbility ability)
        {
            switch (ability)
            {
                // ONの間だけ。OFF後に消えていく波が残っていても、ほかの能力は使える
                case PlayerAbility.Echolocation:
                    return _echoController != null && _echoController.IsScanRequested;
                // 対象を探している間と、掴んでいる間
                case PlayerAbility.Phononkinesis:
                    return _phononkinesis != null && _phononkinesis.IsActive;
                // 発射ボタンを押している間だけ。扇で撃った波が飛んでいても、離せばほかの能力は使える
                case PlayerAbility.Wave:
                    return _waveEmitter != null && _waveEmitter.IsFiring;
                default:
                    return false;
            }
        }
    }
}
