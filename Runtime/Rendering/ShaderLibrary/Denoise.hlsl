#ifndef PIT_DENOISE
#define PIT_DENOISE

#include "Gaussian.hlsl"
#include "Geometry.hlsl"

//YOU CAN DEFINE THESE CONSTANTS IN ORDER TO BLUR SMARTLY
//#define DENOISE_DEPTH_AWARE
//#define DENOISE_NORMAL_AWARE
//#define DENOISE_SPATIAL_AWARE

//YOU MUST DEFINE THE FOLLOWING MACROS BEFORE INCLUDING THIS FILE
//#define DENOISE_DEPTH_THERSHOLD _AO_DenoiseDepthThreshold
//#define DENOISE_NORMAL_THRESHOLD _AO_DenoiseNormalSharpness
//#define DENOISE_RADIUS _AO_DenoiseRadius
//#define DENOISE_IS_HORIZONTAL _AO_DenoiseIsHorizontal
//#define DENOISE_COLOR_TYPE float
//#define DENOISE_GET_DATA(pixelCoord)(_BlitTexture.Load(int3(otherPixelCoord, 0)))
//#define DENOISE_GET_COLOR(pixelCoord, data)(data.r)
//#define DENOISE_GET_DEPTH(pixelCoord, data)(DecodeDepth(data.gba))
//#define DENOISE_GET_NORMAL(pixelCoord, data)(LoadNormal(pixelCoord))
//#define DENOISE_OUTPUT_TYPE float4
//#define DENOISE_SET_OUTPUT(totalColor, data)(float4(totalColor, data.gba))


#define REFERENCE_HEIGHT 540.0

float GetDepthSharpnessThreshold(float3 normalWS, float eyeDepth, float threshold /*, float maxScale = GEO_INFINITY*/)
{
    float screenHeightCompensation = REFERENCE_HEIGHT / _AO_DepthInput_TexelSize.w;
    float depthThreshold = threshold * screenHeightCompensation;
    depthThreshold = CalculateDepthThreshold(normalWS, depthThreshold);
        //depthThreshold = min(depthThreshold, threshold * maxScale);
    return depthThreshold;
}

float CalculateDepthWeight(float centerDepth, float otherDepth, float sharpnessThreshold)
{
    float depthDiff = saturate(abs(otherDepth - centerDepth) / sharpnessThreshold);
    float weight = 1.0 - smoothstep(0, 1, depthDiff) * (sharpnessThreshold > 0);
    return weight;
}

float CalculateNormalWeight(float3 centerNormal, float3 otherNormal)
{
    float normalDiff = saturate(dot(centerNormal, otherNormal));
    normalDiff = smoothstep(0.0, 1.0, normalDiff);
    normalDiff = lerp(1.0 - DENOISE_NORMAL_THRESHOLD, 1.0, normalDiff);
    return normalDiff;
}

#define DENOISE_MAX_RADIUS 4
//These weights have been calculating using the next formulas:
//sigma(radius) = 0.07f * radius * radius + 0.31f * radius + 0.615f;
//weight = gaussian(radius, sigma) / gaussian(radius, 0.0) -> this division ensures normalized weights
const static float DenoiseSpatialWeights[1 + 2 + 3 + 4] =
{
    0.6034827,
    0.8042513, 0.4183761,
    0.8996996, 0.6552246, 0.3862585,
    0.945073, 0.7977399, 0.6014339, 0.4049908,
};

void DenoiseSeparableIter(int i, int2 pixelCoord, int2 direction
#ifdef DENOISE_DEPTH_AWARE
, float depthThreshold, float centerDepth
#endif
#ifdef DENOISE_NORMAL_AWARE
, float3 centerNormal
#endif
#ifdef DENOISE_SPATIAL_AWARE
, int spatialIdx
#endif
, inout float totalWeight, inout DENOISE_COLOR_TYPE totalColor)
{
    int2 otherPixelCoord = pixelCoord + direction * i;
    float4 data = DENOISE_GET_DATA(otherPixelCoord);
    DENOISE_COLOR_TYPE color = DENOISE_GET_COLOR(otherPixelCoord, data);
    float weight = 1.0;

    #ifdef DENOISE_DEPTH_AWARE
        float otherDepth = GetLinearEyeDepth(DENOISE_GET_DEPTH(otherPixelCoord, data));
        weight *= CalculateDepthWeight(centerDepth, otherDepth, depthThreshold);
    #endif

    #ifdef DENOISE_NORMAL_AWARE
        float3 otherNormal = DENOISE_GET_NORMAL(otherPixelCoord, data);
        weight *= CalculateNormalWeight(centerNormal, otherNormal);
    #endif

    #ifdef DENOISE_SPATIAL_AWARE
        weight *= DenoiseSpatialWeights[spatialIdx + abs(i) - 1];
    #endif

    totalWeight += weight;
    totalColor += color * weight;
}

DENOISE_OUTPUT_TYPE DenoiseSeparable(int2 pixelCoord)
{
    float4 data = DENOISE_GET_DATA(pixelCoord);
    DENOISE_COLOR_TYPE totalColor = DENOISE_GET_COLOR(pixelCoord, data);
    float totalWeight = 1.0;
    int2 denoiseDirection = int2(DENOISE_IS_HORIZONTAL, 1 - DENOISE_IS_HORIZONTAL);
    int clampedRadius = max(1, min(DENOISE_RADIUS, DENOISE_MAX_RADIUS));
    
    //if(_AO_DenoiseIsHorizontal == 0) return data;

    #if defined(DENOISE_DEPTH_AWARE) || defined(DENOISE_NORMAL_AWARE)
        float rawDepth = DENOISE_GET_DEPTH(pixelCoord, data);
        float3 normalWS = DENOISE_GET_NORMAL(pixelCoord, data);
    #endif
    
    #ifdef DENOISE_DEPTH_AWARE
        float centerDepth = GetLinearEyeDepth(rawDepth);
        float depthThreshold = GetDepthSharpnessThreshold(normalWS, centerDepth, DENOISE_DEPTH_THERSHOLD);
    #endif
    
    #ifdef DENOISE_SPATIAL_AWARE
        int spatialIndex = 0;
        for (int s = 0; s < clampedRadius; s++)
            spatialIndex += s;
    #endif

    for (int l = -clampedRadius; l < 0; l++)
    {
        DenoiseSeparableIter(l, pixelCoord, denoiseDirection
        #ifdef DENOISE_DEPTH_AWARE
        , depthThreshold, centerDepth
        #endif
        #ifdef DENOISE_NORMAL_AWARE
        , normalWS
        #endif
        #ifdef DENOISE_SPATIAL_AWARE
        , spatialIndex
        #endif
        , totalWeight, totalColor);
    }

    for (int r = 1; r <= clampedRadius; r++)
    {
        DenoiseSeparableIter(r, pixelCoord, denoiseDirection
        #ifdef DENOISE_DEPTH_AWARE
        , depthThreshold, centerDepth
        #endif
        #ifdef DENOISE_NORMAL_AWARE
        , normalWS
        #endif
        #ifdef DENOISE_SPATIAL_AWARE
        , spatialIndex
        #endif
        , totalWeight, totalColor);
    }

    totalColor /= totalWeight;
    totalColor = saturate(totalColor);

    DENOISE_OUTPUT_TYPE output = DENOISE_SET_OUTPUT(totalColor, data);
    
    return output;
}

DENOISE_OUTPUT_TYPE Upscale(float2 texcoord, float4 lowTexelSize)
{
    const float upscaleSpatialSigma = 0.8;
    
    int2 pixelCoordsHigh = texcoord * _CameraDepthTexture_TexelSize.zw;
    int2 pixelCoordsLow = texcoord * lowTexelSize.zw;
                
    #ifdef DENOISE_SPATIAL_AWARE
        float2 uvLow = (pixelCoordsLow + float2(0.5, 0.5)) * lowTexelSize.xy;
    #endif

    #if defined(DENOISE_DEPTH_AWARE) || defined(DENOISE_NORMAL_AWARE)
        float3 normalWS = DENOISE_GET_NORMAL(pixelCoordsLow, 0);
        float rawDepthHigh = LoadSceneDepth(pixelCoordsHigh);
        float depthHigh = GetLinearEyeDepth(rawDepthHigh);
        float closestColorDist = 3.402823466e+20f;
        DENOISE_COLOR_TYPE closestColor = 0.0;
    #endif
    
    #if defined(DENOISE_DEPTH_AWARE)
        float depthThreshold = min(1.0, GetDepthSharpnessThreshold(normalWS, depthHigh, DENOISE_DEPTH_THERSHOLD));
    #endif
    
    #ifdef DENOISE_SPATIAL_AWARE
        float maxGaussian = GaussianDistribution(0, upscaleSpatialSigma);
    #endif

    float totalWeight = 0.0001;
    DENOISE_COLOR_TYPE totalColor = 0.0;
    

    for (int y = -DENOISE_RADIUS; y <= DENOISE_RADIUS; y++)
    {
        for (int x = -DENOISE_RADIUS; x <= DENOISE_RADIUS; x++)
        {
            int2 otherPixelCoordsLow = pixelCoordsLow + int2(x,y);
            float4 data = DENOISE_GET_DATA(otherPixelCoordsLow);
            DENOISE_COLOR_TYPE color = DENOISE_GET_COLOR(otherPixelCoordsLow, data);
            float weight = 1.0;

            #if defined(DENOISE_DEPTH_AWARE) || defined(DENOISE_NORMAL_AWARE)
                float depthLow = DENOISE_GET_DEPTH(otherPixelCoordsLow, data);
                depthLow = GetLinearEyeDepth(depthLow);
                float distDiff = abs(depthLow - depthHigh);
                if(distDiff < closestColorDist)
                {
                    closestColor = color;
                    closestColorDist = distDiff;
                }
            #endif
            
            #ifdef DENOISE_DEPTH_AWARE
                weight *= CalculateDepthWeight(depthHigh, depthLow, depthThreshold) + 0.0001;
            #endif

            #ifdef DENOISE_NORMAL_AWARE
                float3 otherNormal = LoadNormal(otherPixelCoordsLow);
                weight *= CalculateNormalWeight(normalWS, otherNormal);
            #endif

            #ifdef DENOISE_SPATIAL_AWARE
                float2 otherUVLow = uvLow + float2(x,y) * lowTexelSize.xy;
                float uvDist = length((otherUVLow - texcoord) * lowTexelSize.zw);
                weight *= GaussianDistribution(uvDist, upscaleSpatialSigma) / maxGaussian;
            #endif


            totalColor += color * weight;
            totalWeight += weight;
        }
    }

    totalColor /= totalWeight;

    #if defined(DENOISE_DEPTH_AWARE) || defined(DENOISE_NORMAL_AWARE)
        if(totalWeight < 0.01)
            totalColor = closestColor;
    #endif

    DENOISE_OUTPUT_TYPE output = DENOISE_SET_OUTPUT(totalColor, lowTexelSize);
    return output;

}

#endif