using System;
using UnityEngine;

namespace Aura.PlayerControl
{
    /// <summary>同時に使えない2つの能力の組み合わせ。どちらかを使っている間は、もう片方を始められない</summary>
    [Serializable]
    public struct PlayerAbilityExclusion
    {
        [SerializeField] private PlayerAbility _first;
        [SerializeField] private PlayerAbility _second;

        public PlayerAbilityExclusion(PlayerAbility first, PlayerAbility second)
        {
            _first = first;
            _second = second;
        }

        public PlayerAbility First => _first;
        public PlayerAbility Second => _second;

        /// <summary>指定した能力がこの組み合わせに入っていれば、相手の能力を返す</summary>
        public bool TryGetPartner(PlayerAbility ability, out PlayerAbility partner)
        {
            partner = ability == _first ? _second : _first;
            // 同じ能力どうしの組み合わせは意味がないため、相手なしとして扱う
            return (ability == _first || ability == _second) && _first != _second;
        }
    }
}
