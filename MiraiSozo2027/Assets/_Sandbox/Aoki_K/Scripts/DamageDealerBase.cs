using System.Collections.Generic;
using UnityEngine;

namespace Aura.Combat
{
    /// <summary>
    /// コライダーの探索キャッシュ機能を持つダメージディーラーの抽象基底クラス
    /// </summary>
    public abstract class DamageDealerBase<TTarget> : MonoBehaviour
        where TTarget : struct, ICachedTarget
    {
        private const int MaxCacheCount = 512;

        [Header("Damage Settings")]
        [Tooltip("与える基礎ダメージ量（段階数）")]
        [SerializeField, Min(1)] protected int _damage = 1;

        [Tooltip("ダメージが反映される対象のレイヤー")]
        [SerializeField] protected LayerMask _targetLayers = ~0;

        // コライダー -> ダメージ対象のキャッシュ辞書
        private readonly Dictionary<Collider, TTarget> _targetCache = new Dictionary<Collider, TTarget>();
        // 辞書からDestroy済みのキーを削除するための一時リスト（GC対策で使いまわす）
        private readonly List<Collider> _keysToRemove = new List<Collider>();

        protected virtual void OnDisable()
        {
            _targetCache.Clear();
            _keysToRemove.Clear();
        }

        /// <summary>
        /// 対象のコライダーが攻撃対象レイヤーに含まれているか判定する
        /// </summary>
        protected bool IsTargetLayer(Collider targetCollider)
        {
            return targetCollider != null && (_targetLayers.value & (1 << targetCollider.gameObject.layer)) != 0;
        }

        /// <summary>
        /// コライダーに対応するターゲット情報を辞書から取得する（未登録なら CreateCachedTarget で探索して記憶）
        /// </summary>
        protected TTarget GetOrFindTarget(Collider hitCollider)
        {
            if (_targetCache.TryGetValue(hitCollider, out TTarget cached))
            {
                return cached;
            }

            if (_targetCache.Count >= MaxCacheCount)
            {
                RemoveDestroyedEntries();
            }

            cached = CreateCachedTarget(hitCollider);
            _targetCache[hitCollider] = cached;
            return cached;
        }

        /// <summary>
        /// 派生クラスごとに必要なコンポーネントを GetComponentInParent で探索して構造体を作る
        /// </summary>
        protected abstract TTarget CreateCachedTarget(Collider hitCollider);

        private void RemoveDestroyedEntries()
        {
            _keysToRemove.Clear();

            foreach (KeyValuePair<Collider, TTarget> pair in _targetCache)
            {
                if (pair.Key == null || pair.Value.IsTargetLost)
                {
                    _keysToRemove.Add(pair.Key);
                }
            }

            for (int i = 0; i < _keysToRemove.Count; i++)
            {
                _targetCache.Remove(_keysToRemove[i]);
            }

            _keysToRemove.Clear();

            if (_targetCache.Count >= MaxCacheCount)
            {
                _targetCache.Clear();
            }
        }
    }
}
