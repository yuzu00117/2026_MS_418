using UnityEngine;

namespace Aura.ObjectInfo
{
    /// @brief 物の情報を取り出す決まりを、1 か所にまとめたもの
    /// 当たったコライダーや見た目（Renderer）から、その物の情報を取り出す。情報はその GameObject か親に付けるので、
    /// 自分から親へ順に探し、いちばん近いものを使う。無効なコンポーネントと、非アクティブな GameObject のものは、無いものとして飛ばす
    /// 再生中は、調べた結果を物の一部ごとに覚えておき、2 回目からは探さずに返す（毎フレームたくさん呼んでも軽い）
    /// 情報のコンポーネントが有効・無効になったときと、シーンを閉じたときは、覚えた結果を自動で捨てる
    /// 実行中に親を付け替えたときだけは自動で気付けないので、ClearCache を呼ぶ
    public static class ObjectInfoLookup
    {
        /// @brief 自分か親から、いちばん近い有効な情報のコンポーネントを探す
        /// Substance は素材の種類が空でも返すので、素材を取り出すときは TryGetSubstanceType を使う
        /// @param part 当たったコライダーや見た目など、物の一部
        /// @param outInfo 見つかった情報のコンポーネント。無ければ null
        /// @return 見つかったら true
        public static bool TryGet<T>(Component part, out T outInfo) where T : Behaviour, IObjectInfo
        {
            if (part == null)
            {
                outInfo = null;
                return false;
            }
            Behaviour info;
            if (!ObjectInfoCache.IsCaching)
            {
                info = Find<T>(part);
            }
            else if (!ObjectInfoCache.TryGetInfo(part, typeof(T), out info))
            {
                info = Find<T>(part);
                ObjectInfoCache.SetInfo(part, typeof(T), info);
            }
            outInfo = info != null ? (T)info : null;
            return outInfo != null;
        }

        /// @brief 自分か親から、いちばん近い素材の種類を探す。素材の種類が空の Substance は、無いものとして飛ばす
        /// @param part 当たったコライダーや見た目など、物の一部
        /// @param outSubstanceType 見つかった素材の種類。無ければ null
        /// @return 見つかったら true
        public static bool TryGetSubstanceType(Component part, out SubstanceType outSubstanceType)
        {
            if (part == null)
            {
                outSubstanceType = null;
                return false;
            }
            if (!ObjectInfoCache.IsCaching)
            {
                outSubstanceType = FindSubstanceType(part);
            }
            else if (!ObjectInfoCache.TryGetSubstanceType(part, out outSubstanceType))
            {
                outSubstanceType = FindSubstanceType(part);
                ObjectInfoCache.SetSubstanceType(part, outSubstanceType);
            }
            return outSubstanceType != null;
        }

        /// @brief その物がエコーをさえぎるか。素材が無ければ isFallbackBlocking を返す
        /// @param part 当たったコライダーや見た目など、物の一部
        /// @param isFallbackBlocking 素材が無いときに返す値（タグで決める今のやり方の結果など）
        /// @return さえぎるなら true
        public static bool IsEchoBlocking(Component part, bool isFallbackBlocking)
        {
            return TryGetSubstanceType(part, out SubstanceType substanceType) ? substanceType.IsEchoBlocking : isFallbackBlocking;
        }

        /// @brief その物が波を跳ね返すか。素材が無ければ isFallbackReflective を返す
        /// @param part 当たったコライダーや見た目など、物の一部
        /// @param isFallbackReflective 素材が無いときに返す値
        /// @return 跳ね返すなら true
        public static bool IsWaveReflective(Component part, bool isFallbackReflective)
        {
            return TryGetSubstanceType(part, out SubstanceType substanceType) ? substanceType.IsWaveReflective : isFallbackReflective;
        }

        /// @brief その物が波を通すか（当てたまま、向こうへ真っすぐ進む）。素材が無ければ isFallbackPassThrough を返す
        /// @param part 当たったコライダーや見た目など、物の一部
        /// @param isFallbackPassThrough 素材が無いときに返す値
        /// @return 通すなら true
        public static bool IsWavePassThrough(Component part, bool isFallbackPassThrough)
        {
            return TryGetSubstanceType(part, out SubstanceType substanceType) ? substanceType.IsWavePassThrough : isFallbackPassThrough;
        }

        /// @brief 覚えた結果をすべて捨てる。実行中に親を付け替えたとき（SetParent など）に呼ぶ
        /// 情報のコンポーネントの付け外し・有効と無効の切り替え・GameObject の表示と非表示は自動で気付くので、呼ばなくてよい
        public static void ClearCache()
        {
            ObjectInfoCache.Clear();
        }

        // 自分から親へ順に、有効な T を探す
        private static T Find<T>(Component part) where T : Behaviour
        {
            for (Transform current = part.transform; current != null; current = current.parent)
            {
                if (current.TryGetComponent(out T info) && IsUsable(info))
                {
                    return info;
                }
            }
            return null;
        }

        // 自分から親へ順に、素材の種類が入った有効な Substance を探す
        private static SubstanceType FindSubstanceType(Component part)
        {
            for (Transform current = part.transform; current != null; current = current.parent)
            {
                if (current.TryGetComponent(out Substance substance) && IsUsable(substance) && substance.Type != null)
                {
                    return substance.Type;
                }
            }
            return null;
        }

        // コンポーネントが有効で、GameObject もアクティブなときだけ使う（エディタでも再生中でも同じ結果になるよう、両方を自分で見る）
        private static bool IsUsable(Behaviour info)
        {
            return info.enabled && info.gameObject.activeInHierarchy;
        }
    }
}
