using NUnit.Framework;
using UnityEngine;

namespace Aura.Gimmick.Tests
{
    /// @brief WaveLinePath（折れ線の上の位置の計算）のテスト
    public sealed class WaveLinePathTests
    {
        private const float Tolerance = 0.0001f;

        /// L 字の折れ線: (0,0,0) → (4,0,0) → (4,0,3)。長さ 7
        private static readonly Vector3[] LShape =
        {
            new Vector3(0, 0, 0), new Vector3(4, 0, 0), new Vector3(4, 0, 3),
        };

        [Test]
        public void GetLength_曲がった線は各線分の合計()
        {
            Assert.AreEqual(7.0f, WaveLinePath.GetLength(LShape), Tolerance);
        }

        [Test]
        public void GetLength_点が2つ未満なら0()
        {
            Assert.AreEqual(0.0f, WaveLinePath.GetLength(new Vector3[0]), Tolerance);
            Assert.AreEqual(0.0f, WaveLinePath.GetLength(new[] { Vector3.one }), Tolerance);
        }

        [Test]
        public void GetPointAt_曲がり角をまたいで進む()
        {
            Vector3 point = WaveLinePath.GetPointAt(LShape, 5.0f, out Vector3 direction);
            Assert.AreEqual(new Vector3(4, 0, 1), point);
            Assert.AreEqual(Vector3.forward, direction);
        }

        [Test]
        public void GetPointAt_範囲外は両端に丸める()
        {
            Assert.AreEqual(Vector3.zero, WaveLinePath.GetPointAt(LShape, -3.0f, out _));
            Assert.AreEqual(new Vector3(4, 0, 3), WaveLinePath.GetPointAt(LShape, 100.0f, out _));
        }

        [Test]
        public void GetPointAt_点が無ければ原点で向きはゼロ()
        {
            Vector3 point = WaveLinePath.GetPointAt(new Vector3[0], 1.0f, out Vector3 direction);
            Assert.AreEqual(Vector3.zero, point);
            Assert.AreEqual(Vector3.zero, direction);
        }

        [Test]
        public void GetPointAt_長さゼロの線分は飛ばす()
        {
            var corners = new[] { Vector3.zero, Vector3.zero, new Vector3(2, 0, 0) };
            Vector3 point = WaveLinePath.GetPointAt(corners, 1.0f, out Vector3 direction);
            Assert.AreEqual(new Vector3(1, 0, 0), point);
            Assert.AreEqual(Vector3.right, direction);
        }

        [Test]
        public void Project_線の横の点は線の上の道のりを返す()
        {
            float distance = WaveLinePath.Project(LShape, new Vector3(2, 1, 1), out float sqr);
            Assert.AreEqual(2.0f, distance, Tolerance);
            Assert.AreEqual(2.0f, sqr, Tolerance);   // 高さ 1 と奥行き 1
        }

        [Test]
        public void Project_2本目の線に近ければその道のりを返す()
        {
            float distance = WaveLinePath.Project(LShape, new Vector3(5, 0, 2), out float sqr);
            Assert.AreEqual(6.0f, distance, Tolerance);
            Assert.AreEqual(1.0f, sqr, Tolerance);
        }

        [Test]
        public void Project_線の外は端に丸める()
        {
            Assert.AreEqual(0.0f, WaveLinePath.Project(LShape, new Vector3(-5, 0, 0), out _), Tolerance);
            Assert.AreEqual(7.0f, WaveLinePath.Project(LShape, new Vector3(4, 0, 9), out _), Tolerance);
        }

        [Test]
        public void Project_点が2つ未満なら0で距離は最大()
        {
            float distance = WaveLinePath.Project(new Vector3[0], Vector3.one, out float sqr);
            Assert.AreEqual(0.0f, distance, Tolerance);
            Assert.AreEqual(float.MaxValue, sqr);
        }
    }
}
