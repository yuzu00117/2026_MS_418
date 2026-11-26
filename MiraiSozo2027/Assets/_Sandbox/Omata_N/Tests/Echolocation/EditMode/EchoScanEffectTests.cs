// エフェクトがスキャン中心の子として1度だけ生成され、大きさ・速さ・向きを生成したものだけに反映することを検証する。
using NUnit.Framework;
using UnityEngine;

namespace Aura.Echolocation.Tests
{
    public sealed class EchoScanEffectTests
    {
        private GameObject _prefab;
        private GameObject _origin;
        private EchoScanEffect _effect;

        [SetUp]
        public void SetUp()
        {
            _prefab = new GameObject("Effect prefab", typeof(ParticleSystem));
            _origin = new GameObject("Origin");
            _effect = new EchoScanEffect { Prefab = _prefab };
        }

        [TearDown]
        public void TearDown()
        {
            _effect.Release();
            Object.DestroyImmediate(_origin);
            Object.DestroyImmediate(_prefab);
        }

        [Test]
        public void PlayCreatesEffectUnderOriginOnlyOnce()
        {
            _effect.SetPlaying(true, _origin.transform);
            Assert.That(_effect.IsPlaying, Is.True);
            Assert.That(_origin.transform.childCount, Is.EqualTo(1));

            _effect.SetPlaying(false, _origin.transform);
            Assert.That(_effect.IsPlaying, Is.False);
            Assert.That(_origin.transform.childCount, Is.EqualTo(1), "停止しても粒子を残すため破棄しない");

            _effect.SetPlaying(true, _origin.transform);
            Assert.That(_origin.transform.childCount, Is.EqualTo(1), "再生のたびに作り直さない");
        }

        [Test]
        public void ReleaseDestroysCreatedEffect()
        {
            _effect.SetPlaying(true, _origin.transform);
            _effect.Release();
            Assert.That(_effect.IsPlaying, Is.False);
            Assert.That(_origin.transform.childCount, Is.Zero);
        }

        [Test]
        public void ApplyMatchesRadiusDurationAndDirection()
        {
            _effect.IsScaledToRadius = true;
            _effect.BaseRadius = 17;
            _effect.IsSpeedMatchedToDuration = true;
            _effect.BaseDuration = 1.5f;
            _effect.IsFacingDirection = true;
            var direction = new GameObject("Direction");
            direction.transform.rotation = Quaternion.Euler(30, 90, 0);
            try
            {
                _effect.SetPlaying(true, _origin.transform);
                _effect.Apply(8.5f, 3, direction.transform);
                var instance = _origin.transform.GetChild(0);
                Assert.That(instance.localScale.x, Is.EqualTo(.5f).Within(1e-4f));
                Assert.That(instance.GetComponent<ParticleSystem>().main.simulationSpeed, Is.EqualTo(.5f).Within(1e-4f));
                Assert.That(_prefab.GetComponent<ParticleSystem>().main.simulationSpeed, Is.EqualTo(1).Within(1e-4f), "元のPrefabは書き換えない");
                // 上下の傾きは無視して、水平方向だけを向く。
                Assert.That(Vector3.Angle(instance.forward, Vector3.right), Is.LessThan(.01f));
            }
            finally
            {
                Object.DestroyImmediate(direction);
            }
        }

        [Test]
        public void ApplyKeepsPrefabValuesWhenOptionsAreOff()
        {
            var main = _prefab.GetComponent<ParticleSystem>().main;
            main.simulationSpeed = 2;
            _effect.SetPlaying(true, _origin.transform);
            _effect.Apply(8.5f, 3, _origin.transform);
            var instance = _origin.transform.GetChild(0);
            Assert.That(instance.localScale, Is.EqualTo(Vector3.one));
            Assert.That(instance.GetComponent<ParticleSystem>().main.simulationSpeed, Is.EqualTo(2).Within(1e-4f), "Prefabに設定された速さのまま");
        }

        [Test]
        public void ApplyKeepsPrefabRootScaleAndRotation()
        {
            _prefab.transform.localScale = new Vector3(2, 1, 3);
            _prefab.transform.localRotation = Quaternion.Euler(0, 45, 0);
            _effect.SetPlaying(true, _origin.transform);
            var instance = _origin.transform.GetChild(0);

            _effect.Apply(8.5f, 3, _origin.transform);
            Assert.That(instance.localScale, Is.EqualTo(new Vector3(2, 1, 3)), "合わせる設定がオフならPrefabの拡大率のまま");
            Assert.That(Quaternion.Angle(instance.localRotation, Quaternion.Euler(0, 45, 0)), Is.LessThan(.01f));

            _effect.IsScaledToRadius = true;
            _effect.BaseRadius = 17;
            _effect.Apply(8.5f, 3, _origin.transform);
            Assert.That(Vector3.Distance(instance.localScale, new Vector3(1, .5f, 1.5f)), Is.LessThan(1e-4f), "Prefabの拡大率に倍率を掛ける");
        }

        [Test]
        public void DoesNothingWithoutPrefab()
        {
            _effect.Prefab = null;
            _effect.SetPlaying(true, _origin.transform);
            Assert.That(_effect.IsPlaying, Is.False);
            Assert.That(_origin.transform.childCount, Is.Zero);
        }
    }
}
