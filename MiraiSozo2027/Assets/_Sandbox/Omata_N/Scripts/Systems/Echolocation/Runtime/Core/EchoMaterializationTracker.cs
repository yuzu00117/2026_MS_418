// 実体化の状態遷移だけを持つ。GPUの接触結果やUnityの時間に依存させず、EditModeで検証できるようにする。
using UnityEngine;

namespace Echo.Echolocation
{
    /// <summary>照射の有無と経過時間から、透明・照射中・実体化の状態を進める。</summary>
    public sealed class EchoMaterializationTracker
    {
        private EchoMaterializationState _state = EchoMaterializationState.Transparent;
        private float _irradiationSeconds;
        private float _idleSeconds;

        public EchoMaterializationState State => _state;
        /// <summary>実体化に向けて蓄積した照射時間（秒）。</summary>
        public float IrradiationSeconds => _irradiationSeconds;

        /// <summary>1フレーム分進める。状態が変わったらtrue。</summary>
        public bool Advance(bool isInContact, float deltaSeconds, EchoMaterializationSettings settings)
        {
            var previous = _state;
            float delta = float.IsFinite(deltaSeconds) ? Mathf.Max(0, deltaSeconds) : 0;
            if (_state == EchoMaterializationState.Materialized) AdvanceMaterialized(isInContact, delta, settings);
            else AdvanceTransparent(isInContact, delta, settings);
            return _state != previous;
        }

        /// <summary>照射時間を満たしたものとして実体化させる。状態が変わったらtrue。</summary>
        public bool Materialize(float requiredSeconds)
        {
            _irradiationSeconds = Mathf.Max(_irradiationSeconds, requiredSeconds);
            _idleSeconds = 0;
            return SetState(EchoMaterializationState.Materialized);
        }

        /// <summary>蓄積を捨てて透明に戻す。状態が変わったらtrue。</summary>
        public bool ResetToTransparent()
        {
            _irradiationSeconds = 0;
            _idleSeconds = 0;
            return SetState(EchoMaterializationState.Transparent);
        }

        /// <summary>通常の描画を隠さない対象は照射と無関係に実体化しているため、蓄積を持たずに固定する。</summary>
        public bool ForceMaterialized()
        {
            _irradiationSeconds = 0;
            _idleSeconds = 0;
            return SetState(EchoMaterializationState.Materialized);
        }

        void AdvanceTransparent(bool isInContact, float delta, EchoMaterializationSettings settings)
        {
            if (isInContact)
            {
                _irradiationSeconds += delta;
                // 0秒指定は「当たった瞬間」なので、同じフレームで実体化させる。
                if (_irradiationSeconds >= settings.RequiredSeconds) { Materialize(settings.RequiredSeconds); return; }
                SetState(EchoMaterializationState.Irradiated);
                return;
            }

            switch (settings.InterruptionMode)
            {
                case EchoIrradiationInterruptionMode.Reset: _irradiationSeconds = 0; break;
                case EchoIrradiationInterruptionMode.Decay: _irradiationSeconds = Mathf.Max(0, _irradiationSeconds - delta * settings.DecaySpeed); break;
            }
            SetState(EchoMaterializationState.Transparent);
        }

        void AdvanceMaterialized(bool isInContact, float delta, EchoMaterializationSettings settings)
        {
            if (settings.PersistenceMode == EchoMaterializedPersistenceMode.Permanent || isInContact)
            {
                _idleSeconds = 0;
                return;
            }
            _idleSeconds += delta;
            if (_idleSeconds >= settings.RevertSeconds) ResetToTransparent();
        }

        bool SetState(EchoMaterializationState state)
        {
            if (_state == state) return false;
            _state = state;
            return true;
        }
    }
}
