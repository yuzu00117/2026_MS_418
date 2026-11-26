using System;
using System.Collections;
using UnityEngine;

namespace MiraiSozo.Options
{
    /// <summary>
    /// 確認ダイアログなどの小さなウィンドウを、メニューと同じ機械的な展開で開閉する。
    /// 波紋は使わず、文字は最初から表示したまま枠ごと展開する。
    /// </summary>
    public sealed class OptionDialogAnimation : MonoBehaviour
    {
        // 一時停止直後などの重いフレームで演出が飛ばないよう、1フレームで進める時間の上限。
        private const float MaxFrameStep = 1f / 30f;
        private RectTransform _rect;
        private CanvasGroup _group;
        private OptionDeployLine _deployLine;
        private Coroutine _running;
        private Action _onClosed;
        private Vector3 _scale;
        private Vector3 _position;
        private float _alpha;
        private bool _interactable;
        private bool _isCaptured;

        public bool IsClosing => _onClosed != null;

        public static OptionDialogAnimation Attach(RectTransform dialog)
        {
            if (dialog == null) return null;
            if (dialog.TryGetComponent(out OptionDialogAnimation existing)) return existing;
            var animation = dialog.gameObject.AddComponent<OptionDialogAnimation>();
            animation._rect = dialog;
            animation._group = dialog.GetComponent<CanvasGroup>();
            if (animation._group == null) animation._group = dialog.gameObject.AddComponent<CanvasGroup>();
            animation._deployLine = new OptionDeployLine(dialog.gameObject.layer);
            return animation;
        }

        public void PlayOpen()
        {
            Complete();
            if (!isActiveAndEnabled) return;
            Capture();
            Apply(0f);
            _running = StartCoroutine(Run(true));
        }

        /// <summary>閉じる演出を再生し、終わったらonClosedを呼ぶ。演出中はダイアログを操作できない。</summary>
        public void PlayClose(Action onClosed)
        {
            if (IsClosing) return;
            Complete();
            if (!isActiveAndEnabled)
            {
                onClosed?.Invoke();
                return;
            }
            _onClosed = onClosed;
            Capture();
            _group.interactable = false;
            _running = StartCoroutine(Run(false));
        }

        /// <summary>再生中の演出を最後まで進める。閉じる途中ならonClosedを呼ぶ。</summary>
        public void Complete()
        {
            if (_running != null) StopCoroutine(_running);
            _running = null;
            Restore();
            var onClosed = _onClosed;
            _onClosed = null;
            onClosed?.Invoke();
        }

        private IEnumerator Run(bool opening)
        {
            float duration = opening ? OptionPanelExpansion.OpenDuration : OptionPanelExpansion.CloseDuration;
            for (float elapsed = 0f; elapsed < duration;)
            {
                yield return null;
                elapsed += Mathf.Min(Time.unscaledDeltaTime, MaxFrameStep);
                float progress = Mathf.Clamp01(elapsed / duration);
                Apply(opening ? progress : 1f - progress);
            }
            _running = null;
            Complete();
        }

        private void Capture()
        {
            _scale = _rect.localScale;
            _position = _rect.localPosition;
            _alpha = _group.alpha;
            _interactable = _group.interactable;
            _isCaptured = true;
        }

        private void Apply(float progress)
        {
            float line = OptionPanelExpansion.Apply(_rect, _scale, _position, _group, _alpha, progress);
            _deployLine.Show(_rect, line);
        }

        private void Restore()
        {
            _deployLine?.Hide();
            if (!_isCaptured) return;
            _isCaptured = false;
            _rect.localScale = _scale;
            _rect.localPosition = _position;
            _group.alpha = _alpha;
            _group.interactable = _interactable;
        }

        // 親ごと非表示にされた場合は、閉じた後の処理を呼ばずに見た目だけ元へ戻す。
        // （非表示の処理中に別の表示切り替えを起こさないため）
        private void OnDisable()
        {
            if (_running != null) StopCoroutine(_running);
            _running = null;
            _onClosed = null;
            Restore();
        }
    }
}
