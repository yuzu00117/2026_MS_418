using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aura.Wave
{
    /// @brief 波の通り道をたどる。当たった物に波を渡し、反射の設定に応じて跳ね返るか止まる
    /// ビームと予測線は毎フレーム Begin からたどり直し、単発の波は 1 発につき 1 つを持って毎フレーム少しずつ進める
    /// 予測線（設定の IsPreview が true）のときは、たどるだけで受け手に波を渡さない
    internal sealed class WaveTracer
    {
        private const float SurfaceOffset = 0.01f;         ///< 跳ね返った後、次の判定を始める位置を面から離す距離（m）
        private const float MinNormalSqrMagnitude = 0.5f;  ///< 面の法線の長さの 2 乗がこれより小さければ、向きが取れなかったとみなす

        private static int _waveCount;  ///< これまでにたどり始めた波の数。波の番号を振るのに使う

        private readonly List<Corner> _corners = new List<Corner>();                         ///< 通り道の曲がり目。最初は始点
        private readonly HashSet<GameObject> _deliveredObjects = new HashSet<GameObject>();  ///< この波で、もう波を渡した受け手の GameObject
        private readonly Action<WaveHit, bool> _onDelivered;                                 ///< 物に当たって波を渡した後に呼ぶ。bool は受け手が受け付けたか

        private WaveTraceSettings _settings;
        private float _energy;  ///< 今のエネルギー。跳ね返るたびに減る

        /// @brief 波の通り道をたどる部品を作る
        /// @param onDelivered 物に当たって波を渡した後に呼ぶ。bool は受け手が受け付けたか。null なら呼ばない
        public WaveTracer(Action<WaveHit, bool> onDelivered)
        {
            _onDelivered = onDelivered;
        }

        /// @brief 始点から先端までに進んだ距離（m）。跳ね返った後の道のりも含む
        public float Travelled { get; private set; }

        /// @brief これまでに跳ね返った回数
        public int ReflectionCount { get; private set; }

        /// @brief 先端が止まったか（跳ね返らない物に当たったか、届く距離を進みきった）
        public bool IsStopped { get; private set; }

        /// @brief 今たどっている波の番号。Begin のたびに 1 つ大きい番号を振る
        public int WaveNumber { get; private set; }

        /// @brief 波の先端の位置
        public Vector3 Head => GetPositionAt(Travelled);

        private Vector3 CurrentDirection => _corners[_corners.Count - 1].Direction;

        //============================================================
        // 公開の操作
        //============================================================

        /// @brief 始点から新しくたどり始める。前にたどった道と、波を渡した受け手の記録は消える
        /// @param settings たどるときの設定（種類、当たり判定、反射）
        /// @param origin 始点
        /// @param direction 進む向き（正規化したもの）
        /// @param energy 最初に当たった物に渡すエネルギー。跳ね返るたびに減る
        public void Begin(in WaveTraceSettings settings, Vector3 origin, Vector3 direction, float energy)
        {
            _settings = settings;
            _energy = energy;
            Travelled = 0.0f;
            ReflectionCount = 0;
            IsStopped = false;
            WaveNumber = ++_waveCount;
            _corners.Clear();
            _deliveredObjects.Clear();
            _corners.Add(new Corner(origin, origin, direction, 0.0f));
        }

        /// @brief 先端を進める。当たった物に波を渡し、跳ね返るなら向きを変えて残りの距離を進む
        /// @param distance 進める距離（m）。届く距離（Range）の残りより長ければ、残りだけ進む
        public void Advance(float distance)
        {
            float remaining = Mathf.Min(distance, _settings.Range - Travelled);
            // 1 回まわるごとに、止まるか、跳ね返る（MaxReflections 回まで）か、残りを進みきって抜けるので、必ず終わる
            while (!IsStopped)
            {
                // 跳ね返った後は、撃った本人（IgnoreRoot の下）にも当たるようにして、当たったらそこで止める
                bool hasReflected = ReflectionCount > 0;
                Transform ignoreRoot = hasReflected ? null : _settings.IgnoreRoot;
                bool hasHit = WavePhysics.TryCast(Head, CurrentDirection, _settings.Radius, remaining,
                    _settings.HitMask, _settings.TriggerInteraction, ignoreRoot, out RaycastHit hit);
                if (!hasHit)
                {
                    Travelled += remaining;
                    IsStopped = Travelled >= _settings.Range;
                    return;
                }

                Travelled += hit.distance;
                remaining -= hit.distance;
                if (hasReflected && IsShooter(hit.collider))
                {
                    // 撃った本人には波を渡さない
                    IsStopped = true;
                    return;
                }

                HandleHit(hit);
                if (remaining <= 0.0f)
                {
                    return;
                }
            }
        }

        /// @brief 通り道のうち、startDistance から endDistance までの折れ線を作る（線を描く用）
        /// 始めの位置、その間で跳ね返った位置、終わりの位置を順に入れる
        /// @param startDistance 始めの位置の、始点からの道のり（m）
        /// @param endDistance 終わりの位置の、始点からの道のり（m）。Travelled より先は Travelled にする
        /// @param isTouchingSurfaces true なら、跳ね返った位置を当たった面の上の点にする（ビームと予測線。面に触れて折れ返って見える）
        /// false なら、当たり判定の球の中心がたどった道の上にする（単発の波。後端が曲がり目を過ぎても、線の長さと模様が跳ばない）
        /// @param corners 折れ線の点を入れるリスト。前の中身は消す
        public void GetCorners(float startDistance, float endDistance, bool isTouchingSurfaces, List<Vector3> corners)
        {
            endDistance = Mathf.Min(endDistance, Travelled);
            startDistance = Mathf.Clamp(startDistance, 0.0f, endDistance);

            corners.Clear();
            corners.Add(GetPositionAt(startDistance));
            for (int i = 1; i < _corners.Count; ++i)
            {
                Corner corner = _corners[i];
                if (corner.Distance > startDistance && corner.Distance < endDistance)
                {
                    corners.Add(isTouchingSurfaces ? corner.DrawPosition : corner.Position);
                }
            }
            corners.Add(GetPositionAt(endDistance));
        }

        //============================================================
        // 内部の処理
        //============================================================

        private void HandleHit(in RaycastHit hit)
        {
            Vector3 direction = CurrentDirection;
            float energy = _energy;
            int reflectionCount = ReflectionCount;

            // 跳ね返るかは、受け手に波を渡す前に決める。受け手の処理で鏡を切り替えても、この波には効かない（次の波から効く）
            if (CanReflect(hit, direction, out Vector3 normal))
            {
                Vector3 center = Head;  // 当たったときの、当たり判定の球の中心
                Vector3 reflected = Vector3.Reflect(direction, normal).normalized;
                _corners.Add(new Corner(center + normal * SurfaceOffset, hit.point, reflected, Travelled));
                _energy *= _settings.ReflectionEnergyMultiplier;
                ++ReflectionCount;
            }
            else
            {
                IsStopped = true;
            }

            // 予測線のときは通り道を見せるだけで、受け手には渡さない
            if (!_settings.IsPreview)
            {
                Deliver(new WaveHit(_settings.Type, energy, hit.point, hit.normal, direction, hit.collider,
                    _settings.Source, _settings.FireMode, reflectionCount, WaveNumber));
            }
        }

        /// @brief 当たった物で跳ね返るかを決める
        /// @param hit 当たりの情報
        /// @param direction 当たったときの進む向き
        /// @param outNormal 跳ね返る向きを決める面の法線。進む向きの手前側を向くようにそろえる
        /// @return 跳ね返るなら true
        private bool CanReflect(in RaycastHit hit, Vector3 direction, out Vector3 outNormal)
        {
            outNormal = hit.normal;
            if (_settings.ReflectionMode == WaveReflectionMode.None || ReflectionCount >= _settings.MaxReflections)
            {
                return false;
            }
            // 始めから重なっていた物（距離 0）と、面の向きが取れない物（壊れたメッシュなど）では跳ね返らない
            // NaN の法線でも止まるよう、比べた結果を否定して判定する
            if (hit.distance <= 0.0f || !(outNormal.sqrMagnitude >= MinNormalSqrMagnitude))
            {
                return false;
            }
            // 面の裏から当たったときも、来た側へ跳ね返す
            if (Vector3.Dot(outNormal, direction) > 0.0f)
            {
                outNormal = -outNormal;
            }

            WaveSurface surface = WavePhysics.FindSurface(hit.collider);
            if (surface != null)
            {
                return surface.Response == WaveSurfaceResponse.Reflect;
            }
            // トリガーは目に見えない範囲なので、反射板にした物のほかは跳ね返さない
            return !hit.collider.isTrigger && _settings.ReflectionMode == WaveReflectionMode.AllSurfaces;
        }

        private void Deliver(in WaveHit hit)
        {
            GameObject receiverObject = WavePhysics.FindReceiverObject(hit.Collider);
            // 同じ受け手には、1 本の波で 1 回だけ渡す（跳ね返って、また同じ受け手に当たっても渡さない）
            if (receiverObject != null && !_deliveredObjects.Add(receiverObject))
            {
                return;
            }
            bool isAccepted = receiverObject != null && WavePhysics.Deliver(hit, receiverObject);
            _onDelivered?.Invoke(hit, isAccepted);
        }

        private bool IsShooter(Collider collider)
        {
            return _settings.IgnoreRoot != null && collider.transform.IsChildOf(_settings.IgnoreRoot);
        }

        /// @brief 始点から distance だけ進んだ位置。跳ね返った曲がり目を順にたどって求める
        private Vector3 GetPositionAt(float distance)
        {
            if (_corners.Count == 0)
            {
                return Vector3.zero;
            }
            int index = _corners.Count - 1;
            while (index > 0 && _corners[index].Distance > distance)
            {
                --index;
            }
            Corner corner = _corners[index];
            return corner.Position + corner.Direction * (distance - corner.Distance);
        }

        //============================================================
        // 曲がり目
        //============================================================

        /// @brief 通り道の曲がり目（始点と、跳ね返った位置）
        private readonly struct Corner
        {
            public Corner(Vector3 position, Vector3 drawPosition, Vector3 direction, float distance)
            {
                Position = position;
                DrawPosition = drawPosition;
                Direction = direction;
                Distance = distance;
            }

            /// @brief ここから先の位置を求める基準。跳ね返った位置では、当たり判定の球の中心を面から少し離した位置
            public Vector3 Position { get; }

            /// @brief 線を描くときの位置。跳ね返った位置では、当たった面の上の点
            public Vector3 DrawPosition { get; }

            /// @brief ここから先へ進む向き（正規化したもの）
            public Vector3 Direction { get; }

            /// @brief 始点からここまでに進んだ距離（m）
            public float Distance { get; }
        }
    }
}
