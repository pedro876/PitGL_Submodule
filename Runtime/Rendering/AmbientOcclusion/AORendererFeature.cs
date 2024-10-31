using Architecture;
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace PitGL
{
    public class AORendererFeature : ScriptableRendererFeature
    {
        private const string globalStringKey = "AO_Configuration";
        private static AORendererFeature instance;
        public static AOStaticConfiguration StaticConfiguration { get { return instance.staticConfiguration; } set { instance.staticConfiguration = value; } }
        public static AOQualityLevel QualityLevel { get { return instance.qualityLevel; } set { instance.qualityLevel = value; } }
        public static AOLook Look { get { return instance.look; } set { instance.look = value; } }
        public static bool Enabled { get => instance != null && instance.enabled; set { if(instance != null) instance.enabled = value; } }
        public static DebugView ViewResults { get { return instance == null ? DebugView.Off : instance.viewResults; } set { if(instance != null) instance.viewResults = value; } }

        



        public static void ApplyConfigurationChanges()
        {
            instance.Dispose();
            instance.Create();
        }

        [SerializeField, ForceAsset("Runtime/Rendering/AmbientOcclusion/AOMaterial.mat")] Material material;
        [SerializeField] bool enabled = true;
        [SerializeField] bool isRendererDeferred = false;
        [SerializeField] DebugView viewResults = DebugView.Off;

        public enum DebugView { Off, Occlusion, Normals }
        
        [SerializeField] public AOLook look;
        [SerializeField] public AOQualityLevel qualityLevel;
        [SerializeField] public AOStaticConfiguration staticConfiguration;

        private AORenderPass aoPass;

        public override void Create()
        {
            instance = this;
            if (!this.isActive) return;
            if (material == null) return;

            aoPass = new AORenderPass(material, look, qualityLevel, staticConfiguration);
            ScriptableRenderPassInput requiredInput = ScriptableRenderPassInput.Depth;
            if (staticConfiguration.useCameraNormalsTexture)
            {
                requiredInput |= ScriptableRenderPassInput.Normal;
            }
            aoPass.ConfigureInput(requiredInput);

            RenderPassEvent renderPassEvent;
            if (ViewResults != DebugView.Off) renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
            else if (isRendererDeferred)
            {
                renderPassEvent = look.applyAfterOpaques ? RenderPassEvent.AfterRenderingOpaques : RenderPassEvent.AfterRenderingGbuffer;
            }
            else
            {
                // Rendering after PrePasses is usually correct except when depth priming is in play:
                // then we rely on a depth resolve taking place after the PrePasses in order to have it ready for SSAO.
                // Hence we set the event to RenderPassEvent.AfterRenderingPrePasses + 1 at the earliest.
                renderPassEvent = look.applyAfterOpaques ? RenderPassEvent.BeforeRenderingTransparents : RenderPassEvent.AfterRenderingPrePasses + 1;
            }

            renderPassEvent += staticConfiguration.renderOrder;
            aoPass.renderPassEvent = renderPassEvent;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!enabled) return;
            if (material == null) return;
            if (aoPass == null) return;

            GlobalString.Set(globalStringKey, $"{(int)qualityLevel.samplesPerPixel}spp"
                + $" | TAA {(qualityLevel.temporalAntialiasing ? "On" : "Off")}"
                //+ $" | Denoise x{configuration.denoiseRadius}"
                //+ $" | Scale x{configuration.renderScale.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}");
                );

             //bool isRendererDeferred => m_Renderer != null
             //                              && m_Renderer is UniversalRenderer
             //                              && ((UniversalRenderer)m_Renderer).renderingModeActual == RenderingMode.Deferred;

            

            var cameraType = renderingData.cameraData.cameraType;
            if(cameraType == CameraType.SceneView || cameraType == CameraType.Game)
            {
                renderer.EnqueuePass(aoPass);
            }
        }

        protected override void Dispose(bool disposing)
        {
            aoPass?.Dispose();

//#if UNITY_EDITOR
//            if (EditorApplication.isPlaying)
//            {
//                Destroy(material);
//            }
//            else
//            {
//                DestroyImmediate(material);
//            }
//#else
//            Destroy(material);
//#endif
        }
    }
}
