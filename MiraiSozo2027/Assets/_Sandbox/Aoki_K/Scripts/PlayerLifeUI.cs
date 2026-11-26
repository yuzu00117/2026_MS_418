using System.Collections;
using System.Collections.Generic;
using Aura.Core;
using Aura.Entities;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Aura.UI
{
    public class PlayerLifeUI : MonoBehaviour
    {
        /// <summary>
        /// 自動生成された各扇形ピースの参照とアニメーション用の基準情報
        /// </summary>
        private class SegmentPiece
        {
            public GameObject RootObject;
            public RectTransform Rect;
            public Image Image;
            public Vector2 OutwardDirection; // 扇形の中心から外側へ向かう方向ベクトル（被弾演出用）
            public Coroutine ActiveCoroutine;
        }

        [Header("UI References")]
        [Tooltip("中央の残りライフ数を表示するテキスト")]
        [SerializeField] private TMP_Text _lifeCountText;

        [Tooltip("扇形ピースを自動生成して並べる親のRectTransform（未指定時は自身のRectTransform）")]
        [SerializeField] private RectTransform _segmentContainer;

        [Tooltip("扇形に切り抜く元になる円形の画像（必須）")]
        [SerializeField] private Sprite _circleSprite;

        [Header("Auto Segment Layout (自動扇形カット・配置設定)")]
        [Tooltip("各扇形ピース間の隙間（スリット）の角度（度）")]
        [SerializeField, Range(0f, 45f)] private float _gapAngle = 10f;

        [Tooltip("1つ目のピースの開始角度オフセット（度、0で真上12時方向が基準）")]
        [SerializeField, Range(-180f, 180f)] private float _startAngleOffset = 0f;

        [Tooltip("生成する扇形ピースのサイズ（0以下の場合は親のSegmentContainerと同じサイズ）")]
        [SerializeField] private Vector2 _segmentSize = Vector2.zero;

        [Header("Color Settings (ゲージ色設定)")]
        [Tooltip("ライフ満タン時のゲージ色")]
        [SerializeField] private Color _healthyColor = new Color(0.2f, 0.9f, 0.4f, 1f);
        [Tooltip("ライフ中程度（半分以下）のゲージ色")]
        [SerializeField] private Color _cautionColor = new Color(1.0f, 0.85f, 0.2f, 1f);
        [Tooltip("ライフ危険時（残り1）のゲージ色")]
        [SerializeField] private Color _dangerColor = new Color(1.0f, 0.25f, 0.25f, 1f);

        [Header("Animation Settings (被弾時アニメーション)")]
        [Tooltip("失われたピースが外側へ弾けて消えるアニメーションの時間（秒）")]
        [SerializeField, Min(0.05f)] private float _losePieceDuration = 0.25f;

        [Tooltip("失われたピースが外側へ飛び出す距離（ピクセル）")]
        [SerializeField, Min(0f)] private float _losePiecePopDistance = 20f;

        [Tooltip("失われたピースが弾ける際の最大拡大倍率")]
        [SerializeField, Min(1f)] private float _losePieceMaxScale = 1.25f;

        [Tooltip("残ったピースが被弾時にバウンス（拡大縮小）する倍率")]
        [SerializeField, Min(1f)] private float _remainBounceScale = 1.15f;

        private readonly List<SegmentPiece> _segments = new List<SegmentPiece>();
        private Player _targetPlayer;
        private int _displayedMaxHp = -1;
        private int _previousHp = -1;

        private void Awake()
        {
            if (_segmentContainer == null)
            {
                _segmentContainer = GetComponent<RectTransform>();
            }

            if (_circleSprite == null)
            {
                Debug.LogError($"{nameof(PlayerLifeUI)}: CircleSprite が設定されていません。", this);
            }
        }

        private void OnEnable()
        {
            GameManager.OnPlayerSpawned += HandlePlayerSpawned;
        }

        private void OnDisable()
        {
            GameManager.OnPlayerSpawned -= HandlePlayerSpawned;
            UnsubscribePlayer();
        }

        private void Start()
        {
            if (GameManager.Instance != null && GameManager.Instance.CurrentPlayer != null)
            {
                BindPlayer(GameManager.Instance.CurrentPlayer);
            }
            else
            {
                Player existingPlayer = FindFirstObjectByType<Player>();
                if (existingPlayer != null)
                {
                    BindPlayer(existingPlayer);
                }
            }
        }

        private void HandlePlayerSpawned(Player player)
        {
            BindPlayer(player);
        }

        private void BindPlayer(Player player)
        {
            if (_targetPlayer == player) return;

            UnsubscribePlayer();
            _targetPlayer = player;

            if (_targetPlayer != null)
            {
                _targetPlayer.OnHpChanged += HandleHpChanged;
                _previousHp = _targetPlayer.CurrentHp;
                UpdateLifeView(_targetPlayer.CurrentHp, _targetPlayer.MaxHp, isInitial: true);
            }
        }

        private void UnsubscribePlayer()
        {
            if (_targetPlayer != null)
            {
                _targetPlayer.OnHpChanged -= HandleHpChanged;
                _targetPlayer = null;
            }
        }

        private void HandleHpChanged(int currentHp, int maxHp)
        {
            UpdateLifeView(currentHp, maxHp, isInitial: false);
            _previousHp = currentHp;
        }

        /// <summary>
        /// 最大HPに合わせて円形画像を扇形ピースに切り抜いて自動配置する
        /// </summary>
        private void RebuildSegments(int maxHp)
        {
            if (_segmentContainer == null)
            {
                _segmentContainer = GetComponent<RectTransform>();
            }

            if (_circleSprite == null)
            {
                Debug.LogError($"{nameof(PlayerLifeUI)}: CircleSprite が未設定のため扇形ピースを生成できません。", this);
                return;
            }

            if (_segmentContainer == null || maxHp <= 0) return;

            // 既存の自動生成ピースを破棄
            for (int i = 0; i < _segments.Count; i++)
            {
                if (_segments[i].RootObject != null)
                {
                    Destroy(_segments[i].RootObject);
                }
            }
            _segments.Clear();
            _displayedMaxHp = maxHp;

            // 1ピースあたりの割り当て角度と、隙間を引いた扇形の角度を計算
            float stepAngle = 360f / maxHp;
            float actualGap = maxHp > 1 ? Mathf.Min(_gapAngle, stepAngle - 1f) : 0f;
            float wedgeAngle = stepAngle - actualGap;
            float wedgeFillAmount = Mathf.Clamp01(wedgeAngle / 360f);

            Vector2 size = (_segmentSize.x > 0f && _segmentSize.y > 0f)
                ? _segmentSize
                : _segmentContainer.rect.size;

            for (int i = 0; i < maxHp; i++)
            {
                GameObject pieceObj = new GameObject($"LifeSegment_{i + 1}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                RectTransform rect = pieceObj.GetComponent<RectTransform>();
                rect.SetParent(_segmentContainer, false);
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = size;

                // 真上(12時方向)を基準に、反時計回りでピースを配置（減るときに右上→下→左上の反時計回りに減る）
                float counterClockwiseStartAngle = _startAngleOffset + (i * stepAngle) + (actualGap * 0.5f);
                rect.localRotation = Quaternion.Euler(0f, 0f, counterClockwiseStartAngle);
                rect.localScale = Vector3.one;

                // Radial 360（反時計回り）で円形画像を1ピース分の扇形に切り抜く
                Image img = pieceObj.GetComponent<Image>();
                img.sprite = _circleSprite;
                img.type = Image.Type.Filled;
                img.fillMethod = Image.FillMethod.Radial360;
                img.fillOrigin = (int)Image.Origin360.Top;
                img.fillClockwise = false;
                img.fillAmount = wedgeFillAmount;
                img.raycastTarget = false;

                // 被弾アニメーション用に、この扇形ピースの中心から外側へ向かうベクトルを計算
                float centerAngleRad = (counterClockwiseStartAngle + wedgeAngle * 0.5f) * Mathf.Deg2Rad;
                Vector2 outwardDir = new Vector2(-Mathf.Sin(centerAngleRad), Mathf.Cos(centerAngleRad));

                _segments.Add(new SegmentPiece
                {
                    RootObject = pieceObj,
                    Rect = rect,
                    Image = img,
                    OutwardDirection = outwardDir
                });
            }
        }

        private void UpdateLifeView(int currentHp, int maxHp, bool isInitial)
        {
            // 最大HPが変わった場合（または初回）に扇形ピースを自動生成・再配置
            if (_displayedMaxHp != maxHp || _segments.Count != maxHp)
            {
                RebuildSegments(maxHp);
            }

            // 1. 中央の数字テキスト更新
            if (_lifeCountText != null)
            {
                _lifeCountText.SetText("{0}", currentHp);
            }

            // 残量に応じた色を決定
            Color gaugeColor = GetColorForHp(currentHp, maxHp);

            // 2. 各扇形ピースの状態更新とアニメーション
            for (int i = 0; i < _segments.Count; i++)
            {
                SegmentPiece piece = _segments[i];
                if (piece.Image == null) continue;

                bool isActive = i < currentHp;
                bool wasActive = !isInitial && _previousHp > i;

                // 進行中のアニメーションがあれば停止して基準位置に戻す
                if (piece.ActiveCoroutine != null)
                {
                    StopCoroutine(piece.ActiveCoroutine);
                    piece.ActiveCoroutine = null;
                }
                piece.Rect.anchoredPosition = Vector2.zero;
                piece.Rect.localScale = Vector3.one;

                if (isActive)
                {
                    piece.Image.enabled = true;
                    piece.Image.color = gaugeColor;

                    // ダメージを受けた瞬間、残っているピースも軽くバウンスさせる
                    if (!isInitial && currentHp < _previousHp && isActiveAndEnabled)
                    {
                        piece.ActiveCoroutine = StartCoroutine(AnimateRemainingPieceBounce(piece));
                    }
                }
                else
                {
                    // 今まさに失われたピースなら、外側へ弾けて消えるアニメーションを再生
                    if (wasActive && isActiveAndEnabled)
                    {
                        piece.ActiveCoroutine = StartCoroutine(AnimateLostPiecePopOut(piece, gaugeColor));
                    }
                    else
                    {
                        piece.Image.enabled = false;
                    }
                }
            }
        }

        private const float MaxAnimationDeltaTime = 0.033f;

        /// <summary>
        /// 失われた扇形ピースが外側へ飛び出しながら拡大・フェードアウトするアニメーション
        /// </summary>
        private IEnumerator AnimateLostPiecePopOut(SegmentPiece piece, Color baseColor)
        {
            piece.Image.enabled = true;
            piece.Image.color = baseColor;
            float elapsed = 0f;

            // ContextMenu等による発火フレームの巨大なdeltaTimeをスキップするため1フレーム待つ
            yield return null;

            while (elapsed < _losePieceDuration)
            {
                elapsed += Mathf.Min(Time.unscaledDeltaTime, MaxAnimationDeltaTime);
                float t = Mathf.Clamp01(elapsed / _losePieceDuration);
                // イーズアウト補間
                float easeOut = 1f - (1f - t) * (1f - t);

                piece.Rect.anchoredPosition = piece.OutwardDirection * (_losePiecePopDistance * easeOut);
                float scale = Mathf.Lerp(1f, _losePieceMaxScale, easeOut);
                piece.Rect.localScale = new Vector3(scale, scale, 1f);

                Color c = baseColor;
                c.a = 1f - t;
                piece.Image.color = c;

                yield return null;
            }

            piece.Rect.anchoredPosition = Vector2.zero;
            piece.Rect.localScale = Vector3.one;
            piece.Image.enabled = false;
            piece.ActiveCoroutine = null;
        }

        /// <summary>
        /// 被弾時に残っているピースがぷるんとバウンスするアニメーション
        /// </summary>
        private IEnumerator AnimateRemainingPieceBounce(SegmentPiece piece)
        {
            const float duration = 0.18f;
            float elapsed = 0f;

            yield return null;

            while (elapsed < duration)
            {
                elapsed += Mathf.Min(Time.unscaledDeltaTime, MaxAnimationDeltaTime);
                float t = Mathf.Clamp01(elapsed / duration);
                // sin波で 1.0 -> _remainBounceScale -> 1.0 に戻る
                float scale = Mathf.Lerp(1f, _remainBounceScale, Mathf.Sin(t * Mathf.PI));
                piece.Rect.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }

            piece.Rect.localScale = Vector3.one;
            piece.ActiveCoroutine = null;
        }

        private Color GetColorForHp(int currentHp, int maxHp)
        {
            if (currentHp <= 1) return _dangerColor;
            if (currentHp < maxHp) return _cautionColor;
            return _healthyColor;
        }
    }
}
