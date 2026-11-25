using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Aura.Gimmick.Tests
{
    /// @brief WaveRider の「波が無くなったとき」の初期位置へ戻る動きの PlayMode テスト
    /// 波を出すもの（WaveEmitter）を置かず、最初から波が無い状態で動かす（Awake で「未設定」のエラーが出るので、期待しておく）
    public sealed class WaveRiderReturnTests
    {
        private const float PositionTolerance = 0.02f;
        private const float AngleTolerance = 1.0f;
        private const int SettleSteps = 60;   ///< 戻り終えるまで待つ物理ステップ数
        private const int NavSettleSteps = 150; ///< 経路探索で戻り終えるまで待つ物理ステップ数（約 14m を 10m/s で）
        private const float MaxAngleBeforeWall = 16.0f; ///< 壁際で回れる最大の角度（度）。約 14.4° で壁に当たる
        private const float RiderSize = 3.0f; ///< 壁際で回る検証に使う、ライダーの 1 辺（m）

        private GameObject _riderObject;
        private GameObject _wallObject;
        private WaveRider _rider;
        private Rigidbody _body;

        [TearDown]
        public void TearDown()
        {
            if (_riderObject != null)
            {
                Object.Destroy(_riderObject);
            }
            if (_wallObject != null)
            {
                Object.Destroy(_wallObject);
            }
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = typeof(WaveRider).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, name);
            field.SetValue(target, value);
        }

        /// 戻る系の設定で、波が無い状態のライダーを作る
        private void CreateRider(float returnSpeed, bool isFacing)
        {
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("_emitter"));
            _riderObject = new GameObject("TestRider");
            _riderObject.AddComponent<BoxCollider>().size = Vector3.one * RiderSize;
            _riderObject.AddComponent<Aura.ObjectInfo.WaveReaction>();
            _rider = _riderObject.AddComponent<WaveRider>();
            _body = _riderObject.GetComponent<Rigidbody>();
            SetField(_rider, "_lostWaveMode", WaveRiderLostWaveMode.ReturnToStart);
            SetField(_rider, "_returnSpeed", returnSpeed);
            SetField(_rider, "_isFacingTravelDirection", isFacing);
        }

        /// 戻り終えた通知を数える。フィールドが無いときは、名前を示して落とす
        private int[] CountReturned()
        {
            FieldInfo field = typeof(WaveRider).GetField("_onReturned", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, "_onReturned");
            var calls = new int[1];
            ((UnityEngine.Events.UnityEvent)field.GetValue(_rider)).AddListener(() => calls[0]++);
            return calls;
        }

        private IEnumerator Settle(int steps = SettleSteps)
        {
            for (int i = 0; i < steps; i++)
            {
                yield return new WaitForFixedUpdate();
            }
        }

        [UnityTest]
        public IEnumerator 初期位置へ戻り終えたら通知を1回だけ出す()
        {
            CreateRider(10.0f, false);
            int[] calls = CountReturned();
            SetField(_rider, "_isReturnPending", true);   // 波に乗ったあと、という状態にする
            _rider.SetInitialPosition(new Vector3(0, 0, 3));

            yield return Settle();

            Assert.That((_body.position - new Vector3(0, 0, 3)).magnitude, Is.LessThan(PositionTolerance));
            Assert.AreEqual(1, calls[0]);
        }

        [UnityTest]
        public IEnumerator 経路探索で戻るときも初期位置と初期の向きへ着いて通知を1回だけ出す()
        {
            // 床だけで NavMesh を焼く（ライダーは焼いたあとに置くので、NavMesh に含まれない）
            _wallObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _wallObject.transform.position = new Vector3(0, -0.5f, 0);
            _wallObject.transform.localScale = new Vector3(30, 1, 30);
            Physics.SyncTransforms();
            var surface = _wallObject.AddComponent<Unity.AI.Navigation.NavMeshSurface>();
            surface.BuildNavMesh();

            CreateRider(10.0f, true);
            SetField(_rider, "_lostWaveMode", WaveRiderLostWaveMode.ReturnByNavMesh);
            _rider.SetInitialPosition(new Vector3(5, 0.5f, 5));
            _rider.SetInitialRotation(Quaternion.Euler(0, 45, 0));
            _body.position = new Vector3(-5, 0.5f, -5);
            _riderObject.transform.position = _body.position;
            int[] calls = CountReturned();
            SetField(_rider, "_isReturnPending", true);

            yield return Settle(NavSettleSteps);

            Assert.That((_body.position - new Vector3(5, 0.5f, 5)).magnitude, Is.LessThan(PositionTolerance));
            Assert.That(Quaternion.Angle(_body.rotation, Quaternion.Euler(0, 45, 0)), Is.LessThan(AngleTolerance));
            Assert.AreEqual(1, calls[0]);
        }

        [UnityTest]
        public IEnumerator 波に乗っていなければ戻っても通知を出さない()
        {
            CreateRider(10.0f, false);
            int[] calls = CountReturned();
            _rider.SetInitialPosition(new Vector3(0, 0, 3));

            yield return Settle();

            Assert.AreEqual(0, calls[0]);
        }

        [UnityTest]
        public IEnumerator 戻る速さが0なら向きも戻さない()
        {
            CreateRider(0.0f, true);
            _body.rotation = Quaternion.Euler(0, 90, 0);

            yield return Settle();

            Assert.That(Quaternion.Angle(_body.rotation, Quaternion.Euler(0, 90, 0)), Is.LessThan(AngleTolerance));
        }

        [UnityTest]
        public IEnumerator 戻るとき初期の向きへ戻る()
        {
            CreateRider(2.0f, true);
            _rider.SetInitialRotation(Quaternion.Euler(0, 45, 0));
            _body.rotation = Quaternion.Euler(0, 90, 0);

            yield return Settle();

            Assert.That(Quaternion.Angle(_body.rotation, Quaternion.Euler(0, 45, 0)), Is.LessThan(AngleTolerance));
        }

        [UnityTest]
        public IEnumerator 壁際で回ると食い込むなら壁の手前で回るのを止める()
        {
            CreateRider(2.0f, true);
            // 一辺 3m のライダーは、θ 回ると角が 1.5×(cosθ+sinθ) まで張り出す。壁の面は 2.0m なので、約 14.4° で壁に当たって止まる
            _wallObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _wallObject.transform.position = new Vector3(2.5f, 0, 0);
            _wallObject.transform.localScale = new Vector3(1, 3, 6);
            Physics.SyncTransforms();
            _rider.SetInitialRotation(Quaternion.Euler(0, 45, 0));

            yield return Settle();

            float angle = Quaternion.Angle(_body.rotation, Quaternion.identity);
            Assert.That(angle, Is.GreaterThan(5.0f), "壁に当たる手前までは回る");
            Assert.That(angle, Is.LessThan(MaxAngleBeforeWall), "壁に当たったら、それ以上回らない");
        }

        [UnityTest]
        public IEnumerator 戻る途中の動くRigidbodyでは止まらず押しのけて戻る()
        {
            CreateRider(10.0f, false);
            // 一辺 3m のライダーの前面は z=1.5。その先に、重力の無い動く箱を置く
            _wallObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _wallObject.transform.position = new Vector3(0, 0, 3.5f);
            Rigidbody box = _wallObject.AddComponent<Rigidbody>();
            box.useGravity = false;
            Physics.SyncTransforms();
            _rider.SetInitialPosition(new Vector3(0, 0, 6));

            yield return Settle();

            Assert.That((_body.position - new Vector3(0, 0, 6)).magnitude, Is.LessThan(PositionTolerance));
        }

        [UnityTest]
        public IEnumerator 壁が無ければ同じ向きへ回れる()
        {
            CreateRider(2.0f, true);
            _rider.SetInitialRotation(Quaternion.Euler(0, 45, 0));

            yield return Settle();

            Assert.That(Quaternion.Angle(_body.rotation, Quaternion.Euler(0, 45, 0)), Is.LessThan(AngleTolerance));
        }
    }
}
