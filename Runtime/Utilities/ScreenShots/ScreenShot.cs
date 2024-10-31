#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using System.IO;

namespace PitGL
{
    public static class ScreenShot
    {
#if UNITY_EDITOR
        public const string SCREENSHOTS_PATH = "Assets/ScreenShots";

        [MenuItem("PitGL/ScreenShot ^F1")]
        private static void TakeScreenShot()
        {
            string path = EditorUtility.SaveFilePanel("ScreenShot save path", SCREENSHOTS_PATH, "NewScreenShot", "png");
            if(path != null && path.Length > 0)
            {
                SaveImage(Capture(), path);
            }
        }
#endif

        /// <summary>
        /// Path must contains final path, including name of the file and format.
        /// </summary>
        /// <param name="tex"></param>
        /// <param name="path"></param>
        public static void SaveImage(Texture2D tex, string path)
        {
            byte[] bytes = tex.EncodeToPNG();
            File.WriteAllBytes(path, bytes);
#if UNITY_EDITOR
            AssetDatabase.Refresh();
#endif
        }

        public static Texture2D Capture()
        {
            Camera camera = Camera.main;
            int w = camera.scaledPixelWidth;
            int h = camera.scaledPixelHeight;
            RenderTexture rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = rt;
            camera.Render();
            camera.targetTexture = null;
            Texture2D tex = new Texture2D(w, h, TextureFormat.ARGB32, false);
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.ReleaseTemporary(rt);
            return tex;
        }
    }
}
