using System.Collections.Generic;
using UnityEngine;

namespace Aura.Gimmick
{
    /// @brief 上に乗った物を運ぶライダーの Collider から、摩擦を取り除く
    /// ライダーは MovePosition で動くので、物理の接触ではライダーの速さが摩擦で上の物へ伝わる。
    /// 運ぶ処理でも位置を同じだけずらすので、摩擦が残っていると上の物が 2 倍進む。摩擦を 0 にして、運ぶ処理だけで動かす
    internal static class WaveRiderFriction
    {
        private const string FrictionlessName = "WaveRider_Frictionless";   ///< 作る材質の名前（Inspector で見分けるため）

        private static PhysicsMaterial s_frictionless;   ///< 材質の無い Collider に付ける、摩擦の無い材質（全ライダーで使い回す）

        /// @brief 摩擦が残っている Collider に、摩擦の無い材質を付ける。トリガーは物を支えないので相手にしない
        /// 材質がすでに付いていれば、跳ね返りなどを残すため複製して摩擦だけ 0 にする。複製は createdMaterials に足す（Release で消すため）
        /// 計算量: O(C)（C は Collider の数）
        public static void RemoveFriction(Collider[] colliders, List<PhysicsMaterial> createdMaterials)
        {
            foreach (Collider each in colliders)
            {
                if (each == null || each.isTrigger)
                {
                    continue;
                }
                PhysicsMaterial current = each.sharedMaterial;
                if (current == null)
                {
                    each.sharedMaterial = GetSharedFrictionless();
                }
                else if (!IsFrictionless(current))
                {
                    PhysicsMaterial copy = Object.Instantiate(current);
                    copy.name = current.name + "_" + FrictionlessName;
                    MakeFrictionless(copy);
                    createdMaterials.Add(copy);
                    each.sharedMaterial = copy;
                }
            }
        }

        /// @brief RemoveFriction で複製した材質を消す（ライダーが消えるときに呼ぶ）
        /// 計算量: O(M)（M は複製した材質の数）
        public static void Release(List<PhysicsMaterial> createdMaterials)
        {
            foreach (PhysicsMaterial each in createdMaterials)
            {
                if (each != null)
                {
                    Object.Destroy(each);
                }
            }
            createdMaterials.Clear();
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
