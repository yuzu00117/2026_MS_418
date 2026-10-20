using System.Collections.Generic;
using System.Text;
using Aura.Inspector;
using Aura.ObjectInfo;
using Aura.Wave;
using UnityEngine;

namespace Aura.ObjectInfoDemo
{
    /// @brief 照準と、今のビーム、最後のビームが当たった物、照準の先の物が持つ情報（ObjectInfo）を画面に出す（確かめる用）
    /// 念力やエコーの動きは無いので、物に付けた情報が正しいかを、ここで目で確かめる
    public sealed class InfoHud : MonoBehaviour
    {
        private const float CrosshairSize = 6.0f;
        private const float RefreshInterval = 0.1f;  ///< 文字を作り直す間隔（秒）。毎フレーム文字列を作らないようにする
        private const float InspectRange = 50.0f;    ///< 照準の先の物を調べる距離（m）
        private const float LabelMargin = 16.0f;
        private const float LabelMinWidth = 400.0f;
        private const float LabelHeight = 520.0f;
        private const float TextWhiteBlend = 0.4f;   ///< 文字の色を、ビームの色から白へ寄せる割合
        private const string Separator = " → ";

        [InspectorLabel("ビームを出す部品")]
        [Tooltip("状態（今のビーム、最後のビームが当たった物）を出す InfoBeamEmitter。照準の先も、これの視点から調べる\n" +
                 "空なら照準だけを出す")]
        [SerializeField] private InfoBeamEmitter _emitter;  ///< 状態を出す InfoBeamEmitter

        private readonly StringBuilder _text = new StringBuilder();
        private readonly List<IObjectInfo> _infos = new List<IObjectInfo>();  ///< 情報の持ち主を探すときの使い回しのリスト
        private GUIStyle _labelStyle;
        private string _displayText = string.Empty;  ///< 画面に出す文字。作り直したときだけ更新する
        private float _nextRefreshTime;

        private void Update()
        {
            if (_emitter == null || Time.unscaledTime < _nextRefreshTime)
            {
                return;
            }
            _nextRefreshTime = Time.unscaledTime + RefreshInterval;
            RebuildText();
        }

        private void OnGUI()
        {
            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true };
            }

            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            GUI.DrawTexture(new Rect(center.x - CrosshairSize * 0.5f, center.y - 1.0f, CrosshairSize, 2.0f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(center.x - 1.0f, center.y - CrosshairSize * 0.5f, 2.0f, CrosshairSize), Texture2D.whiteTexture);

            if (_emitter == null)
            {
                return;
            }

            BeamSourceInfo source = _emitter.CurrentSource;
            WaveType type = source != null ? source.WaveType : null;
            Color previousColor = GUI.color;
            GUI.color = type != null ? Color.Lerp(type.Color, Color.white, TextWhiteBlend) : Color.white;
            var rect = new Rect(LabelMargin, LabelMargin, Mathf.Max(LabelMinWidth, Screen.width - LabelMargin * 2.0f), LabelHeight);
            GUI.Label(rect, _displayText, _labelStyle);
            GUI.color = previousColor;
        }

        //============================================================
        // 文字を作る
        //============================================================

        private void RebuildText()
        {
            _text.Clear();
            AppendSource(_emitter.CurrentSource);
            AppendPoints();
            AppendLastHits();
            _text.Append('\n');
            AppendAimedObject();
            _text.Append('\n')
                .Append("左クリック: ビーム   Q/E: ビームの切り替え   V: 一人称/三人称\n")
                .Append("WASD: 移動   Space: ジャンプ   Shift: 走る   Esc: カーソルを解放");
            _displayText = _text.ToString();
        }

        private void AppendSource(BeamSourceInfo source)
        {
            _text.Append("ビーム: ");
            if (source == null || source.WaveType == null)
            {
                _text.Append("（なし）\n");
                return;
            }
            _text.Append(source.WaveType.DisplayName)
                .Append("  [").Append(_emitter.SourceIndex + 1).Append('/').Append(_emitter.SourceCount).Append("]   ")
                .Append("強さ ").Append(source.EnergyPerSecond.ToString("0.##")).Append("/秒   ")
                .Append("届く距離 ").Append(source.Range.ToString("0.#")).Append("m   ")
                .Append(source.IsBendable ? "念力で曲げられる" : "念力で曲げられない")
                .Append('\n');
        }

        private void AppendPoints()
        {
            _text.Append("ビームの点: ").Append(_emitter.Points.Count).Append(" 個、間隔 ")
                .Append(_emitter.PointSpacing.ToString("0.##")).Append("m、支点 ").Append(_emitter.AnchorCount).Append(" 個\n");
        }

        private void AppendLastHits()
        {
            _text.Append("最後のビームが当たった物: ");
            IReadOnlyList<InfoBeamHit> hits = _emitter.LastHits;
            if (hits.Count == 0)
            {
                _text.Append("-\n");
                return;
            }
            for (int i = 0; i < hits.Count; ++i)
            {
                if (i > 0)
                {
                    _text.Append(Separator);
                }
                AppendHit(hits[i]);
            }
            _text.Append('\n');
        }

        private void AppendHit(in InfoBeamHit hit)
        {
            if (hit.Collider == null)
            {
                _text.Append('-');
                return;
            }
            bool hasReaction = ObjectInfoLookup.TryGet(hit.Collider, out WaveReaction reaction);
            _text.Append(hasReaction ? reaction.name : hit.Collider.name).Append('（');
            if (hasReaction)
            {
                _text.Append(reaction.CanReactTo(hit.Type) ? "反応あり" : "反応なし");
            }
            else
            {
                _text.Append(hit.IsReflected ? "跳ね返し" : "止まる");
            }
            if (!Mathf.Approximately(hit.Multiplier, WeaknessType.NormalMultiplier))
            {
                _text.Append("、弱点 ×").Append(hit.Multiplier.ToString("0.##"));
            }
            _text.Append('）');
        }

        private void AppendAimedObject()
        {
            Transform aimOrigin = _emitter.CurrentAimOrigin != null ? _emitter.CurrentAimOrigin : _emitter.CurrentMuzzle;
            if (!BeamPhysics.TryCast(aimOrigin.position, aimOrigin.forward, 0.0f, InspectRange, Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore, _emitter.IgnoreRoot, out RaycastHit hit))
            {
                _text.Append("照準の先: -\n");
                return;
            }

            Collider part = hit.collider;
            _text.Append("照準の先: ").Append(part.name).Append("（").Append(hit.distance.ToString("0.0")).Append("m）");
            // 情報を親（壁のまとまりなど）に付けている物は、どこに付いているかも出す
            Transform owner = FindInfoOwner(part);
            if (owner != null && owner != part.transform)
            {
                _text.Append("  情報の持ち主: ").Append(owner.name);
            }
            _text.Append('\n');
            AppendSubstance(part);
            AppendReaction(part);
            AppendWeakPoint(part);
            AppendConcealment(part);
            AppendBeamSource(part);
        }

        private void AppendSubstance(Component part)
        {
            _text.Append("  素材: ");
            if (!ObjectInfoLookup.TryGetSubstanceType(part, out SubstanceType substanceType))
            {
                _text.Append("なし（波は止まる）\n");
                return;
            }
            _text.Append(substanceType.DisplayName)
                .Append(substanceType.IsWaveReflective ? "（波を跳ね返す・" : "（波を止める・")
                .Append(substanceType.IsEchoBlocking ? "エコーをさえぎる）\n" : "エコーを通す）\n");
        }

        private void AppendReaction(Component part)
        {
            _text.Append("  波で動く: ");
            if (!ObjectInfoLookup.TryGet(part, out WaveReaction reaction))
            {
                _text.Append("なし\n");
                return;
            }
            if (reaction.CanReactToAnyType)
            {
                _text.Append("どの波でも");
            }
            else if (reaction.ReactiveTypes.Count == 0)
            {
                _text.Append("（反応する波が未設定）");
            }
            else
            {
                AppendWaveTypeNames(reaction.ReactiveTypes);
            }
            _text.Append("、強さ 1 で ").Append(reaction.RequiredSeconds.ToString("0.##")).Append(" 秒");
            if (ReactionTrigger.TryGetTrigger(reaction, out ReactionTrigger trigger))
            {
                _text.Append("（溜まり ").Append(Mathf.RoundToInt(trigger.ChargeRatio * 100.0f)).Append('%')
                    .Append(trigger.IsActive ? "、起動中）" : "）");
            }
            _text.Append('\n');
        }

        private void AppendWaveTypeNames(IReadOnlyList<WaveType> types)
        {
            for (int i = 0; i < types.Count; ++i)
            {
                if (i > 0)
                {
                    _text.Append('・');
                }
                _text.Append(types[i] != null ? types[i].DisplayName : "（空）");
            }
        }

        private void AppendWeakPoint(Component part)
        {
            _text.Append("  弱点: ");
            if (!ObjectInfoLookup.TryGet(part, out WeakPoint weakPoint))
            {
                _text.Append("なし\n");
                return;
            }
            WeaknessType weakness = weakPoint.WeaknessType;
            if (weakness == null)
            {
                _text.Append("弱点（種類は未設定、どの波も ×1）\n");
                return;
            }
            _text.Append(weakness.DisplayName).Append('（');
            IReadOnlyList<WeaknessEntry> entries = weakness.Entries;
            for (int i = 0; i < entries.Count; ++i)
            {
                WaveType type = entries[i].WaveType;
                _text.Append(type != null ? type.DisplayName : "（空）")
                    .Append(" ×").Append(entries[i].Multiplier.ToString("0.##")).Append('、');
            }
            _text.Append("ほか ×").Append(weakness.OtherMultiplier.ToString("0.##")).Append("）\n");
        }

        private void AppendConcealment(Component part)
        {
            _text.Append("  隠れ方: ");
            if (!ObjectInfoLookup.TryGet(part, out Concealment concealment))
            {
                _text.Append("なし\n");
                return;
            }
            _text.Append(concealment.Style == ConcealmentStyle.Invisible ? "見えない" : "化けている");
            if (concealment.DisguiseForm != null)
            {
                _text.Append("（見た目: ").Append(concealment.DisguiseForm.name).Append('）');
            }
            _text.Append("、本当の姿: ").Append(concealment.TrueForm != null ? concealment.TrueForm.name : "（未設定）").Append('\n');
        }

        private void AppendBeamSource(Component part)
        {
            if (!ObjectInfoLookup.TryGet(part, out BeamSourceInfo source))
            {
                return;
            }
            _text.Append("  ビームを出す: ")
                .Append(source.WaveType != null ? source.WaveType.DisplayName : "（種類は未設定）")
                .Append("、強さ ").Append(source.EnergyPerSecond.ToString("0.##")).Append("/秒、")
                .Append(source.Range.ToString("0.#")).Append("m、")
                .Append(source.IsBendable ? "念力で曲げられる、" : "念力で曲げられない、")
                .Append("出したもの: ").Append(source.SourceKind.ToString()).Append('\n');
        }

        /// @brief 当たった部分から親へたどり、情報（IObjectInfo）を持つ最初の GameObject を返す。無ければ null
        private Transform FindInfoOwner(Component part)
        {
            for (Transform current = part.transform; current != null; current = current.parent)
            {
                current.GetComponents(_infos);
                if (_infos.Count > 0)
                {
                    _infos.Clear();
                    return current;
                }
            }
            return null;
        }
    }
}
