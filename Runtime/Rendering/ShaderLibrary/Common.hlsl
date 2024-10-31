#ifndef PIT_COMMON
#define PIT_COMMON

inline float IsTexcoordInside(half2 uv)
{
    uv -= 0.5;
    uv = abs(uv);
    float m = max(uv.x, uv.y);
    return step(m, 0.5);
}

float GetLinearEyeDepth(float rawDepth)
{
    #if defined(ORTHOGRAPHIC)
        return LinearDepthToEyeDepth(rawDepth);
    #else
        return LinearEyeDepth(rawDepth, _ZBufferParams);
    #endif
}

#endif