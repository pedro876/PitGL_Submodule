using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.Rendering;
using PitGL;
using Architecture;

namespace PitGL.IMGUI
{
    public class IMGUI_SubMenu_Graphics : IMGUI_SubMenu
    {
        public override Type GetParentSubMenu() => typeof(IMGUI_SubMenu_Main);

        protected override IEnumerable<IMGUI_Element> EnumerateElements(IntWrapper indent)
        {
            yield return new IMGUI_ElementFolder<IMGUI_SubMenu_Framerate>("Framerate");
            yield return new IMGUI_ElementFolder<IMGUI_SubMenu_DebugView>("Debug View");
            yield return new IMGUI_ElementFolder<IMGUI_SubMenu_AmbientOcclusion>("Ambient Occlusion");
            yield return new IMGUI_ElementEnum<AnisotropicFiltering>("Anisotropic Filtering",
                ()=> QualitySettings.anisotropicFiltering, (aniso) => QualitySettings.anisotropicFiltering = aniso);
        }
    }

    public class IMGUI_SubMenu_Framerate : IMGUI_SubMenu
    {
        public override Type GetParentSubMenu() => typeof(IMGUI_SubMenu_Graphics);

        protected override IEnumerable<IMGUI_Element> EnumerateElements(IntWrapper indent)
        {
            yield return new IMGUI_ElementSliderInt("VSync", () => QualitySettings.vSyncCount, (value) => QualitySettings.vSyncCount = value, min: 0, max: 4);
            yield return new IMGUI_ElementEnum<PossibleFramerates>("Target Framerate", () => (PossibleFramerates)Application.targetFrameRate, (value) => Application.targetFrameRate = (int)value);
            indent++;
            yield return new IMGUI_ElementButton("Unlock framerate", () => Application.targetFrameRate = -1).SetInteractableFunc(() => Application.targetFrameRate >= 0);
            indent--;
            yield return new IMGUI_ElementSliderInt("Max Queued Frames", () => QualitySettings.maxQueuedFrames, (value) => QualitySettings.maxQueuedFrames = value, min: 1, max: 3);
        }

        enum PossibleFramerates
        {
            Unlocked = -1,
            x30 = 30,
            x40 = 40,
            x60 = 60,
            x120 = 120,
            x144 = 144,
            x165 = 165,
            x240 = 240
        }
    }

    public class IMGUI_SubMenu_DebugView : IMGUI_SubMenu
    {
        public override Type GetParentSubMenu() => typeof(IMGUI_SubMenu_Graphics);

        protected override IEnumerable<IMGUI_Element> EnumerateElements(IntWrapper indent)
        {
            yield return new IMGUI_ElementToggle("View Depth", () => DebugViewRendererFeature.s_viewDepth, () => DebugViewRendererFeature.s_viewDepth = !DebugViewRendererFeature.s_viewDepth);
            yield return new IMGUI_ElementToggle("View Normals", () => DebugViewRendererFeature.s_viewNormals, () => DebugViewRendererFeature.s_viewNormals = !DebugViewRendererFeature.s_viewNormals);
            yield return new IMGUI_ElementToggle("View Opaque", () => DebugViewRendererFeature.s_viewOpaque, () => DebugViewRendererFeature.s_viewOpaque = !DebugViewRendererFeature.s_viewOpaque);
            yield return new IMGUI_ElementToggle("View Motion", () => DebugViewRendererFeature.s_viewMotion, () => DebugViewRendererFeature.s_viewMotion = !DebugViewRendererFeature.s_viewMotion);
            yield return new IMGUI_ElementEnum<AORendererFeature.DebugView>("View AO", () => AORendererFeature.ViewResults, (value) => AORendererFeature.ViewResults = value);
        }

        protected override void OnDestroy()
        {
            DebugViewRendererFeature.s_viewDepth = false;
            DebugViewRendererFeature.s_viewNormals = false;
            DebugViewRendererFeature.s_viewOpaque = false;
            DebugViewRendererFeature.s_viewMotion = false;
        }
    }

    public class IMGUI_SubMenu_AmbientOcclusion : IMGUI_SubMenu
    {
        private AORendererFeature.DebugView ogDebugView;

        public IMGUI_SubMenu_AmbientOcclusion() : base("Ambient Occlusion")
        {
            ogDebugView = AORendererFeature.ViewResults;
        }

        public override Type GetParentSubMenu() => typeof(IMGUI_SubMenu_Graphics);
        AOQualityLevel config => AORendererFeature.QualityLevel;
        AOStaticConfiguration staticConfig => AORendererFeature.StaticConfiguration;
        AOLook look => AORendererFeature.Look;
        void Apply() => AORendererFeature.ApplyConfigurationChanges();

        protected override IEnumerable<IMGUI_Element> EnumerateElements(IntWrapper indent)
        {
            yield return new IMGUI_ElementToggle("Enabled", () => AORendererFeature.Enabled, () => AORendererFeature.Enabled = !AORendererFeature.Enabled);
            yield return new IMGUI_ElementEnum<AORendererFeature.DebugView>("View AO", () => AORendererFeature.ViewResults, (value) => { AORendererFeature.ViewResults = value; Apply(); });
            //yield return new IMGUI_ElementToggle("After Opaques", () => look.applyAfterOpaques, () => { look.applyAfterOpaques = !look.applyAfterOpaques; Apply(); });
            yield return new IMGUI_ElementSliderFloat("Render Scale", () => config.renderScale, (value) => { config.renderScale = value; Apply(); }, min: 0.25f, max: 1f, step: 0.05f);
            yield return new IMGUI_ElementEnum<VectorDistribution.SampleCount>("Spp (Samples per pixel)", () => config.samplesPerPixel, (value) => { config.samplesPerPixel = value; Apply(); });
            yield return new IMGUI_ElementEnum<AOStaticConfiguration.NoiseTechnique>("Noise Technique", () => staticConfig.noiseTechnique, (value) => { staticConfig.noiseTechnique = value; Apply(); });
            yield return new IMGUI_ElementSliderInt("Denoise Radius", () => config.denoiseRadius, (value) => { config.denoiseRadius = value; Apply(); }, min: 0, max: 4);
            yield return new IMGUI_ElementToggle("Upscale", () => config.upscale, () => config.upscale = !config.upscale);
            yield return new IMGUI_ElementToggle("TAA", () => config.temporalAntialiasing, () => { config.temporalAntialiasing = !config.temporalAntialiasing; Apply(); });
            indent++;
            yield return new IMGUI_ElementToggle("World Space Rejection", () => staticConfig.worldSpaceRejection, () => { staticConfig.worldSpaceRejection = !staticConfig.worldSpaceRejection; Apply(); }).SetInteractableFunc(() => config.temporalAntialiasing); ;
            yield return new IMGUI_ElementSliderFloat("Temporal accumulation", () => staticConfig.temporalMaxDistance, (value) => { staticConfig.temporalMaxDistance = value; Apply(); }, min: 0.01f, max: 0.2f, step: 0.01f).SetInteractableFunc(() => config.temporalAntialiasing);
            yield return new IMGUI_ElementToggle("Neighbourhood Clamping", () => staticConfig.neighbourhoodClamping, () => { staticConfig.neighbourhoodClamping = !staticConfig.neighbourhoodClamping; Apply(); }).SetInteractableFunc(() => config.temporalAntialiasing); ;
            yield return new IMGUI_ElementSliderFloat("Temporal accumulation", () => config.temporalAccumulation, (value) => { config.temporalAccumulation = value; Apply(); }, min: 0.01f, max: 1f, step: 0.01f).SetInteractableFunc(() => config.temporalAntialiasing);
            yield return new IMGUI_ElementSliderFloat("Temporal variance", () => staticConfig.temporalVariance, (value) => { staticConfig.temporalVariance = value; Apply(); }, min: 0.0f, max: 10f, step: 0.1f).SetInteractableFunc(() => config.temporalAntialiasing);
            indent--;
        }

        protected override void OnDestroy()
        {
            AORendererFeature.ViewResults = ogDebugView;
        }
    }
}
