Shader "Hidden/AmbientOcclusion"
{
    HLSLINCLUDE

    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    // The Blit.hlsl file provides the vertex shader (Vert),
    // the input structure (Attributes), and the output structure (Varyings)
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
    #include "../ShaderLibrary/Common.hlsl"
    #include "../ShaderLibrary/BilinearFuncs.hlsl"
    #include "../ShaderLibrary/Encoding.hlsl"

    #pragma multi_compile_local _ DOWNSAMPLED
    #pragma multi_compile_local_fragment _ ORTHOGRAPHIC
    #pragma multi_compile_local_fragment _ RECONSTRUCT_NORMALS

    #if defined(DOWNSAMPLED) || defined(RECONSTRUCT_NORMALS)
        //TEXTURES DOWNSAMPLED DEPTH AND NORMALS
        TEXTURE2D_X_FLOAT(_AO_NormalsInput);
        float4 _AO_NormalsInput_TexelSize;
    #else
        #define _AO_NormalsInput _CameraNormalsTexture
        #define _AO_NormalsInput_TexelSize _CameraNormalsTexture_TexelSize
    #endif

    #if defined(DOWNSAMPLED)
        //TEXTURES DOWNSAMPLED DEPTH AND NORMALS
        TEXTURE2D_X_FLOAT(_AO_DepthInput);
        float4 _AO_DepthInput_TexelSize;
    #else
        #define _AO_DepthInput _CameraDepthTexture
        #define _AO_DepthInput_TexelSize _CameraDepthTexture_TexelSize
    #endif

    #define SKY_EARLY_EXIT
    #define SKY_DEPTH_VALUE 0.0001
    
    uint _AO_FrameCount;
    float _AO_DenoiseDepthThreshold;
    float _AO_DenoiseNormalSharpness;
    float _AO_Strength;
    float _AO_Exponent;

    SamplerState point_clamp_sampler;
    SamplerState linear_clamp_sampler;

    float3 LoadNormal(uint2 pixelCoords)
    {
        float3 normal = LOAD_TEXTURE2D_X(_AO_NormalsInput, pixelCoords).xyz;

        #if defined(_GBUFFER_NORMALS_OCT)
            float2 remappedOctNormalWS = Unpack888ToFloat2(normal); // values between [ 0,  1]
            float2 octNormalWS = remappedOctNormalWS.xy * 2.0 - 1.0;    // values between [-1, +1]
            normal = UnpackNormalOctQuadEncode(octNormalWS);
        #endif

        return normal;
    }

    float LoadDepth(uint2 pixelCoords)
    {
        return LOAD_TEXTURE2D_X(_AO_DepthInput, pixelCoords).r;
    }

    float SampleDepth(float2 uv)
    {
        return SAMPLE_TEXTURE2D_X_LOD(_AO_DepthInput, linear_clamp_sampler, uv, 0).r;
    }

    float AOColorCorrection(float ao)
    {
        ao = PositivePow(ao, _AO_Exponent);
        ao = 1.0 - ao;
        ao = saturate(ao * _AO_Strength);
        //ao = saturate(PositivePow(ao * _AO_Strength, _AO_Contrast));
        return 1.0 - ao;
    }

    ENDHLSL
    
    SubShader
    {
        ZWrite Off
        Cull Off
        ZTest Always
        ZWrite Off
        
        //DOWNSAMPLE DEPTH AND NORMALS
        Pass
        {
            Name "passDownsampleDepthAndNormals"

            ZWrite On

            HLSLPROGRAM
            
            #pragma vertex Vert
            #pragma fragment frag

            #define NORMAL_QUALITY_HIGH
            #include "../ShaderLibrary/NormalReconstruction.hlsl"

		    float3 SampleNormal(int2 pixelCoord, float depth_center)
		    {
		    	#if RECONSTRUCT_NORMALS
                    float2 uv_center = (pixelCoord + float2(0.5, 0.5)) * _CameraDepthTexture_TexelSize.xy;
                    float3 position_center = ComputeWorldSpacePosition(uv_center, depth_center, UNITY_MATRIX_I_VP);
		    	    float3 normalWS = ReconstructNormalWS(pixelCoord, position_center, depth_center);
                #else
                    float3 normalWS = LOAD_TEXTURE2D_X(_CameraNormalsTexture, pixelCoord).xyz;
		    	#endif

		    	return normalWS;
		    }

            struct FragOut
            {
                float4 normal : SV_Target;
                #ifdef DOWNSAMPLED
                    float depth : SV_Depth;
                #endif
            };

            FragOut frag (Varyings input)
            {
                FragOut output;

                #ifdef DOWNSAMPLED

                    BilinearInfo info = GetBilinearInfo(_CameraDepthTexture_TexelSize, input.texcoord);
                    float depth00 = LoadSceneDepth(info.pixel00.xy);
                    float depth01 = LoadSceneDepth(info.pixel01.xy);
                    float depth10 = LoadSceneDepth(info.pixel10.xy);
                    float depth11 = LoadSceneDepth(info.pixel11.xy);

                    int3 normalCoord = info.pixel00;
                    float maxDepth = depth00;
                    if(depth01 > maxDepth)
                    {
                        maxDepth = depth01;
                        normalCoord = info.pixel01;
                    }
                    if(depth10 > maxDepth)
                    {
                        maxDepth = depth10;
                        normalCoord = info.pixel10;
                    }
                    if(depth11 > maxDepth)
                    {
                        maxDepth = depth11;
                        normalCoord = info.pixel11;
                    }
                #else
                    int3 normalCoord = int3(input.texcoord * _CameraDepthTexture_TexelSize.zw, 0);
                    float maxDepth = LoadSceneDepth(normalCoord.xy);
                #endif

                output.normal = float4(SampleNormal(normalCoord.xy, maxDepth), 1);

                #ifdef DOWNSAMPLED
                    output.depth = maxDepth;
                #endif

                return output;
            }
            
            ENDHLSL
        }

        //COMPUTE OCCLUSION FACTOR
        Pass
        {
            Name "passComputeOcclusionFactor"

            HLSLPROGRAM
            
            #pragma vertex Vert
            #pragma fragment frag

            #include "../ShaderLibrary/VectorDistribution.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Random.hlsl"

            #pragma shader_feature_local_fragment NOISE_BLUE
            #pragma multi_compile_fog

            #ifndef NOISE_BLUE
                #define NOISE_IGN
            #endif

            #define MITIGATE_HALOS

            #define NORMAL_BIAS 0.01
            static const half kEpsilon = half(0.0001);

            float2 _AO_Radius;
            float _AO_SelfOcclude;
            float _AO_TemporalDither;
            float _AO_HaloRemoval;
            float _AO_Sin_FovDiv2;
            float _AO_NoiseDepthMult;

            TEXTURE2D_X(_AO_NoiseInput);
            float4 _AO_NoiseInput_TexelSize;
            float2 _AO_InvNoiseDimensions;
            float2 _AO_NoiseTiling;
            float2 _AO_NoiseOffset;

            float2 LoadNoise(uint2 pixelCoords)
            {
                float2 uv = (pixelCoords + float2(0.5, 0.5)) * _AO_InvNoiseDimensions;
                uv = frac(uv) * _AO_NoiseTiling + _AO_NoiseOffset;
                return _AO_NoiseInput.SampleLevel(point_clamp_sampler, uv, 0).rg;
            }

            // float InterleavedGradientNoise(uint2 pixelCoords)
            // {
            //     //return std::fmodf(52.9829189f * std::fmodf(0.06711056f*float(pixelX) + 0.00583715f*float(pixelY), 1.0f), 1.0f);
            //     return frac(52.9829189 * frac(0.06711056*pixelCoords.x + 0.00583715*pixelCoords.y));
            // }            // float InterleavedGradientNoise(uint2 pixelCoords)
            // {
            //     //return std::fmodf(52.9829189f * std::fmodf(0.06711056f*float(pixelX) + 0.00583715f*float(pixelY), 1.0f), 1.0f);
            //     return frac(52.9829189 * frac(0.06711056*pixelCoords.x + 0.00583715*pixelCoords.y));
            // }

            float CalculateAORadius(float eyeDepth)
            {
                //If a sphere was placed in front of the camera at eyeDepth distance,
                //how large could its radius be before it exits the frustrum vertically?
                //return _AO_Radius.y;
                float fittingRadius = _AO_Sin_FovDiv2 * eyeDepth;
                return clamp(fittingRadius, _AO_Radius.x, _AO_Radius.y);
            }

            float2 CalculateOcclusionAndWeight(float3 positionWS, float3 normalWS, float rawDepth, float eyeDepth,
                float3 displacedWS, float influenceRadius, float influenceRadiusSqr,
                out float3 actualWS, out int2 displacedPixel, out float intersected)
            {
                float3 displacedSS = ComputeNormalizedDeviceCoordinatesWithZ(displacedWS, UNITY_MATRIX_VP);
                displacedPixel = displacedSS.xy * _AO_DepthInput_TexelSize.zw;
                displacedSS.xy = (displacedPixel + float2(0.5, 0.5)) * _AO_DepthInput_TexelSize.xy;
                float actualDepth = LoadDepth(displacedPixel);
                float eyeActualDepth = GetLinearEyeDepth(actualDepth);
                actualWS = ComputeWorldSpacePosition(displacedSS.xy, actualDepth, UNITY_MATRIX_I_VP);
                float insideScreen = IsTexcoordInside(displacedSS.xy);
                intersected = actualDepth > displacedSS.z;
                
                //Based on unity's SSAO to match engine implementation
                float3 dir = actualWS - positionWS;
                float distSqr = dot(dir,dir) + kEpsilon;
                float insideRadius = distSqr < influenceRadiusSqr;
                insideRadius *= rawDepth > SKY_DEPTH_VALUE ? 1.0 : 0.0;
                float a1 = max(0, dot(dir, normalWS));
                float a2 = distSqr / influenceRadiusSqr;
                float occlusion = a1 * rcp(a2);
                occlusion *= insideRadius; //This prevents occlusion from out of hemisphere fragments
                occlusion *= lerp(intersected, 1.0, _AO_SelfOcclude); //Main diff between unity and me
                occlusion *= insideScreen;

                float weight = 1.0;//insideScreen;

                #ifdef MITIGATE_HALOS
                    //Out of radius samples will not contribute to the ao factor.
                    //This means that the remaining valid samples are used to compute ao,
                    //This could lead to flickering with low sample counts
                    weight *= lerp(1.0, max(0, eyeDepth - eyeActualDepth) < influenceRadius, _AO_HaloRemoval);
                #endif

                return float2(occlusion, weight);
            }

            float4 frag (Varyings input) : SV_Target
            {
                uint2 pixelCoords = input.texcoord * _AO_DepthInput_TexelSize.zw;
                float rawDepth = LoadDepth(pixelCoords);

                #ifdef SKY_EARLY_EXIT
                UNITY_BRANCH
                if(rawDepth < SKY_DEPTH_VALUE)
                {
                    return float4(1.0, 0.0, 0.0, 0.0);
                }
                #endif

                float eyeDepth = GetLinearEyeDepth(rawDepth);

                float noise;
                float3 cos_sin_noise;
                #ifdef NOISE_BLUE
                    float2 blueNoise = LoadNoise(pixelCoords);
                    noise = blueNoise.x;
                    float xzNoise = noise*PI*2;
                    float yNoise = blueNoise.y;
                    cos_sin_noise = float3(cos(xzNoise), sin(xzNoise), yNoise);
                #elif defined(NOISE_IGN)
                    noise = InterleavedGradientNoise(pixelCoords, _AO_FrameCount);
                    float xzNoise = noise*PI*2;
                    float yNoise = frac(noise*_AO_NoiseDepthMult);
                    cos_sin_noise = float3(cos(xzNoise), sin(xzNoise), yNoise);
                #else
                    noise = 1.0;
                    cos_sin_noise = float3(1.0, 0.0, 1.0);
                #endif

                float3 ogPositionWS = ComputeWorldSpacePosition(input.texcoord, rawDepth, UNITY_MATRIX_I_VP);

                //COMPUTE THE OCCLUSION FACTOR
                float3 normalWS = LoadNormal(pixelCoords);
                float3 positionWS = ogPositionWS + normalWS * NORMAL_BIAS;

                float3x3 hemisphereMatrix = GetHemisphereMatrix(ogPositionWS, normalWS);

                float aoRadius = CalculateAORadius(eyeDepth);
                float aoRadiusSqr = aoRadius * aoRadius;

                uint sampleCount;
                float totalOcclusion = 0.0;
                float totalWeight = 0.0;
                BEGIN_VECTOR_DISTRIBUTION_LOOP(sampleCount)
                    float3 displacement = GetDisplacementVector(i);
                    displacement = ApplyRandomRotation(i, displacement, cos_sin_noise);
                    displacement = mul(displacement, hemisphereMatrix);

                    float3 displacedWS = positionWS + displacement * aoRadius;
                    
                    float3 actualWS;
                    int2 displacedPixel;
                    float intersected;
                    float2 occlusion_weight = CalculateOcclusionAndWeight(positionWS, normalWS, rawDepth, eyeDepth,
                        displacedWS, aoRadius, aoRadiusSqr,
                        actualWS, displacedPixel, intersected);
                    occlusion_weight.x *= occlusion_weight.y;
                    
                    totalWeight += occlusion_weight.y;
                    totalOcclusion += saturate(occlusion_weight.x);

                END_VECTOR_DISTRIBUTION_LOOP

                //totalOcclusion *= aoRadius;
                //totalOcclusion *= _RcpVectorDistributionSampleCount;
                totalOcclusion = saturate(totalOcclusion * rcp(totalWeight+kEpsilon));
                
                #if defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2)
                if(IsFogEnabled())
                {
                    //float fogFactor = ComputeFogFactor(TransformWorldToHClip(ogPositionWS).z));
                    float fogFactor = ComputeFogFactor(rawDepth * eyeDepth);
                    float fogIntensity = ComputeFogIntensity(fogFactor); //1 when there is no fog
                    totalOcclusion *= fogIntensity;
                }
                #endif
                

                //totalOcclusion = pow(totalOcclusion, rcp(float(sampleCount)));

                totalOcclusion = 1.0 - totalOcclusion;


                //BLEND WITH HISTORY
                totalOcclusion = saturate(totalOcclusion + (noise-0.5) * _AO_TemporalDither);

                totalOcclusion = AOColorCorrection(totalOcclusion);

                //return float4(GetLinearEyeDepth(RGB_To_Float(Float_To_RGB(rawDepth))) * 0.05, Float_To_RGB(rawDepth));
                //return float4(rawDepth, Float_To_RGB(rawDepth));
                //return float4(eyeDepth * 0.05, Float_To_RGB(rawDepth));
                //return float4(ogPositionWS.x, Float_To_RGB(rawDepth));
                //return float4(steepness, Float_To_RGB(rawDepth));
                //return float4(validSamplesPct, Float_To_RGB(rawDepth));
                return float4(totalOcclusion, Float_To_RGB(rawDepth));
            }
            
            ENDHLSL
        }

        //DENOISE OCCLUSION FACTOR
        Pass
        {
            Name "passDenoiseOcclusionFactor"

            HLSLPROGRAM
            
            #pragma vertex Vert
            #pragma fragment frag

            int _AO_DenoiseIsHorizontal;
            int _AO_DenoiseRadius;

            #pragma multi_compile_local_fragment _ DENOISE_NORMAL_AWARE
            #define DENOISE_SPATIAL_AWARE
            #define DENOISE_DEPTH_AWARE
            
            #define DENOISE_DEPTH_THERSHOLD _AO_DenoiseDepthThreshold
            #define DENOISE_NORMAL_THRESHOLD _AO_DenoiseNormalSharpness
            #define DENOISE_RADIUS _AO_DenoiseRadius
            #define DENOISE_IS_HORIZONTAL _AO_DenoiseIsHorizontal
            #define DENOISE_COLOR_TYPE float
            #define DENOISE_GET_DATA(pixelCoord)(_BlitTexture.Load(int3(pixelCoord, 0)))
            #define DENOISE_GET_COLOR(pixelCoord, data)(data.r)
            #define DENOISE_GET_DEPTH(pixelCoord, data)(RGB_To_Float(data.gba))
            #define DENOISE_GET_NORMAL(pixelCoord, data)(LoadNormal(pixelCoord))
            #define DENOISE_OUTPUT_TYPE float4
            #define DENOISE_SET_OUTPUT(totalColor, data)(float4(totalColor, data.gba))
            #include "../ShaderLibrary/Denoise.hlsl"

            float4 frag (Varyings input) : SV_Target
            {
                float4 denoiseOutput = DenoiseSeparable(int2(input.texcoord * _BlitTexture_TexelSize.zw));
                denoiseOutput.r = AOColorCorrection(denoiseOutput.r);
                return denoiseOutput;
            }
            
            ENDHLSL
        }

        //TAA RESOLVE PASS
        Pass
        {
            Name "passTAAResolveOcclusionFactor"

            HLSLPROGRAM
            
            #pragma vertex Vert
            #pragma fragment frag

            #pragma shader_feature_local_fragment WORLD_SPACE_REJECTION
            #pragma shader_feature_local_fragment NEIGHBOURHOOD_CLAMPING
            #define TEMPORAL_REPROJECTION

            float _AO_TemporalAccumulation;
            float _AO_TemporalMaxDistance;
            float _AO_Variance;
            float _AO_VarianceBoost;
            float4x4 _AO_Temporal_VP;
            float4x4 _AO_Temporal_I_VP;

            TEXTURE2D_X(_AO_OcclusionInput);
            float4 _AO_OcclusionInput_TexelSize;

            float4 LoadOcclusion(int2 pixelCoords)
            {
                return LOAD_TEXTURE2D_X(_AO_OcclusionInput, pixelCoords);
            }

            float SampleOcclusion(float2 uv)
            {
                return _AO_OcclusionInput.SampleLevel(linear_clamp_sampler, uv, 0).r;
            }

            // float4 GatherOcclusion(float2 uv)
            // {
            //     return _AO_OcclusionInput.GatherRed(linear_clamp_sampler, uv, 0);
            // }

            // #if defined(TEMPORAL_ACCUMULATION_MOTION)
            //     TEXTURE2D_X(_MotionVectorTexture);
            //     SAMPLER(sampler_MotionVectorTexture);

            //     float2 GetMotionVector(float2 uv)
            //     {
            //         return SAMPLE_TEXTURE2D_X(_MotionVectorTexture, sampler_MotionVectorTexture, uv).xy;
            //     }
            // #endif

            float WeighTemporalData(float3 positionWS, int3 pixelCoord, out half occlusion)
            {
                float4 temporalData = _BlitTexture.Load(pixelCoord);
                occlusion = temporalData.r;
                float2 reprojectionUV = (pixelCoord.xy + half2(0.5,0.5)) * _BlitTexture_TexelSize.xy;
                float valid = IsTexcoordInside(reprojectionUV);

                #ifdef WORLD_SPACE_REJECTION
                    float reprojectionDepth = RGB_To_Float(temporalData.gba);
                    float3 reprojectionWS = ComputeWorldSpacePosition(reprojectionUV, reprojectionDepth, _AO_Temporal_I_VP);
                    float3 reprojectionDir = reprojectionWS - positionWS;
                    float reprojectionDist = dot(reprojectionDir, reprojectionDir);
                    reprojectionDist = 1.0 - saturate(reprojectionDist / _AO_TemporalMaxDistance);
                    valid *= reprojectionDist;
                #endif

                return valid;
            }

            float4 frag (Varyings input) : SV_Target
            {
                uint2 pixelCoords = input.texcoord * _AO_DepthInput_TexelSize.zw;
                

                float4 currentData = LoadOcclusion(pixelCoords);
                float totalOcclusion = currentData.r;
                float rawDepth = RGB_To_Float(currentData.gba);

                #ifdef SKY_EARLY_EXIT
                UNITY_BRANCH
                if(rawDepth < SKY_DEPTH_VALUE)
                {
                    return float4(1.0, 0.0, 0.0, 0.0);
                }
                #endif

                //return float4(rawDepth * 10, 0,0,0);
                float eyeDepth = GetLinearEyeDepth(rawDepth);
                //return float4(eyeDepth * 0.05, 0,0,0);
                float3 ogPositionWS = ComputeWorldSpacePosition(input.texcoord, rawDepth, UNITY_MATRIX_I_VP);

                //return float4(ogPositionWS.x, 0,0,0);

                float valid = 1.0;
                float previousOcclusion;
                #ifdef TEMPORAL_REPROJECTION
                    // #if defined(TEMPORAL_ACCUMULATION_MOTION)
                    //     float2 reprojectionUV = input.texcoord - GetMotionVector(input.texcoord);
                    // #else
                        float2 reprojectionUV = ComputeNormalizedDeviceCoordinatesWithZ(ogPositionWS, _AO_Temporal_VP).xy;
                    //#endif

                    //Bilinearly sample history to prevent distortion
                    BilinearInfo info = GetBilinearInfo(_BlitTexture_TexelSize, reprojectionUV);
                    half previousOcclusion00;
                    half previousOcclusion01;
                    half previousOcclusion10;
                    half previousOcclusion11;

                    half reprojectionDist00 = WeighTemporalData(ogPositionWS, info.pixel00, previousOcclusion00);
                    half reprojectionDist01 = WeighTemporalData(ogPositionWS, info.pixel01, previousOcclusion01);
                    half reprojectionDist10 = WeighTemporalData(ogPositionWS, info.pixel10, previousOcclusion10);
                    half reprojectionDist11 = WeighTemporalData(ogPositionWS, info.pixel11, previousOcclusion11);

                    info.weight00 *= reprojectionDist00;
                    info.weight01 *= reprojectionDist01;
                    info.weight10 *= reprojectionDist10;
                    info.weight11 *= reprojectionDist11;

                    half temporalWeight = info.weight00 + info.weight01 + info.weight10 + info.weight11;

                    previousOcclusion =(previousOcclusion00 * info.weight00 +
                                        previousOcclusion01 * info.weight01 + 
                                        previousOcclusion10 * info.weight10 + 
                                        previousOcclusion11 * info.weight11) / max(0.001, temporalWeight);

                    //valid = IsTexcoordInside(reprojectionUV);
                    valid *= temporalWeight;

                    #ifdef NEIGHBOURHOOD_CLAMPING
                        half nmin = 1.0;
                        half nmax = 0.0;

                        // half2 smallBlurUVs[4];
                        // const half smallBlurRadius = 1.5 * 1.0; //Default is 1.5
                        // smallBlurUVs[0] = half2(-0.5, smallBlurRadius);
                        // smallBlurUVs[1] = half2(-smallBlurRadius, -0.5);
                        // smallBlurUVs[2] = half2(0.5, -smallBlurRadius);
                        // smallBlurUVs[3] = half2(smallBlurRadius, 0.5);
                        // nmin = totalOcclusion;
                        // nmax = totalOcclusion;

                        // for(int i = 0; i < 4; i++)
                        // {
                        //     float2 neighbourUV = input.texcoord + smallBlurUVs[i] * _AO_OcclusionInput_TexelSize.xy;
                        //     // float4 neighbours = GatherOcclusion(neighbourUV);
                        //     // nmin = min(nmin, min(min(neighbours.x, neighbours.y), min(neighbours.z, neighbours.w)));
                        //     // nmax = max(nmax, max(max(neighbours.x, neighbours.y), max(neighbours.z, neighbours.w)));
                        //     float neighbour = SampleOcclusion(neighbourUV);
                        //     nmin = min(nmin, neighbour);
                        //     nmax = max(nmax, neighbour);
                        // }

                        const int NEIGHBOURHOOD_RADIUS = 1;

                        for(int y = -NEIGHBOURHOOD_RADIUS; y <= NEIGHBOURHOOD_RADIUS; y++)
                        {
                            for(int x = -NEIGHBOURHOOD_RADIUS; x <= NEIGHBOURHOOD_RADIUS; x++)
                            {
                                int2 neighbourCoords = pixelCoords;
                                neighbourCoords += int2(x,y);
                                half neighbourOcclusion = LoadOcclusion(neighbourCoords).r;

                                nmin = min(nmin, neighbourOcclusion);
                                nmax = max(nmax, neighbourOcclusion);
                            }
                        }

                        half avg = (nmax + nmin) * 0.5;
                        half diff = (nmax - nmin) * 0.5 * lerp(_AO_VarianceBoost, _AO_Variance, valid);
                        nmin = avg - diff;
                        nmax = avg + diff;

                        previousOcclusion = clamp(previousOcclusion, nmin, nmax);
                    #endif

                #else
                    previousOcclusion = _BlitTexture.Load(int3(pixelCoords, 0)).r;
                #endif

                //BLEND WITH HISTORY
                //This prevents low precision artifacts. Without this, old occlusion values don't fully disappear over time.
                //totalOcclusion = saturate(totalOcclusion + (noise-0.5) * _AO_TemporalDither);
                totalOcclusion = lerp(totalOcclusion, previousOcclusion, (1.0 - _AO_TemporalAccumulation) * valid);

                return float4(saturate(totalOcclusion), Float_To_RGB(rawDepth));
            }
            
            ENDHLSL
        }

        //Color Correction
        Pass
        {
            Name "passColorCorrection"
            ColorMask R

            HLSLPROGRAM
            
            #pragma vertex Vert
            #pragma fragment frag

            float frag (Varyings input) : SV_Target
            {
                int3 pixelCoords = int3(input.texcoord * _BlitTexture_TexelSize.zw, 0);
                float occlusion = _BlitTexture.Load(pixelCoords).r;
                occlusion = AOColorCorrection(occlusion);
                return occlusion;
            }
            
            ENDHLSL
        }

        //UPSCALE OCCLUSION FACTOR
        Pass
        {
            Name "passUpscaleOcclusionFactor"

            HLSLPROGRAM
            
            #pragma vertex Vert
            #pragma fragment frag

            #pragma multi_compile_local_fragment _ DENOISE_NORMAL_AWARE
            #define DENOISE_SPATIAL_AWARE
            #define DENOISE_DEPTH_AWARE

            #define DENOISE_DEPTH_THERSHOLD 0.2
            #define DENOISE_NORMAL_THRESHOLD _AO_DenoiseNormalSharpness
            #define DENOISE_RADIUS 1
            #define DENOISE_IS_HORIZONTAL 0
            #define DENOISE_COLOR_TYPE float
            #define DENOISE_GET_DATA(pixelCoord)(_BlitTexture.Load(int3(pixelCoord, 0)))
            #define DENOISE_GET_COLOR(pixelCoord, data)(data.r)
            #define DENOISE_GET_DEPTH(pixelCoord, data)(RGB_To_Float(data.gba))
            #define DENOISE_GET_NORMAL(pixelCoord, data)(LoadNormal(pixelCoord))
            #define DENOISE_OUTPUT_TYPE float
            #define DENOISE_SET_OUTPUT(totalColor, data)(totalColor)
            #include "../ShaderLibrary/Denoise.hlsl"

            float frag (Varyings input) : SV_Target
            {
                float upscaleOutput = Upscale(input.texcoord, _BlitTexture_TexelSize);
                upscaleOutput = AOColorCorrection(upscaleOutput);
                return upscaleOutput;
            }
            
            ENDHLSL
        }

        //APPLY AFTER OPAQUES
        Pass
        {
            Name "passApplyOcclusionAfterOpaques"
            Blend Zero SrcColor

            HLSLPROGRAM
            
            #pragma vertex Vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/AmbientOcclusion.hlsl"

            float4 frag (Varyings input) : SV_Target
            {
                //return half4(1,1,1,1);
                half occlusion = SampleAmbientOcclusion(input.texcoord);
                return half4(occlusion, occlusion, occlusion, 1);
            }
            
            ENDHLSL
        }

        //VIEW AO
        Pass
        {
            Name "passViewAO"

            HLSLPROGRAM
            
            #pragma vertex Vert
            #pragma fragment frag

            //#include "Assets/Scripts/PitGL/Runtime/AmbientOcclusion/DeclareAOTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/AmbientOcclusion.hlsl"

            half4 frag (Varyings input) : SV_Target
            {
                //return half4(SampleSceneAO(input.texcoord, linear_clamp_sampler).rrr, 1);
                half occlusion = SampleAmbientOcclusion(input.texcoord);
                return half4(occlusion, occlusion, occlusion, 1);
            }
            
            ENDHLSL
        }

        //VIEW NORMALS
        Pass
        {
            Name "passViewNormals"

            HLSLPROGRAM
            
            #pragma vertex Vert
            #pragma fragment frag

            half4 frag (Varyings input) : SV_Target
            {
                float3 normal = LoadNormal(input.texcoord * _AO_NormalsInput_TexelSize.zw);
                return half4(normal, 1);
                //return half4(SampleSceneAO(input.texcoord, linear_clamp_sampler).rrr, 1);
            }
            
            ENDHLSL
        }
    }
}