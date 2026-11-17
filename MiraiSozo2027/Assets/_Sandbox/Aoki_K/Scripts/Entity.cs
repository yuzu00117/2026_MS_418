using System;
using Aura.Inspector;
using UnityEngine;

public abstract class Entity : MonoBehaviour, IDamageable
{
    [Header("Status Settings")]
    [Tooltip("最大HP")]
    [SerializeField] protected int _maxHp = 100;
    protected int _currentHp;
    public int MaxHp => _maxHp;
    public int CurrentHp => _currentHp;
    public bool IsDead { get; private set; }

    /// <summary>
    /// イベント：HPが変化したときに呼び出される＜現在HP、最大HP＞
    /// </summary>
    public event Action<int, int> OnHpChanged;

    protected bool _isInvincible = false;
    
    [SerializeField] protected float _invincibleTime = 0.5f;
    private float _invincibleTimer = 0f;

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
    public void TakeDamage(int damage)
    {
        if(IsDead || damage <= 0 || _isInvincible) return;
        _currentHp = Mathf.Max(0, _currentHp - damage);
        OnHpChanged?.Invoke(_currentHp, _maxHp);
        OnDamaged(damage);
        if (_currentHp <= 0)
        {
            IsDead = true;
            OnDeath();
        }
        else if(_invincibleTime > 0f)
        {
            _isInvincible = true;
            _invincibleTimer = 0f;
        }
    }

    /// <summary>
    /// 即死する（IDamageable実装・ギミックのUnityEventからも呼び出し可能）
    /// </summary>
    [ShowInEventMenu]
    public void Kill()
    {
        if(IsDead) return;
        _isInvincible = false;
        TakeDamage(_currentHp);
    }

    /// <summary>
    /// イベント：ダメージを受けた際の演出等用
    /// </summary>
    protected virtual void OnDamaged(int damage = 0) { }

    /// <summary>
    /// HPが0になった際の処理（死亡演出等用）
    /// </summary>
    protected abstract void OnDeath();
}
