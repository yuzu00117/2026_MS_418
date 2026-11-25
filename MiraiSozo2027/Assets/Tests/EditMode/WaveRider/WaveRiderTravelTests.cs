using NUnit.Framework;

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
    }
}
