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
        [SerializeField, Tooltip("Scene Viewに範囲を表示します。")]
        [UnityEngine.Serialization.FormerlySerializedAs("showGizmos")]
        private bool _isGizmoVisible = true;
        [SerializeField, Tooltip("状態変更時に旧状態、新状態の順で通知します。")]
        [UnityEngine.Serialization.FormerlySerializedAs("onStateChanged")]
        private EchoStateEvent _onStateChanged = new EchoStateEvent();
        private static readonly List<EchoController> _activeInstances = new List<EchoController>();
        private static readonly ProfilerMarker _updateMarker = new ProfilerMarker("Echo.Update");
        private readonly Queue<int> _commands = new Queue<int>();
        private readonly EchoTargetRegistry _registry = new EchoTargetRegistry();
        private readonly EchoTagResolver _tags = new EchoTagResolver();
        private readonly List<EchoRenderItem> _items = new List<EchoRenderItem>();
        private readonly List<EchoFrameData> _frames = new List<EchoFrameData>(EchoPulseTimeline.MaxPulses);
        private readonly EchoPulseTimeline _pulseTimeline = new EchoPulseTimeline();
        private readonly EchoShadowMaps _shadowMaps = new EchoShadowMaps();
        /// <summary>各エコー地点からの遮蔽距離。強調表示と範囲表示で共有する。</summary>
        public EchoShadowMaps ShadowMaps => _shadowMaps;

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
        /// <summary>当フレームの描画条件。</summary>
        public EchoFrameData Frame { get; private set; }
        /// <summary>生存中の全領域。ソナーは古い波から順。追従は最大1件。</summary>
        public IReadOnlyList<EchoFrameData> Frames => _frames;
        /// <summary>候補Renderer一覧。変更禁止。</summary>
        public IReadOnlyList<EchoRenderItem> RenderItems => _items;
        /// <summary>Inspectorおよび導入ツールが使用する設定。</summary>
        public EchoSettings Settings => _settings;
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
        /// <summary>指定Cameraに一意に対応するController。重複時はnull。</summary>
        public static EchoController GetForCamera(Camera camera)
        {
            EchoController found = null;
            foreach (var c in _activeInstances)
                if (c && c._targetCamera == camera)
                {
                    if (found)
                        return null;
                    found = c;
                }

            return found;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _activeInstances.Clear();
        void Reset()
        {
            _targetCamera = Camera.main;
            _excludedRoot = transform;
        }

        void OnValidate()
        {
            _settings ??= new EchoSettings();
            _settings.Validate();
        }

        void OnEnable()
        {
            _settings.Validate();
            _activeInstances.Add(this);
            RememberSettings();
            SceneManager.sceneLoaded += HandleSceneLoaded;
            SceneManager.sceneUnloaded += HandleSceneUnloaded;
            EchoTarget.Changed += RefreshTargets;
            RefreshTargets();
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
            CurrentOuterRadius = CurrentEraseRadius = _elapsed = 0;
            ChangeState(EchoState.Hidden);
            _commands.Clear();
            _items.Clear();
            _frames.Clear();
            _pulseTimeline.Clear();
            _registry.Clear();
            _shadowMaps.Clear();
        }

        void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => RefreshTargets();
        void HandleSceneUnloaded(Scene scene) => RefreshTargets();
        /// <summary>ON/OFFを要求する。同じ要求は進行をリセットしない。</summary>
        public void SetScanEnabled(bool isEnabled)
        {
            if (isActiveAndEnabled)
                _commands.Enqueue(isEnabled ? 1 : 0);
        }

        /// <summary>同じ操作でON/OFF要求を反転する。</summary>
        public void ToggleScan()
        {
            if (isActiveAndEnabled)
                _commands.Enqueue(2);
        }

        /// <summary>ロード済みシーンの描画対象を再収集する。</summary>
        public void RefreshTargets() => _registry.Refresh(_settings.IsAutoDiscoveryEnabled);
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
                    if (IsScanRequested)
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
                    Request(cmd == 2 ? !IsScanRequested : cmd == 1);
                }

                float delta = _hasPendingClockReset ? 0 : _settings.TimeMode == EchoTimeMode.Scaled ? Time.deltaTime : Time.unscaledDeltaTime;
                if (_settings.ScanMode == EchoScanMode.Sonar)
                {
                    var preview = GetPreview();
                    _pulseTimeline.Advance(delta, _settings, preview.Origin, preview.Forward, _frames);
                    Frame = _frames.Count > 0 ? _frames[_frames.Count - 1] : new EchoFrameData(preview.Origin, preview.Forward, _settings.AngleDegrees, 0, 0, EchoState.Hidden, _settings.OcclusionMode);
                    CurrentOuterRadius = Frame.OuterRadius;
                    CurrentEraseRadius = Frame.EraseRadius;
                    ChangeState(_frames.Count > 0 ? Frame.State : IsScanRequested && _settings.Radius > 0 ? EchoState.Active : EchoState.Hidden);
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

                    Transform o = _originTransform ? _originTransform : transform, d = _directionTransform ? _directionTransform : o;
                    Frame = new EchoFrameData(o.position, d.forward, _settings.AngleDegrees, CurrentOuterRadius, CurrentEraseRadius, CurrentState, _settings.OcclusionMode);
                    _frames.Clear();
                    if (CurrentState != EchoState.Hidden)
                        _frames.Add(Frame);
                }

                _shadowMaps.Update(_frames, _settings, _excludedRoot ? _excludedRoot : transform);
                _tags.Update(_settings.TagColorRules);
                _items.Clear();
                if (_frames.Count > 0 && _targetCamera)
                    _registry.Collect(_frames, _settings.TargetLayers, _excludedRoot ? _excludedRoot : transform, _targetCamera, _tags, _items);
            }
        }

        /// <summary>Scene View用に最大領域を返す。</summary>
        public EchoFrameData GetPreview()
        {
            var o = _originTransform ? _originTransform : transform;
            return new EchoFrameData(o.position, (_directionTransform ? _directionTransform : o).forward, _settings.AngleDegrees, _settings.Radius, 0, EchoState.Active, _settings.OcclusionMode);
        }

        /// <summary>範囲Gizmo表示の可否。</summary>
        public bool IsGizmoVisible => _isGizmoVisible;
    }
}
