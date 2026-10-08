// 交換可能なプロトタイプ入力。各操作を独立監視し、Updateで1回に集約してControllerへ渡す。
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace Echo.Echolocation
{
    sealed internal class EchoInputRow
    {
        public EchoInputBinding Binding { get; set; }
        public InputAction Action { get; set; }
        public Dictionary<InputControl, EchoInputEdgeDetector> Edges { get; } = new Dictionary<InputControl, EchoInputEdgeDetector>();
    }
}
