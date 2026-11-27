// エコーを使っている間だけ、パーティクルのエフェクトをスキャン中心で再生する。
// 大きさ・速さ・向きは生成したエフェクトだけを変え、元のPrefabは書き換えない。
using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Aura.Echolocation
{
    /// <summary>エコーの使用中に再生するエフェクト。未設定なら何もしない。</summary>
    [Serializable]
    public sealed class EchoScanEffect
    {
        // Yamazaki_SのPF_vfx_echolocationのリングが広がりきったときの半径と、広がるまでの時間。
        private const float DefaultBaseRadius = 17;
        private const float DefaultBaseDuration = 1.5f;
        // 0で割らないための最小値。
        private const float MinimumBaseValue = .01f;
        // 正面の基準が真上・真下を向いた時は水平の向きが決まらないため、向きを変えない。
        private const float MinimumHorizontalSqrLength = 1e-6f;

        [SerializeField, Tooltip("エコーを使っている間に再生するパーティクルのPrefab。スキャン中心の子として生成します。")]
        private GameObject _prefab;
        [SerializeField, Tooltip("最大半径に合わせて、エフェクト全体を拡大・縮小します。")]
        private bool _isScaledToRadius;
        [SerializeField, Min(MinimumBaseValue), Tooltip("Prefabのままの大きさで広がりきったときの半径（m）。最大半径がこの値なら等倍です。")]
        private float _baseRadius = DefaultBaseRadius;
        [SerializeField, Tooltip("伝播時間に合わせて、エフェクトの再生速度を変えます。")]
        private bool _isSpeedMatchedToDuration;
        [SerializeField, Min(MinimumBaseValue), Tooltip("Prefabのままの速さで広がりきるまでの時間（秒）。伝播時間がこの値なら等速です。")]
        private float _baseDuration = DefaultBaseDuration;
        [SerializeField, Tooltip("正面の基準の水平方向にエフェクトの正面を向けます。オフならスキャン中心の向きのままです。")]
        private bool _isFacingDirection;
        [SerializeField, Tooltip("正面の基準に向きを合わせる時に、上下の傾きも合わせます。オフなら水平方向だけ合わせます。")]
        private bool _isPitchFollowed;

        private GameObject _instance;
        private GameObject _createdFrom;
        private ParticleSystem[] _particles = Array.Empty<ParticleSystem>();
        private float[] _originalSpeeds = Array.Empty<float>();
        // Prefabの一番上に設定された拡大率と回転。合わせる設定はこれを基準に掛け、オフの時はこれに戻す。
        private Vector3 _originalScale = Vector3.one;
        private Quaternion _originalRotation = Quaternion.identity;
        private float _appliedSpeedRatio = 1;

        /// <summary>再生するPrefab。変更すると次の再生から反映する。</summary>
        public GameObject Prefab { get => _prefab; set => _prefab = value; }
        /// <summary>最大半径に合わせて大きさを変えるか。</summary>
        public bool IsScaledToRadius { get => _isScaledToRadius; set => _isScaledToRadius = value; }
        /// <summary>Prefabのままの大きさの半径（m）。IsScaledToRadiusがtrueのときだけ効く。</summary>
        public float BaseRadius { get => _baseRadius; set => _baseRadius = value; }
        /// <summary>伝播時間に合わせて再生速度を変えるか。</summary>
        public bool IsSpeedMatchedToDuration { get => _isSpeedMatchedToDuration; set => _isSpeedMatchedToDuration = value; }
        /// <summary>Prefabのままの速さで広がりきるまでの時間（秒）。IsSpeedMatchedToDurationがtrueのときだけ効く。</summary>
        public float BaseDuration { get => _baseDuration; set => _baseDuration = value; }
        /// <summary>正面の基準の水平方向に向けるか。</summary>
        public bool IsFacingDirection { get => _isFacingDirection; set => _isFacingDirection = value; }
        /// <summary>上下の傾きも合わせるか。IsFacingDirectionがtrueのときだけ効く。</summary>
        public bool IsPitchFollowed { get => _isPitchFollowed; set => _isPitchFollowed = value; }

        /// <summary>再生中か。</summary>
        public bool IsPlaying { get; private set; }

        /// <summary>再生・停止を切り替える。停止しても出ている粒子は寿命まで残す。</summary>
        public void SetPlaying(bool isPlaying, Transform parent)
        {
            if (isPlaying)
                Play(parent);
            else
                Stop();
        }

        /// <summary>
        /// 生成したエフェクトの大きさ・速さ・向きを設定に合わせる。生成前は何もしない。
        /// 伝播時間が0（即時）の時は合わせる速さがないため、Prefabのままの速さにする。
        /// </summary>
        public void Apply(float radius, float duration, Transform direction) =>
            Apply(radius, duration, direction ? direction.forward : Vector3.zero, direction ? direction.up : Vector3.up);

        /// <summary>
        /// 向きを直接指定して、生成したエフェクトの大きさ・速さ・向きを設定に合わせる。
        /// ADS中に照準へ向けたエコーの発射向きなど、Transformのforwardとは別の向きに合わせる時に使う。forwardが0なら向きは変えない。
        /// </summary>
        public void Apply(float radius, float duration, Vector3 forward, Vector3 up)
        {
            if (!_instance)
                return;

            var effect = _instance.transform;
            float scale = _isScaledToRadius ? Mathf.Max(0, radius) / Mathf.Max(MinimumBaseValue, _baseRadius) : 1;
            effect.localScale = _originalScale * scale;

            bool hasDirection = forward.sqrMagnitude > MinimumHorizontalSqrLength;
            if (!_isFacingDirection)
                effect.localRotation = _originalRotation;
            else if (hasDirection && _isPitchFollowed)
                // 正面の基準の上方向を使うと、真上・真下を向いても向きが決まる。
                effect.rotation = Quaternion.LookRotation(forward, up) * _originalRotation;
            else if (hasDirection)
            {
                Vector3 horizontal = Vector3.ProjectOnPlane(forward, Vector3.up);
                if (horizontal.sqrMagnitude > MinimumHorizontalSqrLength)
                    effect.rotation = Quaternion.LookRotation(horizontal, Vector3.up) * _originalRotation;
            }

            float speedRatio = _isSpeedMatchedToDuration && duration > 0 ? Mathf.Max(MinimumBaseValue, _baseDuration) / duration : 1;
            // 再生速度はパーティクルごとに持つため、変わった時だけ書き換える。
            if (Mathf.Approximately(speedRatio, _appliedSpeedRatio))
                return;
            _appliedSpeedRatio = speedRatio;
            for (int i = 0; i < _particles.Length; i++)
                if (_particles[i])
                {
                    var main = _particles[i].main;
                    main.simulationSpeed = _originalSpeeds[i] * speedRatio;
                }
        }

        /// <summary>生成したエフェクトを破棄する。</summary>
        public void Release()
        {
            IsPlaying = false;
            if (_instance)
            {
                if (Application.isPlaying) Object.Destroy(_instance);
                else Object.DestroyImmediate(_instance);
            }
            _instance = null;
            _createdFrom = null;
            _particles = Array.Empty<ParticleSystem>();
            _originalSpeeds = Array.Empty<float>();
            _originalScale = Vector3.one;
            _originalRotation = Quaternion.identity;
            _appliedSpeedRatio = 1;
        }

        /// <summary>
        /// 最初から1回だけ再生する。ループしないエフェクト用で、再生中に呼ぶと最初からやり直す。
        /// 再生・停止の状態（IsPlaying）は変えない。
        /// </summary>
        public void PlayOnce(Transform parent)
        {
            if (!PrepareInstance(parent))
                return;
            // 続けて呼ばれた時も始まりから見せるため、前の再生を消してからやり直す。
            foreach (var particle in _particles)
                if (particle)
                {
                    particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    particle.Play(false);
                }
        }

        void Play(Transform parent)
        {
            // 毎フレーム呼ばれても、再生中なら何もしない。
            if (IsPlaying && _instance && _createdFrom == _prefab && _instance.transform.parent == parent)
                return;
            if (!PrepareInstance(parent))
            {
                Stop();
                return;
            }

            // Prefabの一番上にParticleSystemがない構成でも再生できるよう、親子をまとめず1つずつ再生する。
            foreach (var particle in _particles)
                if (particle) particle.Play(false);
            IsPlaying = true;
        }

        // 生成は初回とPrefabを差し替えた時だけにし、再生のたびに作り直さない。Prefabが未設定ならfalse。
        bool PrepareInstance(Transform parent)
        {
            if (!_prefab || !parent)
                return false;

            if (!_instance || _createdFrom != _prefab)
            {
                Release();
                _instance = Object.Instantiate(_prefab, parent);
                _createdFrom = _prefab;
                _originalScale = _instance.transform.localScale;
                _originalRotation = _instance.transform.localRotation;
                _particles = _instance.GetComponentsInChildren<ParticleSystem>(true);
                // 速さを倍率で掛けるため、Prefabに設定された元の再生速度を覚えておく。
                _originalSpeeds = new float[_particles.Length];
                for (int i = 0; i < _particles.Length; i++)
                    _originalSpeeds[i] = _particles[i].main.simulationSpeed;
            }
            else if (_instance.transform.parent != parent)
                _instance.transform.SetParent(parent, false);

            _instance.SetActive(true);
            return true;
        }

        void Stop()
        {
            if (!IsPlaying)
                return;
            IsPlaying = false;
            // 途中で消すと見た目が途切れるため、放出だけ止めて出ている粒子は自然に消えるのを待つ。
            foreach (var particle in _particles)
                if (particle) particle.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }
    }
}
