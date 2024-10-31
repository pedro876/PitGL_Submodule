#ifndef PIT_VECTOR_DISTRIBUTION
#define PIT_VECTOR_DISTRIBUTION

#define MAX_DISPLACEMENT_VECTORS_PER_PIXEL 64
float _DisplacementVectors[MAX_DISPLACEMENT_VECTORS_PER_PIXEL*3];
int _VectorDistributionSampleCount;
float _RcpVectorDistributionSampleCount;
float2 _VectorDistributionMinMaxDepth;
float _VectorDistributionVerticalAngleStep;
float _VectorDistributionDepthExponent;

#define BEGIN_VECTOR_DISTRIBUTION_LOOP(sampleCount) \
    sampleCount = min(_VectorDistributionSampleCount, MAX_DISPLACEMENT_VECTORS_PER_PIXEL); \
    for(uint i = 0; i < sampleCount; i++) \
    {

#define END_VECTOR_DISTRIBUTION_LOOP }

float3 ApplyRandomRotation(float sampleIndex, float3 displacement, float3 cos_sin_noise)
{
    //APPLY NOISE TO THE HORIZONTAL ANGLE OF THE VECTOR
    float3 v = float3(
            displacement.x * cos_sin_noise.x - displacement.z * cos_sin_noise.y,
            displacement.y,
            displacement.x * cos_sin_noise.y + displacement.z * cos_sin_noise.x
        );
    //v = displacement;
    
    //APPLY NOISE TO THE VERTICAL ANGLE OF THE VECTOR
    //v.xz *= lerp(0.0, 1.0, cos_sin_lengthAdd.w);
    //v = normalize(v);
    
    //float verticalAndDepthPerturbation = 1.0; //1.0 / _VectorDistributionSampleCount;
    
    //const float minAngleScale = 0.1;
    //const float maxAngleScale = 2.0 - minAngleScale;
    //float3 rotatedV = v;
    //rotatedV.xz *= lerp(1.0, lerp(minAngleScale, maxAngleScale, cos_sin_lengthAdd.w), verticalAndDepthPerturbation);
    //rotatedV = normalize(rotatedV);
    //v = rotatedV;
    
    float3 axis = float3(v.z, 0, -v.x);
    float theta = (cos_sin_noise.z * 2.0 - 1.0);
    theta *= _VectorDistributionVerticalAngleStep;
    
    float cosTheta = cos(theta);
    float sinTheta = sin(theta);
    
    float3 crossProd = cross(axis, v);
    float dotProd = dot(axis, v);
    
    v = v * cosTheta + crossProd * sinTheta + axis * dotProd * (1.0 - cosTheta);
    
    //APPLY NOISE TO THE MAGNITUDE OF THE VECTOR
    //cos_sin_lengthAdd.z *= _PoissonDepthStep;
    //float magnitude = frac(displacementNormalized.w + cos_sin_lengthAdd.z * verticalAndDepthPerturbation);
    //magnitude = clamp(magnitude, _VectorDistributionMinMaxDepth.x, _VectorDistributionMinMaxDepth.y);
    //v *= magnitude;
    
    //float depth = displacementNormalized.w;
    float depth = (sampleIndex + cos_sin_noise.z) * _RcpVectorDistributionSampleCount;
    //depth = frac(depth + cos_sin_noise.z);
    //depth = depth * cos_sin_noise.z;
    depth = PositivePow(depth, _VectorDistributionDepthExponent);
    depth = lerp(_VectorDistributionMinMaxDepth.x, _VectorDistributionMinMaxDepth.y, depth);
    v *= depth;
    
    return v;
}

float3 GetDisplacementVector(uint i)
{
    return float3(_DisplacementVectors[i*3], _DisplacementVectors[i*3+1], _DisplacementVectors[i*3+2]);
}

float3x3 GetHemisphereMatrix(float3 positionWS, float3 normalWS)
{
    float3 toPos = positionWS - _WorldSpaceCameraPos;
    float3 up = normalWS;
    float3 right = normalize(cross(normalWS, toPos));
    float3 forward = normalize(cross(right, up));
    float3x3 hemisphereMatrix = float3x3(right, up, forward);
    return hemisphereMatrix;
}

#endif