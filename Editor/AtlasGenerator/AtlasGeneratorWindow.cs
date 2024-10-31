using System.IO;
using UnityEditor;
using UnityEditor.PackageManager.UI;
using UnityEngine;

namespace PitGL
{
    public class AtlasGeneratorWindow : ScriptableObjectWindow<AtlasGeneratorObject>
    {
        [MenuItem("PitGL/Atlas Generator")]
        public static void ShowWindow()
        {
            var window = EditorWindow.GetWindow(typeof(AtlasGeneratorWindow));
            window.name = "Atlas Generator";
        }
    }
}


