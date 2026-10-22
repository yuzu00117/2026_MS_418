using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace Echo.Echolocation
{
    /// <summary>元マテリアル表示の確定状態。GameObject.activeSelfやカメラ可視性とは独立。</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(EchoTag))]
    public sealed class EchoRevealState : MonoBehaviour
    {
        [SerializeField] private EchoRevealWorld _world;
        [SerializeField] private EchoSurfaceBinding[] _surfaces = Array.Empty<EchoSurfaceBinding>();
        private readonly List<Renderer> _sources = new List<Renderer>();
        private readonly List<MeshRenderer> _patches = new List<MeshRenderer>();
        private readonly List<MeshCollider> _colliders = new List<MeshCollider>();
        private readonly List<EchoRenderItem> _originalItems = new List<EchoRenderItem>();
        private readonly List<float> _areas = new List<float>();
        private readonly List<Matrix4x4> _sourceMatrices = new List<Matrix4x4>();
        private readonly List<Material[]> _sourceMaterials = new List<Material[]>();
        private EchoContactSampler _sampler;
        private EchoRevealCollider _collision;
        private EchoTagColorRule _rule;
        private EchoOriginalRevealMode _mode;
        private EchoTimeMode _timeMode;
        private EchoRevealCollisionMode _collisionMode;
        private int _version;
        private bool[] _contacts = Array.Empty<bool>();
        private bool[] _activePatches = Array.Empty<bool>();
        private float[] _lastContacts = Array.Empty<float>();
        private double _lastResult;
        private bool _hasResult;
        private bool _hasFailed;
        private bool _hasPendingRefresh;
        private bool _canAcceptPending = true;
        private EchoTarget _owner;
        public EchoRevealWorld World => _world;
        public IReadOnlyList<EchoSurfaceBinding> Surfaces => _surfaces;
        public EchoTag Tag { get; private set; }
        public bool IsActive { get; private set; }
        public bool HasActiveCollision { get; private set; }
        public bool IsOperational { get; private set; }
        public float RevealedFraction { get; private set; }
        public uint Revision { get; private set; }
        public int PendingPatchCount { get; private set; }
        public IReadOnlyList<EchoRenderItem> OriginalItems => _originalItems;
        public event Action<EchoRevealSnapshot> Changed;
        public EchoRevealSnapshot Snapshot => new EchoRevealSnapshot(IsActive, HasActiveCollision, IsOperational,
            RevealedFraction, Revision, PendingPatchCount);
        private void Awake() => Tag = GetComponent<EchoTag>();
        private void OnEnable()
        {
            Tag = GetComponent<EchoTag>();
            _hasFailed = false;
            _hasPendingRefresh = false;
            if (_world) _world.Refresh();
            GetComponent<EchoTarget>().RefreshRenderers();
        }
        private void OnDisable() => Release();
        private void OnValidate() => _hasPendingRefresh = true;
        private void LateUpdate()
        {
            if (!_hasPendingRefresh) return;
            _hasPendingRefresh = false;
            Refresh();
            if (_world) _world.Refresh();
        }
        public void Configure(EchoRevealWorld world, EchoSurfaceBinding[] surfaces)
        {
            var previousWorld = _world;
            Release(); _world = world; _surfaces = surfaces ?? Array.Empty<EchoSurfaceBinding>();
            _hasFailed = false;
            if (previousWorld && previousWorld != _world) previousWorld.Refresh();
            if (_world) _world.Refresh();
        }
        private bool Initialize(EchoTagColorRule rule)
        {
            _rule = rule; _mode = rule.RevealMode;
            _timeMode = _world.Controller.Settings.TimeMode; _version = _world.Controller.EnableVersion;
            _collision = GetComponent<EchoRevealCollider>();
            _collisionMode = _collision ? _collision.Mode : EchoRevealCollisionMode.Unchanged;
            if ((_collisionMode == EchoRevealCollisionMode.RevealedRegion && _mode != EchoOriginalRevealMode.ContactRegion) ||
                (_collisionMode == EchoRevealCollisionMode.WholeObject && _mode != EchoOriginalRevealMode.WholeObject))
                return Fail("表示範囲とColliderモードをそろえてください。");
            var owner = GetComponent<EchoTarget>();
            _owner = owner;
            foreach (var source in GetComponentsInChildren<Renderer>(true))
                if (!source.GetComponent<EchoRevealProxy>() && owner.Owns(source) && source.enabled && source.gameObject.activeInHierarchy)
                {
                    _sources.Add(source); _sourceMatrices.Add(source.localToWorldMatrix);
                    _sourceMaterials.Add(EchoTargetRegistry.GetOriginalMaterials(source));
                }
            if (_sources.Count == 0) return Fail("表示対象のRendererがありません。");
            if (_mode == EchoOriginalRevealMode.ContactRegion)
            {
                if (GetComponentInParent<Rigidbody>()) return Fail("部分表示は静的オブジェクト専用です。");
                foreach (var source in _sources)
                {
                    EchoSurfaceBinding binding = null;
                    foreach (var candidate in _surfaces) if (candidate != null && candidate.Renderer == source) { binding = candidate; break; }
                    if (binding == null || !binding.Data)
                        return Fail($"「{source.name}」の分割データがありません。再生を停止し、エコー表示状態のInspectorで「所有Rendererを分割ベイク」を実行してください。");
                    if (!source.TryGetComponent<MeshFilter>(out var filter) ||
                        filter.sharedMesh != binding.Data.SourceMesh ||
                        (source.transform.lossyScale - binding.Data.SourceScale).sqrMagnitude > .000001f ||
                        source.GetComponentInParent<LODGroup>() || source.GetComponentInParent<Rigidbody>())
                        return Fail("メッシュ・スケール・LOD/Rigidbodyを確認し、分割データを再ベイクしてください。");
                    var materials = EchoTargetRegistry.GetOriginalMaterials(source);
                    foreach (var material in materials)
                        if (!material || material.renderQueue > 2500 || material.IsKeywordEnabled("_ALPHATEST_ON"))
                            return Fail("部分表示は不透明・非AlphaClipマテリアル専用です。");
                    foreach (var patch in binding.Data.Patches)
                    {
                        if (_patches.Count >= 1024) return Fail("1対象の分割数は1024以下にしてください。");
                        var child = new GameObject("Echo reveal patch") { hideFlags = HideFlags.DontSave, layer = source.gameObject.layer };
                        child.transform.SetParent(source.transform, false);
                        child.AddComponent<EchoRevealProxy>();
                        child.AddComponent<MeshFilter>().sharedMesh = patch.Mesh;
                        var renderer = child.AddComponent<MeshRenderer>();
                        renderer.sharedMaterial = materials[Mathf.Min(patch.MaterialIndex, materials.Length - 1)];
                        renderer.shadowCastingMode = ShadowCastingMode.Off;
                        renderer.receiveShadows = source.receiveShadows;
                        renderer.lightProbeUsage = source.lightProbeUsage;
                        renderer.reflectionProbeUsage = source.reflectionProbeUsage;
                        renderer.probeAnchor = source.probeAnchor;
                        renderer.lightmapIndex = source.lightmapIndex;
                        renderer.lightmapScaleOffset = source.lightmapScaleOffset;
                        var properties = new MaterialPropertyBlock();
                        source.GetPropertyBlock(properties); renderer.SetPropertyBlock(properties);
                        _patches.Add(renderer);
                        _areas.Add(patch.Area);
                        EchoTargetRegistry.AcquireHidden(renderer);
                        var collider = child.AddComponent<MeshCollider>();
                        collider.sharedMesh = patch.Mesh; collider.enabled = false;
                        if (_collision) collider.sharedMaterial = _collision.PatchMaterial;
                        _colliders.Add(collider);
                    }
                }
                if (_patches.Count == 0) return Fail("分割データに三角形がありません。");
            }
            else if (_collision && _collision.Mode == EchoRevealCollisionMode.RevealedRegion)
                return Fail("部分ColliderはContactRegion表示と組み合わせてください。");
            int count = _mode == EchoOriginalRevealMode.ContactRegion ? _patches.Count : 1;
            _contacts = new bool[count]; _activePatches = new bool[count]; _lastContacts = new float[count];
            for (int i = 0; i < count; i++) _lastContacts[i] = float.NegativeInfinity;
            _sampler = new EchoContactSampler();
            if (!_sampler.IsSupported) return Fail("AsyncGPUReadback/RFloatが非対応です。");
            _lastResult = Time.realtimeSinceStartupAsDouble;
            _collision?.Capture();
            return true;
        }
        private bool Fail(string message)
        {
            _hasFailed = true;
            Release(true);
            Debug.LogError("エコー表示状態：" + message, this);
            return false;
        }
        public void Sample(EchoTagColorRule rule)
        {
            if (_hasFailed || !_world || !_world.isActiveAndEnabled) return;
            if (_rule != null && (_rule != rule || _mode != rule.RevealMode || _timeMode != _world.Controller.Settings.TimeMode ||
                _version != _world.Controller.EnableVersion ||
                _collisionMode != (_collision ? _collision.Mode : EchoRevealCollisionMode.Unchanged))) Release();
            if (_sampler == null)
            {
                try { if (!Initialize(rule)) return; }
                catch (Exception exception) { Fail(exception.Message); return; }
            }
            if (_mode == EchoOriginalRevealMode.ContactRegion)
                for (int i = 0; i < _sources.Count; i++)
                    if (!_sources[i] || _sources[i].localToWorldMatrix != _sourceMatrices[i])
                    { Fail("部分表示対象が移動しました。静的な配置で使用してください。"); return; }
            for (int i = 0; i < _sources.Count; i++)
            {
                if (!_sources[i]) { Release(); return; }
                var current = EchoTargetRegistry.GetOriginalMaterials(_sources[i]);
                bool hasChanged = current.Length != _sourceMaterials[i].Length;
                for (int s = 0; !hasChanged && s < current.Length; s++) hasChanged |= current[s] != _sourceMaterials[i][s];
                if (hasChanged) { Release(); Sample(rule); return; }
            }
            bool areSourcesAvailable = _owner && _owner.isActiveAndEnabled;
            foreach (var source in _sources)
                areSourcesAvailable &= source && source.enabled && !source.forceRenderingOff && source.gameObject.activeInHierarchy;
            if (!areSourcesAvailable) { _canAcceptPending = false; _hasResult = false; Array.Clear(_contacts, 0, _contacts.Length); }
            if (_sampler.TryReceive(_contacts, out bool hasError))
            {
                if (hasError) { Fail("GPU接触判定の読み戻しに失敗しました。"); return; }
                if (_canAcceptPending && areSourcesAvailable && Time.realtimeSinceStartupAsDouble - _sampler.SubmittedAt <= _world.StaleTimeout)
                {
                    _hasResult = true; _lastResult = Time.realtimeSinceStartupAsDouble;
                    for (int i = 0; i < _contacts.Length; i++) if (_contacts[i]) _lastContacts[i] = _sampler.SampleTime;
                }
                else { _hasResult = false; Array.Clear(_contacts, 0, _contacts.Length); }
            }
            if (!areSourcesAvailable) return;
            if (!_sampler.IsPending) _canAcceptPending = true;
            _sampler.Submit(_world.Controller, _sources, _patches, _mode == EchoOriginalRevealMode.ContactRegion, rule.ContactResolution);
        }
        public void Commit()
        {
            if (_sampler == null || _rule == null) return;
            bool isFresh = _hasResult && Time.realtimeSinceStartupAsDouble - _lastResult <= _world.StaleTimeout;
            float now = _timeMode == EchoTimeMode.Scaled ? Time.time : Time.unscaledTime;
            bool isActive = false;
            float activeArea = 0, totalArea = 0;
            int pending = 0;
            bool hasRegionChanged = false;
            _originalItems.Clear();
            for (int i = 0; i < _activePatches.Length; i++)
            {
                bool isShown = isFresh && _rule.OriginalOpacity > 0 && (_contacts[i] || now - _lastContacts[i] <= _rule.OriginalHoldDuration);
                if (!isShown && isFresh && _rule.OriginalOpacity > 0 && _collision && _collision.Mode == EchoRevealCollisionMode.RevealedRegion &&
                    _activePatches[i] && _collision.ShouldRetain(_colliders[i])) { isShown = true; pending++; }
                hasRegionChanged |= _activePatches[i] != isShown;
                _activePatches[i] = isShown; isActive |= isShown;
                float area = _mode == EchoOriginalRevealMode.ContactRegion ? _areas[i] : 1;
                totalArea += area; if (isShown) activeArea += area;
                if (isShown && _mode == EchoOriginalRevealMode.ContactRegion)
                    _originalItems.Add(new EchoRenderItem(_patches[i], _rule, 1, this));
            }
            if (isActive && _mode == EchoOriginalRevealMode.WholeObject)
                foreach (var source in _sources)
                    if (source && source.enabled && source.gameObject.activeInHierarchy)
                        _originalItems.Add(new EchoRenderItem(source, _rule, EchoTargetRegistry.GetOriginalMaterials(source).Length, this));
            if (_collision) _collision.Commit(isActive, _colliders, _activePatches);
            Publish(isActive, _collision && _collision.HasActiveCollision, isFresh,
                totalArea > 0 ? activeArea / totalArea : 0, pending, hasRegionChanged);
        }
        private void Publish(bool isActive, bool hasCollision, bool isOperational, float fraction, int pending, bool hasRegionChanged = false)
        {
            if (IsActive == isActive && HasActiveCollision == hasCollision && IsOperational == isOperational &&
                RevealedFraction == fraction && PendingPatchCount == pending && !hasRegionChanged) return;
            IsActive = isActive; HasActiveCollision = hasCollision; IsOperational = isOperational;
            RevealedFraction = fraction; PendingPatchCount = pending; Revision++;
            Changed?.Invoke(Snapshot);
        }
        public void Refresh()
        {
            Release(); _hasFailed = false;
        }
        public void Release() => Release(false);
        private void Release(bool hasFailed)
        {
            _sampler?.Dispose(); _sampler = null;
            if (_collision) _collision.Restore();
            foreach (var patch in _patches)
                if (patch) { EchoTargetRegistry.ReleaseHidden(patch); patch.gameObject.SetActive(false); Destroy(patch.gameObject); }
            _patches.Clear(); _colliders.Clear(); _sources.Clear(); _sourceMatrices.Clear(); _sourceMaterials.Clear(); _areas.Clear(); _originalItems.Clear();
            _contacts = Array.Empty<bool>(); _activePatches = Array.Empty<bool>(); _lastContacts = Array.Empty<float>();
            _rule = null; _hasResult = false; _canAcceptPending = true;
            if (hasFailed && _collision && _collision.Mode != EchoRevealCollisionMode.Unchanged)
                _collision.Commit(false, Array.Empty<MeshCollider>(), Array.Empty<bool>());
            Publish(false, false, false, 0, 0);
        }
    }
}

