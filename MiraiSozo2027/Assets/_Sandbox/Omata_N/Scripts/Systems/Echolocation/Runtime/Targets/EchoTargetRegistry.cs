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
        private static readonly ProfilerMarker _marker = new ProfilerMarker("Echo.Candidates");
        /// <summary>登録されたRenderer数。</summary>
        public int Count => _entries.Count;

        /// <summary>専用形状取得を追加する。再収集前に登録する。</summary>
        public void AddAdapter(IEchoGeometryAdapter adapter)
        {
            if (adapter != null)
                _adapters.Insert(0, adapter);
        }

        /// <summary>全参照を解放する。</summary>
        public void Clear() => _entries.Clear();
        /// <summary>シーンロードまたは明示的な再収集時だけ全検索する。</summary>
        public void Refresh(bool isAuto)
        {
            _entries.Clear();
            var foundRenderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include);
            System.Array.Sort(foundRenderers, (a, b) => string.CompareOrdinal(a.GetEntityId().ToString(), b.GetEntityId().ToString()));
            foreach (var r in foundRenderers)
            {
                if (!r.gameObject.scene.IsValid())
                    continue;
                var owner = r.GetComponentInParent<EchoTarget>(true);
                if (!isAuto && !owner)
                    continue;
                var group = r.GetComponentInParent<LODGroup>(true);
                _entries.Add(new EchoTargetEntry { Renderer = r, Owner = owner, Group = group, Lods = group ? group.GetLODs() : null });
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
                    if (e.Owner && !e.Owner.Owns(r))
                        continue;
                    GameObject owner = e.Owner ? e.Owner.gameObject : r.gameObject;
                    if ((layers & (1 << owner.layer)) == 0 || (camera.cullingMask & (1 << r.gameObject.layer)) == 0)
                        continue;
                    if (!tags.TryGet(owner.tag, out var color) || color.a <= 0)
                        continue;
                    bool isIntersects = false;
                    for (int i = 0; i < frames.Count; i++)
                        if (EchoVolumeMath.Intersects(r.bounds, frames[i].Origin, frames[i].OuterRadius))
                        {
                            isIntersects = true;
                            break;
                        }

                    if (!isIntersects || !GeometryUtility.TestPlanesAABB(_planes, r.bounds))
                        continue;
                    if (!EchoLodSelector.Includes(e.Group, e.Lods, r, camera))
                        continue;
                    foreach (var adapter in _adapters)
                        if (adapter.TryGetGeometry(r, out var mesh))
                        {
                            outputItems.Add(new EchoRenderItem(r, color, mesh.subMeshCount));
                            break;
                        }
                }
            }
        }
    }
}
