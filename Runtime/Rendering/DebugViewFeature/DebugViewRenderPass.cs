using Architecture;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace PitGL
{
    public class DebugViewRenderPass : ScriptableRenderPass
    {
        private string pass;
        private int passIndex;
        private Hook<Material> materialPtr;
        private Material material => materialPtr.Value;

        public DebugViewRenderPass(Hook<Material> materialPtr, string passName)
        {
            this.pass = passName;
            this.materialPtr = materialPtr;
            materialPtr.set += SetStaticMaterialProperties;
            SetStaticMaterialProperties();
        }

        public void Dispose()
        {
            materialPtr.set -= SetStaticMaterialProperties;
        }

        private void SetStaticMaterialProperties()
        {
            if (material == null) return;
            this.passIndex = material.FindPass(pass);
        }

        private class PassData
        {
            internal Material material;
        }

        private static Vector4 m_ScaleBias = new Vector4(1f, 1f, 0f, 0f);

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (material == null) return;
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

            TextureHandle srcCamColor = resourceData.activeColorTexture;

            if (resourceData.isActiveTargetBackBuffer)
                return;

            if (!srcCamColor.IsValid()) return;

            using (var builder = renderGraph.AddRasterRenderPass<PassData>(pass,
                    out var passData))
            {
                passData.material = material;
                builder.UseAllGlobalTextures(true);
                builder.SetRenderAttachment(srcCamColor, 0);
                builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecutePass(data, context, passIndex));
            }
        }

        // This static method is passed as the RenderFunc delegate to the RenderGraph render pass.
        // It is used to execute draw commands.
        private static void ExecutePass(PassData data, RasterGraphContext context, int pass)
        {
            Blitter.BlitTexture(context.cmd, m_ScaleBias, data.material, pass);
        }
    }
}
