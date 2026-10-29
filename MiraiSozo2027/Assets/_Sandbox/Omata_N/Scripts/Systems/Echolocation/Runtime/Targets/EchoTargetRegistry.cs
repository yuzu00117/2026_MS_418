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
            if (_queues.Count + count > 5000)
                throw new System.InvalidOperationException("エコーの元マテリアル表示は同時5000スロットまでです。");
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
        private static readonly ProfilerMarker _marker = new ProfilerMarker("Echo.Candidates");
        /// <summary>登録されたRenderer数。</summary>
        public int Count => _entries.Count;
        private static bool TryGetRule(EchoTagResolver tags, EchoTargetEntry entry, GameObject owner, out EchoTagColorRule rule)
        {
            if (entry.Tag) return tags.TryGetRule(entry.Tag, out rule);
            // 専用タグ設定済みのルールはUnityタグにはフォールバックしない。
            return tags.TryGetRule(owner.tag, out rule) && !rule.Definition;
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
            foreach (var r in _hidden) ReleaseHidden(r);
            _hidden.Clear();
            _wantedHidden.Clear();
            _releaseHidden.Clear();
            _entries.Clear();
        }

        public static void ReleaseHidden(Renderer r)
        {
            if (!_hiddenOwners.TryGetValue(r, out var state)) return;
            if (state.Count > 1) state.Count--;
            else
            {
                if (r)
                {
                    var materials = r.sharedMaterials;
                    RestoreOriginalReferences(materials, state);
                    r.sharedMaterials = materials;
                }
                _hiddenOwners.Remove(r);
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
            foreach (var e in _entries)
            {
                var r = e.Renderer;
                if (!r || !r.enabled || r.forceRenderingOff || !r.gameObject.activeInHierarchy || !camera) continue;
                if (excluded && r.transform.IsChildOf(excluded)) continue;
                if (e.Owner && (!e.Owner.isActiveAndEnabled || !e.IsOwned)) continue;
                var owner = e.Owner ? e.Owner.gameObject : r.gameObject;
                if ((layers & (1 << owner.layer)) == 0 || (camera.cullingMask & (1 << r.gameObject.layer)) == 0) continue;
                if (!TryGetRule(tags, e, owner, out var rule) || !rule.OriginalEnabled || !rule.HideOriginalOutsideEcho) continue;

                bool supported = false;
                foreach (var adapter in _adapters)
                    if (adapter.TryGetGeometry(r, out _)) { supported = true; break; }
                if (!supported) continue;
                var shader = Resources.Load<Shader>("EchoInvisible");
                if (!shader) continue;
                _wantedHidden.Add(r);
                if (_hidden.Add(r))
                {
                    if (_hiddenOwners.TryGetValue(r, out var state)) state.Count++;
                    else
                    {
                        var originals = r.sharedMaterials;
                        var invisible = CreateInvisible(shader, originals.Length);
                        _hiddenOwners.Add(r, new HiddenState { Count = 1, Originals = originals, Invisible = invisible });
                        r.sharedMaterials = invisible;
                    }
                }
                var current = r.sharedMaterials;
                var hidden = _hiddenOwners[r];
                if (!UsesInvisibleMaterials(current))
                {
                    RestoreOriginalReferences(current, hidden);
                    hidden.Originals = current;
                    DestroyInvisible(hidden);
                    hidden.Invisible = CreateInvisible(shader, current.Length);
                    r.sharedMaterials = hidden.Invisible;
                }
            }
            _releaseHidden.Clear();
            foreach (var r in _hidden) if (!_wantedHidden.Contains(r)) _releaseHidden.Add(r);
            foreach (var r in _releaseHidden) { ReleaseHidden(r); _hidden.Remove(r); }
        }
        /// <summary>シーンロードまたは明示的な再収集時だけ全検索する。</summary>
        public void Refresh(bool isAuto)
        {
            _entries.Clear();
            var foundRenderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include);
            System.Array.Sort(foundRenderers, (a, b) => string.CompareOrdinal(a.GetEntityId().ToString(), b.GetEntityId().ToString()));
            foreach (var r in foundRenderers)
            {
                if (!r.gameObject.scene.IsValid() || r.GetComponent<EchoRevealProxy>())
                    continue;
                var owner = r.GetComponentInParent<EchoTarget>(true);
                if (!isAuto && !owner)
                    continue;
                var group = r.GetComponentInParent<LODGroup>(true);
                _entries.Add(new EchoTargetEntry { Renderer = r, Owner = owner, IsOwned = !owner || owner.Owns(r), Tag = owner ? owner.GetComponent<EchoTag>() : null, Reveal = owner ? owner.GetComponent<EchoRevealState>() : null, Group = group, Lods = group ? group.GetLODs() : null });
            }
        }

        /// <summary>タグに一致し球と交差する全サブメッシュを提出する。</summary>
        public void Collect(IReadOnlyList<EchoFrameData> frames, int layers, Transform excluded, Camera camera, EchoTagResolver tags, List<EchoRenderItem> outputItems)
        {
            using (_marker.Auto())
            {
                GeometryUtility.CalculateFrustumPlanes(camera, _planes);
                foreach (EchoTargetEntry e in _entries)
                {
                    var r = e.Renderer;
                    if (!r || !r.enabled || r.forceRenderingOff || !r.gameObject.activeInHierarchy)
                        continue;
                    if (excluded && r.transform.IsChildOf(excluded))
                        continue;
                    if (e.Owner && (!e.Owner.isActiveAndEnabled || !e.IsOwned))
                        continue;
                    GameObject owner = e.Owner ? e.Owner.gameObject : r.gameObject;
                    if ((layers & (1 << owner.layer)) == 0 || (camera.cullingMask & (1 << r.gameObject.layer)) == 0)
                        continue;
                    if (!TryGetRule(tags, e, owner, out var rule) || (!rule.OriginalEnabled && !rule.OutlineEnabled && !rule.Glitch.IsEnabled && (!rule.FillEnabled || rule.Color.a <= 0)))
                        continue;
                    bool isIntersects = false;
                    for (int i = 0; i < frames.Count; i++)
                        if (EchoVolumeMath.Intersects(r.bounds, frames[i].Origin, frames[i].OuterRadius))
                        {
                            isIntersects = true;
                            break;
                        }

                    // 全体表示は画面外の接触も残存時間へ反映する。
                    if (!rule.OriginalEnabled && !rule.OutlineEnabled && !rule.Glitch.IsEnabled && (!isIntersects || !GeometryUtility.TestPlanesAABB(_planes, r.bounds)))
                        continue;
                    if (!EchoLodSelector.Includes(e.Group, e.Lods, r, camera))
                        continue;
                    foreach (var adapter in _adapters)
                        if (adapter.TryGetGeometry(r, out var mesh))
                        {
                            outputItems.Add(new EchoRenderItem(r, rule, mesh.subMeshCount, e.Owner, e.Reveal));
                            break;
                        }
                }
            }
        }
    }
}
