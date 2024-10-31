Shader "Hidden/Debug/DebugView"
{
    HLSLINCLUDE
    
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        // The Blit.hlsl file provides the vertex shader (Vert),
        // the input structure (Attributes), and the output structure (Varyings)
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
    

        SamplerState point_clamp_sampler;
        SamplerState linear_clamp_sampler;
    ENDHLSL
    
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline"}
        LOD 100
        ZWrite Off Cull Off
        Pass
        {
            Name "passViewDepth"

            HLSLPROGRAM
            
            #pragma vertex Vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            float4 frag (Varyings input) : SV_Target
            {
                //float rawDepth = SampleSceneDepth(input.texcoord, point_clamp_sampler);
                float rawDepth = _CameraDepthTexture.Load(int3(input.texcoord * _CameraDepthTexture_TexelSize.zw, 0)).r;
                float eyeDepth = LinearEyeDepth(rawDepth, _ZBufferParams) * 0.05;

                return eyeDepth;
            }
            
            ENDHLSL
        }

        Pass
        {
            Name "passViewNormals"

            HLSLPROGRAM
            
            #pragma vertex Vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            float4 frag (Varyings input) : SV_Target
            {
                return float4(SampleSceneNormals(input.texcoord, point_clamp_sampler), 1);
            }
            
            ENDHLSL
        }

        Pass
        {
            Name "passViewOpaque"

            HLSLPROGRAM
            
            #pragma vertex Vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"


            float4 frag (Varyings input) : SV_Target
            {
                return float4(SampleSceneColor(input.texcoord), 1);
            }
            
            ENDHLSL
        }

        Pass
        {
            Name "passViewMotion"

            HLSLPROGRAM
            
            #pragma vertex Vert
            #pragma fragment frag

            TEXTURE2D_X(_MotionVectorTexture);
            SAMPLER(sampler_MotionVectorTexture);

            float4 frag (Varyings input) : SV_Target
            {
                return SAMPLE_TEXTURE2D_X(_MotionVectorTexture, sampler_MotionVectorTexture, input.texcoord);
            }
            
            ENDHLSL
        }
    }
}