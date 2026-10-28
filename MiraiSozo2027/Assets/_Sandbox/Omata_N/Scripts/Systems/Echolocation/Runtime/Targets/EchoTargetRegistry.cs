// シーンの候補をキャッシュし、描画直前に有効状態・タグ・範囲を判定する。Colliderには依存しない。
using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling;

namespace Echo.Echolocation
{
    /// <summary>Rendererごとの重複をなくし候補を収集する。</summary>
    public sealed class EchoTargetRegistry
    {
        private readonly List<EchoTargetEntry> _entries = new List<EchoTargetEntry>();
        private readonly List<IEchoGeometryAdapter> _adapters = new List<IEchoGeometryAdapter>
        {
            new EchoMeshGeometryAdapter(),
            new EchoSkinnedGeometryAdapter()
        };
        private readonly Plane[] _planes = new Plane[6];
        private readonly HashSet<Renderer> _hidden = new HashSet<Renderer>();
        private readonly HashSet<Renderer> _wantedHidden = new HashSet<Renderer>();
        private readonly List<Renderer> _releaseHidden = new List<Renderer>();
        sealed class HiddenState
        {
            public int Count;
            public Material[] Originals;
            public Material[] Invisible;
        }
        private static readonly Dictionary<Renderer, HiddenState> _hiddenOwners = new Dictionary<Renderer, HiddenState>();
        private static readonly HashSet<Material> _invisibleMaterials = new HashSet<Material>();
        private static readonly HashSet<int> _queues = new HashSet<int>();

        public static Material[] GetOriginalMaterials(Renderer renderer) =>
            _hiddenOwners.TryGetValue(renderer, out var state) ? state.Originals : renderer.sharedMaterials;
        public static Material[] GetInvisibleMaterials(Renderer renderer) =>
            _hiddenOwners.TryGetValue(renderer, out var state) ? state.Invisible : null;

        public static void AcquireHidden(Renderer renderer)
        {
            if (_hiddenOwners.TryGetValue(renderer, out var state)) { state.Count++; return; }
            var shader = Resources.Load<Shader>("EchoInvisible");
            if (!shader) throw new System.InvalidOperationException("EchoInvisible shader is missing.");
            var originals = renderer.sharedMaterials;
            var invisible = CreateInvisible(shader, originals.Length);
            _hiddenOwners.Add(renderer, new HiddenState { Count = 1, Originals = originals, Invisible = invisible });
            renderer.sharedMaterials = invisible;
        }

        static void DestroyInvisible(HiddenState state)
        {
            foreach (var material in state.Invisible)
            {
                _queues.Remove(material.renderQueue);
                _invisibleMaterials.Remove(material);
                if (Application.isPlaying) Object.Destroy(material);
                else Object.DestroyImmediate(material);
            }
        }

        static Material[] CreateInvisible(Shader shader, int count)
        {
            if (_queues.Count + count > MaximumInvisibleSlots)
                throw new System.InvalidOperationException($"エコーの元マテリアル表示は同時{MaximumInvisibleSlots}スロットまでです。");
            var result = new Material[count];
            for (int i = 0; i < count; i++)
            {
                int queue = 1;
                while (_queues.Contains(queue)) queue++;
                // Unityの有効なrenderQueueの範囲で、Echo専用LightModeと組み合わせて個別抽出する。
                _queues.Add(queue);
                result[i] = new Material(shader) { hideFlags = HideFlags.HideAndDontSave, renderQueue = queue };
                _invisibleMaterials.Add(result[i]);
            }
            return result;
        }
        // 専用の描画順を1つずつ割り当てるため、Unityが扱えるrenderQueueの範囲に収める。
        private const int MaximumInvisibleSlots = 5000;
        private static readonly ProfilerMarker _marker = new ProfilerMarker("Echo.Candidates");
        /// <summary>登録されたRenderer数。</summary>
        public int Count => _entries.Count;
        private static bool TryGetRule(EchoTagResolver tags, EchoTargetEntry entry, GameObject owner, out EchoTagColorRule rule)
        {
            rule = null;
            var definition = entry.Owner ? entry.Owner.Definition : null;
            if (!entry.Owner || !entry.Owner.isActiveAndEnabled || !definition) return false;
            rule = definition.Settings;
            return rule.HasAnyEffect;
        }

        /// <summary>専用形状取得を追加する。再収集前に登録する。</summary>
        public void AddAdapter(IEchoGeometryAdapter adapter)
        {
            if (adapter != null)
                _adapters.Insert(0, adapter);
        }

        /// <summary>全参照を解放する。</summary>
        public void Clear()
        {
            foreach (var renderer in _hidden) ReleaseHidden(renderer);
            _hidden.Clear();
            _wantedHidden.Clear();
            _releaseHidden.Clear();
            _entries.Clear();
        }

        public static void ReleaseHidden(Renderer renderer)
        {
            if (!_hiddenOwners.TryGetValue(renderer, out var state)) return;
            if (state.Count > 1) state.Count--;
            else
            {
                if (renderer)
                {
                    var materials = renderer.sharedMaterials;
                    RestoreOriginalReferences(materials, state);
                    renderer.sharedMaterials = materials;
                }
                _hiddenOwners.Remove(renderer);
                DestroyInvisible(state);
            }
        }

        static bool UsesInvisibleMaterials(Material[] materials)
        {
            if (materials.Length == 0) return false;
            foreach (var material in materials) if (!_invisibleMaterials.Contains(material)) return false;
            return true;
        }

        static void RestoreOriginalReferences(Material[] materials, HiddenState state)
        {
            for (int i = 0; i < materials.Length; i++)
                if (_invisibleMaterials.Contains(materials[i])) materials[i] = i < state.Originals.Length ? state.Originals[i] : null;
        }

        // Rendererは有効なままカリングへ渡し、照明データを維持する。
        // 元マテリアルの内容は変更せず、一時的な透明マテリアルから復元する。
        public void UpdateHidden(int layers, Transform excluded, Camera camera, EchoTagResolver tags)
        {
            _wantedHidden.Clear();
            foreach (var entry in _entries)
            {
                var renderer = entry.Renderer;
                if (!renderer || !renderer.enabled || renderer.forceRenderingOff || !renderer.gameObject.activeInHierarchy || !camera) continue;
                if (excluded && renderer.transform.IsChildOf(excluded)) continue;
                if (entry.Owner && (!entry.Owner.isActiveAndEnabled || !entry.IsOwned)) continue;
                var owner = entry.Owner ? entry.Owner.gameObject : renderer.gameObject;
                if ((layers & (1 << owner.layer)) == 0 || (camera.cullingMask & (1 << renderer.gameObject.layer)) == 0) continue;
                if (!TryGetRule(tags, entry, owner, out var rule) || !rule.IsOriginalEnabled || !rule.IsOriginalHiddenOutsideEcho) continue;

                bool isSupported = false;
                foreach (var adapter in _adapters)
                    if (adapter.TryGetGeometry(renderer, out _)) { isSupported = true; break; }
                if (!isSupported) continue;
                var shader = Resources.Load<Shader>("EchoInvisible");
                if (!shader) continue;
                _wantedHidden.Add(renderer);
                if (_hidden.Add(renderer))
                {
                    if (_hiddenOwners.TryGetValue(renderer, out var state)) state.Count++;
                    else
                    {
                        var originals = renderer.sharedMaterials;
                        var invisible = CreateInvisible(shader, originals.Length);
                        _hiddenOwners.Add(renderer, new HiddenState { Count = 1, Originals = originals, Invisible = invisible });
                        renderer.sharedMaterials = invisible;
                    }
                }
                var current = renderer.sharedMaterials;
                var hidden = _hiddenOwners[renderer];
                if (!UsesInvisibleMaterials(current))
                {
                    RestoreOriginalReferences(current, hidden);
                    hidden.Originals = current;
                    DestroyInvisible(hidden);
                    hidden.Invisible = CreateInvisible(shader, current.Length);
                    renderer.sharedMaterials = hidden.Invisible;
                }
            }
            _releaseHidden.Clear();
            foreach (var renderer in _hidden) if (!_wantedHidden.Contains(renderer)) _releaseHidden.Add(renderer);
            foreach (var renderer in _releaseHidden) { ReleaseHidden(renderer); _hidden.Remove(renderer); }
        }
        /// <summary>シーンロードまたは明示的な再収集時だけ全検索する。</summary>
        public void Refresh(bool isAuto)
        {
            _entries.Clear();
            foreach (var adapter in _adapters)
                if (adapter is EchoMeshGeometryAdapter meshAdapter) meshAdapter.Clear();
            var foundRenderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include);
            System.Array.Sort(foundRenderers, (left, right) => string.CompareOrdinal(left.GetEntityId().ToString(), right.GetEntityId().ToString()));
            foreach (var renderer in foundRenderers)
            {
                if (!renderer.gameObject.scene.IsValid() || renderer.GetComponent<EchoRevealProxy>())
                    continue;
                var owner = renderer.GetComponentInParent<EchoTarget>(true);
                if (!isAuto && !owner)
                    continue;
                var group = renderer.GetComponentInParent<LODGroup>(true);
                _entries.Add(new EchoTargetEntry { Renderer = renderer, Owner = owner, IsOwned = !owner || owner.Owns(renderer), Reveal = owner ? owner.GetComponent<EchoRevealState>() : null, Group = group, Lods = group ? group.GetLODs() : null });
            }
        }

        /// <summary>タグに一致し球と交差する全サブメッシュを提出する。</summary>
        public void Collect(IReadOnlyList<EchoFrameData> frames, int layers, Transform excluded, Camera camera, EchoTagResolver tags, List<EchoRenderItem> outputItems)
        {
            using (_marker.Auto())
            {
                GeometryUtility.CalculateFrustumPlanes(camera, _planes);
                foreach (EchoTargetEntry entry in _entries)
                {
                    var renderer = entry.Renderer;
                    if (!renderer || !renderer.enabled || renderer.forceRenderingOff || !renderer.gameObject.activeInHierarchy)
                        continue;
                    if (excluded && renderer.transform.IsChildOf(excluded))
                        continue;
                    if (entry.Owner && (!entry.Owner.isActiveAndEnabled || !entry.IsOwned))
                        continue;
                    GameObject owner = entry.Owner ? entry.Owner.gameObject : renderer.gameObject;
                    if ((layers & (1 << owner.layer)) == 0 || (camera.cullingMask & (1 << renderer.gameObject.layer)) == 0)
                        continue;
                    if (!TryGetRule(tags, entry, owner, out var rule) || (!rule.IsOriginalEnabled && !rule.IsOutlineEnabled && (!rule.IsFillEnabled || rule.Color.a <= 0)))
                        continue;
                    if (!EchoLodSelector.Includes(entry.Group, entry.Lods, renderer, camera))
                        continue;
                    foreach (var adapter in _adapters)
                        if (adapter.TryGetGeometry(renderer, out var mesh))
                        {
                            outputItems.Add(new EchoRenderItem(renderer, rule, mesh.subMeshCount, entry.Owner, entry.Reveal));
                            break;
                        }
                }
            }
        }
    }
}
