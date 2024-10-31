#ifndef PIT_GEOMETRY
#define PIT_GEOMETRY

#define GEO_EPSILON 0.0001
#define GEO_INFINITY 1e30f

float Intersect_Ray_Plane(float3 rayPoint, float3 rayDir, float3 planePoint, float3 planeNormal)
{
    float t;
    
    float denom = dot(planeNormal, rayDir);
    if (abs(denom) > 0.0001)
    {
        t = dot(planeNormal, (planePoint - rayPoint)) / denom;
    }
    else
    {
        //If it is parallel, we return t = 0 so that the ray stays where
        // it is since it won't collide against the plane
        t = GEO_INFINITY;
    }
    
    return t;
}

float3 GetIntersection_Ray_Plane(float3 rayPoint, float3 rayDir, float3 planePoint, float3 planeNormal)
{
    float t = Intersect_Ray_Plane(rayPoint, rayDir, planePoint, planeNormal);
    return rayPoint + rayDir * t;
}


/*
    Given a normal in world space and a depth threshold, it assumes
    that the threshold is intended for surfaces orthogonal to the
    camera forward direction. This functions returns the hypothenuse
    of a triangle formed by the threshold as the adjacent side whose
    length equals the threshold, and a hypothenuse whose direction
    is the forward vector of the camera.

    This means that the threshold will become greater as the normal points away
    from the camera.
*/
float CalculateDepthThreshold(float3 normalWS, float linearThreshold)
{
    //return linearThreshold;
    
    float3 normalVS = TransformWorldToViewNormal(normalWS, false);
    //float cos_alpha = dot(normalVS, float3(0, 0, 1));
    float cos_alpha = abs(normalVS.z); //dot(normalVS, float3(0,0,1))
    float hyp = linearThreshold * rcp(cos_alpha + GEO_EPSILON);
    
    
    
    
    return hyp;
}

#endif