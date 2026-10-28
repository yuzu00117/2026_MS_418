// スキャンの操作キュー・状態遷移・描画データの作成を管理する。入力方式とURPを知らない。
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using Unity.Profiling;

namespace Echo.Echolocation
{
    /// <summary>エコーロケーションの操作窓口。入力は次のLateUpdateで反映する。</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(1000)]
    public sealed class EchoController : MonoBehaviour
    {
        [SerializeField, Tooltip("未設定なら自身を中心にします。")]
        [UnityEngine.Serialization.FormerlySerializedAs("originTransform")]
        private Transform _originTransform;
        [SerializeField, Tooltip("未設定なら中心のforwardを使います。")]
        [UnityEngine.Serialization.FormerlySerializedAs("directionTransform")]
        private Transform _directionTransform;
        [SerializeField, Tooltip("強調表示を描画するBase Cameraです。")]
        [UnityEngine.Serialization.FormerlySerializedAs("targetCamera")]
        private Camera _targetCamera;
        [SerializeField, Tooltip("このルート配下を表示対象から除外します。")]
        [UnityEngine.Serialization.FormerlySerializedAs("excludedRoot")]
        private Transform _excludedRoot;
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("settings")]
        private EchoSettings _settings = new EchoSettings();
        [SerializeField]
        private EchoRangeDisplaySettings _rangeDisplay = new EchoRangeDisplaySettings();
        [SerializeField, Tooltip("Scene Viewに範囲を表示します。")]
        [UnityEngine.Serialization.FormerlySerializedAs("showGizmos")]
        private bool _isGizmoVisible = true;
        [SerializeField, Tooltip("状態変更時に旧状態、新状態の順で通知します。")]
        [UnityEngine.Serialization.FormerlySerializedAs("onStateChanged")]
        private EchoStateEvent _onStateChanged = new EchoStateEvent();
        private static readonly List<EchoController> _activeInstances = new List<EchoController>();
        private static readonly ProfilerMarker _updateMarker = new ProfilerMarker("Echo.Update");
        // 入力はLateUpdateでまとめて処理するため、操作の種類を順番に積む。
        private const int CommandOff = 0;
        private const int CommandOn = 1;
        private const int CommandToggle = 2;
        private readonly Queue<int> _commands = new Queue<int>();
        private readonly EchoTargetRegistry _registry = new EchoTargetRegistry();
        private readonly EchoTagResolver _tags = new EchoTagResolver();
        private readonly List<EchoRenderItem> _items = new List<EchoRenderItem>();
        private readonly List<EchoFrameData> _frames = new List<EchoFrameData>(EchoPulseTimeline.MaxPulses);
        private readonly EchoPulseTimeline _pulseTimeline = new EchoPulseTimeline();
        private readonly EchoShadowMaps _shadowMaps = new EchoShadowMaps();
        /// <summary>各エコー地点からの遮蔽距離。強調表示と範囲表示で共有する。</summary>
        public EchoShadowMaps ShadowMaps => _shadowMaps;
        /// <summary>再有効化を描画履歴へ通知する世代番号。</summary>
        public int EnableVersion { get; private set; }

        private EchoScanMode _oldMode;
        private float _oldInterval;
        private float _oldHold;
        private float _elapsed;
        private float _oldRadius;
        private float _oldAngle;
        private float _oldDuration;
        private EchoOffMode _oldOff;
        private EchoTimeMode _oldTime;
        private bool _hasPendingClockReset;
        private bool _isPreviousAutoDiscoveryEnabled;
        /// <summary>ON要求。消去中はfalse。</summary>
        public bool IsScanRequested { get; private set; }
        /// <summary>現在の状態。</summary>
        public EchoState CurrentState { get; private set; }
        /// <summary>外側表示半径（ワールド単位）。</summary>
        public float CurrentOuterRadius { get; private set; }
        /// <summary>内側消去半径（ワールド単位）。</summary>
        public float CurrentEraseRadius { get; private set; }
        /// <summary>追従モードで繰り返す拡張波の先端半径（ワールド単位）。表示していなければ負。</summary>
        public float FollowWaveRadius { get; private set; } = -1;
        private float _followWaveElapsed;
        private struct OnceWave
        {
            public Vector3 Origin;
            public Vector3 Forward;
            public float Elapsed;
            public long Id;
        }
        private readonly List<OnceWave> _onceWaves = new List<OnceWave>(EchoPulseTimeline.MaxPulses);
        private long _nextOnceWaveId;
        private int _onceStock = -1;
        private int _onceStockCapacityApplied = -1;
        private float _onceRechargeElapsed;
        /// <summary>一回だけのスキャンで今打てる回数。</summary>
        public int OnceStock => Mathf.Max(0, _onceStock);
        /// <summary>次のストックが回復するまでの進み具合（0～1）。満タンなら1。</summary>
        public float OnceRechargeProgress => OnceStock >= _settings.OnceStockCapacity ? 1 :
            _settings.OnceRechargeSeconds <= 0 ? 1 : Mathf.Clamp01(_onceRechargeElapsed / _settings.OnceRechargeSeconds);
        /// <summary>ストック数が変わった時に新しい値を通知する。</summary>
        public event Action<int> OnceStockChanged;
        /// <summary>当フレームの描画条件。</summary>
        public EchoFrameData Frame { get; private set; }
        /// <summary>生存中の全領域。ソナーは古い波から順。追従は最大1件。</summary>
        public IReadOnlyList<EchoFrameData> Frames => _frames;
        /// <summary>候補Renderer一覧。変更禁止。</summary>
        public IReadOnlyList<EchoRenderItem> RenderItems => _items;
        /// <summary>Inspectorおよび導入ツールが使用する設定。</summary>
        public EchoSettings Settings => _settings;
        /// <summary>スキャン範囲の色と境界線。検出範囲そのものは変えない。</summary>
        public EchoRangeDisplaySettings RangeDisplay => _rangeDisplay ??= new EchoRangeDisplaySettings();
        /// <summary>描画するCamera。</summary>
        public Camera TargetCamera { get => _targetCamera; set => _targetCamera = value; }
        /// <summary>カメラ位置に依存しないスキャン中心。未指定時は自身。</summary>
        public Transform OriginTransform { get => _originTransform; set => _originTransform = value; }
        /// <summary>照準方向。三人称のカメラ後退でも中心は変更しない。</summary>
        public Transform DirectionTransform { get => _directionTransform; set => _directionTransform = value; }
        /// <summary>強調対象から除外するプレイヤールート。</summary>
        public Transform ExcludedRoot { get => _excludedRoot; set => _excludedRoot = value; }
        /// <summary>登録数。</summary>
        public int RegisteredCount => _registry.Count;

        /// <summary>状態が実際に変わったときだけ旧状態・新状態を通知。</summary>
        public event Action<EchoState, EchoState> StateChanged;
        /// <summary>
        /// エコーロケーションを使用中か。ON中（ソナーの波と波の間も含む）と、OFF後に波が消えきるまでがtrue。
        /// ON/OFFの操作だけを知りたい場合は<see cref="IsScanRequested"/>を使う。
        /// </summary>
        public bool IsEchoInUse => isActiveAndEnabled && (IsScanRequested || _frames.Count > 0);
        /// <summary><see cref="IsEchoInUse"/>が変わった時に新しい値を通知する。</summary>
        public event Action<bool> UsageChanged;
        private bool _isPreviousEchoInUse;
        /// <summary>指定Cameraに一意に対応するController。重複時はnull。</summary>
        public static EchoController GetForCamera(Camera camera)
        {
            EchoController found = null;
            foreach (var instance in _activeInstances)
                if (instance && instance._targetCamera == camera)
                {
                    if (found)
                        return null;
                    found = instance;
                }

            return found;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _activeInstances.Clear();
        void Reset()
        {
            _targetCamera = Camera.main;
            _excludedRoot = transform;
            // 新しく追加したControllerだけ範囲表示を有効にする。既存シーンの見た目は変えない。
            RangeDisplay.IsEnabled = true;
        }

        void OnValidate()
        {
            _settings ??= new EchoSettings();
            _settings.Validate();
            RangeDisplay.Validate();
        }

        void OnDestroy() => RangeDisplay.ReleaseMesh();

        void OnEnable()
        {
            EnableVersion++;
            _settings.Validate();
            _activeInstances.Add(this);
            RememberSettings();
            SceneManager.sceneLoaded += HandleSceneLoaded;
            SceneManager.sceneUnloaded += HandleSceneUnloaded;
            EchoTarget.Changed += RefreshTargets;
            RefreshTargets();
            if (_onceStock < 0)
            {
                _onceStockCapacityApplied = _settings.OnceStockCapacity;
                SetOnceStock(_settings.OnceStockCapacity);
            }
            if (_settings.IsEnabledOnStart)
                SetScanEnabled(true);
        }

        void OnDisable()
        {
            _activeInstances.Remove(this);
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
            EchoTarget.Changed -= RefreshTargets;
            _commands.Clear();
            IsScanRequested = false;
            CurrentOuterRadius = CurrentEraseRadius = _elapsed = _followWaveElapsed = 0;
            FollowWaveRadius = -1;
            ChangeState(EchoState.Hidden);
            _commands.Clear();
            _items.Clear();
            _frames.Clear();
            _onceWaves.Clear();
            _pulseTimeline.Clear();
            _registry.Clear();
            _shadowMaps.Clear();
            RangeDisplay.ReleaseMesh();
            NotifyUsage();
        }

        void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => RefreshTargets();
        void HandleSceneUnloaded(Scene scene) => RefreshTargets();
        /// <summary>ON/OFFを要求する。同じ要求は進行をリセットしない。</summary>
        public void SetScanEnabled(bool isEnabled)
        {
            if (isActiveAndEnabled)
                _commands.Enqueue(isEnabled ? CommandOn : CommandOff);
        }

        /// <summary>同じ操作でON/OFF要求を反転する。</summary>
        public void ToggleScan()
        {
            if (isActiveAndEnabled)
                _commands.Enqueue(CommandToggle);
        }

        /// <summary>ロード済みシーンの描画対象を再収集する。</summary>
        public void RefreshTargets()
        {
            _registry.Refresh(_settings.IsAutoDiscoveryEnabled);
            _shadowMaps.Refresh();
        }
        /// <summary>独自の形状取得を追加する。標準DrawRendererに適合するRenderer用。</summary>
        public void RegisterGeometryAdapter(IEchoGeometryAdapter adapter)
        {
            _registry.AddAdapter(adapter);
            RefreshTargets();
        }

        /// <summary>設定をコピーして適用する。状態への反映は次のLateUpdate。</summary>
        public void ApplySettings(EchoSettings value)
        {
            _settings = value?.Copy() ?? throw new ArgumentNullException(nameof(value));
        }

        void RememberSettings()
        {
            _oldRadius = _settings.Radius;
            _oldAngle = _settings.AngleDegrees;
            _oldDuration = _settings.Duration;
            _oldOff = _settings.OffMode;
            _oldTime = _settings.TimeMode;
            _isPreviousAutoDiscoveryEnabled = _settings.IsAutoDiscoveryEnabled;
            _oldMode = _settings.ScanMode;
            _oldInterval = _settings.PulseInterval;
            _oldHold = _settings.PulseHoldDuration;
        }

        void ChangeState(EchoState next)
        {
            if (CurrentState == next)
                return;
            var old = CurrentState;
            CurrentState = next;
            StateChanged?.Invoke(old, next);
            _onStateChanged.Invoke(old, next);
        }

        void BeginReveal()
        {
            _shadowMaps.Clear();
            _shadowMaps.Refresh();
            _elapsed = 0;
            CurrentOuterRadius = CurrentEraseRadius = 0;
            _hasPendingClockReset = true;
            _frames.Clear();
            _pulseTimeline.Clear();
            if (_settings.ScanMode == EchoScanMode.Sonar)
            {
                var preview = GetPreview();
                _pulseTimeline.Start(preview.Origin, preview.Forward, _settings);
            }

            ChangeState(_settings.Radius > 0 ? EchoState.Revealing : EchoState.Hidden);
        }

        void Request(bool isOn)
        {
            if (IsScanRequested == isOn)
                return;
            IsScanRequested = isOn;
            if (isOn)
                BeginReveal();
            else if (_settings.ScanMode == EchoScanMode.Sonar)
            {
                _pulseTimeline.Stop(_settings);
                _hasPendingClockReset = true;
            }
            else if (_settings.OffMode == EchoOffMode.Immediate || CurrentOuterRadius <= 0 || _settings.Duration == 0)
            {
                CurrentOuterRadius = CurrentEraseRadius = 0;
                ChangeState(EchoState.Hidden);
            }
            else
            {
                _elapsed = 0;
                CurrentEraseRadius = 0;
                _hasPendingClockReset = true;
                ChangeState(EchoState.Hiding);
            }
        }

        void LateUpdate()
        {
            using (_updateMarker.Auto())
            {
                _settings.Validate();
                _hasPendingClockReset = false;
                // イベント内で積まれた操作は次回へ回し、無限の再入を防ぐ。
                int count = _commands.Count;
                bool isChanged = _oldRadius != _settings.Radius || _oldAngle != _settings.AngleDegrees || _oldDuration != _settings.Duration || _oldOff != _settings.OffMode || _oldTime != _settings.TimeMode || _oldMode != _settings.ScanMode || _oldInterval != _settings.PulseInterval || _oldHold != _settings.PulseHoldDuration;
                if (_isPreviousAutoDiscoveryEnabled != _settings.IsAutoDiscoveryEnabled)
                    RefreshTargets();
                if (isChanged)
                    _shadowMaps.Clear();
                if (isChanged)
                {
                    // 一回だけの波は打ち直さず、伝播中の波だけを消す。ストックは返さない。
                    if (_settings.ScanMode == EchoScanMode.Once || _oldMode == EchoScanMode.Once)
                    {
                        _onceWaves.Clear();
                        _pulseTimeline.Clear();
                        _frames.Clear();
                        FinishOnce();
                    }
                    else if (IsScanRequested)
                        BeginReveal();
                    else
                    {
                        _pulseTimeline.Clear();
                        _frames.Clear();
                        CurrentOuterRadius = CurrentEraseRadius = 0;
                        ChangeState(EchoState.Hidden);
                    }
                }

                RememberSettings();
                for (int i = 0; i < count; i++)
                {
                    int cmd = _commands.Dequeue();
                    // 一回だけのスキャンはON・切り替えのどちらでも1発打つ。連打で連続発信できるようにするため。
                    if (_settings.ScanMode == EchoScanMode.Once)
                    {
                        if (cmd == CommandOff) StopOnce();
                        else FireOnce();
                    }
                    else
                        Request(cmd == CommandToggle ? !IsScanRequested : cmd == CommandOn);
                }

                float delta = _hasPendingClockReset ? 0 : _settings.TimeMode == EchoTimeMode.Scaled ? Time.deltaTime : Time.unscaledDeltaTime;
                if (_settings.ScanMode == EchoScanMode.Sonar)
                {
                    // ソナーは波そのものを繰り返すため、追従用の拡張波は出さない。
                    FollowWaveRadius = -1;
                    var preview = GetPreview();
                    _pulseTimeline.Advance(delta, _settings, preview.Origin, preview.Forward, _frames);
                    Frame = _frames.Count > 0 ? _frames[_frames.Count - 1] : new EchoFrameData(preview.Origin, preview.Forward, _settings.AngleDegrees, 0, 0, EchoState.Hidden, _settings.OcclusionMode);
                    CurrentOuterRadius = Frame.OuterRadius;
                    CurrentEraseRadius = Frame.EraseRadius;
                    ChangeState(_frames.Count > 0 ? Frame.State : IsScanRequested && _settings.Radius > 0 ? EchoState.Active : EchoState.Hidden);
                }
                else if (_settings.ScanMode == EchoScanMode.Once)
                {
                    FollowWaveRadius = -1;
                    AdvanceOnce(delta);
                }
                else
                {
                    _elapsed += delta;
                    if (CurrentState == EchoState.Revealing)
                    {
                        CurrentOuterRadius = EchoPropagation.GetDistance(_settings.Radius, _settings.Duration, _elapsed);
                        if (_settings.Duration == 0 || _elapsed >= _settings.Duration)
                            ChangeState(EchoState.Active);
                    }
                    else if (CurrentState == EchoState.Hiding)
                    {
                        CurrentEraseRadius = EchoPropagation.GetDistance(_settings.Radius, _settings.Duration, _elapsed);
                        if (CurrentEraseRadius >= CurrentOuterRadius)
                        {
                            CurrentOuterRadius = CurrentEraseRadius = 0;
                            ChangeState(EchoState.Hidden);
                        }
                    }

                    UpdateFollowWave(delta);
                    Transform origin = _originTransform ? _originTransform : transform, direction = _directionTransform ? _directionTransform : origin;
                    Frame = new EchoFrameData(origin.position, direction.forward, _settings.AngleDegrees, CurrentOuterRadius, CurrentEraseRadius, CurrentState, _settings.OcclusionMode);
                    _frames.Clear();
                    if (CurrentState != EchoState.Hidden)
                        _frames.Add(Frame);
                }

                _shadowMaps.Update(_frames, _settings, _excludedRoot ? _excludedRoot : transform);
                _tags.Update(_settings.TagColorRules);
                _registry.UpdateHidden(_settings.TargetLayers, _excludedRoot ? _excludedRoot : transform, _targetCamera, _tags);
                _items.Clear();
                if (_targetCamera)
                    _registry.Collect(_frames, _settings.TargetLayers, _excludedRoot ? _excludedRoot : transform, _targetCamera, _tags, _items);
                NotifyUsage();
            }
        }

        /// <summary>ストックがあれば、今の位置・向きから帯状の波を1つ発信する。</summary>
        void FireOnce()
        {
            if (_onceStock <= 0 || _settings.Radius <= 0 || _onceWaves.Count >= EchoPulseTimeline.MaxPulses)
                return;
            // 対象の再収集は全検索で重いため、伝播中の波がない時の発信だけで行う。
            if (_onceWaves.Count == 0)
                _shadowMaps.Refresh();
            // 発信した瞬間の位置・向きから伝播させる。プレイヤーが動いても波は付いて来ない。
            var preview = GetPreview();
            _onceWaves.Add(new OnceWave { Origin = preview.Origin, Forward = preview.Forward, Id = ++_nextOnceWaveId });
            SetOnceStock(_onceStock - 1);
            IsScanRequested = true;
            ChangeState(EchoState.Revealing);
        }

        // 帯状の波は手前から自然に消えるため、「手前から順番」では伝播を最後まで続ける。
        void StopOnce()
        {
            if (_settings.OffMode != EchoOffMode.Immediate)
                return;
            _onceWaves.Clear();
            _frames.Clear();
            FinishOnce();
        }

        // 当たり判定は先端から「波の帯幅」までの帯だけ。帯が通り過ぎた表面は当たっていない扱いになる。
        // 帯の後端が最大半径を抜けた波から取り除き、全部の波が抜けたら自動でOFFに戻す。
        void AdvanceOnce(float delta)
        {
            RechargeOnce(delta);
            _frames.Clear();
            float width = RangeDisplay.WaveWidth;
            for (int i = _onceWaves.Count - 1; i >= 0; i--)
            {
                var wave = _onceWaves[i];
                wave.Elapsed += delta;
                float front = _settings.Duration <= 0 ? _settings.Radius + width : _settings.Radius / _settings.Duration * wave.Elapsed;
                if (front - width >= _settings.Radius)
                    _onceWaves.RemoveAt(i);
                else
                    _onceWaves[i] = wave;
            }

            // 描画側は古い波から順に受け取る。
            foreach (var wave in _onceWaves)
            {
                float front = _settings.Duration <= 0 ? _settings.Radius + width : _settings.Radius / _settings.Duration * wave.Elapsed;
                _frames.Add(new EchoFrameData(wave.Origin, wave.Forward, _settings.AngleDegrees, Mathf.Min(front, _settings.Radius),
                    Mathf.Max(0, front - width), EchoState.Revealing, _settings.OcclusionMode, wave.Id, true));
            }

            if (_frames.Count == 0)
            {
                FinishOnce();
                return;
            }

            Frame = _frames[_frames.Count - 1];
            CurrentOuterRadius = Frame.OuterRadius;
            CurrentEraseRadius = Frame.EraseRadius;
        }

        void RechargeOnce(float delta)
        {
            int capacity = _settings.OnceStockCapacity;
            // 最大数を変えたら満タンから始める。調整中に回復を待たずに試せるようにする。
            if (capacity != _onceStockCapacityApplied)
            {
                _onceStockCapacityApplied = capacity;
                _onceRechargeElapsed = 0;
                SetOnceStock(capacity);
            }
            if (_onceStock >= capacity)
            {
                _onceRechargeElapsed = 0;
                return;
            }

            float seconds = _settings.OnceRechargeSeconds;
            if (seconds <= 0)
            {
                SetOnceStock(capacity);
                return;
            }

            _onceRechargeElapsed += delta;
            while (_onceRechargeElapsed >= seconds && _onceStock < capacity)
            {
                _onceRechargeElapsed -= seconds;
                SetOnceStock(_onceStock + 1);
            }
        }

        void SetOnceStock(int value)
        {
            if (_onceStock == value)
                return;
            _onceStock = value;
            OnceStockChanged?.Invoke(value);
        }

        void FinishOnce()
        {
            CurrentOuterRadius = CurrentEraseRadius = 0;
            IsScanRequested = false;
            var preview = GetPreview();
            Frame = new EchoFrameData(preview.Origin, preview.Forward, _settings.AngleDegrees, 0, 0, EchoState.Hidden, _settings.OcclusionMode, 0, true);
            ChangeState(EchoState.Hidden);
        }

        // 最初の展開自体が1回目の拡張波なので、展開しきってから間隔ごとに2回目以降を出す。
        void UpdateFollowWave(float delta)
        {
            float interval = _settings.FollowWaveInterval;
            if (CurrentState != EchoState.Active || interval <= 0)
            {
                _followWaveElapsed = 0;
                FollowWaveRadius = -1;
                return;
            }

            _followWaveElapsed += delta;
            float phase = _followWaveElapsed - Mathf.Floor(_followWaveElapsed / interval) * interval;
            // スキャン自体と同じ速さで進める。即時展開の設定では帯が見える時間がないため出さない。
            if (_followWaveElapsed < interval || _settings.Duration <= 0)
            {
                FollowWaveRadius = -1;
                return;
            }

            // 先端が端に届いても、内側の帯が端を抜けきるまでは先端を進めて描き続ける。
            float front = _settings.Radius / _settings.Duration * phase;
            FollowWaveRadius = front < _settings.Radius + RangeDisplay.WaveWidth ? front : -1;
        }

        void NotifyUsage()
        {
            bool isInUse = IsEchoInUse;
            if (isInUse == _isPreviousEchoInUse)
                return;
            _isPreviousEchoInUse = isInUse;
            UsageChanged?.Invoke(isInUse);
        }

        /// <summary>Scene View用に最大領域を返す。</summary>
        public EchoFrameData GetPreview()
        {
            var origin = _originTransform ? _originTransform : transform;
            return new EchoFrameData(origin.position, (_directionTransform ? _directionTransform : origin).forward, _settings.AngleDegrees, _settings.Radius, 0, EchoState.Active, _settings.OcclusionMode);
        }

        /// <summary>範囲Gizmo表示の可否。</summary>
        public bool IsGizmoVisible => _isGizmoVisible;
    }
}
