using System.Collections.Generic;
using Aura.Inspector;
using Aura.ObjectInfo;
using Aura.Wave;
using UnityEngine;
using UnityEngine.Events;
using Aura.Entities;

namespace Aura.Combat
{
    /// <summary>
    /// Entity（Enemy / Player）にアタッチし、波動ビームの一定時間照射によるアーマー破壊と段階ダメージを管理する（受け手側）
    /// 通常部位コライダーと弱点部位コライダーのチャージ時間はコライダーごとに別管理される
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Entity))]
    public class WaveDamageReceiver : MonoBehaviour
    {
        private const float ReceivingGraceTime = 0.1f;

        /// <summary>
        /// コライダー（通常部位・各弱点部位）ごとの照射チャージ状態
        /// </summary>
        private struct ColliderChargeState
        {
            public Collider Collider;
            public float Charge;
            public float LastReceiveTime;
        }

        [Header("Armor Settings（アーマー設定）")]
        [Tooltip("初期状態でアーマーを持っているか（アーマーがある間はKill以外のHPダメージを無効化）")]
        [SerializeField] private bool _hasInitialArmor = false;

        [Tooltip("電磁波の継続照射でアーマーを剥がせるか")]
        [SerializeField, ShowWaveTypeAsset(WaveKind.Electromagnetic)] private bool _canArmorReactToElectromagnetic = false;

        [Tooltip("超音波の継続照射でアーマーを剥がせるか")]
        [SerializeField, ShowWaveTypeAsset(WaveKind.Ultrasound)] private bool _canArmorReactToUltrasound = true;

        [Tooltip("アーマーを剥がすのに必要な継続照射秒数（強さ1のビーム基準）")]
        [SerializeField, Min(0.1f)] private float _armorRequiredSeconds = 2.0f;

        [Tooltip("ビームを当てていない間にアーマーの蓄積値が1秒あたりに減衰する量")]
        [SerializeField, Min(0.0f)] private float _armorDecayPerSecond = 0.5f;

        [Tooltip("アーマーが剥がれたときに呼ばれるイベント（アーマーの見た目非表示やエフェクト再生など）")]
        [SerializeField, FilteredEventMenu] private UnityEvent _onArmorBroken = new UnityEvent();

        [Header("HP Damage Settings（HPダメージ設定）")]
        [Tooltip("電磁波でHPにダメージを与えられるか")]
        [SerializeField, ShowWaveTypeAsset(WaveKind.Electromagnetic)] private bool _canHpReactToElectromagnetic = true;

        [Tooltip("超音波でHPにダメージを与えられるか")]
        [SerializeField, ShowWaveTypeAsset(WaveKind.Ultrasound)] private bool _canHpReactToUltrasound = true;

        [Tooltip("1回ダメージが入るまでに必要な継続照射秒数（通常部位・弱点部位共通）")]
        [SerializeField, Min(0.1f)] private float _damageRequiredSeconds = 1.0f;

        [Tooltip("ビームを当てていない間に各部位のダメージ蓄積値が1秒あたりに減衰する量")]
        [SerializeField, Min(0.0f)] private float _damageDecayPerSecond = 0.5f;

        private Entity _entity;
        private IArmored _armored;
        private bool HasArmor => _armored != null && _armored.IsArmored;

        private WaveReaction _waveReaction;

        private float _armorCharge;
        private float _lastArmorReceiveTime = float.NegativeInfinity;
        private int _lastArmorReceiveFrame = -1;

        // 部位（コライダー）ごとのダメージチャージ管理リスト
        private readonly List<ColliderChargeState> _colliderCharges = new List<ColliderChargeState>(4);

        /// <summary>
        /// アーマー破壊までのチャージ割合（0〜1）。UIや演出用
        /// </summary>
        public float ArmorChargeRatio => _entity != null && HasArmor && _armorRequiredSeconds > 0f
            ? Mathf.Clamp01(_armorCharge / _armorRequiredSeconds)
            : 0f;

        /// <summary>
        /// 現在最もチャージが進んでいる部位のダメージチャージ割合（0〜1）。UIや演出用
        /// </summary>
        public float DamageChargeRatio
        {
            get
            {
                if (_damageRequiredSeconds <= 0f) return 0f;

                float maxCharge = 0f;
                for (int i = 0; i < _colliderCharges.Count; i++)
                {
                    if (_colliderCharges[i].Charge > maxCharge)
                    {
                        maxCharge = _colliderCharges[i].Charge;
                    }
                }
                return Mathf.Clamp01(maxCharge / _damageRequiredSeconds);
            }
        }

        private bool IsReceivingArmor => Time.time - _lastArmorReceiveTime <= ReceivingGraceTime;

        private void Awake()
        {
            _entity = GetComponent<Entity>();
            TryGetComponent(out _armored);
            TryGetComponent(out _waveReaction);

            _armored?.SetArmored(_hasInitialArmor);
            SyncWaveReaction();
        }

        private void OnDisable()
        {
            _armorCharge = 0f;
            _colliderCharges.Clear();
        }

        private void Update()
        {
            if (_entity == null || _entity.IsDead) return;

            // 1. アーマー中の減衰処理
            if (HasArmor)
            {
                if (!IsReceivingArmor)
                {
                    _armorCharge = Mathf.Max(0f, _armorCharge - _armorDecayPerSecond * Time.deltaTime);
                }
                return;
            }

            // 2. 各部位（コライダー）ごとのダメージチャージ減衰処理
            for (int i = _colliderCharges.Count - 1; i >= 0; i--)
            {
                ColliderChargeState state = _colliderCharges[i];
                if (state.Collider == null)
                {
                    _colliderCharges.RemoveAt(i);
                    continue;
                }

                if (Time.time - state.LastReceiveTime > ReceivingGraceTime)
                {
                    state.Charge = Mathf.Max(0f, state.Charge - _damageDecayPerSecond * Time.deltaTime);
                    _colliderCharges[i] = state;
                }
            }
        }

        /// <summary>
        /// WaveDamageDealer から毎フレーム呼ばれる波動ヒット処理
        /// </summary>
        public void ReceiveWaveHit(in InfoBeamHit hit, int baseDamage)
        {
            if (!isActiveAndEnabled || _entity == null || _entity.IsDead) return;
            if (!WaveTypeInfo.TryGetKind(hit.Type, out WaveKind waveKind)) return;

            // 無効部位（倍率0以下）の場合はチャージもダメージも与えない
            if (hit.Multiplier <= 0f) return;

            // WaveEmitter側で hit.Energy に hit.Multiplier が掛けられているため、純粋な照射秒数（エネルギー）に戻す
            float rawEnergy = hit.Energy / hit.Multiplier;
            if (rawEnergy <= 0f) return;

            // フェーズ1：アーマー中
            if (HasArmor)
            {
                if (!CanArmorReactTo(waveKind)) return;
                // 同一フレーム内で複数コライダーに当たってもアーマーチャージは1回分のみ加算
                if (_lastArmorReceiveFrame == Time.frameCount) return;

                _lastArmorReceiveFrame = Time.frameCount;
                _lastArmorReceiveTime = Time.time;
                _armorCharge += rawEnergy;

                if (_armorCharge >= _armorRequiredSeconds)
                {
                    BreakArmor();
                }
                return;
            }

            // フェーズ2：HPダメージ（アーマー無し or アーマー破壊後）
            // 無敵時間中は次のダメージチャージを溜めない
            if (_entity.IsInvincible) return;
            if (!CanHpReactTo(waveKind)) return;

            int index = GetOrAddColliderChargeIndex(hit.Collider);
            ColliderChargeState state = _colliderCharges[index];

            state.LastReceiveTime = Time.time;
            state.Charge = Mathf.Min(state.Charge + rawEnergy, _damageRequiredSeconds);
            _colliderCharges[index] = state;

            // 照射時間が規定秒数に達したら、倍率分のダメージを一括で与えて全チャージをリセット
            if (state.Charge >= _damageRequiredSeconds)
            {
                ResetAllDamageCharges();
                int finalDamage = Mathf.Max(1, Mathf.RoundToInt(baseDamage * hit.Multiplier));
                _entity.TakeDamage(finalDamage);
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
            ResetAllDamageCharges();
            _armored.SetArmored(false);
            SyncWaveReaction();
            _onArmorBroken?.Invoke();
        }

        private int GetOrAddColliderChargeIndex(Collider targetCollider)
        {
            for (int i = 0; i < _colliderCharges.Count; i++)
            {
                if (_colliderCharges[i].Collider == targetCollider)
                {
                    return i;
                }
            }

            _colliderCharges.Add(new ColliderChargeState
            {
                Collider = targetCollider,
                Charge = 0f,
                LastReceiveTime = Time.time
            });
            return _colliderCharges.Count - 1;
        }

        private void ResetAllDamageCharges()
        {
            for (int i = 0; i < _colliderCharges.Count; i++)
            {
                ColliderChargeState state = _colliderCharges[i];
                state.Charge = 0f;
                _colliderCharges[i] = state;
            }
        }

        private bool CanArmorReactTo(WaveKind kind)
        {
            return kind == WaveKind.Ultrasound ? _canArmorReactToUltrasound : _canArmorReactToElectromagnetic;
        }

        private bool CanHpReactTo(WaveKind kind)
        {
            return kind == WaveKind.Ultrasound ? _canHpReactToUltrasound : _canHpReactToElectromagnetic;
        }

        /// <summary>
        /// 同じオブジェクトに WaveReaction が付いている場合、現在のフェーズ（アーマー中 / 本体）に合わせて反応属性を同期する
        /// </summary>
        private void SyncWaveReaction()
        {
            if (_waveReaction == null) return;

            if (HasArmor)
            {
                _waveReaction.CanReactToElectromagnetic = _canArmorReactToElectromagnetic;
                _waveReaction.CanReactToUltrasound = _canArmorReactToUltrasound;
            }
            else
            {
                _waveReaction.CanReactToElectromagnetic = _canHpReactToElectromagnetic;
                _waveReaction.CanReactToUltrasound = _canHpReactToUltrasound;
            }
        }
    }
}
