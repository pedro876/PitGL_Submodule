#ifndef PIT_DECLARE_AO_TEXTURE_INCLUDED
#define PIT_DECLARE_AO_TEXTURE_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/AmbientOcclusion.hlsl"

float4 _ScreenSpaceOcclusionTexture_TexelSize;

inline half SampleSceneAO(float2 texcoord)
{
    return SampleAmbientOcclusion(texcoord);
}

inline half LoadSceneAO(uint2 pixelCoords)
{
    return LOAD_TEXTURE2D_X(_ScreenSpaceOcclusionTexture, pixelCoords).r;
}
#endif
