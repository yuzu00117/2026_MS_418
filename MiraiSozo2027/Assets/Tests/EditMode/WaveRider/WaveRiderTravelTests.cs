using NUnit.Framework;
using UnityEngine;

namespace Aura.Gimmick.Tests
{
    /// @brief WaveRiderTravel（線の上の道のりを進める計算）のテスト
    public sealed class WaveRiderTravelTests
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void 終点の端で止まり折り返す設定なら向きを変える()
        {
            float direction = 1.0f;
            float distance = WaveRiderTravel.Advance(4.9f, ref direction, 0.2f, 0.5f, 5.0f, true);

            Assert.AreEqual(5.0f, distance, Tolerance);
            Assert.AreEqual(-1.0f, direction);
        }

        [Test]
        public void 始まり側の端で折り返す()
        {
            float direction = -1.0f;
            float distance = WaveRiderTravel.Advance(0.6f, ref direction, 0.2f, 0.5f, 5.0f, true);

            Assert.AreEqual(0.5f, distance, Tolerance);
            Assert.AreEqual(1.0f, direction);
        }

        [Test]
        public void 動ける範囲が無ければ向きを変えずに終点側の端に留まる()
        {
            // 線がライダーの前後の広がり以下に短いと、始まり側の端と終点側の端が同じになる
            float direction = 1.0f;
            for (int i = 0; i < 10; i++)
            {
                float distance = WaveRiderTravel.Advance(0.3f, ref direction, 0.04f, 0.3f, 0.3f, true);
                Assert.AreEqual(0.3f, distance, Tolerance);
                Assert.AreEqual(1.0f, direction, "毎ステップ向きが反転しない");
            }
        }

        [Test]
        public void 乗った道のりが終点側の端より先なら端に収める()
        {
            Assert.AreEqual(5.0f, WaveRiderTravel.ClampOnBoard(5.4f, 1.0f, 0.5f, 5.0f), Tolerance);
        }

        [Test]
        public void 終点へ向かって乗るなら始まり側の端より手前でもそのまま()
        {
            Assert.AreEqual(0.2f, WaveRiderTravel.ClampOnBoard(0.2f, 1.0f, 0.5f, 5.0f), Tolerance);
        }

        [Test]
        public void 始まりへ向かって乗るなら始まり側の端に収める()
        {
            Assert.AreEqual(0.5f, WaveRiderTravel.ClampOnBoard(0.2f, -1.0f, 0.5f, 5.0f), Tolerance);
        }

        [Test]
        public void 壁際で押し戻した分は線から遅れた距離に数えない()
        {
            // 幅 3m のライダーが反射の曲がり角で 1.5m 押し戻され、上限 0.2m だけ寄った所。線は動いていないので遅れは無い
            Assert.AreEqual(0.0f, WaveRiderTravel.GetFollowLag(new Vector3(1.3f, 0.0f, 0.0f), new Vector3(-1.5f, 0.0f, 0.0f)), Tolerance);
        }

        [Test]
        public void 押し戻しが無ければ目指す位置との差がそのまま遅れになる()
        {
            Assert.AreEqual(1.3f, WaveRiderTravel.GetFollowLag(new Vector3(1.3f, 0.0f, 0.0f), Vector3.zero), Tolerance);
        }

        [Test]
        public void 押し戻した分より大きく離れた分は遅れに数える()
        {
            // 線が壁の向こうへ速く動いたときは、押し戻した分を超えて離れていくので降りられる
            Assert.AreEqual(0.8f, WaveRiderTravel.GetFollowLag(new Vector3(1.0f, 0.0f, 0.0f), new Vector3(-0.2f, 0.0f, 0.0f)), Tolerance);
        }

        [Test]
        public void 横向きの押し戻しは遅れから引かない()
        {
            // 横の壁から 1.5m 押し戻されている間に、線が前へ 1.3m 離れた。押し戻しは離れた向きと関係ないので、そのまま遅れになる
            Assert.AreEqual(1.3f, WaveRiderTravel.GetFollowLag(new Vector3(1.3f, 0.0f, 0.0f), new Vector3(0.0f, 0.0f, 1.5f)), Tolerance);
        }

        [Test]
        public void 斜めの押し戻しは離れる向きの分だけ引く()
        {
            // 押し戻し (-0.6, 0, 0.8) のうち、目指す位置から離れる向きの成分は 0.6
            Assert.AreEqual(0.4f, WaveRiderTravel.GetFollowLag(new Vector3(1.0f, 0.0f, 0.0f), new Vector3(-0.6f, 0.0f, 0.8f)), Tolerance);
        }

        [Test]
        public void 目指す位置へ近づく向きの押し戻しは遅れを増やさない()
        {
            Assert.AreEqual(1.0f, WaveRiderTravel.GetFollowLag(new Vector3(1.0f, 0.0f, 0.0f), new Vector3(0.5f, 0.0f, 0.0f)), Tolerance);
        }
    }
}
