using System.Collections.Generic;
using UnityEngine;

namespace Aura.Gimmick
{
    /// @brief 上に乗った物を運ぶライダーの Collider から、摩擦を取り除く
    /// ライダーは MovePosition で動くので、物理の接触ではライダーの速さが摩擦で上の物へ伝わる。
    /// 運ぶ処理でも位置を同じだけずらすので、摩擦が残っていると上の物が 2 倍進む。摩擦を 0 にして、運ぶ処理だけで動かす。
    /// 変えた Collider ごとに元の材質を覚えておき、ライダーの子から外れたときやライダーが消えるときに元へ戻す
    internal sealed class WaveRiderFriction
    {
        private const string FrictionlessName = "WaveRider_Frictionless";   ///< 作る材質の名前（Inspector で見分けるため）

        private static PhysicsMaterial s_frictionless;   ///< 材質の無い Collider に付ける、摩擦の無い材質（全ライダーで使い回す）

        private readonly Dictionary<Collider, PhysicsMaterial> _originals = new Dictionary<Collider, PhysicsMaterial>();   ///< 摩擦を取り除いた Collider と、その元の材質（無ければ null）
        private readonly Dictionary<Collider, PhysicsMaterial> _applied = new Dictionary<Collider, PhysicsMaterial>();     ///< 摩擦を取り除いた Collider と、代わりに付けた材質
        private readonly HashSet<Collider> _current = new HashSet<Collider>();   ///< いまの自分の Collider（外れた物を見分けるため使い回す）
        private readonly List<Collider> _detached = new List<Collider>();       ///< 外れた Collider（辞書を回しながら消せないため使い回す）

        /// @brief 摩擦が残っている Collider に、摩擦の無い材質を付ける。トリガーは物を支えないので相手にしない
        /// 材質がすでに付いていれば、跳ね返りなどを残すため複製して摩擦だけ 0 にする。
        /// 前に摩擦を取り除いたのに colliders に無い（子から外れた）Collider は、元の材質へ戻す
        /// 計算量: O(C + A)（C は colliders の数、A は摩擦を取り除いてある Collider の数）
        public void Apply(Collider[] colliders)
        {
            RestoreDetached(colliders);
            foreach (Collider each in colliders)
            {
                if (each == null || each.isTrigger || _originals.ContainsKey(each))
                {
                    continue;
                }
                PhysicsMaterial current = each.sharedMaterial;
                PhysicsMaterial replacement;
                if (current == null)
                {
                    replacement = GetSharedFrictionless();
                }
                else if (IsFrictionless(current))
                {
                    continue;
                }
                else
                {
                    replacement = Object.Instantiate(current);
                    replacement.name = current.name + "_" + FrictionlessName;
                    MakeFrictionless(replacement);
                }
                _originals.Add(each, current);
                _applied.Add(each, replacement);
                each.sharedMaterial = replacement;
            }
        }

        /// @brief 摩擦を取り除いた Collider をすべて元の材質へ戻し、複製した材質を消す（ライダーが消えるときに呼ぶ）
        /// 計算量: O(A)（A は摩擦を取り除いてある Collider の数）
        public void RestoreAll()
        {
            _detached.Clear();
            _detached.AddRange(_originals.Keys);
            foreach (Collider each in _detached)
            {
                Restore(each);
            }
            _detached.Clear();
        }

        // colliders に無くなった Collider を元の材質へ戻す
        private void RestoreDetached(Collider[] colliders)
        {
            if (_originals.Count == 0)
            {
                return;
            }
            _current.Clear();
            foreach (Collider each in colliders)
            {
                _current.Add(each);
            }
            _detached.Clear();
            foreach (Collider each in _originals.Keys)
            {
                if (!_current.Contains(each))
                {
                    _detached.Add(each);
                }
            }
            foreach (Collider each in _detached)
            {
                Restore(each);
            }
            _detached.Clear();
            _current.Clear();
        }

        // 1 つの Collider を元の材質へ戻し、覚えていた分を消す。
        // 付けた材質がほかの処理で差し替えられていたら、その材質を残す（相手の変更を消さないため）
        private void Restore(Collider collider)
        {
            PhysicsMaterial original = _originals[collider];
            PhysicsMaterial replacement = _applied[collider];
            _originals.Remove(collider);
            _applied.Remove(collider);
            if (collider != null && collider.sharedMaterial == replacement)
            {
                collider.sharedMaterial = original;
            }
            // 複製した材質はこの Collider だけが使うので消す。使い回しの材質は残す
            if (replacement != null && replacement != s_frictionless)
            {
                Object.Destroy(replacement);
            }
        }

        /// 使い回す材質。プレイを止めると消えるので、Unity の null 判定で消えたかを見て作り直す
        private static PhysicsMaterial GetSharedFrictionless()
        {
            if (s_frictionless == null)
            {
                s_frictionless = new PhysicsMaterial(FrictionlessName);
                MakeFrictionless(s_frictionless);
            }
            return s_frictionless;
        }

        /// 相手の材質によらず摩擦が 0 になるよう、合わせ方を Minimum にする（Average だと相手の摩擦の半分が残る）
        private static void MakeFrictionless(PhysicsMaterial material)
        {
            material.staticFriction = 0.0f;
            material.dynamicFriction = 0.0f;
            material.frictionCombine = PhysicsMaterialCombine.Minimum;
        }

        private static bool IsFrictionless(PhysicsMaterial material)
        {
            return material.staticFriction == 0.0f && material.dynamicFriction == 0.0f
                   && material.frictionCombine == PhysicsMaterialCombine.Minimum;
        }
    }
}
