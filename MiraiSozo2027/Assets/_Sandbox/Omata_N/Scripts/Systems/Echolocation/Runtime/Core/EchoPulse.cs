// 各波の位置・向き・寿命を保持する。描画と入力に依存せず、同じ時間基準で全波を進める。
using System.Collections.Generic;
using UnityEngine;

namespace Echo.Echolocation
{
    internal struct EchoPulse
    {
        public Vector3 Origin { get; set; }
        public Vector3 Forward { get; set; }
        public double Age { get; set; }
        public float FrozenRadius { get; set; }
        public long Id { get; set; }
    }
}
