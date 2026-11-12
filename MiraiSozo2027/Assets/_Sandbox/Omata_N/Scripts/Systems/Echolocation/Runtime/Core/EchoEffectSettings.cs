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
        // 照射中の表示だけで使う。実体化しているときの表示では無視する。
        [SerializeField] private bool _isSeamlessTransition;
        public bool IsEnabled { get => _isEnabled; set => _isEnabled = value; }
        public bool IsThroughWalls { get => _isThroughWalls; set => _isThroughWalls = value; }
        /// <summary>接触中と残存中の両方に使う表示範囲。</summary>
        public EchoOriginalRevealMode DisplayScope { get => _displayScope; set => _displayScope = value; }
        public bool HasPersistence { get => _hasPersistence; set => _hasPersistence = value; }
        public float PersistenceSeconds { get => float.IsFinite(_persistenceSeconds) ? Mathf.Clamp(_persistenceSeconds, 0, MaximumPersistence) : 0; set => _persistenceSeconds = value; }
        /// <summary>照射時間の進み具合に合わせて、実体化しているときの見た目へ少しずつ近づけるか。</summary>
        public bool IsSeamlessTransition { get => _isSeamlessTransition; set => _isSeamlessTransition = value; }
        public float EffectivePersistence => IsEnabled && HasPersistence ? PersistenceSeconds : 0;
        public bool NeedsSurfaceMemory => EffectivePersistence > 0 && DisplayScope == EchoOriginalRevealMode.ContactRegion;
        public bool NeedsWholeObjectContact => IsEnabled && DisplayScope == EchoOriginalRevealMode.WholeObject;
        public EchoEffectSettings Copy() => (EchoEffectSettings)MemberwiseClone();
        /// <summary>参照を保ったまま、他の設定の表示内容を写す。シームレス移行の指定は照射中だけの設定なので写さない。</summary>
        public void CopyFrom(EchoEffectSettings source)
        {
            _isEnabled = source._isEnabled; _isThroughWalls = source._isThroughWalls; _displayScope = source._displayScope;
            _hasPersistence = source._hasPersistence; _persistenceSeconds = source._persistenceSeconds;
        }
    }
}
