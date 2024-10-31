//TEMPORAL FILTERING: https://youtu.be/jraz6Dxz7GU?si=WrBChksad5m0G1zf
//GTAO: https://www.iryoku.com/downloads/Practical-Realtime-Strategies-for-Accurate-Indirect-Occlusion.pdf

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using UnityEngine.Experimental.Rendering;
using Architecture;
using System.Collections.Generic;

namespace PitGL
{
    [System.Serializable]
    public class AOLook
    {
        //public bool applyAfterOpaques = false;
        public bool applyAfterOpaques => true;
        //[Resettable(0.9f), DisableIf(nameof(applyAfterOpaques)), Range(0,1)] public float directLightingStrength = 0.9f;
        public float directLightingStrength => 0.9f;
        [Resettable(0.25f), Range(0f, 1f)] public float selfOcclude = 0.25f;
        [Resettable(0.3f, 1f), MinMax(0f, 3f)] public Vector2 radius = new Vector2(0.3f, 1f);
        [Resettable(5f), Range(1f, 90f)] public float minOcclusionAngle = 5f;
        [Resettable(0.75f), Range(0f, 1f)] public float haloRemoval = 0.75f;
        public bool colorCorrection = true;
        [EnableIf(nameof(colorCorrection)), Resettable(1.1f), Range(0f, 10f)] public float strength = 1.1f;
        [EnableIf(nameof(colorCorrection)), Resettable(2f), Range(0.01f, 10f)] public float exponent = 2f;
    }

    [System.Serializable]
    public class AOQualityLevel
    {
        public const int MAX_DENOISE_RADIUS = 4;

        [Header("QUALITY")]
        [SerializeField, Range(0.25f, 1f)] public float renderScale = 0.5f;
        [RangeEnum] public VectorDistribution.SampleCount samplesPerPixel = VectorDistribution.SampleCount.x4;
       

        [Header("DENOISING")]
        [Resettable(1), Range(0, MAX_DENOISE_RADIUS)] public int denoiseRadius = 1;
        public bool upscale = false;
        //public float upscaleSigma = 1.0f;

        public bool ShouldPerformDenoisePass => denoiseRadius > 0;

        [Header("TAA")]
        public bool temporalAntialiasing = false;

        [Tooltip("Attemps to reconstruct information at disoccluded areas")]
        [Resettable(0.10f), Range(0,1)] public float temporalAccumulation = 0.10f;
        
        public bool ShouldPerformTAA => temporalAntialiasing;
    }

    [System.Serializable]
    public class AOStaticConfiguration
    {
        [Resettable(0)] public int renderOrder = 0;

        [Header("SEED")]
        [Tooltip("This should be equal to the denoise diameter to avoid visible patterns. For example, Set this to 3 if the denoiseRadius is set to BoxBlur3x3")]
        [HideInInspector] public bool overrideSeed = false;
        [Overridable(nameof(overrideSeed))] public int seed = 11;

        [Header("NORMALS")]
        public bool useCameraNormalsTexture = true;
        public enum NormalQuality { Low, Medium, High }

        [Header("NOISE")]
        public NoiseTechnique noiseTechnique = NoiseTechnique.InterleavedGradientNoise;
        [ForceAsset("PitGL/Content/NoiseTextures/BlueNoise/32x32/LDR_RG01_Atlas_32x32.png")] public Texture2D blueNoiseAtlas;
        [Resettable(20.94f)] public float noiseDepthVariation = 20.94f;
        public const int blueNoiseAtlasRows = 8;
        public const int blueNoiseAtlasColumns = 8;
        public const int blueNoiseAtlasLength = blueNoiseAtlasRows * blueNoiseAtlasColumns;
        public int BlueNoiseWidth => blueNoiseAtlas.width / blueNoiseAtlasColumns;
        public int BlueNoiseHeight => blueNoiseAtlas.height / blueNoiseAtlasRows;
        public static Vector2 BlueNoiseTiling => new Vector2(1f / blueNoiseAtlasColumns, 1f / blueNoiseAtlasRows);


        [Header("DENOISING")]
        [Resettable(0.95f), Range(0, 1)] public float denoiseDepthSharpness = 0.95f;
        [Resettable(0f), Range(0, 1)] public float denoiseNormalSharpness = 0.0f;
        //[Resettable(0f), Range(0,10)] public float denoiseSpatialSigma = 1.0f;

        public enum NoiseTechnique { InterleavedGradientNoise, BlueNoise }

        [Header("TEMPORAL REJECTION")]
        public bool worldSpaceRejection = true;
        [EnableIf(nameof(worldSpaceRejection)), Resettable(0.05f), Range(0.01f, 0.2f)] public float temporalMaxDistance = 0.05f;
        public bool neighbourhoodClamping = true;
        [EnableIf(nameof(neighbourhoodClamping)), Resettable(1f), Range(0, 10)] public float temporalVariance = 1f;
        [EnableIf(nameof(neighbourhoodClamping)), Resettable(2f), Range(1, 4)] public float temporalVarianceBoost = 2f; //Boost the temporal variance threshold on dissocluded areas

        [Tooltip("This prevents low precision artifacts. Without this, old occlusion values don't fully disappear over time.")]
        [Resettable(0.15f), Range(0, 0.4f)] public float temporalDither = 0.15f;
    }

    public class AORenderPass : ScriptableRenderPass
    {
        private Material material;
        private ProfilingSampler scope;
        private AOLook look;
        private AOQualityLevel qualityLevel;
        private AOStaticConfiguration staticConfig;
        private VectorDistributionParams distribution;
        private static int passDownsampleDepthAndNormals;
        private static int passComputeOcclusionFactor;
        private static int passTAAResolveOcclusionFactor;
        private static int passColorCorrection;
        private static int passDenoiseOcclusionFactor;
        private static int passUpscaleOcclusionFactor;
        private static int passApplyOcclusionAfterOpaques;
        private static int passViewAO;
        private static int passViewNormals;
        private LocalKeyword DENOISE_NORMAL_AWARE;
        private LocalKeyword DOWNSAMPLED;
        private LocalKeyword NOISE_BLUE;
        private LocalKeyword ORTHOGRAPHIC;
        private LocalKeyword WORLD_SPACE_REJECTION;
        private LocalKeyword NEIGHBOURHOOD_CLAMPING;
        private LocalKeyword RECONSTRUCT_NORMALS;

        private RenderTextureDescriptor RTD_Depth;
        private RenderTextureDescriptor RTD_Normals;
        private RenderTextureDescriptor RTD_OcclusionFactor;
        private RenderTextureDescriptor RTD_OcclusionFactorUpscaled;

        private float[] poissonVectors = null;
        private static int frameCount = 0;
        private static int noiseIdx = 0;
        //private RTHandle blueNoiseAtlasHandle = null;
        private bool ortographic = false;

        private static Vector4 m_ScaleBias = new Vector4(1f, 1f, 0f, 0f);
        private static readonly int _AO_NormalsInput = Shader.PropertyToID(nameof(_AO_NormalsInput));
        private static readonly int _AO_DepthInput = Shader.PropertyToID(nameof(_AO_DepthInput));
        private static readonly int _AO_NoiseInput = Shader.PropertyToID(nameof(_AO_NoiseInput));
        private static readonly int _AO_OcclusionInput = Shader.PropertyToID(nameof(_AO_OcclusionInput));

        private static readonly int _AO_Radius = Shader.PropertyToID(nameof(_AO_Radius));
        private static readonly int _AO_SelfOcclude = Shader.PropertyToID(nameof(_AO_SelfOcclude));
        private static readonly int _AO_Strength = Shader.PropertyToID(nameof(_AO_Strength));
        private static readonly int _AO_Exponent = Shader.PropertyToID(nameof(_AO_Exponent));
        private static readonly int _AO_Edges = Shader.PropertyToID(nameof(_AO_Edges));

        private static readonly int _AO_InvNoiseDimensions = Shader.PropertyToID(nameof(_AO_InvNoiseDimensions));
        private static readonly int _AO_NoiseTiling = Shader.PropertyToID(nameof(_AO_NoiseTiling));
        private static readonly int _AO_NoiseOffset = Shader.PropertyToID(nameof(_AO_NoiseOffset));
        private static readonly int _AO_NoiseDepthMult = Shader.PropertyToID(nameof(_AO_NoiseDepthMult));
        private static readonly int _AO_HaloRemoval = Shader.PropertyToID(nameof(_AO_HaloRemoval));
        private static readonly int _AO_Sin_FovDiv2 = Shader.PropertyToID(nameof(_AO_Sin_FovDiv2));

        private static readonly int _AO_DenoiseRadius = Shader.PropertyToID(nameof(_AO_DenoiseRadius));
        private static readonly int _AO_DenoiseIsHorizontal = Shader.PropertyToID(nameof(_AO_DenoiseIsHorizontal));
        private static readonly int _AO_DenoiseDepthThreshold = Shader.PropertyToID(nameof(_AO_DenoiseDepthThreshold));
        private static readonly int _AO_DenoiseNormalSharpness = Shader.PropertyToID(nameof(_AO_DenoiseNormalSharpness));
        
        private static readonly int _AO_FrameCount = Shader.PropertyToID(nameof(_AO_FrameCount));
        private static readonly int _AO_TemporalAccumulation = Shader.PropertyToID(nameof(_AO_TemporalAccumulation));
        private static readonly int _AO_TemporalMaxDistance = Shader.PropertyToID(nameof(_AO_TemporalMaxDistance));
        private static readonly int _AO_Variance = Shader.PropertyToID(nameof(_AO_Variance));
        private static readonly int _AO_VarianceBoost = Shader.PropertyToID(nameof(_AO_VarianceBoost));
        private static readonly int _AO_TemporalDither = Shader.PropertyToID(nameof(_AO_TemporalDither));
        private static readonly int _AO_Temporal_VP = Shader.PropertyToID(nameof(_AO_Temporal_VP));
        private static readonly int _AO_Temporal_I_VP = Shader.PropertyToID(nameof(_AO_Temporal_I_VP));

        private static GlobalKeyword _SCREEN_SPACE_OCCLUSION;
        private const string k_AmbientOcclusionParamName = "_AmbientOcclusionParam";
        internal static readonly int s_AmbientOcclusionParamID = Shader.PropertyToID(k_AmbientOcclusionParamName);
        private const string k_SSAOTextureName = "_ScreenSpaceOcclusionTexture";
        private static readonly int s_SSAOFinalTextureID = Shader.PropertyToID(k_SSAOTextureName);

        public static bool ShouldPerformUpscalePass(AOQualityLevel config, AOStaticConfiguration staticConfig) => config.renderScale < 1f && config.upscale;
        public static bool NeedsDepthBuffer(AOQualityLevel config, AOStaticConfiguration staticConfig) => config.renderScale < 1f;
        public static bool NeedsNormalBuffer(AOQualityLevel config, AOStaticConfiguration staticConfig) => config.renderScale < 1f || !staticConfig.useCameraNormalsTexture;
        public static bool ShouldPerformDownsampling(AOQualityLevel config, AOStaticConfiguration staticConfig) => NeedsDepthBuffer(config, staticConfig) || NeedsNormalBuffer(config, staticConfig);

        #region INITIALIZATION & DISPOSAL

        public AORenderPass(Material material, AOLook look, AOQualityLevel qualityLevel, AOStaticConfiguration staticConfig)
        {
            this.material = material;
            this.look = look;
            this.qualityLevel = qualityLevel;
            this.staticConfig = staticConfig;
            this.scope = new ProfilingSampler("Pit_SSAO");

            distribution = VectorDistributionParams.Clone(VectorDistribution.Distribution_SSAO);
            distribution.minAngle = look.minOcclusionAngle;
            distribution.minDepth = Mathf.Lerp(0.1f, 0.2f, look.selfOcclude);
            distribution.maxAngle = Mathf.Lerp(90f, Mathf.Min(90f, distribution.minAngle + 10f), Mathf.Pow(look.selfOcclude, 0.25f));
            distribution.depthExponent = Mathf.Lerp(0.75f, 1.5f, look.selfOcclude);

            CreateVectors();
            SetStaticMaterialProperties();

            //_SCREEN_SPACE_OCCLUSION = new GlobalKeyword(nameof(_SCREEN_SPACE_OCCLUSION));
            _SCREEN_SPACE_OCCLUSION = GlobalKeyword.Create(nameof(_SCREEN_SPACE_OCCLUSION));

            RTD_Depth = new RenderTextureDescriptor(Screen.width, Screen.height, GraphicsFormat.None, 24);
            RTD_Normals = new RenderTextureDescriptor(Screen.width, Screen.height, GraphicsFormat.R8G8B8A8_SNorm, 0);
            RTD_OcclusionFactor = new RenderTextureDescriptor(Screen.width, Screen.height, GraphicsFormat.R8G8B8A8_UNorm, 0) { sRGB = false };
            RTD_OcclusionFactorUpscaled = new RenderTextureDescriptor(Screen.width, Screen.height, GraphicsFormat.R8_UNorm, 0) { sRGB = false };
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            base.OnCameraCleanup(cmd);
            if (!look.applyAfterOpaques)
                cmd.SetKeyword(_SCREEN_SPACE_OCCLUSION, false);
        }

        public void Dispose()
        {
            DisposeTemporalDatas();
            //blueNoiseAtlasHandle?.Release();
        }

        private void CreateVectors()
        {
            Vector3[] vectors;
            if (staticConfig.overrideSeed)
            {
                vectors = VectorDistributionGenerator.GenerateVectors(distribution, qualityLevel.samplesPerPixel, staticConfig.seed);
            }
            else
            {
                VectorDistribution.CreateVectors_SSAO(distribution, qualityLevel.samplesPerPixel, 0, out vectors);
            }
            VectorDistributionGenerator.CopyVectorsToFloat3Array(ref poissonVectors, vectors);
        }

        private void SetStaticMaterialProperties()
        {
            if (material == null) return;

            passDownsampleDepthAndNormals = material.FindPass(nameof(passDownsampleDepthAndNormals));
            passComputeOcclusionFactor = material.FindPass(nameof(passComputeOcclusionFactor));
            passTAAResolveOcclusionFactor = material.FindPass(nameof(passTAAResolveOcclusionFactor));
            passColorCorrection = material.FindPass(nameof(passColorCorrection));
            passDenoiseOcclusionFactor = material.FindPass(nameof(passDenoiseOcclusionFactor));
            passUpscaleOcclusionFactor = material.FindPass(nameof(passUpscaleOcclusionFactor));
            passApplyOcclusionAfterOpaques = material.FindPass(nameof(passApplyOcclusionAfterOpaques));
            passViewAO = material.FindPass(nameof(passViewAO));
            passViewNormals = material.FindPass(nameof(passViewNormals));

            VectorDistributionGenerator.SetVectorDistributionProperties(material, qualityLevel.samplesPerPixel, distribution, poissonVectors);

            material.SetVector(_AO_Radius, look.radius);
            material.SetFloat(_AO_SelfOcclude, look.selfOcclude);
            //Shader.SetGlobalFloat(_AO_Strength, look.colorCorrection ? look.strength : 1f);
            //Shader.SetGlobalFloat(_AO_Exponent, look.colorCorrection ? look.exponent : 1f);
            
            material.SetFloat(_AO_NoiseDepthMult, Mathf.Max(1f, staticConfig.noiseDepthVariation));
            material.SetFloat(_AO_HaloRemoval, look.haloRemoval);

            material.SetInt(_AO_DenoiseRadius, qualityLevel.denoiseRadius);
            material.SetFloat(_AO_DenoiseDepthThreshold, Mathf.Lerp(3f, 0.05f, staticConfig.denoiseDepthSharpness));
            material.SetFloat(_AO_DenoiseNormalSharpness, staticConfig.denoiseNormalSharpness);

            material.SetFloat(_AO_TemporalMaxDistance, staticConfig.temporalMaxDistance);
            material.SetFloat(_AO_Variance, staticConfig.temporalVariance);
            material.SetFloat(_AO_VarianceBoost, staticConfig.temporalVariance * staticConfig.temporalVarianceBoost);
            material.SetFloat(_AO_TemporalDither, qualityLevel.ShouldPerformTAA ? staticConfig.temporalDither : 0f);

            if(staticConfig.blueNoiseAtlas != null && staticConfig.noiseTechnique == AOStaticConfiguration.NoiseTechnique.BlueNoise)
            {
                material.SetTexture(_AO_NoiseInput, staticConfig.blueNoiseAtlas);
                material.SetVector(_AO_InvNoiseDimensions, new Vector2(1f / staticConfig.BlueNoiseWidth, 1f / staticConfig.BlueNoiseHeight));
                material.SetVector(_AO_NoiseTiling, AOStaticConfiguration.BlueNoiseTiling);
            }
            

            DENOISE_NORMAL_AWARE = new LocalKeyword(material.shader, nameof(DENOISE_NORMAL_AWARE));
            DOWNSAMPLED = new LocalKeyword(material.shader, nameof(DOWNSAMPLED));
            NOISE_BLUE = new LocalKeyword(material.shader, nameof(NOISE_BLUE));
            WORLD_SPACE_REJECTION = new LocalKeyword(material.shader, nameof(WORLD_SPACE_REJECTION));
            NEIGHBOURHOOD_CLAMPING = new LocalKeyword(material.shader, nameof(NEIGHBOURHOOD_CLAMPING));
            ORTHOGRAPHIC = new LocalKeyword(material.shader, nameof(ORTHOGRAPHIC));
            RECONSTRUCT_NORMALS = new LocalKeyword(material.shader, nameof(RECONSTRUCT_NORMALS));
            

            material.SetKeyword(in DENOISE_NORMAL_AWARE, staticConfig.denoiseNormalSharpness > 0f);
            material.SetKeyword(in DOWNSAMPLED, qualityLevel.renderScale < 1f);
            material.SetKeyword(in NOISE_BLUE, staticConfig.noiseTechnique == AOStaticConfiguration.NoiseTechnique.BlueNoise);
            material.SetKeyword(in WORLD_SPACE_REJECTION, staticConfig.worldSpaceRejection);
            material.SetKeyword(in NEIGHBOURHOOD_CLAMPING, staticConfig.neighbourhoodClamping);
            material.SetKeyword(in ORTHOGRAPHIC, false);
            material.SetKeyword(in RECONSTRUCT_NORMALS, !staticConfig.useCameraNormalsTexture);
            ortographic = false;

            
        }

        #endregion

        #region PASS DATA

        private class PassData
        {
            public Camera camera;
            public Material material;
            public TextureHandle RT_CameraColor;
            public TextureHandle RT_OcclusionFactorCurrent;
            public TextureHandle RT_OcclusionFactorDenoiseAux;
            public TextureHandle RT_OcclusionFactorHistory;
            public TextureHandle RT_OcclusionFactorTAA;
            public TextureHandle RT_OcclusionFactorUpscaled;
            public TextureHandle RT_Depth;
            public TextureHandle RT_Normals;
            public TextureHandle RT_Noise;

            public bool needsDepthBuffer;
            public bool shouldPerformDownsampling;
            public bool shouldPerformDenoise;
            public bool shouldPerformTAA;
            public bool shouldPerformUpscale;
            public float temporalAccumulation;
            public TemporalData temporalData;
            public bool applyAfterOpaques;
            public AORendererFeature.DebugView debugView;
            public float directLightingStrength;
            public float colorCorrectionStrength;
            public float colorCorrectionExponent;
        }

        #endregion

        #region PASS SETUP

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (material == null) return;
            if (look.strength == 0f) return;
            if (look.radius.y == 0f) return;

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            if (resourceData.isActiveTargetBackBuffer) return;
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

            #if UNITY_EDITOR
            SetStaticMaterialProperties();
            #endif

            if(cameraData.camera.orthographic != ortographic)
            {
                ortographic = cameraData.camera.orthographic;
                material.SetKeyword(in ORTHOGRAPHIC, ortographic);
            }

            int width0 = cameraData.cameraTargetDescriptor.width;
            int height0 = cameraData.cameraTargetDescriptor.height;

            int width1 = Mathf.RoundToInt(width0 * qualityLevel.renderScale);
            int height1 = Mathf.RoundToInt(height0 * qualityLevel.renderScale);

            RTD_Depth.width = width1;
            RTD_Depth.height = height1;
            RTD_Normals.width = width1;
            RTD_Normals.height = height1;
            RTD_OcclusionFactor.width = width1;
            RTD_OcclusionFactor.height = height1;
            RTD_OcclusionFactor.width = width1;
            RTD_OcclusionFactor.height = height1;
            RTD_OcclusionFactorUpscaled.width = width0;
            RTD_OcclusionFactorUpscaled.height = height0;

            TextureHandle RT_CameraColor = resourceData.activeColorTexture;
            bool valid = RT_CameraColor.IsValid();

            TextureHandle RT_Depth = TextureHandle.nullHandle;
            TextureHandle RT_Normals = TextureHandle.nullHandle;
            if (NeedsDepthBuffer(qualityLevel, staticConfig))
            {
                RT_Depth = UniversalRenderer.CreateRenderGraphTexture(renderGraph, RTD_Depth, nameof(RT_Depth), false);
                valid &= RT_Depth.IsValid();
            }

            if(NeedsNormalBuffer(qualityLevel, staticConfig))
            {
                RT_Normals = UniversalRenderer.CreateRenderGraphTexture(renderGraph, RTD_Normals, nameof(RT_Normals), false);
                valid &= RT_Normals.IsValid();
            }

            TextureHandle RT_OcclusionFactorHistory = TextureHandle.nullHandle;
            TextureHandle RT_OcclusionFactorTAAResolved = TextureHandle.nullHandle;
            TextureHandle RT_OcclusionFactorCurrent = TextureHandle.nullHandle;
            TemporalData temporalData = null;
            if(qualityLevel.ShouldPerformTAA)
            {
                PruneTemporalDatas();
                temporalData = GetCameraData(cameraData);
                temporalData.UpdateViewProjection();
                temporalData.ReallocateIfNeeded(ref RTD_OcclusionFactor);
                RT_OcclusionFactorTAAResolved = renderGraph.ImportTexture(temporalData.RTH_Current);
                RT_OcclusionFactorHistory = renderGraph.ImportTexture(temporalData.RTH_History);
                valid &= RT_OcclusionFactorHistory.IsValid();
                valid &= RT_OcclusionFactorTAAResolved.IsValid();
                temporalData.SwapBuffers();

                if (qualityLevel.ShouldPerformTAA) noiseIdx = (noiseIdx + 1) % AOStaticConfiguration.blueNoiseAtlasLength;
                else noiseIdx = 0;
            }
            else
            {
                DisposeTemporalDatas();
            }

            RT_OcclusionFactorCurrent = UniversalRenderer.CreateRenderGraphTexture(renderGraph, RTD_OcclusionFactor, nameof(RT_OcclusionFactorCurrent), false);
            valid &= RT_OcclusionFactorCurrent.IsValid();

            TextureHandle RT_OcclusionFactorDenoiseAux = TextureHandle.nullHandle;
            if (qualityLevel.ShouldPerformDenoisePass)
            {
                if (qualityLevel.ShouldPerformTAA) RT_OcclusionFactorDenoiseAux = RT_OcclusionFactorTAAResolved;
                else RT_OcclusionFactorDenoiseAux = UniversalRenderer.CreateRenderGraphTexture(renderGraph, RTD_OcclusionFactor, nameof(RT_OcclusionFactorDenoiseAux), false);
                valid &= RT_OcclusionFactorDenoiseAux.IsValid();
            }

            TextureHandle RT_OcclusionFactorUpscaled = TextureHandle.nullHandle;
            if (ShouldPerformUpscalePass(qualityLevel, staticConfig))
            {
                RT_OcclusionFactorUpscaled = UniversalRenderer.CreateRenderGraphTexture(renderGraph, RTD_OcclusionFactorUpscaled, nameof(RT_OcclusionFactorUpscaled), false);
                valid &= RT_OcclusionFactorUpscaled.IsValid();
            }

            if (!valid) return;

            using (IUnsafeRenderGraphBuilder builder = renderGraph.AddUnsafePass<PassData>("Blit SSAO",
                out var passData, scope))
            {
                passData.material = material;
                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);

                if(look.applyAfterOpaques || AORendererFeature.ViewResults != AORendererFeature.DebugView.Off)
                {
                    passData.RT_CameraColor = RT_CameraColor;
                    builder.UseTexture(RT_CameraColor, AccessFlags.Write);
                }

                //
                //builder.UseTexture(RT_CameraColor, AccessFlags.ReadWrite);
                //passData.RT_CameraDepth = RT_CameraDepth;
                //builder.UseTexture(RT_CameraDepth, AccessFlags.ReadWrite);


                passData.RT_Depth = RT_Depth;
                passData.RT_Normals = RT_Normals;
                passData.RT_OcclusionFactorCurrent = RT_OcclusionFactorCurrent;
                passData.RT_OcclusionFactorHistory = RT_OcclusionFactorHistory;
                passData.RT_OcclusionFactorDenoiseAux = RT_OcclusionFactorDenoiseAux;
                passData.RT_OcclusionFactorTAA = RT_OcclusionFactorTAAResolved;
                passData.RT_OcclusionFactorUpscaled = RT_OcclusionFactorUpscaled;

                
                if (RT_Depth.IsValid()) builder.UseTexture(RT_Depth, AccessFlags.ReadWrite);
                if (RT_Normals.IsValid()) builder.UseTexture(RT_Normals, AccessFlags.ReadWrite);
                if (RT_OcclusionFactorHistory.IsValid()) builder.UseTexture(RT_OcclusionFactorHistory, AccessFlags.ReadWrite);
                if (RT_OcclusionFactorTAAResolved.IsValid()) builder.UseTexture(RT_OcclusionFactorTAAResolved, AccessFlags.ReadWrite);
                else if (RT_OcclusionFactorDenoiseAux.IsValid()) builder.UseTexture(RT_OcclusionFactorDenoiseAux, AccessFlags.ReadWrite);
                if (RT_OcclusionFactorUpscaled.IsValid()) builder.UseTexture(RT_OcclusionFactorUpscaled, AccessFlags.Write);
                builder.UseTexture(RT_OcclusionFactorCurrent, AccessFlags.ReadWrite);

                passData.camera = cameraData.camera;
                passData.needsDepthBuffer = NeedsDepthBuffer(qualityLevel, staticConfig);
                passData.shouldPerformDownsampling = ShouldPerformDownsampling(qualityLevel, staticConfig);
                passData.shouldPerformDenoise = qualityLevel.ShouldPerformDenoisePass;
                passData.shouldPerformTAA = qualityLevel.ShouldPerformTAA;
                passData.shouldPerformUpscale = ShouldPerformUpscalePass(qualityLevel, staticConfig);
                passData.temporalAccumulation = qualityLevel.temporalAccumulation;
                passData.temporalData = temporalData;
                passData.applyAfterOpaques = look.applyAfterOpaques;
                passData.debugView = AORendererFeature.ViewResults;
                passData.directLightingStrength = look.directLightingStrength;
                passData.colorCorrectionStrength = look.colorCorrection ? look.strength : 1f;
                passData.colorCorrectionExponent = look.colorCorrection ? look.exponent : 1f;

                builder.SetRenderFunc<PassData>(ExecutePass);
            }
        }

        #endregion

        #region PASS EXECUTION

        private static void ExecutePass(PassData data, UnsafeGraphContext context)
        {
            CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);

            //Downsample
            if (data.shouldPerformDownsampling)
            {
                if (data.needsDepthBuffer) CoreUtils.SetRenderTarget(cmd, data.RT_Normals, data.RT_Depth);
                else CoreUtils.SetRenderTarget(cmd, data.RT_Normals);
                Blitter.BlitTexture(cmd, m_ScaleBias, data.material, passDownsampleDepthAndNormals);

                if (data.needsDepthBuffer) cmd.SetGlobalTexture(_AO_DepthInput, data.RT_Depth);
                cmd.SetGlobalTexture(_AO_NormalsInput, data.RT_Normals);
            }

            //Occlusion factor
            if (!data.shouldPerformDenoise && !data.shouldPerformTAA && !data.shouldPerformUpscale)
            {
                cmd.SetGlobalFloat(_AO_Strength, data.colorCorrectionStrength);
                cmd.SetGlobalFloat(_AO_Exponent, data.colorCorrectionExponent);
            }
            else
            {
                cmd.SetGlobalFloat(_AO_Strength, 1f);
                cmd.SetGlobalFloat(_AO_Exponent, 1f);
            }

            if (data.shouldPerformTAA)
            {
                frameCount = (frameCount + 1) % 2147000000;
                cmd.SetGlobalInt(_AO_FrameCount, frameCount);

                Vector2 blueNoiseTiling = AOStaticConfiguration.BlueNoiseTiling;
                Vector2 blueNoiseOffset = new Vector2(
                            (noiseIdx % AOStaticConfiguration.blueNoiseAtlasColumns) * blueNoiseTiling.x,
                            (noiseIdx / AOStaticConfiguration.blueNoiseAtlasRows) * blueNoiseTiling.y
                        );
                cmd.SetGlobalVector(_AO_NoiseOffset, blueNoiseOffset);
            }
            else cmd.SetGlobalInt(_AO_FrameCount, 0);

            cmd.SetGlobalFloat(_AO_Sin_FovDiv2, Mathf.Sin(data.camera.fieldOfView * Mathf.Deg2Rad * 0.5f));
            

            CoreUtils.SetRenderTarget(cmd, data.RT_OcclusionFactorCurrent);
            Blitter.BlitTexture(cmd, m_ScaleBias, data.material, passComputeOcclusionFactor);

            TextureHandle finalAOTexture = data.RT_OcclusionFactorCurrent;

            //Denoise
            if (data.shouldPerformDenoise)
            {
                cmd.SetGlobalInt(_AO_DenoiseIsHorizontal, 1);
                Blitter.BlitTexture(cmd, data.RT_OcclusionFactorCurrent, data.RT_OcclusionFactorDenoiseAux, data.material, passDenoiseOcclusionFactor);
                cmd.SetGlobalInt(_AO_DenoiseIsHorizontal, 0);
                if (!data.shouldPerformTAA && !data.shouldPerformUpscale)
                {
                    cmd.SetGlobalFloat(_AO_Strength, data.colorCorrectionStrength);
                    cmd.SetGlobalFloat(_AO_Exponent, data.colorCorrectionExponent);
                }
                Blitter.BlitTexture(cmd, data.RT_OcclusionFactorDenoiseAux, data.RT_OcclusionFactorCurrent, data.material, passDenoiseOcclusionFactor);
            }

            //TAA
            if (data.shouldPerformTAA)
            {
                cmd.SetGlobalFloat(_AO_TemporalAccumulation, data.temporalData.reallocatedThisFrame ? 1f : data.temporalAccumulation);
                cmd.SetGlobalMatrix(_AO_Temporal_VP, data.temporalData.lastViewProj);
                cmd.SetGlobalMatrix(_AO_Temporal_I_VP, data.temporalData.lastViewProj.inverse);
                cmd.SetGlobalTexture(_AO_OcclusionInput, data.RT_OcclusionFactorCurrent);
                if (true)
                {
                    cmd.SetGlobalFloat(_AO_Strength, data.colorCorrectionStrength);
                    cmd.SetGlobalFloat(_AO_Exponent, data.colorCorrectionExponent);
                }
                Blitter.BlitTexture(cmd, data.RT_OcclusionFactorHistory, data.RT_OcclusionFactorTAA, data.material, passTAAResolveOcclusionFactor);

                //When using TAA, history cannot be contaminated with color correction
                if(!data.shouldPerformUpscale && (data.colorCorrectionStrength != 1f || data.colorCorrectionExponent != 1f))
                {
                    cmd.SetGlobalFloat(_AO_Strength, data.colorCorrectionStrength);
                    cmd.SetGlobalFloat(_AO_Exponent, data.colorCorrectionExponent);
                    Blitter.BlitTexture(cmd, data.RT_OcclusionFactorTAA, data.RT_OcclusionFactorCurrent, data.material, passColorCorrection);
                }
                else
                {
                    finalAOTexture = data.RT_OcclusionFactorTAA;
                }
            }

            //Upscale
            if (data.shouldPerformUpscale)
            {
                cmd.SetGlobalFloat(_AO_Strength, data.colorCorrectionStrength);
                cmd.SetGlobalFloat(_AO_Exponent, data.colorCorrectionExponent);
                Blitter.BlitTexture(cmd, finalAOTexture, data.RT_OcclusionFactorUpscaled, data.material, passUpscaleOcclusionFactor);
                finalAOTexture = data.RT_OcclusionFactorUpscaled;
            }
            context.cmd.SetGlobalTexture(s_SSAOFinalTextureID, finalAOTexture);

            //Results
            if (data.applyAfterOpaques || data.debugView != AORendererFeature.DebugView.Off)
            {
                CoreUtils.SetRenderTarget(cmd, data.RT_CameraColor);
                if (data.applyAfterOpaques) Blitter.BlitTexture(cmd, m_ScaleBias, data.material, passApplyOcclusionAfterOpaques);
                if (data.debugView == AORendererFeature.DebugView.Occlusion) Blitter.BlitTexture(cmd, m_ScaleBias, data.material, passViewAO);
                if (data.debugView == AORendererFeature.DebugView.Normals) Blitter.BlitTexture(cmd, m_ScaleBias, data.material, passViewNormals);
            }

            if (!data.applyAfterOpaques)
            {
                context.cmd.SetKeyword(_SCREEN_SPACE_OCCLUSION, true);
                context.cmd.SetGlobalVector(s_AmbientOcclusionParamID, new Vector4(1f, 0f, 0f, data.directLightingStrength));
            }
        }

        #endregion

        #region TEMPORAL DATA

        private static List<TemporalData> temporalDatas = new();

        private TemporalData GetCameraData(UniversalCameraData cameraData)
        {
            TemporalData temporalData = null;
            for(int i = 0; i < temporalDatas.Count && temporalData == null; i++)
            {
                if (temporalDatas[i].camera == cameraData.camera)
                {
                    temporalData = temporalDatas[i];
                    temporalData.cameraData = cameraData;
                }
            }

            if(temporalData == null)
            {
                temporalData = new TemporalData(temporalDatas.Count, cameraData);
                temporalDatas.Add(temporalData);
            }

            return temporalData;
        }

        private void DisposeTemporalDatas()
        {
            int count = temporalDatas.Count;
            for (int i = 0; i < count; i++)
            {
                temporalDatas[i].Dispose();
            }
            temporalDatas.Clear();
        }

        private void PruneTemporalDatas()
        {
            int count = temporalDatas.Count;
            for (int i = 0; i < count; i++)
            {
                if (!temporalDatas[i].HasValidCamera())
                {
                    count--;
                    temporalDatas[i].Dispose();
                    temporalDatas[i] = temporalDatas[count]; //Put the last element here
                    temporalDatas[i].index = i;
                    temporalDatas.RemoveAt(count); //Remove the last element of the list
                    i--; //Ensure this position is checked again
                }
            }
        }

        private class TemporalData
        {
            public int index; //Index of the data the list of cameraDatas
            public Camera camera;
            public UniversalCameraData cameraData;
            public CameraType cameraType;
            public Matrix4x4 viewProj;
            public Matrix4x4 lastViewProj;
            public bool reallocatedThisFrame;

            public RTHandle RTH_Current => historyIs0 ? RTH_OcclusionFactorHistory1 : RTH_OcclusionFactorHistory0;
            public RTHandle RTH_History => historyIs0 ? RTH_OcclusionFactorHistory0 : RTH_OcclusionFactorHistory1;

            private RTHandle RTH_OcclusionFactorHistory0 = null;
            private RTHandle RTH_OcclusionFactorHistory1 = null;
            private bool historyIs0 = false;

            public TemporalData(int index, UniversalCameraData cameraData)
            {
                this.index = index;
                this.camera = cameraData.camera;
                this.cameraData = cameraData;
                this.cameraType = camera.cameraType;
                viewProj = GetViewProjMatrix();
                lastViewProj = viewProj;
            }

            public void ReallocateIfNeeded(ref RenderTextureDescriptor descriptor)
            {
                reallocatedThisFrame = false;
                reallocatedThisFrame |= RenderingUtils.ReAllocateHandleIfNeeded(ref RTH_OcclusionFactorHistory0, descriptor,
                    FilterMode.Point, TextureWrapMode.Clamp, name: nameof(RTH_OcclusionFactorHistory0));
                reallocatedThisFrame |= RenderingUtils.ReAllocateHandleIfNeeded(ref RTH_OcclusionFactorHistory1, descriptor,
                    FilterMode.Point, TextureWrapMode.Clamp, name: nameof(RTH_OcclusionFactorHistory1));
            }

            public bool HasValidCamera()
            {
                if (cameraData == null) return false;
                if (camera == null) return false;
                if (!camera.enabled && cameraType != CameraType.SceneView) return false;
                var obj = camera.gameObject;
                if (!obj.activeSelf) return false;
                if (!obj.activeInHierarchy) return false;
                return true;
            }

            public void UpdateViewProjection()
            {
                lastViewProj = viewProj;
                viewProj = GetViewProjMatrix();
            }

            public void SwapBuffers()
            {
                historyIs0 = !historyIs0;
                //RTHandle tmp = RTH_OcclusionFactorHistory0;
                //RTH_OcclusionFactorHistory0 = RTH_OcclusionFactorHistory1;
                //RTH_OcclusionFactorHistory1 = tmp;
            }

            public void Dispose()
            {
                RTH_OcclusionFactorHistory0?.Release();
                RTH_OcclusionFactorHistory1?.Release();
            }

            private Matrix4x4 GetViewProjMatrix()
            {
                Matrix4x4 viewMat = cameraData.GetViewMatrix();
                Matrix4x4 projMat = GL.GetGPUProjectionMatrix(cameraData.GetProjectionMatrix(), true);
                return projMat * viewMat;
            }
        }

        #endregion
    }
}
