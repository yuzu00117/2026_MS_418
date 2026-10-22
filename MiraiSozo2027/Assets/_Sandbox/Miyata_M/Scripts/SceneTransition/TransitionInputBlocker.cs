using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SceneFlow
{
    /// <summary>
    /// 遷移中のゲーム入力(キーボード・マウス・パッドなど)を、デバイス単位で止める。
    /// Canvas のレイキャスト遮断では止まらない、キーボードやパッドの入力を止めるために使う。
    /// 解除し忘れるとキーボードが効かなくなるため、Release を遷移完了・ローダー破棄・アプリ終了のすべてで呼ぶ。
    ///
    /// 抜き差しされたデバイスの扱い(Input System の挙動を実験で確認済み):
    ///   - 無効にしたデバイスは、抜かれて再接続されても無効のままになる
    ///   - 抜かれたデバイスに EnableDevice を呼ぶと InvalidOperationException になる
    ///
    ///   遷移中に抜かれた -> Release 時は復帰できない -> 保留に回し、再接続された時点で復帰する
    ///   遷移中に再接続   -> 止める(記録に残っていれば二重登録しない)
    ///   遷移中に新規接続 -> 止める
    /// </summary>
    internal static class TransitionInputBlocker
    {
        // 止めたデバイスの記録。遷移1回につき O(接続デバイス数)(遷移時のみ、ホットパスではない)
        private static readonly List<InputDevice> BlockedDevices = new List<InputDevice>();

        // 遷移中に抜かれ、まだ復帰できていないデバイス(再接続されたら復帰する)
        private static readonly List<InputDevice> PendingDevices = new List<InputDevice>();

        private static bool _isBlocking;
        private static bool _isListening;

        /// <summary>有効なデバイスをすべて無効化する。すでに止めている場合は何もしない。</summary>
        public static void Block()
        {
            if (_isBlocking) return;
            _isBlocking = true;

            foreach (var device in InputSystem.devices)
            {
                if (!device.enabled) continue; // 元から無効なもの(センサー等)は触らない
                BlockedDevices.Add(device);
                InputSystem.DisableDevice(device);
            }
            Listen();
        }

        /// <summary>止めたデバイスを元に戻す。止めていない場合は何もしない。</summary>
        public static void Release()
        {
            if (!_isBlocking) return;
            _isBlocking = false;

            foreach (var device in BlockedDevices)
            {
                if (device.added) InputSystem.EnableDevice(device);
                else PendingDevices.Add(device); // 抜かれている間は戻せない。再接続を待つ
            }
            BlockedDevices.Clear();

            if (PendingDevices.Count == 0) StopListening();
        }

        private static void Listen()
        {
            if (_isListening) return;
            _isListening = true;
            InputSystem.onDeviceChange += HandleDeviceChange;
            // Play 終了時に無効のまま残らないようにする
            Application.quitting += HandleQuit;
        }

        private static void StopListening()
        {
            if (!_isListening) return;
            _isListening = false;
            InputSystem.onDeviceChange -= HandleDeviceChange;
            Application.quitting -= HandleQuit;
        }

        private static void HandleQuit()
        {
            Release();
            PendingDevices.Clear();
            StopListening();
        }

        private static void HandleDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (change != InputDeviceChange.Added && change != InputDeviceChange.Reconnected) return;

            if (_isBlocking)
            {
                // 遷移中に接続・再接続された。再接続の場合は、すでに記録済みのことがある
                if (!BlockedDevices.Contains(device)) BlockedDevices.Add(device);
                PendingDevices.Remove(device);
                if (device.enabled) InputSystem.DisableDevice(device);
                return;
            }

            // 遷移の完了後に再接続された。無効のまま戻ってくるため、ここで復帰させる
            if (PendingDevices.Remove(device))
            {
                InputSystem.EnableDevice(device);
                if (PendingDevices.Count == 0) StopListening();
            }
        }
    }
}
