using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MiraiSozo.Options
{
    /// <summary>キーボード・パッドで選択したキー設定の行をスクロール領域に収める。</summary>
    [RequireComponent(typeof(ScrollRect))]
    public sealed class OptionScrollSelection : MonoBehaviour
    {
        private ScrollRect _scroll;
        private GameObject _previous;
        private readonly Vector3[] _corners = new Vector3[4];
        private void Awake() => _scroll = GetComponent<ScrollRect>();
        private void LateUpdate()
        {
            if (EventSystem.current == null || _scroll.content == null) return;
            var selected = EventSystem.current.currentSelectedGameObject;
            if (selected == _previous) return;
            _previous = selected;
            if (selected == null || !selected.transform.IsChildOf(_scroll.content)) return;
            var rect = selected.GetComponent<RectTransform>();
            if (rect == null) return;
            rect.GetWorldCorners(_corners);
            float bottom = _scroll.viewport.InverseTransformPoint(_corners[0]).y;
            float top = _scroll.viewport.InverseTransformPoint(_corners[1]).y;
            var bounds = _scroll.viewport.rect;
            float delta = top > bounds.yMax ? top - bounds.yMax : bottom < bounds.yMin ? bottom - bounds.yMin : 0;
            _scroll.content.anchoredPosition -= new Vector2(0, delta);
        }
    }
}
