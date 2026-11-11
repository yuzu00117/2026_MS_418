// 透明な間だけColliderを無効にする。元から無効だったColliderを有効に戻さないよう、自分が無効にしたものだけを記録する。
using System.Collections.Generic;
using UnityEngine;

namespace Echo.Echolocation
{
    /// <summary>EchoTargetが所有するColliderの一時的な無効化と復元。</summary>
    internal sealed class EchoColliderSwitch
    {
        private readonly List<Collider> _disabledColliders = new List<Collider>();
        private readonly List<Collider> _foundColliders = new List<Collider>();

        private bool _isDisabled;

        /// <summary>ownerが最も近いEchoTargetであるColliderのうち、有効なものを無効にする。</summary>
        public void Disable(EchoTarget owner)
        {
            if (_isDisabled) return;
            _isDisabled = true;
            owner.GetComponentsInChildren(true, _foundColliders);
            foreach (var collider in _foundColliders)
            {
                // 子EchoTargetの物体は、その子自身の状態で切り替える。
                if (!collider.enabled || collider.GetComponentInParent<EchoTarget>(true) != owner) continue;
                collider.enabled = false;
                _disabledColliders.Add(collider);
            }
            _foundColliders.Clear();
        }

        /// <summary>Disableで無効にしたColliderだけを有効に戻す。</summary>
        public void Restore()
        {
            if (!_isDisabled) return;
            _isDisabled = false;
            foreach (var collider in _disabledColliders)
                if (collider) collider.enabled = true;
            _disabledColliders.Clear();
        }
    }
}
