using System;
using UnityEngine;

namespace Echo.Echolocation
{
    /// <summary>通常の描画を隠す対象が、照射によって実体化するまでの条件。</summary>
    [Serializable]
    public sealed class EchoMaterializationSettings
    {
        private const float DefaultRequiredSeconds = 1;
        private const float DefaultDecaySpeed = 1;
        private const float DefaultRevertSeconds = 5;
        private const float MaximumSeconds = 3600;
        private const float MaximumDecaySpeed = 100;

        [SerializeField, Min(0), Tooltip("この秒数エコーを当て続けると実体化します。0なら当たった瞬間に実体化します。")]
        private float _requiredSeconds = DefaultRequiredSeconds;
        [SerializeField, Tooltip("実体化する前に照射が途切れたときの、蓄積した照射時間の扱い。")]
        private EchoIrradiationInterruptionMode _interruptionMode = EchoIrradiationInterruptionMode.Reset;
        [SerializeField, Min(0), Tooltip("照射していない1秒あたりに減らす照射時間（秒）。1なら当てた時間と同じ速さで減ります。")]
        private float _decaySpeed = DefaultDecaySpeed;
        [SerializeField, Tooltip("実体化した後、エコーが当たらなくなったときの扱い。")]
        private EchoMaterializedPersistenceMode _persistenceMode = EchoMaterializedPersistenceMode.Permanent;
        [SerializeField, Min(0), Tooltip("最後にエコーが当たってからこの秒数で透明に戻ります。")]
        private float _revertSeconds = DefaultRevertSeconds;
        [SerializeField, Tooltip("実体化するまで、自身と子のColliderを無効にします。子EchoTargetの所有物は対象外です。")]
        private bool _isColliderDisabledWhileTransparent;

        public float RequiredSeconds { get => Sanitize(_requiredSeconds, MaximumSeconds); set => _requiredSeconds = value; }
        public EchoIrradiationInterruptionMode InterruptionMode { get => _interruptionMode; set => _interruptionMode = value; }
        public float DecaySpeed { get => Sanitize(_decaySpeed, MaximumDecaySpeed); set => _decaySpeed = value; }
        public EchoMaterializedPersistenceMode PersistenceMode { get => _persistenceMode; set => _persistenceMode = value; }
        public float RevertSeconds { get => Sanitize(_revertSeconds, MaximumSeconds); set => _revertSeconds = value; }
        public bool IsColliderDisabledWhileTransparent { get => _isColliderDisabledWhileTransparent; set => _isColliderDisabledWhileTransparent = value; }

        public EchoMaterializationSettings Copy() => (EchoMaterializationSettings)MemberwiseClone();

        static float Sanitize(float value, float max) => EchoTagColorRule.FiniteClamp(value, 0, max, 0);
    }
}
