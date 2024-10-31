using UnityEngine;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;
using System;

namespace PitGL
{
    public class BlurRenderPass : ScriptableRenderPass
    {
        private BlurSettings defaultSettings;
        private Material material;
        private RenderTextureDescriptor blurTextureDescriptor;

        private static readonly int horizontalBlurId = Shader.PropertyToID("_HorizontalBlur");
        private static readonly int verticalBlurId = Shader.PropertyToID("_VerticalBlur");
        private const string k_BlurTextureName = "_BlurTexture";
        private const string k_VerticalPassName = "VerticalBlurRenderPass";
        private const string k_HorizontalPassName = "HorizontalBlurRenderPass";

        private static Vector4 m_ScaleBias = new Vector4(1f, 1f, 0f, 0f);

        public BlurRenderPass(Material material, BlurSettings defaultSettings)
        {
            this.material = material;
            this.defaultSettings = defaultSettings;

            blurTextureDescriptor = new RenderTextureDescriptor(Screen.width, Screen.height, RenderTextureFormat.Default, 0);
        }

        // This class stores the data needed by the RenderGraph pass.
        // It is passed as a parameter to the delegate function that executes the RenderGraph pass.
        private class PassData
        {
            internal TextureHandle src;
            internal Material material;
        }

        // This static method is passed as the RenderFunc delegate to the RenderGraph render pass.
        // It is used to execute draw commands.
        private static void ExecutePass(PassData data, RasterGraphContext context, int pass)
        {
            Blitter.BlitTexture(context.cmd, data.src, m_ScaleBias, data.material, pass);
        }
        
        // RecordRenderGraph is where the RenderGraph handle can be accessed, through which render passes can be added to the graph.
        // FrameData is a context container through which URP resources can be accessed and managed.
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

            TextureHandle srcCamColor = resourceData.activeColorTexture;
            TextureHandle dst = UniversalRenderer.CreateRenderGraphTexture(renderGraph, blurTextureDescriptor, k_BlurTextureName, false);

            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

            // The following line ensures that the render pass doesn't blit
            // from the back buffer.
            if (resourceData.isActiveTargetBackBuffer)
                return;

            // Set the blur texture size to be the same as the camera target size.
            blurTextureDescriptor.width = cameraData.cameraTargetDescriptor.width;
            blurTextureDescriptor.height = cameraData.cameraTargetDescriptor.height;
            blurTextureDescriptor.depthBufferBits = 0;

            // Update the blur settings in the material
            if (!UpdateBlurSettings())
                return;

            // This check is to avoid an error from the material preview in the scene
            if (!srcCamColor.IsValid() || !dst.IsValid())
                return;


            // Vertical blur pass
            using (var builder = renderGraph.AddRasterRenderPass<PassData>(k_VerticalPassName,
                    out var passData))
            {
                // Configure pass data
                passData.src = srcCamColor;
                passData.material = material;

                // Configure render graph input and output
                builder.UseTexture(passData.src);
                builder.SetRenderAttachment(dst, 0);

                // Blit from the camera color to the render graph texture,
                // using the first shader pass.
                builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecutePass(data, context, 0));
            }

            // Horizontal blur pass
            using (var builder = renderGraph.AddRasterRenderPass<PassData>(k_HorizontalPassName, out var passData))
            {
                // Configure pass data
                passData.src = dst;
                passData.material = material;

                // Use the output of the previous pass as the input
                builder.UseTexture(passData.src);

                // Use the input texture of the previous pass as the output
                builder.SetRenderAttachment(srcCamColor, 0);

                // Blit from the render graph texture to the camera color,
                // using the second shader pass.
                builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecutePass(data, context, 1));
            }
        }

        private bool UpdateBlurSettings()
        {
            if (material == null) return false;

            // Use the Volume settings or the default settings if no Volume is set.
            var volumeComponent =
                VolumeManager.instance.stack.GetComponent<CustomVolumeComponent>();
            float horizontalBlur = volumeComponent.horizontalBlur.overrideState ?
                volumeComponent.horizontalBlur.value : defaultSettings.horizontalBlur;
            float verticalBlur = volumeComponent.verticalBlur.overrideState ?
                volumeComponent.verticalBlur.value : defaultSettings.verticalBlur;
            material.SetFloat(horizontalBlurId, horizontalBlur);
            material.SetFloat(verticalBlurId, verticalBlur);

            return volumeComponent.isActive.value;
        }
    }
}