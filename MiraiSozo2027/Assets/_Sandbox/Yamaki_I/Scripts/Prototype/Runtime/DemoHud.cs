using System.Text;
using Aura.Wave;
using UnityEngine;

namespace Aura.Prototype
{
    /// @brief 照準と、今の波の種類、撃ち方、反射のしかた、最後の波が当たった物、操作の説明を画面に出す（確かめる用）
    public sealed class DemoHud : MonoBehaviour
    {
        private const float CrosshairSize = 6.0f;

        [Tooltip("状態（波の種類、撃ち方、反射のしかた、最後の波が当たった物）を画面の左上に出す WaveEmitter。空なら照準だけを出す")]
        [SerializeField] private WaveEmitter _emitter;          ///< 状態を出す WaveEmitter

        [Tooltip("今の視点（一人称か三人称か）を画面に出すための ViewModeSwitcher。空なら視点を「-」と出す")]
        [SerializeField] private ViewModeSwitcher _viewSwitcher; ///< 視点を出す ViewModeSwitcher。空でもよい

        private readonly StringBuilder _hitChain = new StringBuilder();  ///< 最後の波が当たった物を、当たった順に並べた説明
        private GUIStyle _labelStyle;
        private int _hitChainWaveNumber;  ///< _hitChain に並べている波の番号（WaveHit.WaveNumber）

        private void OnEnable()
        {
            if (_emitter != null)
            {
                _emitter.WaveDelivered += HandleWaveDelivered;
            }
        }

        private void OnDisable()
        {
            if (_emitter != null)
            {
                _emitter.WaveDelivered -= HandleWaveDelivered;
            }
        }

        private void HandleWaveDelivered(WaveHit hit, bool isAccepted)
        {
            // 後に出た波（ビームは次のフレーム）の当たりが来たら、そこから並べ直す
            // 前に撃って、まだ飛んでいる単発の当たりは混ぜない
            if (hit.WaveNumber > _hitChainWaveNumber)
            {
                _hitChain.Clear();
                _hitChainWaveNumber = hit.WaveNumber;
            }
            else if (hit.WaveNumber < _hitChainWaveNumber)
            {
                return;
            }
            if (_hitChain.Length > 0)
            {
                _hitChain.Append(" → ");
            }
            _hitChain.Append(GetHitName(hit)).Append(isAccepted ? "（反応あり）" : "（反応なし）");
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

            WaveType type = _emitter.CurrentType;
            string typeName = type != null ? type.DisplayName : "（なし）";
            string fireModeName = _emitter.FireMode == WaveFireMode.Pulse ? "単発" : "連続（ビーム）";
            string viewName = "-";
            if (_viewSwitcher != null)
            {
                viewName = _viewSwitcher.Mode == ViewMode.FirstPerson ? "一人称" : "三人称";
            }
            string hitText = _hitChain.Length > 0 ? _hitChain.ToString() : "-";
            string text =
                $"波の種類: {typeName}  [{_emitter.TypeIndex + 1}/{_emitter.TypeCount}]\n" +
                $"撃ち方: {fireModeName}\n" +
                $"反射: {GetReflectionText()}\n" +
                $"視点: {viewName}\n" +
                $"最後の波が当たった物: {hitText}\n\n" +
                "左クリック: 撃つ   Q/E: 波の切り替え   F: 単発/連続   R: 反射の切り替え   V: 一人称/三人称\n" +
                "WASD: 移動   Space: ジャンプ   Shift: 走る   Esc: カーソルを解放";

            Color previousColor = GUI.color;
            GUI.color = type != null ? Color.Lerp(type.Color, Color.white, 0.4f) : Color.white;
            GUI.Label(new Rect(16.0f, 16.0f, Mathf.Max(400.0f, Screen.width - 32.0f), 280.0f), text, _labelStyle);
            GUI.color = previousColor;
        }

        /// @brief 反射のしかたの説明。跳ね返るときは、回数の上限、エネルギーが減る割合（1 でなければ）、予測線を出すかも添える
        private string GetReflectionText()
        {
            switch (_emitter.ReflectionMode)
            {
                case WaveReflectionMode.None:
                    return "なし（最初に当たった物で止まる）";
                case WaveReflectionMode.AllSurfaces:
                    return $"すべての面で跳ね返る（{GetReflectionLimitText()}）";
                case WaveReflectionMode.ReflectorsOnly:
                    return $"反射板だけで跳ね返る（{GetReflectionLimitText()}）";
                default:
                    return _emitter.ReflectionMode.ToString();
            }
        }

        private string GetReflectionLimitText()
        {
            string text = $"最大 {_emitter.MaxReflections} 回";
            if (_emitter.ReflectionEnergyMultiplier < 1.0f)
            {
                text += $"、1 回ごとにエネルギー ×{_emitter.ReflectionEnergyMultiplier:0.##}";
            }
            text += _emitter.IsReflectionPreviewEnabled ? "、予測線あり" : "、予測線なし";
            return text;
        }

        /// @brief 当たった物の名前。受け手に当たったら受け手の GameObject の名前、ほかはコライダーの名前
        private static string GetHitName(in WaveHit hit)
        {
            if (hit.Collider == null)
            {
                return "-";
            }
            var receiver = hit.Collider.GetComponentInParent<IWaveReceiver>() as Component;
            return receiver != null ? receiver.name : hit.Collider.name;
        }
    }
}
