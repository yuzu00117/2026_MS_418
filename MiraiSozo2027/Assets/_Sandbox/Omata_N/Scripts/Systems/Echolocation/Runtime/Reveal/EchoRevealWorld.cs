using System.Collections.Generic;
using UnityEngine;
namespace Echo.Echolocation
{
    /// <summary>ControllerのLateUpdate後に判定し、次のFixedUpdateで確定する。自動常駐しない。</summary>
    [DefaultExecutionOrder(2000), DisallowMultipleComponent, RequireComponent(typeof(EchoController))]
    public sealed class EchoRevealWorld : MonoBehaviour
    {
        [SerializeField, Min(.05f), Tooltip("GPU結果が届かない場合に状態を解除する実時間秒数。")]
        private float _staleTimeout = .25f;
        private readonly List<EchoRevealState> _states = new List<EchoRevealState>();
        private readonly EchoTagResolver _resolver = new EchoTagResolver();
        public EchoController Controller { get; private set; }
        public float StaleTimeout => float.IsFinite(_staleTimeout) ? Mathf.Max(.05f, _staleTimeout) : .25f;
        private void Awake() => Controller = GetComponent<EchoController>();
        private void OnEnable() { if (!Controller) Controller = GetComponent<EchoController>(); EchoTarget.Changed += Refresh; Refresh(); }
        private void OnDisable()
        {
            EchoTarget.Changed -= Refresh;
            for (int i = 0; i < _states.Count; i++)
                if (_states[i] && _states[i].World == this) _states[i].Release();
            _states.Clear();
        }
        public void Refresh()
        {
            _states.Clear();
            foreach (var state in FindObjectsByType<EchoRevealState>(FindObjectsInactive.Include))
                if (state.World == this) _states.Add(state);
        }
        private void LateUpdate()
        {
            _resolver.Update(Controller.Settings.TagColorRules);
            for (int i = 0; i < _states.Count; i++)
            {
                var state = _states[i];
                if (state && state.World == this)
                {
                    var excluded = Controller.ExcludedRoot ? Controller.ExcludedRoot : Controller.transform;
                    if (!Controller.isActiveAndEnabled || !Controller.TargetCamera || !state.isActiveAndEnabled ||
                        state.transform.IsChildOf(excluded) || (Controller.Settings.TargetLayers.value & (1 << state.gameObject.layer)) == 0 ||
                        !_resolver.TryGetRule(state.Tag, out var rule) || !rule.OriginalEnabled || !rule.HideOriginalOutsideEcho)
                    { state.Release(); continue; }
                    state.Sample(rule);
                }
            }
        }
        private void FixedUpdate()
        {
            if (!Controller || !Controller.isActiveAndEnabled) return;
            for (int i = 0; i < _states.Count; i++)
                if (_states[i] && _states[i].World == this && _states[i].isActiveAndEnabled) _states[i].Commit();
        }
    }
}

