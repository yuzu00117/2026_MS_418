using Aura.Inspector;
using Aura.ObjectInfo;
using Aura.Wave;
using UnityEngine;
using UnityEngine.Events;

public enum WaveDamageMode
{
    [InspectorName("多段ヒット方式（無敵時間ごとに連続ヒット）")]
    MultiHit,

    [InspectorName("継続照射チャージ方式（一定時間当て続けるごとにヒット）")]
    ChargeHit
}

/// <summary>
/// Entity（Enemy / Player）にアタッチし、波動ビームによるアーマー破壊とダメージ方式（多段 / チャージ）を管理する（受け手側）
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Entity))]
public class WaveDamageReceiver : MonoBehaviour
{
    private const float ReceivingGraceTime = 0.1f;

    [Header("Armor Settings（アーマー設定）")]
    [Tooltip("初期状態でアーマーを持っているか（アーマーがある間はKill以外のHPダメージを無効化）")]
    [SerializeField] private bool _hasInitialArmor = false;

    [Tooltip("電磁波の継続照射でアーマーを剥がせるか")]
    [SerializeField, ShowWaveTypeAsset(WaveKind.Electromagnetic)] private bool _armorReactElectromagnetic = false;

    [Tooltip("超音波の継続照射でアーマーを剥がせるか")]
    [SerializeField, ShowWaveTypeAsset(WaveKind.Ultrasound)] private bool _armorReactUltrasound = true;

    [Tooltip("アーマーを剥がすのに必要な継続照射秒数（強さ1のビーム基準）")]
    [SerializeField, Min(0.1f)] private float _armorRequiredSeconds = 2.0f;

    [Tooltip("ビームを当てていない間にアーマーの蓄積値が1秒あたりに減衰する量")]
    [SerializeField, Min(0.0f)] private float _armorDecayPerSecond = 0.5f;

    [Tooltip("アーマーが剥がれたときに呼ばれるイベント（アーマーの見た目非表示やエフェクト再生など）")]
    [SerializeField, FilteredEventMenu] private UnityEvent _onArmorBroken = new UnityEvent();

    [Header("HP Damage Settings（HPダメージ設定）")]
    [Tooltip("HPへのダメージの入り方（多段ヒット方式 / 継続照射チャージ方式）")]
    [SerializeField] private WaveDamageMode _damageMode = WaveDamageMode.MultiHit;

    [Tooltip("電磁波でHPにダメージを与えられるか")]
    [SerializeField, ShowWaveTypeAsset(WaveKind.Electromagnetic)] private bool _hpReactElectromagnetic = true;

    [Tooltip("超音波でHPにダメージを与えられるか")]
    [SerializeField, ShowWaveTypeAsset(WaveKind.Ultrasound)] private bool _hpReactUltrasound = true;

    [Tooltip("【継続照射チャージ方式のみ】1回ダメージが入るまでに必要な照射秒数")]
    [SerializeField, Min(0.1f)] private float _damageRequiredSeconds = 1.0f;

    [Tooltip("【継続照射チャージ方式のみ】ビームを当てていない間にダメージ蓄積値が1秒あたりに減衰する量")]
    [SerializeField, Min(0.0f)] private float _damageDecayPerSecond = 0.5f;

    private Entity _entity;
    private IArmored _armored;
    private bool HasArmor => _armored != null && _armored.IsArmored;

    private WaveReaction _waveReaction;

    private float _armorCharge;
    private float _damageCharge;
    private float _lastReceiveTime = float.NegativeInfinity;

    /// <summary>
    /// アーマー破壊までのチャージ割合（0〜1）。UIや演出用
    /// </summary>
    public float ArmorChargeRatio => _entity != null && HasArmor && _armorRequiredSeconds > 0f
        ? Mathf.Clamp01(_armorCharge / _armorRequiredSeconds)
        : 0f;

    /// <summary>
    /// 継続照射チャージダメージのチャージ割合（0〜1）。UIや演出用
    /// </summary>
    public float DamageChargeRatio => _damageMode == WaveDamageMode.ChargeHit && _damageRequiredSeconds > 0f
        ? Mathf.Clamp01(_damageCharge / _damageRequiredSeconds)
        : 0f;

    private bool IsReceiving => Time.time - _lastReceiveTime <= ReceivingGraceTime;

    private void Awake()
    {
        _entity = GetComponent<Entity>();
        TryGetComponent(out _armored);
        TryGetComponent(out _waveReaction);

        _armored?.SetArmored(_hasInitialArmor);
        SyncWaveReaction();
    }

    private void Update()
    {
        if (_entity == null || _entity.IsDead || IsReceiving) return;

        // 照射されていない間はチャージを減衰させる
        if (HasArmor)
        {
            _armorCharge = Mathf.Max(0f, _armorCharge - _armorDecayPerSecond * Time.deltaTime);
        }
        else if (_damageMode == WaveDamageMode.ChargeHit)
        {
            _damageCharge = Mathf.Max(0f, _damageCharge - _damageDecayPerSecond * Time.deltaTime);
        }
    }

    /// <summary>
    /// WaveDamageDealer から毎フレーム呼ばれる波動ヒット処理
    /// </summary>
    public void ReceiveWaveHit(in InfoBeamHit hit, int baseDamage)
    {
        if (_entity == null || _entity.IsDead) return;
        if (!WaveTypeInfo.TryGetKind(hit.Type, out WaveKind waveKind)) return;

        // フェーズ1：アーマー中
        if (HasArmor)
        {
            if (!CanArmorReactTo(waveKind)) return;

            _lastReceiveTime = Time.time;
            _armorCharge += hit.Energy;

            if (_armorCharge >= _armorRequiredSeconds)
            {
                BreakArmor();
            }
            return;
        }

        // フェーズ2：HPダメージ（アーマー無し or アーマー破壊後）
        if (!CanHpReactTo(waveKind)) return;

        _lastReceiveTime = Time.time;
        int finalDamage = Mathf.Max(1, Mathf.RoundToInt(baseDamage * hit.Multiplier));

        if (_damageMode == WaveDamageMode.MultiHit)
        {
            // 多段ヒット方式：Entity側の無敵時間（_invincibleTime）ごとに自動でダメージが入る
            _entity.TakeDamage(finalDamage);
        }
        else if (_damageMode == WaveDamageMode.ChargeHit)
        {
            // 継続照射チャージ方式：指定秒数溜まるごとに1回ダメージを与える
            _damageCharge = Mathf.Min(_damageCharge + hit.Energy, _damageRequiredSeconds);
            if (_damageCharge >= _damageRequiredSeconds && !_entity.IsInvincible)
            {
                _damageCharge = 0f;
                _entity.TakeDamage(finalDamage);
            }
        }
    }

    /// <summary>
    /// アーマーを破壊する（外部やUnityEventからも呼び出し可能）
    /// </summary>
    [ShowInEventMenu]
    public void BreakArmor()
    {
        if (_entity == null || !HasArmor) return;

        _armorCharge = 0f;
        _armored.SetArmored(false);
        SyncWaveReaction();
        _onArmorBroken?.Invoke();
    }

    private bool CanArmorReactTo(WaveKind kind)
    {
        return kind == WaveKind.Ultrasound ? _armorReactUltrasound : _armorReactElectromagnetic;
    }

    private bool CanHpReactTo(WaveKind kind)
    {
        return kind == WaveKind.Ultrasound ? _hpReactUltrasound : _hpReactElectromagnetic;
    }

    /// <summary>
    /// 同じオブジェクトに WaveReaction が付いている場合、現在のフェーズ（アーマー中 / 本体）に合わせて反応属性を同期する
    /// </summary>
    private void SyncWaveReaction()
    {
        if (_waveReaction == null) return;

        if (HasArmor)
        {
            _waveReaction.CanReactToElectromagnetic = _armorReactElectromagnetic;
            _waveReaction.CanReactToUltrasound = _armorReactUltrasound;
        }
        else
        {
            _waveReaction.CanReactToElectromagnetic = _hpReactElectromagnetic;
            _waveReaction.CanReactToUltrasound = _hpReactUltrasound;
        }
    }
}