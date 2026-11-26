using System;
using Aura.Inspector;
using UnityEngine;
using Aura.Combat;

namespace Aura.Entities
{
    public abstract class Entity : MonoBehaviour, IDamageable
    {
        [Header("Status Settings")]
        [Tooltip("最大HP（耐えられる被弾段階数）")]
        [SerializeField, Min(1)] protected int _maxHp = 3;
        [SerializeField] protected int _currentHp;
        public int MaxHp => _maxHp;
        public int CurrentHp => _currentHp;
        public bool IsDead { get; private set; }

        /// <summary>
        /// イベント：HPが変化したときに呼び出される＜現在HP、最大HP＞
        /// </summary>
        public event Action<int, int> OnHpChanged;

        protected bool _isInvincible = false;
        public bool IsInvincible => _isInvincible;

        [SerializeField] protected float _invincibleTime = 0.5f;
        private float _invincibleTimer = 0f;

        // ダメージ無効化フラグ（アーマー等用）
        protected virtual bool IsDamageBlocked => false;

        protected virtual void Awake()
        {
            _currentHp = _maxHp;
            IsDead = false;
        }

        protected virtual void Update()
        {
            if (_isInvincible)
            {
                _invincibleTimer += Time.deltaTime;
                if (_invincibleTimer >= _invincibleTime)
                {
                    _isInvincible = false;
                    _invincibleTimer = 0f;
                }
            }
        }


        /// <summary>
        /// ダメージを与える（IDamageable実装・ギミックのUnityEventからも呼び出し可能）
        /// </summary>
        [ShowInEventMenu]
        public void TakeDamage(int damage = 1)
        {
            if (IsDead || damage <= 0 || _isInvincible || IsDamageBlocked) return;
            _currentHp = Mathf.Max(0, _currentHp - damage);
            OnHpChanged?.Invoke(_currentHp, _maxHp);
            OnDamaged(damage);
            if (_currentHp <= 0)
            {
                IsDead = true;
                OnDeath();
            }
            else if (_invincibleTime > 0f)
            {
                _isInvincible = true;
                _invincibleTimer = 0f;
            }
        }

        /// <summary>
        /// 即死する（IDamageable実装・ギミックのUnityEventからも呼び出し可能）
        /// </summary>
        [ShowInEventMenu]
        public virtual void Kill()
        {
            if (IsDead) return;

            _isInvincible = false;
            int fatalDamage = _currentHp;
            _currentHp = 0;
            OnHpChanged?.Invoke(_currentHp, _maxHp);
            OnDamaged(fatalDamage);
            IsDead = true;
            OnDeath();
        }

        /// <summary>
        /// イベント：ダメージを受けた際の演出等用
        /// </summary>
        protected virtual void OnDamaged(int damage = 1) { }

        /// <summary>
        /// HPが0になった際の処理（死亡演出等用）
        /// </summary>
        protected abstract void OnDeath();

        #if UNITY_EDITOR
        [ContextMenu("Debug: 1ダメージ受ける (TakeDamage)")]
        private void DebugTakeDamage()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("Playモード中に実行してください。");
                return;
            }
            TakeDamage(1);
        }
        [ContextMenu("Debug: 即死する (Kill)")]
        private void DebugKill()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("Playモード中に実行してください。");
                return;
            }
            Kill();
        }
        #endif
    }
}
