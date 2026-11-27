using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MiraiSozo.Options
{
    /// <summary>
    /// キーボード・パッドで今選んでいる項目に枠を重ねて表示する。
    /// メニューのオーバーレイに付け、オーバーレイが非表示の間は動かない。
    /// </summary>
    /// <remarks>
    /// 枠は選択中の項目より少し外側に、4本の線で囲む。
    ///
    ///   +------------------+  <- 上
    ///   |                  |
    ///   |  選択中の項目    |  <- 左 / 右
    ///   |                  |
    ///   +------------------+  <- 下
    ///
    /// 計算量: 1フレームあたり O(1)。
    /// </remarks>
    [RequireComponent(typeof(RectTransform))]
    public sealed class OptionSelectionHighlight : MonoBehaviour
    {
        private const string FrameName = "Selection Frame";
        private const float FrameThickness = 3f;
        private const float FrameOutset = 4f;
        private const int EdgeCount = 4;
        private const int CornerCount = 4;
        private const int BottomLeftCorner = 0;
        private const int TopRightCorner = 2;
        private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
        private static readonly Color FrameColor = Color.white;

        private readonly Vector3[] _corners = new Vector3[CornerCount];
        private readonly RectTransform[] _edges = new RectTransform[EdgeCount];
        private RectTransform _root;
        private RectTransform _frame;

        private void Awake()
        {
            _root = (RectTransform)transform;
            _frame = new GameObject(FrameName, typeof(RectTransform)).GetComponent<RectTransform>();
            _frame.SetParent(_root, false);
            _frame.anchorMin = _frame.anchorMax = Center;
            for (int i = 0; i < EdgeCount; i++) _edges[i] = CreateEdge(i);
            // 上・下・左・右の順に、枠の内側へ線を置く
            SetEdge(_edges[0], new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, FrameThickness));
            SetEdge(_edges[1], new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, FrameThickness));
            SetEdge(_edges[2], new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(FrameThickness, 0));
            SetEdge(_edges[3], new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(FrameThickness, 0));
            _frame.gameObject.SetActive(false);
        }

        private RectTransform CreateEdge(int index)
        {
            var edge = new GameObject(FrameName + " Edge " + index, typeof(RectTransform)).GetComponent<RectTransform>();
            edge.SetParent(_frame, false);
            var image = edge.gameObject.AddComponent<Image>();
            image.color = FrameColor;
            image.raycastTarget = false;
            return edge;
        }

        private void OnDisable()
        {
            if (_frame != null) _frame.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            var target = selected != null && selected.activeInHierarchy && selected.transform.IsChildOf(_root)
                ? selected.transform as RectTransform
                : null;
            if (target == null)
            {
                if (_frame.gameObject.activeSelf) _frame.gameObject.SetActive(false);
                return;
            }
            if (!_frame.gameObject.activeSelf) _frame.gameObject.SetActive(true);
            // 確認ダイアログなど後から並ぶパネルより手前に描く
            if (_frame.GetSiblingIndex() != _root.childCount - 1) _frame.SetAsLastSibling();
            FitTo(target);
        }

        private void FitTo(RectTransform target)
        {
            // 項目の四隅をオーバーレイ基準の座標に直す（拡大縮小やスクロールにも追従する）
            target.GetWorldCorners(_corners);
            Vector2 min = _root.InverseTransformPoint(_corners[BottomLeftCorner]);
            Vector2 max = _root.InverseTransformPoint(_corners[TopRightCorner]);
            min -= Vector2.one * FrameOutset;
            max += Vector2.one * FrameOutset;
            _frame.sizeDelta = max - min;
            _frame.anchoredPosition = (min + max) * 0.5f - _root.rect.center;
        }

        private static void SetEdge(RectTransform edge, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size)
        {
            edge.anchorMin = anchorMin;
            edge.anchorMax = anchorMax;
            edge.pivot = pivot;
            edge.sizeDelta = size;
            edge.anchoredPosition = Vector2.zero;
        }
    }
}
