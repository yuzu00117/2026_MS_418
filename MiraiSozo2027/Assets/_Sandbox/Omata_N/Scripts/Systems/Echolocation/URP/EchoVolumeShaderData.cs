// CPUの全領域をGPUへまとめて渡す。重なった対象を波数ぶん再描画しない。
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Echo.Echolocation
{
    /// <summary>Render Graphパスごとに再利用する固定長の波データ。</summary>
    public sealed class EchoVolumeShaderData
    {
        private readonly Vector4[] _origins = new Vector4[EchoPulseTimeline.MaxPulses];
        private readonly Vector4[] _forwards = new Vector4[EchoPulseTimeline.MaxPulses];
        private readonly Vector4[] _volumes = new Vector4[EchoPulseTimeline.MaxPulses];
        /// <summary>境界線の描画に使う同一フレームの固定値。</summary>
        public EchoFrameData[] Frames { get; } = new EchoFrameData[EchoPulseTimeline.MaxPulses];
        /// <summary>当フレームで有効な波数。</summary>
        public int Count { get; private set; }
        public TextureHandle Shadows { get; set; }

        private int _shadowsEnabled;
        private float _bias;
        private readonly Vector4[] _slots = new Vector4[EchoPulseTimeline.MaxPulses];
        private readonly Matrix4x4[] _shadowMatrices = new Matrix4x4[EchoPulseTimeline.MaxPulses * 6];
        /// <summary>実際の描画までにControllerが更新されても変わらないようコピーする。</summary>
        public void Capture(EchoController controller)
        {
            var frames = controller.Frames;
            _shadowsEnabled = controller.ShadowMaps.IsReady ? 1 : 0;
            _bias = controller.Settings.Shadow.Bias;
            System.Array.Copy(controller.ShadowMaps.Slots, _slots, _slots.Length);
            System.Array.Copy(controller.ShadowMaps.Matrices, _shadowMatrices, _shadowMatrices.Length);
            Count = Mathf.Min(frames.Count, EchoPulseTimeline.MaxPulses);
            for (int i = 0; i < Count; i++)
            {
                var f = frames[i];
                Frames[i] = f;
                _origins[i] = new Vector4(f.Origin.x, f.Origin.y, f.Origin.z, f.Angle);
                _forwards[i] = new Vector4(f.Forward.x, f.Forward.y, f.Forward.z, Mathf.Cos(f.Angle * .5f * Mathf.Deg2Rad));
                _volumes[i] = new Vector4(f.OuterRadius, f.EraseRadius, f.State == EchoState.Hiding ? 1 : 0, f.State == EchoState.Revealing ? 1 : 0);
            }
        }

        /// <summary>タグ強調と範囲表示へ同じ包含条件を渡す。</summary>
        public void Bind(RasterCommandBuffer cmd)
        {
            cmd.SetGlobalInteger("_EchoPulseCount", Count);
            cmd.SetGlobalInteger("_EchoShadowsEnabled", _shadowsEnabled);
            cmd.SetGlobalFloat("_EchoShadowBias", _bias);
            if (Shadows.IsValid())
                cmd.SetGlobalTexture("_EchoShadowMaps", Shadows);
            if (_shadowsEnabled != 0)
            {
                cmd.SetGlobalVectorArray("_EchoShadowSlots", _slots);
                cmd.SetGlobalMatrixArray("_EchoShadowMatrices", _shadowMatrices);
            }

            cmd.SetGlobalVectorArray("_EchoOrigins", _origins);
            cmd.SetGlobalVectorArray("_EchoForwards", _forwards);
            cmd.SetGlobalVectorArray("_EchoVolumes", _volumes);
        }
    }
}
