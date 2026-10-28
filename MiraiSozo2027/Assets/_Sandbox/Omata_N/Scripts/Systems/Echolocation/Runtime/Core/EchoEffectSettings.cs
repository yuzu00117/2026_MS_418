using System;
using UnityEngine;
namespace Echo.Echolocation
{
    [Serializable]
    public class EchoEffectSettings
    {
        private const float MaximumPersistence = 3600;
        [SerializeField] private bool _isEnabled;
        [SerializeField] private bool _isThroughWalls;
        // 接触中と残存中で範囲が変わると表示が途中で広がる・欠けるため、1つの設定で両方に使う。
        [SerializeField] private EchoOriginalRevealMode _displayScope = EchoOriginalRevealMode.ContactRegion;
        [SerializeField] private bool _hasPersistence;
        [SerializeField, Min(0)] private float _persistenceSeconds = 1;
        public bool IsEnabled { get => _isEnabled; set => _isEnabled = value; }
        public bool IsThroughWalls { get => _isThroughWalls; set => _isThroughWalls = value; }
        /// <summary>接触中と残存中の両方に使う表示範囲。</summary>
        public EchoOriginalRevealMode DisplayScope { get => _displayScope; set => _displayScope = value; }
        public bool HasPersistence { get => _hasPersistence; set => _hasPersistence = value; }
        public float PersistenceSeconds { get => float.IsFinite(_persistenceSeconds) ? Mathf.Clamp(_persistenceSeconds, 0, MaximumPersistence) : 0; set => _persistenceSeconds = value; }
        public float EffectivePersistence => IsEnabled && HasPersistence ? PersistenceSeconds : 0;
        public bool NeedsSurfaceMemory => EffectivePersistence > 0 && DisplayScope == EchoOriginalRevealMode.ContactRegion;
        public bool NeedsWholeObjectContact => IsEnabled && DisplayScope == EchoOriginalRevealMode.WholeObject;
        public EchoEffectSettings Copy() => (EchoEffectSettings)MemberwiseClone();
    }
}
