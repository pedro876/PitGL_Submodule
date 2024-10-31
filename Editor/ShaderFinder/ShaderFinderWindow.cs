using System.IO;
using UnityEditor;
using UnityEditor.PackageManager.UI;
using UnityEngine;

namespace PitGL
{
    public class ShaderFinderWindow : ScriptableObjectWindow<ShaderFinderObject>
    {
        [MenuItem("PitGL/Shader Finder")]
        public static void ShowWindow()
        {
            var window = EditorWindow.GetWindow(typeof(ShaderFinderWindow));
            window.name = "Shader Finder";
        }

        protected override void OnGUIBeforeScriptableObject(ShaderFinderObject scriptableObject)
        {
            GUILayout.Label("The aim of this tool is to help you track where functions, variables, " +
                "or anything else comes from in a given shader or hlsl file.", EditorStyles.helpBox);
        }
    }
}


