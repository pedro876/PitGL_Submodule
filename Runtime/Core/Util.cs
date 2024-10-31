using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PitGL
{
    public static class Util
    {
        #region FULL SCREEN QUAD
        private static Mesh fullscreenMesh = null;
        public static Mesh FullScreenMesh { get
            {
                if (fullscreenMesh != null) return fullscreenMesh;
#if UNITY_2022_1_OR_NEWER
                float topV = 1.0f;
                float bottomV = 0.0f;

                fullscreenMesh = new Mesh { name = "Fullscreen Quad" };
                fullscreenMesh.SetVertices(new List<Vector3>
                {
                    new Vector3(-1.0f, -1.0f, 0.0f),
                    new Vector3(-1.0f,  1.0f, 0.0f),
                    new Vector3(1.0f, -1.0f, 0.0f),
                    new Vector3(1.0f,  1.0f, 0.0f)
                });

                fullscreenMesh.SetUVs(0, new List<Vector2>
                {
                    new Vector2(0.0f, bottomV),
                    new Vector2(0.0f, topV),
                    new Vector2(1.0f, bottomV),
                    new Vector2(1.0f, topV)
                });

                fullscreenMesh.SetIndices(new[] { 0, 1, 2, 2, 1, 3 }, MeshTopology.Triangles, 0, false);
                fullscreenMesh.UploadMeshData(true);
#else
                fullscreenMesh = RenderingUtils.fullscreenMesh;
#endif
                return fullscreenMesh;
            } 
        }
        #endregion

        #region RENDER OBJECTS

        //https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@14.0/manual/urp-shaders/urp-shaderlab-pass-tags.html
        /// <summary>
        /// Use this list to render the standard color output of the objects
        /// </summary>
        public static List<ShaderTagId> forwardShaderTagIds => new()
        {
            srpDefaultUnlitShaderTagId,
            universalForwardShaderTagId,
            universalForwardOnlyShaderTagId,
        };

        public static ShaderTagId srpDefaultUnlitShaderTagId => new ShaderTagId("SRPDefaultUnlit");
        public static ShaderTagId universalForwardShaderTagId => new ShaderTagId("UniversalForward");
        public static ShaderTagId universalForwardOnlyShaderTagId => new ShaderTagId("UniversalForwardOnly");
        public static ShaderTagId depthShaderTagId => new ShaderTagId("DepthOnly");
        public static ShaderTagId normalsShaderTagId => new ShaderTagId("DepthNormalsOnly");
        public static ShaderTagId shadowCasterShaderTagId => new ShaderTagId("ShadowCaster");
        public static ShaderTagId metaShaderTagId => new ShaderTagId("Meta");

        #endregion

        #region RANDOM

        public static float RandomRange(System.Random rnd, float min, float max)
        {
            float value = ((float)rnd.NextDouble()) * (max - min) + min;
            return value;
        }

        public static int RandomRange(System.Random rnd, int min, int max)
        {
            int a = rnd.Next();
            a = a % (max - min);
            a += min;
            return a;
        }

        #endregion

        #region GAUSSIAN

        public static float GaussianDistribution(float x, float sigma)
        {
            return (1.0f / (2.0f * Mathf.PI * sigma * sigma)) * Mathf.Exp(-x * x / (2.0f * sigma * sigma));
        }

        #endregion
    }
}
