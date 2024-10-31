#ifndef PIT_NORMAL_RECONSTRUCTION
#define PIT_NORMAL_RECONSTRUCTION

//YOU MUST DEFINE ONE OF THE NEXT QUALITY LEVELS BEFORE INLCUDING THIS FILE
//#define NORMAL_QUALITY_LOW
//#define NORMAL_QUALITY_MEDIUM
//#define NORMAL_QUALITY_HIGH

inline float XOR_float(float a, float b)
{
    return (a + b) - 2.0 * min(a * b, 1.0);
}

float3 ReconstructNormalWS(int2 pixelCoord, float3 position_center, float depth_center)
{
    #ifdef NORMAL_QUALITY_LOW
        float3 normalWS = -normalize(cross(ddx(position_center), ddy(position_center)));;
    #else
                    //https://wickedengine.net/2019/09/improved-normal-reconstruction-from-depth/
        int2 pixel_top = pixelCoord + int2(0, 1);
        int2 pixel_bottom = pixelCoord + int2(0, -1);
        int2 pixel_right = pixelCoord + int2(1, 0);
        int2 pixel_left = pixelCoord + int2(-1, 0);

        float2 uv_top = (pixel_top + float2(0.5, 0.5)) * _CameraDepthTexture_TexelSize.xy;
        float2 uv_bottom = (pixel_bottom + float2(0.5, 0.5)) * _CameraDepthTexture_TexelSize.xy;
        float2 uv_right = (pixel_right + float2(0.5, 0.5)) * _CameraDepthTexture_TexelSize.xy;
        float2 uv_left = (pixel_left + float2(0.5, 0.5)) * _CameraDepthTexture_TexelSize.xy;

        float depth_top = LoadSceneDepth(pixel_top);
        float depth_bottom = LoadSceneDepth(pixel_bottom);
        float depth_right = LoadSceneDepth(pixel_right);
        float depth_left = LoadSceneDepth(pixel_left);

        float eye_depth_center = GetLinearEyeDepth(depth_center);
        float eye_depth_top = GetLinearEyeDepth(depth_top);
        float eye_depth_bottom = GetLinearEyeDepth(depth_bottom);
        float eye_depth_right = GetLinearEyeDepth(depth_right);
        float eye_depth_left = GetLinearEyeDepth(depth_left);

        float diff_left = abs(eye_depth_center - eye_depth_left);
        float diff_right = abs(eye_depth_center - eye_depth_right);
        float diff_top = abs(eye_depth_center - eye_depth_top);
        float diff_bottom = abs(eye_depth_center - eye_depth_bottom);

        #ifdef NORMAL_QUALITY_HIGH
            //https://atyuwen.github.io/posts/normal-reconstruction/#fn:1
	    	float eye_depth_top2		= GetLinearEyeDepth(LoadSceneDepth(pixel_top+int2(0,1)));
	    	float eye_depth_bottom2	= GetLinearEyeDepth(LoadSceneDepth(pixel_bottom+int2(0,-1)));
	    	float eye_depth_right2	= GetLinearEyeDepth(LoadSceneDepth(pixel_right+int2(1,0)));
	    	float eye_depth_left2	= GetLinearEyeDepth(LoadSceneDepth(pixel_left+int2(-1,0)));

            //const uint closest_horizontal = abs( (2.0 * l1.z - l2.z) - linearDepth) < abs( (2.0 * r1.z - r2.z) - linearDepth) ? 0 : 1;
            //const uint closest_vertical   = abs( (2.0 * d1.z - d2.z) - linearDepth) < abs( (2.0 * u1.z - u2.z) - linearDepth) ? 0 : 1;

            float extrapolated_left = abs((2.0 * eye_depth_left - eye_depth_left2) - eye_depth_center);
            float extrapolated_right = abs((2.0 * eye_depth_right - eye_depth_right2) - eye_depth_center);
            float extrapolated_top = abs((2.0 * eye_depth_top - eye_depth_top2) - eye_depth_center);
            float extrapolated_bottom = abs((2.0 * eye_depth_bottom - eye_depth_bottom2) - eye_depth_center);

            float leftIsCloser = step(extrapolated_left, extrapolated_right);
            float topIsCloser  = step(extrapolated_top, extrapolated_bottom);
        #elif defined NORMAL_QUALITY_MEDIUM
            float leftIsCloser = step(diff_left, diff_right); //diff_left < diff_right;
            float topIsCloser = step(diff_top, diff_bottom); //diff_top < diff_bottom;
        #endif

			    	
			    	    //XOR: topIsCloser ^ leftIsCloser ? -1.0 : 1.0;
        float invert = (1.0 - XOR_float(leftIsCloser, topIsCloser)) * 2.0 - 1.0;


        float2 uv_horizontal = lerp(uv_right, uv_left, leftIsCloser);
        float2 uv_vertical = lerp(uv_bottom, uv_top, topIsCloser);
        float depth_horizontal = lerp(depth_right, depth_left, leftIsCloser);
        float depth_vertical = lerp(depth_bottom, depth_top, topIsCloser);

        float3 position_horizontal = ComputeWorldSpacePosition(uv_horizontal, depth_horizontal, UNITY_MATRIX_I_VP);
        float3 position_vertical = ComputeWorldSpacePosition(uv_vertical, depth_vertical, UNITY_MATRIX_I_VP);
        float3 normalWS = normalize(cross(position_horizontal - position_center, position_vertical - position_center)) * invert;
    #endif

    return normalWS;
}

#endif