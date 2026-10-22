using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SceneFlow
{
    /// <summary>
    /// 遷移中のゲーム入力(キーボード・マウス・パッドなど)を、デバイス単位で止める。
    /// Canvas のレイキャスト遮断では止まらない、キーボードやパッドの入力を止めるために使う。
    /// 解除し忘れるとキーボードが効かなくなるため、Release を遷移完了・ローダー破棄・アプリ終了のすべてで呼ぶ。
    /// </summary>
    internal static class TransitionInputBlocker
    {
        // 止めたデバイスの記録。遷移1回につき O(接続デバイス数)(遷移時のみ、ホットパスではない)
        private static readonly List<InputDevice> BlockedDevices = new List<InputDevice>();
        private static bool _isBlocking;

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
            // Play 終了時に無効のまま残らないようにする
            Application.quitting += Release;
        }

        /// <summary>止めたデバイスを元に戻す。止めていない場合は何もしない。</summary>
        public static void Release()
        {
            if (!_isBlocking) return;
            _isBlocking = false;
            Application.quitting -= Release;

            foreach (var device in BlockedDevices)
            {
                if (device.added) InputSystem.EnableDevice(device); // 遷移中に抜かれたデバイスは除く
            }
            BlockedDevices.Clear();
        }
    }
}
