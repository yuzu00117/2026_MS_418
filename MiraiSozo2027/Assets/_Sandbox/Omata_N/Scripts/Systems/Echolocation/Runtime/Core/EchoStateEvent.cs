// スキャンの操作キュー・状態遷移・描画データの作成を管理する。入力方式とURPを知らない。
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using Unity.Profiling;

namespace Echo.Echolocation
{
    /// <summary>Inspectorから接続できる状態変更通知。</summary>
    [Serializable]
    public sealed class EchoStateEvent : UnityEvent<EchoState, EchoState>
    {
    }
}
