// 仮想キーボード・マウス・ゲームパッドを使い入力のOR条件と同時入力を検証する。
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Echo.Echolocation.Tests
{
    public sealed class EchoPrototypeInputTests
    {
        private GameObject _go;
        private Keyboard _keyboard;
        private Mouse _mouse;
        private Gamepad _pad;
        private InputSettings.BackgroundBehavior _oldBackground;
        private 
#if UNITY_EDITOR
        InputSettings.EditorInputBehaviorInPlayMode _oldEditorBehavior;
#endif
        [UnitySetUp]
        public IEnumerator Setup()
        {
            UnityEngine.InputSystem.InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
            _oldBackground = UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior;
            UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            _oldEditorBehavior = UnityEngine.InputSystem.InputSystem.settings.editorInputBehaviorInPlayMode;
            UnityEngine.InputSystem.InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            _keyboard = UnityEngine.InputSystem.InputSystem.AddDevice<Keyboard>();
            _mouse = UnityEngine.InputSystem.InputSystem.AddDevice<Mouse>();
            _pad = UnityEngine.InputSystem.InputSystem.AddDevice<Gamepad>();
            _go = new GameObject("Input test");
            var c = _go.AddComponent<EchoController>();
            c.Settings.Duration = 0;
            var input = _go.AddComponent<EchoPrototypeInput>();
            input.Bindings.Add(new EchoInputBinding { ControlPath = "<Mouse>/rightButton" });
            input.Bindings.Add(new EchoInputBinding { ControlPath = "<Gamepad>/buttonSouth" });
            input.RebuildBindings();
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (_go)
                Object.Destroy(_go);
            yield return null;
            if (_keyboard != null)
                UnityEngine.InputSystem.InputSystem.RemoveDevice(_keyboard);
            if (_mouse != null)
                UnityEngine.InputSystem.InputSystem.RemoveDevice(_mouse);
            if (_pad != null)
                UnityEngine.InputSystem.InputSystem.RemoveDevice(_pad);
            UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior = _oldBackground;
#if UNITY_EDITOR
            UnityEngine.InputSystem.InputSystem.settings.editorInputBehaviorInPlayMode = _oldEditorBehavior;
#endif
        }

        [UnityTest]
        public IEnumerator SimultaneousInputsCollapseAndAnotherHeldSourceDoesNotBlock()
        {
            var c = _go.GetComponent<EchoController>();
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.E));
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(_pad, new GamepadState().WithButton(GamepadButton.South));
            yield return null;
            yield return null;
            Assert.True(c.IsScanRequested, "同時に2つ押しても1回だけONになる");
            yield return null;
            Assert.True(c.IsScanRequested, "長押しで再発火しない");
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(_mouse, new MouseState().WithButton(MouseButton.Right));
            yield return null;
            yield return null;
            Assert.False(c.IsScanRequested, "Eとパッドを押したままでも別入力で切り替わる");
        }

        [UnityTest]
        public IEnumerator QuickTapWithinOneFrameIsNotLost()
        {
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.E));
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            yield return null;
            yield return null;
            Assert.True(_go.GetComponent<EchoController>().IsScanRequested);
        }

        [UnityTest]
        public IEnumerator EmptyBindingsDoNotRestoreE()
        {
            var input = _go.GetComponent<EchoPrototypeInput>();
            input.Bindings.Clear();
            input.RebuildBindings();
            yield return null;
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.E));
            yield return null;
            yield return null;
            Assert.False(_go.GetComponent<EchoController>().IsScanRequested);
        }

        [UnityTest]
        public IEnumerator TriggerUsesHysteresisAndDoesNotRepeat()
        {
            var input = _go.GetComponent<EchoPrototypeInput>();
            input.Bindings.Clear();
            input.Bindings.Add(new EchoInputBinding { ControlPath = "<Gamepad>/leftTrigger", InputKind = EchoInputKind.AxisDirection });
            input.RebuildBindings();
            yield return null;
            var c = _go.GetComponent<EchoController>();
            UnityEngine.InputSystem.InputSystem.QueueDeltaStateEvent(_pad.leftTrigger, .6f);
            yield return null;
            yield return null;
            Assert.True(c.IsScanRequested);
            UnityEngine.InputSystem.InputSystem.QueueDeltaStateEvent(_pad.leftTrigger, .4f);
            yield return null;
            UnityEngine.InputSystem.InputSystem.QueueDeltaStateEvent(_pad.leftTrigger, .6f);
            yield return null;
            yield return null;
            Assert.True(c.IsScanRequested);
            UnityEngine.InputSystem.InputSystem.QueueDeltaStateEvent(_pad.leftTrigger, 0f);
            yield return null;
            UnityEngine.InputSystem.InputSystem.QueueDeltaStateEvent(_pad.leftTrigger, .6f);
            yield return null;
            yield return null;
            Assert.False(c.IsScanRequested);
        }

        [UnityTest]
        public IEnumerator EnablingWhileHeldWaitsForRelease()
        {
            var input = _go.GetComponent<EchoPrototypeInput>();
            input.enabled = false;
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.E));
            yield return null;
            input.enabled = true;
            yield return null;
            yield return null;
            Assert.False(_go.GetComponent<EchoController>().IsScanRequested);
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            yield return null;
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.E));
            yield return null;
            yield return null;
            Assert.True(_go.GetComponent<EchoController>().IsScanRequested);
        }

        [UnityTest]
        public IEnumerator WheelDeltaResetDoesNotRepeatWhileMoving()
        {
            var input = _go.GetComponent<EchoPrototypeInput>();
            input.Bindings.Clear();
            input.Bindings.Add(new EchoInputBinding { ControlPath = "<Mouse>/scroll/y", InputKind = EchoInputKind.DeltaDirection, PressThreshold = 1, ReleaseThreshold = 0 });
            input.RebuildBindings();
            yield return null;
            yield return null;
            var c = _go.GetComponent<EchoController>();
            for (int i = 0; i < 4; i++)
            {
                UnityEngine.InputSystem.InputSystem.QueueDeltaStateEvent(_mouse.scroll, new Vector2(0, 120));
                yield return null;
            }

            yield return null;
            Assert.True(c.IsScanRequested);
            yield return null;
            UnityEngine.InputSystem.InputSystem.QueueDeltaStateEvent(_mouse.scroll, new Vector2(0, 120));
            yield return null;
            yield return null;
            Assert.False(c.IsScanRequested);
        }
    }
}
