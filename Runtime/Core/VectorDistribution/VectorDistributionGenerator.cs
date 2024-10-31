//#define GENERATION_METHOD_0
#define GENERATION_METHOD_1

using System.Collections.Generic;
using UnityEngine;
using System;
using Architecture;

namespace PitGL
{
    [System.Serializable]
    public class VectorDistributionParams
    {
        [Header("Angle XZ")]
        [SerializeField, Resettable(20f)] public float revolutions = 20f;

        [Header("Angle Y")]
        [SerializeField, Resettable(10f), Range(1, 90)] public float minAngle = 10f;
        [SerializeField, Resettable(90f), Range(1, 90)] public float maxAngle = 90f;
        [SerializeField, Resettable(0.5f), Range(0, 1)] public float verticalPerturbation = 0.5f;

        [Header("Radius")]
        [SerializeField, Resettable(0.1f), Range(0, 1)] public float minDepth = 0.1f;
        [SerializeField, Resettable(1f), Range(0, 1)] public float maxDepth = 1f;
        [SerializeField, Resettable(0.5f), Range(0.01f,4f)] public float depthExponent = 0.5f;

        public static VectorDistributionParams Clone(VectorDistributionParams prototype)
        {
            return new VectorDistributionParams()
            {
                revolutions = prototype.revolutions,
                minAngle = prototype.minAngle,
                maxAngle = prototype.maxAngle,
                verticalPerturbation = prototype.verticalPerturbation,
                minDepth = prototype.minDepth,
                maxDepth = prototype.maxDepth,
                depthExponent = prototype.depthExponent,
            };
        }

        public override string ToString()
        {
            return JsonUtility.ToJson(this);
        }
    }

    public static class VectorDistributionGenerator
    {
        private static readonly int _DisplacementVectors = Shader.PropertyToID(nameof(_DisplacementVectors));
        private static readonly int _VectorDistributionSampleCount = Shader.PropertyToID(nameof(_VectorDistributionSampleCount));
        private static readonly int _RcpVectorDistributionSampleCount = Shader.PropertyToID(nameof(_RcpVectorDistributionSampleCount));
        private static readonly int _VectorDistributionMinMaxDepth = Shader.PropertyToID(nameof(_VectorDistributionMinMaxDepth));
        private static readonly int _VectorDistributionVerticalAngleStep = Shader.PropertyToID(nameof(_VectorDistributionVerticalAngleStep));
        private static readonly int _VectorDistributionDepthExponent = Shader.PropertyToID(nameof(_VectorDistributionDepthExponent));

        #region MATERIAL SETUP

        public static void SetVectorDistributionProperties(
            Material material, VectorDistribution.SampleCount sampleCount, VectorDistributionParams distribution, float[] vectors)
        {
            int spp = (int)sampleCount;
            material.SetFloatArray(_DisplacementVectors, vectors);
            material.SetInt(_VectorDistributionSampleCount, spp);
            material.SetFloat(_RcpVectorDistributionSampleCount, 1f / spp);
            material.SetVector(_VectorDistributionMinMaxDepth, new Vector2(distribution.minDepth, distribution.maxDepth));


            float verticalAngleStep = (distribution.maxAngle - distribution.minAngle) / (spp + 1f);
            verticalAngleStep *= Mathf.Deg2Rad;

            material.SetFloat(_VectorDistributionVerticalAngleStep, verticalAngleStep);
            material.SetFloat(_VectorDistributionDepthExponent, distribution.depthExponent);
        }

        public static void CopyVectorsToFloat3Array(ref float[] arr, Vector3[] vectors)
        {
            int count = vectors.Length;
            int arrLength = count * 3;

            if (arr == null || arr.Length != arrLength)
            {
                arr = new float[VectorDistribution.MAX_SAMPLE_COUNT * 3];
            }
            for (int i = 0; i < count; i++)
            {
                Vector3 vec = vectors[i];
                vec = vec.normalized;

                arr[i * 3 + 0] = vec.x;
                arr[i * 3 + 1] = vec.y;
                arr[i * 3 + 2] = vec.z;
            }
        }

        #endregion

        #region GENERATION

        public static Vector3[] GenerateVectors(VectorDistributionParams distribution, VectorDistribution.SampleCount sampleCountEnum, int seed)
        {
            System.Random rnd = new System.Random(seed);
            List<Vector3> vectors = new List<Vector3>();
            int sampleCount = (int)sampleCountEnum;
            float fSampleCount = sampleCount;
#if GENERATION_METHOD_0
            for (int i = 0; i < sampleCount; i++)
            {
                float horizontalAngle = Util.RandomRange(rnd, 0f, 360f);
                float verticalAngle = Mathf.Lerp(distribution.minAngle, distribution.maxAngle, Util.RandomRange(rnd, 0f, 1f));
                float depth = Mathf.Lerp(distribution.minDepth, distribution.maxDepth, Mathf.Pow((i + 1) / fSampleCount, distribution.depthExponent));

                Vector3 vector = Quaternion.AngleAxis(horizontalAngle, Vector3.up) * Vector3.right;
                vector = Quaternion.AngleAxis(verticalAngle, Vector3.Cross(vector, Vector3.up)) * vector;
                vector = vector.normalized * depth;
                vectors.Add(vector);
            }
#elif GENERATION_METHOD_1
            float horizontalStep = 360f / sampleCount;
            float verticalStep = 90f / (sampleCount+1);

            for (int i = 0; i < sampleCount; i++)
            {
                float horizontalAngle = horizontalStep * 0.5f + horizontalStep * i + Util.RandomRange(rnd, -horizontalStep * 0.5f, horizontalStep * 0.5f);
                float verticalAngle = verticalStep * (i + 1) + Util.RandomRange(rnd, -verticalStep * 0.5f, verticalStep * 0.5f) * distribution.verticalPerturbation;
                //verticalAngle = 45f;
                verticalAngle = Mathf.Lerp(distribution.maxAngle, distribution.minAngle, verticalAngle / 90f);
                float depth = Mathf.Lerp(distribution.minDepth, distribution.maxDepth, Mathf.Pow((i+1)/ fSampleCount, distribution.depthExponent));

                
                Vector3 vector = Quaternion.AngleAxis(horizontalAngle * distribution.revolutions, Vector3.up) * Vector3.right;
                vector = Quaternion.AngleAxis(verticalAngle, Vector3.Cross(vector, Vector3.up)) * vector;
                vector = vector.normalized * depth;
                vectors.Add(vector);
            }
#endif

            return vectors.ToArray();
        }

#endregion

        #region EDITOR ONLY
#if UNITY_EDITOR

        public static bool IsPointFeashible(VectorDistributionParams distribution, Vector3 vector)
        {
            if (vector.y < 0f) return false;
            if (vector.sqrMagnitude > 1f) return false;

            Vector3 proj = Vector3.ProjectOnPlane(vector, Vector3.up);
            float angle = Vector3.Angle(proj, vector);
            if (angle < distribution.minAngle) return false;
            if(angle > distribution.maxAngle) return false;

            float depth = vector.magnitude;
            if (depth < distribution.minDepth) return false;
            if (depth > distribution.maxDepth) return false;

            return true;
        }

        public static Vector3 PerturbPoint(System.Random rnd, Vector3 vector, float perturbationDist)
        {
            Vector3 perturbation = new Vector3(
                    Util.RandomRange(rnd, -perturbationDist, perturbationDist),
                    Util.RandomRange(rnd, -perturbationDist, perturbationDist),
                    Util.RandomRange(rnd, -perturbationDist, perturbationDist));

            vector += perturbation;
            return vector;
        }

        public static bool TryPerturbPoints(VectorDistributionParams distribution, System.Random rnd, Vector3[] vectors, float perturbationDist, out int idx, out Vector3 previousVector)
        {
            idx = Util.RandomRange(rnd, 0, vectors.Length);
            previousVector = vectors[idx];
            Vector3 newVector = PerturbPoint(rnd, previousVector, perturbationDist);
            if (IsPointFeashible(distribution, newVector))
            {
                vectors[idx] = newVector;
                return true;
            }
            else
            {
                return false;
            }
        }
#endif
#endregion
    }
}
