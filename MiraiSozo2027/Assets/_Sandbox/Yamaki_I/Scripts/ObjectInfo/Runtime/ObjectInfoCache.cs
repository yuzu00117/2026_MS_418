using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Aura.ObjectInfo
{
    /// @brief ObjectInfoLookup が調べた結果を、物の一部（コライダーや Renderer）ごとに覚えておく表。再生中だけ使う
    /// 情報のコンポーネントが有効・無効になったとき、素材の種類を変えたとき、シーンを閉じたときに、覚えた結果をすべて捨てる
    /// 捨てるのは次に問い合わせが来たときにまとめて行うので、続けて何度も知らされても重くならない
    internal static class ObjectInfoCache
    {
        /// @brief 覚える行の上限。情報の無い物（演出の Renderer など）を作っては消すと、消えた物の行が残るので、
        /// これを超えたら一度すべて捨てる。ふつうのシーンで問い合わせる物の数より十分に大きくしてある
        private const int MaxEntryCount = 65536;

        private static readonly Dictionary<(Component part, Type infoType), Behaviour> InfosByPart =
            new Dictionary<(Component part, Type infoType), Behaviour>();                                    ///< 物の一部と情報の型 → 見つかった情報（無ければ null）
        private static readonly Dictionary<Component, SubstanceType> SubstanceTypesByPart =
            new Dictionary<Component, SubstanceType>();                                                      ///< 物の一部 → 見つかった素材の種類（無ければ null）
        private static bool _isDirty;  ///< 覚えた結果を、次の問い合わせの前に捨てるか

        /// @brief 結果を覚えておくか。再生中だけ覚える
        /// 再生していないエディタでは、情報のコンポーネントの有効・無効が知らされないので、毎回探す
        internal static bool IsCaching => Application.isPlaying;

        /// @brief 覚えた結果が古くなったことを知らせる。次の問い合わせの前に、すべて捨てる
        internal static void MarkDirty()
        {
            _isDirty = true;
        }

        /// @brief 覚えた結果を、今すぐすべて捨てる
        internal static void Clear()
        {
            InfosByPart.Clear();
            SubstanceTypesByPart.Clear();
            _isDirty = false;
        }

        /// @brief 覚えた情報を取り出す
        /// @param part 物の一部
        /// @param infoType 情報の型
        /// @param outInfo 覚えていた情報。探して見つからなかったと覚えているときは null
        /// @return 覚えていれば true（見つからなかったと覚えている場合も true）
        internal static bool TryGetInfo(Component part, Type infoType, out Behaviour outInfo)
        {
            ClearIfDirty();
            return InfosByPart.TryGetValue((part, infoType), out outInfo);
        }

        /// @brief 探した情報を覚える
        /// @param part 物の一部
        /// @param infoType 情報の型
        /// @param info 見つかった情報。見つからなかったときは null
        internal static void SetInfo(Component part, Type infoType, Behaviour info)
        {
            ClearIfFull();
            InfosByPart[(part, infoType)] = info;
        }

        /// @brief 覚えた素材の種類を取り出す
        /// @param part 物の一部
        /// @param outSubstanceType 覚えていた素材の種類。探して見つからなかったと覚えているときは null
        /// @return 覚えていれば true（見つからなかったと覚えている場合も true）
        internal static bool TryGetSubstanceType(Component part, out SubstanceType outSubstanceType)
        {
            ClearIfDirty();
            return SubstanceTypesByPart.TryGetValue(part, out outSubstanceType);
        }

        /// @brief 探した素材の種類を覚える
        /// @param part 物の一部
        /// @param substanceType 見つかった素材の種類。見つからなかったときは null
        internal static void SetSubstanceType(Component part, SubstanceType substanceType)
        {
            ClearIfFull();
            SubstanceTypesByPart[part] = substanceType;
        }

        private static void ClearIfDirty()
        {
            if (_isDirty)
            {
                Clear();
            }
        }

        private static void ClearIfFull()
        {
            if (InfosByPart.Count + SubstanceTypesByPart.Count >= MaxEntryCount)
            {
                Clear();
            }
        }

        // domain reload を切っていると、前の再生で覚えた結果が残るので、再生を始めるたびに空にする
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Clear();
            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
            SceneManager.sceneUnloaded += HandleSceneUnloaded;
        }

        private static void HandleSceneUnloaded(Scene scene)
        {
            MarkDirty();
        }
    }
}
