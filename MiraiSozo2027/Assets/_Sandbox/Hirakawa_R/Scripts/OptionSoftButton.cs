using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MiraiSozo.Options
{
    /// <summary>ボタンに柔らかいSF調の装飾を付け、選択・マウスオンに合わせてなめらかに点灯させる。</summary>
    [RequireComponent(typeof(Selectable))]
    public sealed class OptionSoftButton : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler
    {
        // 点灯の時間に0秒が設定されても0で割らないための下限。
        private const float MinimumDuration = 0.0001f;
        private OptionSoftButtonGraphic _graphic;
        private Coroutine _fade;
        private float _highlight;
        private bool _isSelected;
        private bool _isHovered;

        /// <summary>
        /// 注目中（キー操作で選択中、またはマウスが乗っている）かどうかが変わったときに通知する。
        /// immediateは非表示になったなど、すぐ閉じるべき場合。
        /// </summary>
        public event Action<OptionSoftButton, bool, bool> FocusChanged;

        public bool IsFocused => _isSelected || _isHovered;

        private void Awake()
        {
            var detail = new GameObject("Soft Detail", typeof(RectTransform), typeof(LayoutElement));
            detail.layer = gameObject.layer;
            // ボタン内のLayoutGroupに並べられないようにする。
            detail.GetComponent<LayoutElement>().ignoreLayout = true;
            var rect = (RectTransform)detail.transform;
            rect.SetParent(transform, false);
            // ボタン文字より奥に描画する。
            rect.SetAsFirstSibling();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            _graphic = detail.AddComponent<OptionSoftButtonGraphic>();
            _graphic.raycastTarget = false;
        }

        // 非表示になったら次に開いたときに残らないよう、点灯を即座に消す。
        private void OnDisable()
        {
            if (IsFocused) FocusChanged?.Invoke(this, false, true);
            _fade = null;
            _isSelected = false;
            _isHovered = false;
            _highlight = 0f;
            if (_graphic != null) _graphic.SetHighlight(0f);
        }

        public void OnSelect(BaseEventData eventData) => SetState(true, _isHovered);
        public void OnDeselect(BaseEventData eventData) => SetState(false, _isHovered);
        public void OnPointerEnter(PointerEventData eventData) => SetState(_isSelected, true);
        public void OnPointerExit(PointerEventData eventData) => SetState(_isSelected, false);

        private void SetState(bool isSelected, bool isHovered)
        {
            bool wasFocused = IsFocused;
            _isSelected = isSelected;
            _isHovered = isHovered;
            StartFade();
            if (IsFocused != wasFocused) FocusChanged?.Invoke(this, IsFocused, false);
        }

        private void StartFade()
        {
            if (_fade == null && isActiveAndEnabled) _fade = StartCoroutine(Fade());
        }

        // ポーズ中も動くよう、unscaledDeltaTimeで目標の明るさへ近づける。
        private IEnumerator Fade()
        {
            while (true)
            {
                float target = _isSelected || _isHovered ? 1f : 0f;
                _highlight = Mathf.MoveTowards(_highlight, target, Time.unscaledDeltaTime / Mathf.Max(MinimumDuration, OptionMenuTheme.Current.HighlightDuration));
                _graphic.SetHighlight(Mathf.SmoothStep(0f, 1f, _highlight));
                if (Mathf.Approximately(_highlight, target)) break;
                yield return null;
            }
            _fade = null;
        }
    }
}
