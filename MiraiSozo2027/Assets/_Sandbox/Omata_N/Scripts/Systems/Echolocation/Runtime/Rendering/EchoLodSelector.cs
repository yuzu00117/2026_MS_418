// LODGroupの画面相対サイズから一つのLODだけを選ぶ。クロスフェードを二重描画しない。
using UnityEngine;

namespace Echo.Echolocation
{
    /// <summary>対象カメラに対応するLOD選択。</summary>
    public static class EchoLodSelector
    {
        /// <summary>LOD一覧はRegistry構築時に保存し、毎フレームの配列確保を避ける。</summary>
        public static bool Includes(LODGroup group, LOD[] lods, Renderer renderer, Camera camera)
        {
            if (!group || !group.enabled || lods == null)
                return true;
            var scale = group.transform.lossyScale;
            float size = group.size * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            float distance = Vector3.Distance(camera.transform.position, group.transform.TransformPoint(group.localReferencePoint));
            float height = camera.orthographic ? size / (2 * camera.orthographicSize) : size / (2 * Mathf.Max(.00001f, distance) * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * .5f));
            height *= QualitySettings.lodBias;
            for (int i = 0; i < lods.Length; i++)
                if (height >= lods[i].screenRelativeTransitionHeight)
                {
                    foreach (var r in lods[i].renderers)
                        if (r == renderer)
                            return true;
                    return false;
                }

            return false;
        }
    }
}
