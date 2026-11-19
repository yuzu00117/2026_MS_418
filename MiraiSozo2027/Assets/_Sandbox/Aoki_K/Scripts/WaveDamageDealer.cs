using System.Collections.Generic;
using Aura.Wave;
using UnityEngine;

/// <summary>
/// WaveEmitter と同じオブジェクトにアタッチし、ビームが当たった対象へダメージを与える（撃ち手側）
/// </summary>
[RequireComponent(typeof(WaveEmitter))]
[DefaultExecutionOrder(100)]
public class WaveDamageDealer : DamageDealerBase<CachedWaveTarget>
{
    private WaveEmitter _waveEmitter;

    // 1フレーム内で同じ対象（複数コライダーや反射ビーム）に重複して処理しないためのセット
    private readonly HashSet<WaveDamageReceiver> _processedReceivers = new HashSet<WaveDamageReceiver>();
    private readonly HashSet<IDamageable> _processedDamageables = new HashSet<IDamageable>();

    private void Awake()
    {
        _waveEmitter = GetComponent<WaveEmitter>();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        _processedReceivers.Clear();
        _processedDamageables.Clear();
    }

    protected override CachedWaveTarget CreateCachedTarget(Collider hitCollider)
    {
        WaveDamageReceiver receiver = hitCollider.GetComponentInParent<WaveDamageReceiver>();
        IDamageable damageable = receiver == null
            ? hitCollider.GetComponentInParent<IDamageable>()
            : null;

        return new CachedWaveTarget(receiver, damageable);
    }

    private void LateUpdate()
    {
        if (_waveEmitter == null || !_waveEmitter.isActiveAndEnabled) return;

        IReadOnlyList<InfoBeamHit> hits = _waveEmitter.LastHits;
        if (hits == null || hits.Count == 0) return;

        _processedReceivers.Clear();
        _processedDamageables.Clear();

        for (int i = 0; i < hits.Count; i++)
        {
            InfoBeamHit hit = hits[i];
            Collider hitCollider = hit.Collider;
            if (!IsTargetLayer(hitCollider)) continue;

            CachedWaveTarget target = GetOrFindTarget(hitCollider);

            // 1. 受け手側に WaveDamageReceiver がある場合
            if (target.HasValidReceiver)
            {
                if (_processedReceivers.Add(target.Receiver))
                {
                    target.Receiver.ReceiveWaveHit(in hit, _damage);
                }
                continue;
            }

            // 2. WaveDamageReceiver が無く、IDamageable のみの場合
            if (target.HasValidDamageable)
            {
                if (_processedDamageables.Add(target.Damageable))
                {
                    int finalDamage = Mathf.Max(0, Mathf.RoundToInt(_damage * hit.Multiplier));
                    target.Damageable.TakeDamage(finalDamage);
                }
            }
        }
    }
}