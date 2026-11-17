using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Aura.Water
{
    /// <summary>
    /// 泡の距離の地図 1 枚分(範囲・画像・作り方)を受け持つ。
    /// WaterFoamDistanceMap が、ステージ全体の「遠い地図」とカメラ周りの「近い地図」の 2 つを持ち、
    /// それぞれにこのクラスを使う。
    /// </summary>
    internal sealed class WaterFoamDistanceMapLayer
    {
        // 計算シェーダー(WaterFoamJumpFlood.compute)の処理名。シェーダー側の #pragma kernel と合わせる
        private const string InitKernelName = "Init";
        private const string FloodKernelName = "Flood";
        private const string ResolveKernelName = "Resolve";
        internal static readonly string[] JumpFloodKernelNames = { InitKernelName, FloodKernelName, ResolveKernelName };

        private const int ThreadGroupSize = 8;
        // 深さには「水面からの高さ / 調べる高さの上限」を入れる。
        // 上限 500m でも 16bit なら 約 8mm 刻みで、水面を輪切りにする判定には十分なため
        private const int MaskDepthBits = 16;
        private const int NoDepthBits = 0;
        private const float HalfRatio = 0.5f;

        // 種には画素の座標(0〜4095 の整数)を入れるので、整数 2 つ組で足りる
        internal const GraphicsFormat SeedFormat = GraphicsFormat.R16G16_UInt;
        private const GraphicsFormat MaskFormat = GraphicsFormat.R8_UNorm;
        private const GraphicsFormat DistanceFormat = GraphicsFormat.R16_SFloat;

        private static readonly int MaskAreaId = Shader.PropertyToID("_FoamMaskArea");
        private static readonly int MaskHeightId = Shader.PropertyToID("_FoamMaskHeight");
        private static readonly int MaskTextureId = Shader.PropertyToID("_Mask");
        private static readonly int SeedReadId = Shader.PropertyToID("_SeedRead");
        private static readonly int SeedWriteId = Shader.PropertyToID("_SeedWrite");
        private static readonly int DistanceId = Shader.PropertyToID("_Distance");
        private static readonly int ResolutionId = Shader.PropertyToID("_Resolution");
        private static readonly int StepId = Shader.PropertyToID("_Step");
        private static readonly int MetersPerPixelId = Shader.PropertyToID("_MetersPerPixel");

        private RenderTexture _mask;
        private RenderTexture _seedA;
        private RenderTexture _seedB;
        private RenderTexture _distance;
        private int _resolution;
        private float _areaSize = 1f;
        private Vector2 _areaMin;

        /// <summary>出来上がった距離の画像。水面シェーダーが読む。</summary>
        internal RenderTexture Distance => _distance;

        /// <summary>x, y: 範囲の最小 XZ、z: 1 / 範囲の一辺、w: 1(地図あり)。</summary>
        internal Vector4 AreaVector => new Vector4(_areaMin.x, _areaMin.y, 1f / _areaSize, 1f);

        /// <summary>
        /// 画像を用意する。すでに同じ大きさのものがあれば作り直さず使い回す(近い地図は何度も作り直すため)。
        /// 作れなかったときは false。
        /// </summary>
        internal bool Allocate(int resolution, float areaSize)
        {
            if (_resolution != resolution)
            {
                Release();
            }
            _resolution = resolution;
            _areaSize = areaSize;

            // 距離の画像だけは「真上から見た距離」を貼るので、なめらかにつなぐ(水面からは画素の間も読むため)
            _distance = EnsureTexture(_distance, "WaterFoamDistance", DistanceFormat, NoDepthBits, true, FilterMode.Bilinear);
            _mask = EnsureTexture(_mask, "WaterFoamMask", MaskFormat, MaskDepthBits, false, FilterMode.Point);
            _seedA = EnsureTexture(_seedA, "WaterFoamSeedA", SeedFormat, NoDepthBits, true, FilterMode.Point);
            _seedB = EnsureTexture(_seedB, "WaterFoamSeedB", SeedFormat, NoDepthBits, true, FilterMode.Point);

            if (_distance == null || _mask == null || _seedA == null || _seedB == null)
            {
                Release();
                return false;
            }
            return true;
        }

        /// <summary>範囲の中心(XZ)を決める。</summary>
        internal void SetAreaCenter(Vector2 centerXZ)
        {
            _areaMin = centerXZ - Vector2.one * (_areaSize * HalfRatio);
        }

        /// <summary>
        /// 物体の絵を描き、距離を求める命令を並べる。命令は並べたあとすぐ実行すること
        /// (マスク用の材質の値を、この地図の範囲で上書きするため)。
        /// </summary>
        internal void Record(
            CommandBuffer cmd,
            Material maskMaterial,
            ComputeShader jumpFloodShader,
            IReadOnlyList<MeshRenderer> targets,
            float waterY,
            float maxHeight,
            float maxFoamRange)
        {
            maskMaterial.SetVector(MaskAreaId, new Vector4(_areaMin.x, _areaMin.y, 1f / _areaSize, 0f));
            maskMaterial.SetVector(MaskHeightId, new Vector4(waterY, 1f / maxHeight, 0f, 0f));
            RecordMaskPass(cmd, maskMaterial, targets);
            RecordJumpFloodPass(cmd, jumpFloodShader, maxFoamRange);
        }

        /// <summary>マスクと計算途中の画像を解放する。距離の画像は残す。</summary>
        internal void ReleaseWorkTextures()
        {
            DestroyResource(_mask);
            DestroyResource(_seedA);
            DestroyResource(_seedB);
            _mask = null;
            _seedA = null;
            _seedB = null;
        }

        /// <summary>すべての画像を解放する。</summary>
        internal void Release()
        {
            ReleaseWorkTextures();
            DestroyResource(_distance);
            _distance = null;
        }

        internal static void DestroyResource(Object target)
        {
            if (target == null)
            {
                return;
            }
            if (target is RenderTexture renderTexture)
            {
                renderTexture.Release();
            }
            if (Application.isPlaying)
            {
                Object.Destroy(target);
            }
            else
            {
                Object.DestroyImmediate(target);
            }
        }

        private RenderTexture EnsureTexture(
            RenderTexture existing, string textureName, GraphicsFormat colorFormat, int depthBits, bool enableRandomWrite, FilterMode filterMode)
        {
            if (existing != null)
            {
                return existing;
            }

            RenderTexture texture = CreateRenderTexture(textureName, _resolution, colorFormat, depthBits, enableRandomWrite, filterMode);
            // 計算シェーダーに書き込ませる画像は、使う前に GPU 側に作っておく必要がある。
            // 作らずに渡すと、エラーも出ずに書き込みが飛ばされ、地図が 0m のまま(範囲全体が泡)になる
            if (!texture.Create())
            {
                DestroyResource(texture);
                return null;
            }
            return texture;
        }

        private static RenderTexture CreateRenderTexture(
            string textureName, int resolution, GraphicsFormat colorFormat, int depthBits, bool enableRandomWrite, FilterMode filterMode)
        {
            var descriptor = new RenderTextureDescriptor(resolution, resolution, colorFormat, depthBits)
            {
                // 計算シェーダーから書き込む画像だけ有効にする。GPU 側に作る前に決める必要がある
                enableRandomWrite = enableRandomWrite,
            };
            return new RenderTexture(descriptor)
            {
                name = textureName,
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = filterMode,
                wrapMode = TextureWrapMode.Clamp,
            };
        }

        /// <summary>対象の物体を水面の高さで輪切りにして、「物体の中」の絵を描く命令を並べる。</summary>
        private void RecordMaskPass(CommandBuffer cmd, Material maskMaterial, IReadOnlyList<MeshRenderer> targets)
        {
            cmd.SetRenderTarget(_mask);
            cmd.ClearRenderTarget(true, true, Color.clear);

            Vector2 areaMax = _areaMin + Vector2.one * _areaSize;
            foreach (MeshRenderer meshRenderer in targets)
            {
                // 地図を作ったあとにシーンから消された物体は飛ばす
                if (meshRenderer == null || !meshRenderer.TryGetComponent(out MeshFilter meshFilter) || meshFilter.sharedMesh == null)
                {
                    continue;
                }
                if (!OverlapsArea(meshRenderer.bounds, _areaMin, areaMax))
                {
                    continue;
                }

                Mesh mesh = meshFilter.sharedMesh;
                Matrix4x4 matrix = meshRenderer.localToWorldMatrix;
                for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                {
                    cmd.DrawMesh(mesh, matrix, maskMaterial, submesh);
                }
            }
        }

        private static bool OverlapsArea(Bounds bounds, Vector2 areaMin, Vector2 areaMax)
        {
            return bounds.max.x >= areaMin.x && bounds.min.x <= areaMax.x &&
                   bounds.max.z >= areaMin.y && bounds.min.z <= areaMax.y;
        }

        /// <summary>
        /// 距離を求める命令を並べる。
        /// 段階ごとに「飛ばす幅」を半分ずつ小さくし、最後に幅 1 をもう一度行って精度を上げる。
        /// 泡や光の模様に必要なのは数 m 先までなので、最初の幅は「その範囲が収まる幅」から始めて、段階の数を減らす。
        /// </summary>
        private void RecordJumpFloodPass(CommandBuffer cmd, ComputeShader shader, float maxFoamRange)
        {
            int initKernel = shader.FindKernel(InitKernelName);
            int floodKernel = shader.FindKernel(FloodKernelName);
            int resolveKernel = shader.FindKernel(ResolveKernelName);
            int groupCount = Mathf.CeilToInt(_resolution / (float)ThreadGroupSize);
            float metersPerPixel = _areaSize / _resolution;

            cmd.SetComputeIntParam(shader, ResolutionId, _resolution);
            cmd.SetComputeFloatParam(shader, MetersPerPixelId, metersPerPixel);

            cmd.SetComputeTextureParam(shader, initKernel, MaskTextureId, _mask);
            cmd.SetComputeTextureParam(shader, initKernel, SeedWriteId, _seedA);
            cmd.DispatchCompute(shader, initKernel, groupCount, groupCount, 1);

            int startStep = Mathf.Clamp(
                Mathf.NextPowerOfTwo(Mathf.CeilToInt(maxFoamRange / metersPerPixel)), 1, _resolution / 2);
            RenderTexture read = _seedA;
            RenderTexture write = _seedB;
            for (int step = startStep; step >= 1; step /= 2)
            {
                RecordFloodStep(cmd, shader, floodKernel, read, write, step, groupCount);
                (read, write) = (write, read);
            }
            RecordFloodStep(cmd, shader, floodKernel, read, write, 1, groupCount);
            (read, write) = (write, read);

            cmd.SetComputeTextureParam(shader, resolveKernel, SeedReadId, read);
            cmd.SetComputeTextureParam(shader, resolveKernel, DistanceId, _distance);
            cmd.DispatchCompute(shader, resolveKernel, groupCount, groupCount, 1);
        }

        private static void RecordFloodStep(
            CommandBuffer cmd, ComputeShader shader, int kernel, RenderTexture read, RenderTexture write, int step, int groupCount)
        {
            cmd.SetComputeIntParam(shader, StepId, step);
            cmd.SetComputeTextureParam(shader, kernel, SeedReadId, read);
            cmd.SetComputeTextureParam(shader, kernel, SeedWriteId, write);
            cmd.DispatchCompute(shader, kernel, groupCount, groupCount, 1);
        }
    }
}
