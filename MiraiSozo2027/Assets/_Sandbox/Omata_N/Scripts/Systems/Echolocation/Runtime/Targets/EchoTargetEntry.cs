// シーンの候補をキャッシュし、描画直前に有効状態・タグ・範囲を判定する。Colliderには依存しない。
using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling;

namespace Echo.Echolocation
{
    internal struct EchoTargetEntry
    {
        public Renderer Renderer { get; set; }
        public EchoTarget Owner { get; set; }
        public LODGroup Group { get; set; }
        public LOD[] Lods { get; set; }
    }
}
