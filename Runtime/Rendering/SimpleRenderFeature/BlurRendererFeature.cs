using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using System;
using UnityEditor;
using PitGL;

namespace PitGL
{
    public class BlurRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] private BlurSettings settings;
        [SerializeField] private Shader shader;
        private Material material;
        private BlurRenderPass blurRenderPass;

        /// <inheritdoc/>
        public override void Create()
        {
            if (shader == null)
            {
                return;
            }
            material = new Material(shader);
            blurRenderPass = new BlurRenderPass(material, settings);

            blurRenderPass.renderPassEvent = RenderPassEvent.AfterRenderingSkybox;
        }

        // Here you can inject one or multiple render passes in the renderer.
        // This method is called when setting up the renderer once per-camera.
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType == CameraType.Game)
            {
                renderer.EnqueuePass(blurRenderPass);
            }
        }

        protected override void Dispose(bool disposing)
        {
#if UNITY_EDITOR
            if (EditorApplication.isPlaying)
            {
                Destroy(material);
            }
            else
            {
                DestroyImmediate(material);
            }
#else
            Destroy(material);
#endif
        }

        


    }

    [Serializable]
    public class BlurSettings
    {
        [Range(0, 0.4f)] public float horizontalBlur;
        [Range(0, 0.4f)] public float verticalBlur;
    }
}


