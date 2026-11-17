using System.Collections.Generic;
using Aura.Wave;
using UnityEngine;

/// <summary>
/// WaveEmitter と同じオブジェクトにアタッチし、ビームが当たった対象へダメージを与える（撃ち手側）
/// WaveEmitter.LateUpdate の直後に実行されるよう DefaultExecutionOrder を設定
/// </summary>
[RequireComponent(typeof(WaveEmitter))]
[DefaultExecutionOrder(100)]
public class WaveDamageDealer : MonoBehaviour
{
    [Header("Wave Damage Settings")]
    [Tooltip("ビーム1ヒットあたりの基礎ダメージ量（弱点に当たった場合は WeakPoint の倍率が掛かります）")]
    [SerializeField, Min(1)] private int _baseDamage = 10;

    [Tooltip("ダメージが反映される対象のレイヤー")]
    [SerializeField] private LayerMask _targetLayers = ~0;

    private WaveEmitter _waveEmitter;

    // 1フレーム内で同じ対象（複数コライダーや反射ビーム）に重複して処理しないためのセット
    private readonly HashSet<WaveDamageReceiver> _processedReceivers = new HashSet<WaveDamageReceiver>();
    private readonly HashSet<IDamageable> _processedDamageables = new HashSet<IDamageable>();

    private void Awake()
    {
        _waveEmitter = GetComponent<WaveEmitter>();
    }

    private void LateUpdate()
    {
        if (_waveEmitter == null) return;

        IReadOnlyList<InfoBeamHit> hits = _waveEmitter.LastHits;
        if (hits == null || hits.Count == 0) return;

        _processedReceivers.Clear();
        _processedDamageables.Clear();

        for (int i = 0; i < hits.Count; i++)
        {
            InfoBeamHit hit = hits[i];
            if (hit.Collider == null) continue;

            // 対象レイヤーに含まれていなければスキップ
            if ((_targetLayers.value & (1 << hit.Collider.gameObject.layer)) == 0) continue;

            // 1. 受け手側に WaveDamageReceiver（アーマー管理・多段/チャージ方式管理）がある場合
            WaveDamageReceiver receiver = hit.Collider.GetComponentInParent<WaveDamageReceiver>();
            if (receiver != null)
            {
                if (_processedReceivers.Add(receiver))
                {
                    receiver.ReceiveWaveHit(in hit, _baseDamage);
                }
                continue;
            }

            // 2. WaveDamageReceiver が無く、IDamageable（Playerや通常オブジェクト）のみの場合
            IDamageable damageable = hit.Collider.GetComponentInParent<IDamageable>();
            if (damageable != null && !damageable.IsDead)
            {
                if (_processedDamageables.Add(damageable))
                {
                    int finalDamage = Mathf.Max(1, Mathf.RoundToInt(_baseDamage * hit.Multiplier));
                    damageable.TakeDamage(finalDamage);
                }
            }
        }
    }
}