using System.Collections.Generic;
using Aura.Wave;
using UnityEngine;

namespace Aura.Combat
{
    /// <summary>
    /// WaveEmitter と同じオブジェクトにアタッチし、ビームが当たった WaveDamageReceiver へ照射情報を渡す（撃ち手側）
    /// </summary>
    [RequireComponent(typeof(WaveEmitter))]
    [DefaultExecutionOrder(100)]
    public class WaveDamageDealer : DamageDealerBase<CachedWaveTarget>
    {
        private WaveEmitter _waveEmitter;

        // 1フレーム内で同じコライダー（反射ビーム等）に重複して処理しないためのセット
        private readonly HashSet<Collider> _processedColliders = new HashSet<Collider>();

        private void Awake()
        {
            _waveEmitter = GetComponent<WaveEmitter>();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            _processedColliders.Clear();
        }

        protected override CachedWaveTarget CreateCachedTarget(Collider hitCollider)
        {
            WaveDamageReceiver receiver = hitCollider.GetComponentInParent<WaveDamageReceiver>();
            return new CachedWaveTarget(receiver);
        }

        private void LateUpdate()
        {
            if (_waveEmitter == null || !_waveEmitter.isActiveAndEnabled) return;

            IReadOnlyList<InfoBeamHit> hits = _waveEmitter.LastHits;
            if (hits == null || hits.Count == 0) return;

            _processedColliders.Clear();

            for (int i = 0; i < hits.Count; i++)
            {
                InfoBeamHit hit = hits[i];
                Collider hitCollider = hit.Collider;
                if (!IsTargetLayer(hitCollider)) continue;

                // 同じフレーム内で同一コライダーへの重複ヒットをスキップ
                if (!_processedColliders.Add(hitCollider)) continue;

                CachedWaveTarget target = GetOrFindTarget(hitCollider);
                if (target.HasValidReceiver)
                {
                    target.Receiver.ReceiveWaveHit(in hit, _damage);
                }
            }
        }
    }
}
