// Inspector上の割り当て1行。特定のキー列挙型ではなくInput Systemのパスで保存する。
using System;
using UnityEngine;

namespace Echo.Echolocation
{
    /// <summary>ボタン、持続する軸、更新ごとの変位の区別。</summary>
    public enum EchoInputKind
    {
        Button,
        AxisDirection,
        DeltaDirection
    }
}
