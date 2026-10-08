// Inspector上の割り当て1行。特定のキー列挙型ではなくInput Systemのパスで保存する。
using System;
using UnityEngine;

namespace Echo.Echolocation
{
    /// <summary>1つの入力をON/OFF切り替えへ変換する設定。</summary>
    [Serializable]
    public sealed class EchoInputBinding
    {
        [Tooltip("この入力を有効にします。")]
        [UnityEngine.SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("Enabled")]
        private bool _isEnabled = true;
        public bool IsEnabled { get => _isEnabled; set => _isEnabled = value; }

        [Tooltip("Input Systemの機器と操作のパス。選択ボタンから設定できます。")]
        [UnityEngine.SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("ControlPath")]
        private string _controlPath = "<Keyboard>/e";
        public string ControlPath { get => _controlPath; set => _controlPath = value; }

        [Tooltip("ボタン・通常軸・マウス変位の種類です。")]
        [UnityEngine.SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("InputKind")]
        private EchoInputKind _inputKind;
        public EchoInputKind InputKind { get => _inputKind; set => _inputKind = value; }

        [Tooltip("軸の負方向を使います。ボタンでは使用しません。")]
        [UnityEngine.SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("Negative")]
        private bool _isNegative;
        public bool IsNegative { get => _isNegative; set => _isNegative = value; }

        [Tooltip("この値以上になった瞬間に切り替えます。")]
        [UnityEngine.SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("PressThreshold")]
        private float _pressThreshold = .5f;
        public float PressThreshold { get => _pressThreshold; set => _pressThreshold = value; }

        [Tooltip("この値以下へ戻った後、次の押下を受け付けます。")]
        [UnityEngine.SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("ReleaseThreshold")]
        private float _releaseThreshold = .25f;
        public float ReleaseThreshold { get => _releaseThreshold; set => _releaseThreshold = value; }

        /// <summary>入力が範囲外のときは種類に応じた安全な初期値へ戻す。</summary>
        public void Validate()
        {
            if (float.IsNaN(PressThreshold) || float.IsInfinity(PressThreshold) || float.IsNaN(ReleaseThreshold) || float.IsInfinity(ReleaseThreshold) || ReleaseThreshold < 0 || PressThreshold <= ReleaseThreshold || (InputKind != EchoInputKind.DeltaDirection && PressThreshold > 1))
            {
                PressThreshold = InputKind == EchoInputKind.DeltaDirection ? 1 : .5f;
                ReleaseThreshold = InputKind == EchoInputKind.DeltaDirection ? 0 : .25f;
            }
        }
    }
}
