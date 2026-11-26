using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MiraiSozo.Options
{
    /// <summary>
    /// メニューのマウス操作とキー操作を切り替える。マウスを動かすとキー操作の選択を外し、
    /// キーを押すとマウスオンの表示を外して、表示中の画面で一番上の項目から選択を始める。
    /// </summary>
    public sealed class OptionInputMode : MonoBehaviour
    {
        // 手の震え程度のわずかなマウスの動きでは切り替えない（Canvas上ではなく画面のピクセル）。
        private const float MouseMoveThreshold = 2f;
        private const float StickThreshold = 0.5f;
        // 縦位置の差がこれ以下の項目は同じ行とみなし、左にある方を上として扱う。
        private const float SameRowTolerance = 1f;
        private readonly List<Selectable> _selectables = new List<Selectable>();
        private readonly List<RaycastResult> _raycastResults = new List<RaycastResult>();
        private Func<Transform> _getRoot;
        private Func<bool> _isBlocked;
        private bool _usingMouse;
        private bool _wasStickHeld;

        /// <summary>getRootは表示中の画面（選択を始める範囲）、isBlockedは切り替えを止める条件（キー設定の入力待ちなど）。</summary>
        public static OptionInputMode Attach(GameObject target, Func<Transform> getRoot, Func<bool> isBlocked)
        {
            if (!target.TryGetComponent(out OptionInputMode mode)) mode = target.AddComponent<OptionInputMode>();
            mode._getRoot = getRoot;
            mode._isBlocked = isBlocked;
            return mode;
        }

        // メニューを開いた直後は、キー操作で一番上の項目を選んだ状態から始める。
        private void OnEnable()
        {
            _usingMouse = false;
            _wasStickHeld = false;
        }

        // 選択の移動は入力モジュールのUpdateで処理されるため、その後のLateUpdateで切り替える。
        // （先に一番上を選ぶと、同じフレームのキー入力でさらに一つ下へ移ってしまう）
        private void LateUpdate()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null || _getRoot == null || (_isBlocked != null && _isBlocked())) return;
            var root = _getRoot();
            if (root == null) return;
            bool keyPressed = IsKeyPressedThisFrame();
            if (IsMouseUsedThisFrame()) EnterMouseMode(eventSystem);
            else if (keyPressed && (_usingMouse || !IsSelectedIn(eventSystem, root))) EnterKeyMode(eventSystem, root);
            // マウス操作中は、クリックや画面の切り替えで付いた選択表示を残さない。
            if (_usingMouse && eventSystem.currentSelectedGameObject != null) eventSystem.SetSelectedGameObject(null);
        }

        private static bool IsMouseUsedThisFrame()
        {
            var mouse = Mouse.current;
            if (mouse == null) return false;
            return mouse.delta.ReadValue().sqrMagnitude > MouseMoveThreshold * MouseMoveThreshold
                || mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame
                || mouse.scroll.ReadValue().sqrMagnitude > 0f;
        }

        private bool IsKeyPressedThisFrame()
        {
            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;
            var gamepad = Gamepad.current;
            if (gamepad == null) return false;
            // スティックは倒し始めた瞬間だけを入力とみなす。
            bool isStickHeld = gamepad.leftStick.ReadValue().magnitude > StickThreshold;
            bool stickPressed = isStickHeld && !_wasStickHeld;
            _wasStickHeld = isStickHeld;
            return stickPressed || gamepad.dpad.up.wasPressedThisFrame || gamepad.dpad.down.wasPressedThisFrame
                || gamepad.dpad.left.wasPressedThisFrame || gamepad.dpad.right.wasPressedThisFrame
                || gamepad.buttonSouth.wasPressedThisFrame || gamepad.buttonEast.wasPressedThisFrame
                || gamepad.startButton.wasPressedThisFrame;
        }

        private static bool IsSelectedIn(EventSystem eventSystem, Transform root)
        {
            var selected = eventSystem.currentSelectedGameObject;
            return selected != null && selected.activeInHierarchy && selected.transform.IsChildOf(root);
        }

        private void EnterMouseMode(EventSystem eventSystem)
        {
            bool wasUsingKeys = !_usingMouse;
            _usingMouse = true;
            eventSystem.SetSelectedGameObject(null);
            // キー操作中に外したマウスオンの表示を、カーソルの下の項目へ戻す。
            if (wasUsingKeys) RestoreHover(eventSystem);
        }

        private void EnterKeyMode(EventSystem eventSystem, Transform root)
        {
            _usingMouse = false;
            var pointer = new PointerEventData(eventSystem);
            root.GetComponentsInChildren(false, _selectables);
            foreach (var selectable in _selectables) ExecuteEvents.Execute(selectable.gameObject, pointer, ExecuteEvents.pointerExitHandler);
            var top = FindTop();
            _selectables.Clear();
            if (top != null) eventSystem.SetSelectedGameObject(top.gameObject);
        }

        // 画面上で一番上（同じ行なら一番左）にある、操作できる項目。
        private Selectable FindTop()
        {
            Selectable top = null;
            Vector3 topPosition = default;
            foreach (var selectable in _selectables)
            {
                if (!selectable.IsInteractable() || !selectable.isActiveAndEnabled) continue;
                var rect = (RectTransform)selectable.transform;
                Vector3 position = rect.TransformPoint(rect.rect.center);
                bool isHigher = top == null || position.y > topPosition.y + SameRowTolerance
                    || (Mathf.Abs(position.y - topPosition.y) <= SameRowTolerance && position.x < topPosition.x);
                if (!isHigher) continue;
                top = selectable;
                topPosition = position;
            }
            return top;
        }

        private void RestoreHover(EventSystem eventSystem)
        {
            var mouse = Mouse.current;
            if (mouse == null) return;
            var pointer = new PointerEventData(eventSystem) { position = mouse.position.ReadValue() };
            eventSystem.RaycastAll(pointer, _raycastResults);
            var hit = _raycastResults.Count > 0 ? _raycastResults[0].gameObject : null;
            _raycastResults.Clear();
            var handler = ExecuteEvents.GetEventHandler<IPointerEnterHandler>(hit);
            if (handler != null) ExecuteEvents.Execute(handler, pointer, ExecuteEvents.pointerEnterHandler);
        }
    }
}
