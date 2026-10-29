using System.Collections.Generic;
using Aura.Inspector;
using UnityEngine;

namespace Aura.Wave
{
    /// @brief 波の見た目を見せる範囲を持つ部品。「見せ方」が「見える範囲の中だけ」の見た目の部品（BeamLineView）は、
    /// シーンにある有効なこの部品すべての範囲（WaveRevealVolume）の中にある部分だけを描く
    /// ・Inspector の「半径（m）」と「角度（度）」で、この GameObject の位置を中心にした範囲を持てる（確かめる用や、エコー以外の仕組み用）
    /// ・コードからは SetVolumes で範囲を入れ替える。エコーロケーションの範囲は、Scripts/Wave/Echo の EchoWaveRevealSource が毎フレーム入れる
    /// 範囲は「見る側」のもので、どの WaveEmitter の波にも効く（波を出す物ごとには持たない）
    [DisallowMultipleComponent]
    public sealed class WaveRevealSource : MonoBehaviour
    {
        private static readonly List<WaveRevealSource> ActiveList = new List<WaveRevealSource>();  ///< 有効なこの部品

        [InspectorLabel("半径（m）")]
        [Tooltip("この GameObject の位置を中心に見せる範囲の半径（m）。0 なら、この欄の範囲は持たない（コードから入れた範囲だけ）\n" +
                 "エコーロケーションの範囲はここでは決めない（同じ GameObject に付けた EchoWaveRevealSource が、エコーの範囲を毎フレーム入れる）")]
        [SerializeField, Min(0.0f)] private float _radius;  ///< この GameObject を中心に見せる範囲の半径（m）。0 なら持たない

        [InspectorLabel("角度（度）")]
        [Tooltip("「半径（m）」の範囲を、この GameObject の正面（青い矢印）から見せる扇の全体の角度（度）。360 なら全方向")]
        [SerializeField, Range(1.0f, 360.0f)] private float _angle = 360.0f;  ///< 「半径（m）」の範囲の、扇の全体の角度（度）

        private readonly List<WaveRevealVolume> _volumes = new List<WaveRevealVolume>();  ///< コードから入れた範囲

        /// @brief 有効なこの部品の一覧
        public static IReadOnlyList<WaveRevealSource> ActiveSources => ActiveList;

        /// @brief この GameObject を中心に見せる範囲の半径（m）。0 なら持たない
        public float Radius
        {
            get => _radius;
            set => _radius = Mathf.Max(0.0f, value);
        }

        /// @brief 「半径（m）」の範囲の、扇の全体の角度（度）
        public float Angle
        {
            get => _angle;
            set => _angle = Mathf.Clamp(value, 1.0f, 360.0f);
        }

        /// @brief 有効なこの部品すべての範囲を集める
        /// @param results 入れる先。空にしてから入れる
        public static void CollectAll(List<WaveRevealVolume> results)
        {
            results.Clear();
            foreach (WaveRevealSource source in ActiveList)
            {
                source.GetVolumes(results);
            }
        }

        /// @brief その位置が、有効なこの部品のどれかの範囲の中か（見た目の部品が、点ごとに調べるときに使う）
        /// @param point 調べる位置（ワールド座標）
        public static bool IsRevealed(Vector3 point)
        {
            foreach (WaveRevealSource source in ActiveList)
            {
                if (source.Contains(point))
                {
                    return true;
                }
            }
            return false;
        }

        /// @brief この部品の範囲（Inspector の欄の範囲と、コードから入れた範囲）を集める
        /// @param results 入れる先。空にせず、後ろに足す
        public void GetVolumes(List<WaveRevealVolume> results)
        {
            if (_radius > 0.0f)
            {
                results.Add(new WaveRevealVolume(transform.position, transform.forward, _angle, _radius, 0.0f));
            }
            results.AddRange(_volumes);
        }

        /// @brief その位置が、この部品のどれかの範囲の中か
        /// @param point 調べる位置（ワールド座標）
        public bool Contains(Vector3 point)
        {
            if (_radius > 0.0f && new WaveRevealVolume(transform.position, transform.forward, _angle, _radius, 0.0f).Contains(point))
            {
                return true;
            }
            foreach (WaveRevealVolume volume in _volumes)
            {
                if (volume.Contains(point))
                {
                    return true;
                }
            }
            return false;
        }

        /// @brief コードから入れた範囲を入れ替える（毎フレーム呼んでよい）
        /// @param volumes 新しい範囲。null なら空にする
        public void SetVolumes(IReadOnlyList<WaveRevealVolume> volumes)
        {
            _volumes.Clear();
            if (volumes == null)
            {
                return;
            }
            for (int i = 0; i < volumes.Count; ++i)
            {
                _volumes.Add(volumes[i]);
            }
        }

        /// @brief コードから入れた範囲を 1 つ足す
        /// @param volume 足す範囲
        public void AddVolume(in WaveRevealVolume volume)
        {
            _volumes.Add(volume);
        }

        /// @brief コードから入れた範囲を空にする（Inspector の欄の範囲は残る）
        public void ClearVolumes()
        {
            _volumes.Clear();
        }

        private void OnEnable()
        {
            ActiveList.Add(this);
        }

        private void OnDisable()
        {
            ActiveList.Remove(this);
        }

        // domain reload を切っていると、前の再生の一覧が残るので、再生を始めるたびに空にする
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ActiveList.Clear();
        }
    }
}
