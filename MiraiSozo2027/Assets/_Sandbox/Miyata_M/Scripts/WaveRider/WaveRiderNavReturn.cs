using UnityEngine;
using UnityEngine.AI;

namespace Aura.Gimmick
{
    /// @brief WaveRider が NavMesh の経路で初期位置へ戻るときの、経路の計算と 1 ステップ分の移動を受け持つ
    /// ・NavMesh は水平の経路を出すので、ライダーの今の位置と目標を、それぞれ NavMesh 上の最寄りの点に置き換えて経路を引く
    /// ・NavMesh からのずれ（高さや横のずれ）は、経路を進むのと同じ速さで少しずつ目標のずれへ縮める。位置が飛ばない
    /// ・シーンに NavMesh のベイクが必要。ライダーの横幅の半分以上の「エージェントの半径」でベイクすると、壁の角に引っかからない
    internal sealed class WaveRiderNavReturn
    {
        private const float SampleRadius = 10.0f;  ///< NavMesh 上の最寄りの点を探す範囲（m）。空中を運ばれた高さからも床を探せるよう広めにする

        private Vector3[] _corners = System.Array.Empty<Vector3>();  ///< 経路の角（最初は出発点）
        private int _nextIndex;           ///< 次に向かう角の番号
        private Vector3 _navPoint;        ///< 経路の上の今の点
        private Vector3 _offset;          ///< 経路の上の点から見た、ライダーの位置のずれ
        private Vector3 _goalOffset;      ///< 経路の終点から見た、目標の位置のずれ

        /// @brief 経路を引いてあるか
        public bool HasPlan { get; private set; }

        /// @brief 経路を捨てる。次の Plan で引き直す
        public void Reset()
        {
            HasPlan = false;
        }

        /// @brief from から goal までの経路を引く。NavMesh が無い、点が範囲外、行き着けないときは false
        /// 計算量: NavMesh の経路探索に従う（戻る動きを始めるときと、壁で止まったときだけ呼ぶ）
        public bool Plan(Vector3 from, Vector3 goal)
        {
            HasPlan = false;
            if (!NavMesh.SamplePosition(from, out NavMeshHit start, SampleRadius, NavMesh.AllAreas)
                || !NavMesh.SamplePosition(goal, out NavMeshHit end, SampleRadius, NavMesh.AllAreas))
            {
                return false;
            }

            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path)
                || path.status != NavMeshPathStatus.PathComplete)
            {
                return false;
            }

            _corners = path.corners;
            _nextIndex = 1;
            _navPoint = start.position;
            _offset = from - start.position;
            _goalOffset = goal - end.position;
            HasPlan = true;
            return true;
        }

        /// @brief 経路に沿って distance だけ進めた、ライダーの目標の位置を返す。経路の終点では、目標の位置に着く
        /// 計算量: O(進む間に通る角の数)
        public Vector3 Step(float distance)
        {
            float remaining = distance;
            while (remaining > 0.0f && _nextIndex < _corners.Length)
            {
                Vector3 corner = _corners[_nextIndex];
                float toCorner = Vector3.Distance(_navPoint, corner);
                if (toCorner <= remaining)
                {
                    _navPoint = corner;
                    remaining -= toCorner;
                    _nextIndex++;
                }
                else
                {
                    _navPoint = Vector3.MoveTowards(_navPoint, corner, remaining);
                    remaining = 0.0f;
                }
            }

            _offset = Vector3.MoveTowards(_offset, _goalOffset, distance);
            return _navPoint + _offset;
        }
    }
}
