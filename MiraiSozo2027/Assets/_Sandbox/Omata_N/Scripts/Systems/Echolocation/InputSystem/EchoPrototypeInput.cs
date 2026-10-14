// 交換可能なプロトタイプ入力。各操作を独立監視し、Updateで1回に集約してControllerへ渡す。
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace Echo.Echolocation
{
    /// <summary>Eキーを初期値に、複数機器のOR入力で切り替える。</summary>
    [DisallowMultipleComponent]
    public sealed class EchoPrototypeInput : MonoBehaviour
    {
        [SerializeField, Tooltip("操作するController。未設定なら同じGameObjectから取得します。")]
        [UnityEngine.Serialization.FormerlySerializedAs("controller")]
        private EchoController _controller;
        [SerializeField, Tooltip("登録したどれかでON/OFFを切り替えます。同時入力は1回です。")]
        [UnityEngine.Serialization.FormerlySerializedAs("bindings")]
        private List<EchoInputBinding> _bindings = new List<EchoInputBinding>
        {
            new EchoInputBinding()
        };
        private static readonly List<EchoPrototypeInput> _activeInstances = new List<EchoPrototypeInput>();
        private readonly List<EchoInputRow> _rows = new List<EchoInputRow>();
        private bool _isRebuildRequired;
        private bool _hasPendingInput;
        private bool _isFocused = true;
        private bool _isDeltaSkipped;
        /// <summary>最後に受け付けた操作の表示名。</summary>
        public string LastInput { get; private set; } = "未入力";
        /// <summary>入力設定の状態表示。</summary>
        public string Status { get; private set; } = "停止中";

        /// <summary>入力先。</summary>
        public EchoController Controller
        {
            get => _controller;
            set
            {
                _controller = value;
                _isRebuildRequired = true;
            }
        }

        /// <summary>編集用割り当て。コード変更後はRebuildBindingsを呼ぶ。</summary>
        public List<EchoInputBinding> Bindings => _bindings;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _activeInstances.Clear();
        void Reset()
        {
            _controller = GetComponent<EchoController>();
            _bindings = new List<EchoInputBinding>
            {
                new EchoInputBinding()
            };
        }

        void OnValidate()
        {
            if (_bindings != null)
                foreach (var b in _bindings)
                    b?.Validate();
            _isRebuildRequired = true;
        }

        void OnEnable()
        {
            if (!_controller)
                _controller = GetComponent<EchoController>();
            _activeInstances.Add(this);
            _isFocused = Application.isFocused || Application.isBatchMode;
            UnityEngine.InputSystem.InputSystem.onBeforeUpdate += HandleBeforeInput;
            UnityEngine.InputSystem.InputSystem.onAfterUpdate += HandleAfterInput;
            UnityEngine.InputSystem.InputSystem.onDeviceChange += HandleDeviceChanged;
            _isRebuildRequired = true;
        }

        void OnDisable()
        {
            _activeInstances.Remove(this);
            UnityEngine.InputSystem.InputSystem.onBeforeUpdate -= HandleBeforeInput;
            UnityEngine.InputSystem.InputSystem.onAfterUpdate -= HandleAfterInput;
            UnityEngine.InputSystem.InputSystem.onDeviceChange -= HandleDeviceChanged;
            DisposeRows();
        }

        void DisposeRows()
        {
            foreach (EchoInputRow row in _rows)
                row.Action.Dispose();
            _rows.Clear();
            _hasPendingInput = false;
        }

        /// <summary>変更した一覧を次の入力更新前に反映する。</summary>
        public void RebuildBindings()
        {
            _isRebuildRequired = true;
            _hasPendingInput = false;
        }

        void HandleDeviceChanged(InputDevice device, InputDeviceChange change)
        {
            _isRebuildRequired = true;
            _hasPendingInput = false;
        }

        void OnApplicationFocus(bool isValue)
        {
            _isFocused = isValue || Application.isBatchMode;
            _hasPendingInput = false;
            _isRebuildRequired = true;
        }

        bool CanRead()
        {
            if (!_isFocused || !_controller || !_controller.isActiveAndEnabled)
                return false;
            foreach (var other in _activeInstances)
                if (other != this && other && other._controller == _controller)
                {
                    Status = "エラー：操作先が重複しています";
                    return false;
                }

            return true;
        }

        void HandleBeforeInput()
        {
            if (UnityEngine.InputSystem.InputSystem.settings.updateMode != InputSettings.UpdateMode.ProcessEventsInDynamicUpdate)
            {
                Status = "エラー：Dynamic Updateを選択してください";
                return;
            }

            if (_isRebuildRequired)
            {
                _isRebuildRequired = false;
                Build();
            }
        }

        static float GetValue(EchoInputRow row, InputControl control)
        {
            if (!(control is AxisControl axis))
                return 0;
            return Mathf.Max(0, axis.ReadValue() * (row.Binding.InputKind != EchoInputKind.Button && row.Binding.IsNegative ? -1 : 1));
        }

        void Build()
        {
            DisposeRows();
            _isDeltaSkipped = true;
            if (!_controller)
            {
                Status = "エラー：操作先がありません";
                return;
            }

            var seenItems = new HashSet<string>();
            int controls = 0;
            foreach (var b in _bindings)
            {
                if (b == null || !b.IsEnabled || string.IsNullOrWhiteSpace(b.ControlPath))
                    continue;
                b.Validate();
                if (!b.ControlPath.Contains("*") && string.IsNullOrEmpty(InputControlPath.TryGetControlLayout(b.ControlPath)))
                {
                    Debug.LogWarning($"エコー入力：無効な操作パス {b.ControlPath} を無視します。", this);
                    continue;
                }

                string key = $"{b.ControlPath}|{b.InputKind}|{b.IsNegative}|{b.PressThreshold}|{b.ReleaseThreshold}";
                if (!seenItems.Add(key))
                {
                    Debug.LogWarning("エコー入力：重複行を無視します。", this);
                    continue;
                }

                EchoInputRow row = new EchoInputRow
                {
                    Binding = new EchoInputBinding
                    {
                        ControlPath = b.ControlPath,
                        InputKind = b.InputKind,
                        IsNegative = b.IsNegative,
                        PressThreshold = b.PressThreshold,
                        ReleaseThreshold = b.ReleaseThreshold
                    }
                };
                try
                {
                    row.Action = new InputAction(type: InputActionType.PassThrough, binding: b.ControlPath, expectedControlType: "Axis");
                    // PassThroughで各機器を独立監視し、他の押しっぱなし入力に隠れないようにする。
                    row.Action.performed += ctx => HandleChanged(row, ctx.control);
                    row.Action.Enable();
                    foreach (var c in row.Action.controls)
                    {
                        if (!(c is AxisControl))
                        {
                            Debug.LogWarning($"エコー入力：{c.path} は軸成分を選択してください。", this);
                            continue;
                        }

                        var edge = new EchoInputEdgeDetector();
                        edge.Synchronize(GetValue(row, c), b.ReleaseThreshold);
                        row.Edges[c] = edge;
                        controls++;
                    }

                    _rows.Add(row);
                }
                catch (Exception ex)
                {
                    row.Action?.Dispose();
                    Debug.LogWarning($"エコー入力：パス {b.ControlPath} を確認してください。{ex.Message}", this);
                }
            }

            Status = _rows.Count == 0 ? "操作割り当てなし" : controls == 0 ? "未接続またはパスを確認してください" : "有効";
        }

        void HandleChanged(EchoInputRow row, InputControl control)
        {
            if (row.Binding.InputKind == EchoInputKind.DeltaDirection || !CanRead() || UnityEngine.InputSystem.InputSystem.settings.updateMode != InputSettings.UpdateMode.ProcessEventsInDynamicUpdate)
                return;
            Evaluate(row, control);
        }

        void Evaluate(EchoInputRow row, InputControl control)
        {
            if (!row.Edges.TryGetValue(control, out var edge))
                return;
            if (edge.Evaluate(GetValue(row, control), row.Binding.PressThreshold, row.Binding.ReleaseThreshold))
            {
                _hasPendingInput = true;
                LastInput = InputControlPath.ToHumanReadableString(control.path);
            }
        }

        void HandleAfterInput()
        {
            if (InputState.currentUpdateType != InputUpdateType.Dynamic)
                return;
            if (!CanRead())
                return;
            foreach (EchoInputRow row in _rows)
                if (row.Binding.InputKind == EchoInputKind.DeltaDirection)
                    foreach (var c in row.Action.controls)
                    {
                        if (_isDeltaSkipped)
                        {
                            if (row.Edges.TryGetValue(c, out var edge))
                                edge.Synchronize(GetValue(row, c), row.Binding.ReleaseThreshold);
                        }
                        else
                            Evaluate(row, c);
                    }

            _isDeltaSkipped = false;
        }

        void Update()
        {
            if (_hasPendingInput && CanRead())
                _controller.ToggleScan();
            _hasPendingInput = false;
        }
    }
}
