//https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@17.0/manual/renderer-features/create-custom-renderer-feature.html
//https://drive.google.com/file/d/1mg1I_670SDc5iTkjobgsobPv7KvLRyLR/view
//https://discussions.unity.com/t/introduction-of-render-graph-in-the-universal-render-pipeline-urp/930355
//https://discussions.unity.com/t/how-to-write-a-custom-attribute/181233
//https://docs.unity3d.com/6000.0/Documentation/Manual/urp/render-graph-pass-textures-between-passes.html
//https://docs.unity3d.com/Packages/com.unity.render-pipelines.core@17.0/manual/srp-custom-getting-started.html

using Architecture;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace PitGL
{
    public class DebugViewRendererFeature : ScriptableRendererFeature
    {
        [SerializeField, ForceShader("Hidden/Debug/DebugView")] private Shader shader; //Make a standard way to have this automatically assigned
        private Hook<Material> materialPtr;

        [SerializeField] bool viewDepth = false;
        [SerializeField] bool viewNormals = false;
        [SerializeField] bool viewOpaque = false;
        [SerializeField] bool viewMotion = false;

        public static bool s_viewDepth = false;
        public static bool s_viewNormals = false;
        public static bool s_viewOpaque = false;
        public static bool s_viewMotion = false;

        private DebugViewRenderPass depthPass;
        private DebugViewRenderPass normalsPass;
        private DebugViewRenderPass opaquePass;
        private DebugViewRenderPass motionPass;

        public override void Create()
        {
            if (shader == null)
            {
                return;
            }
            materialPtr = new Hook<Material>(new Material(shader));

            depthPass = new DebugViewRenderPass(materialPtr, "passViewDepth");
            depthPass.ConfigureInput(ScriptableRenderPassInput.Depth);
            depthPass.renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;

            normalsPass = new DebugViewRenderPass(materialPtr, "passViewNormals");
            normalsPass.ConfigureInput(ScriptableRenderPassInput.Normal);
            normalsPass.renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;

            opaquePass = new DebugViewRenderPass(materialPtr, "passViewOpaque");
            opaquePass.ConfigureInput(ScriptableRenderPassInput.Color);
            opaquePass.renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;

            motionPass = new DebugViewRenderPass(materialPtr, "passViewMotion");
            motionPass.ConfigureInput(ScriptableRenderPassInput.Motion);
            motionPass.renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (materialPtr.Value == null)
            {
                if (shader == null) return;
                materialPtr.Value = new Material(shader);
            }

            if (viewDepth || s_viewDepth) renderer.EnqueuePass(depthPass);
            else if (viewNormals || s_viewNormals) renderer.EnqueuePass(normalsPass);
            else if(viewOpaque || s_viewOpaque) renderer.EnqueuePass(opaquePass);
            else if(viewMotion || s_viewMotion) renderer.EnqueuePass(motionPass);
        }

        protected override void Dispose(bool disposing)
        {
            depthPass.Dispose();
            normalsPass.Dispose();
            opaquePass.Dispose();
            motionPass.Dispose();

            #if UNITY_EDITOR
            if (EditorApplication.isPlaying)
            {
                Destroy(materialPtr.Value);
            }
            else
            {
                DestroyImmediate(materialPtr.Value);
            }
            #else
            Destroy(materialPtr.Value);
            #endif
        }
    }
}
