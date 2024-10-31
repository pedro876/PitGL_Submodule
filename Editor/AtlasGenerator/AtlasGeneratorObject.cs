using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;

namespace PitGL
{
    public class AtlasGeneratorObject : ScriptableObject
    {
        [SerializeField] Texture2D[] textures;
        [SerializeField] int rows;
        [SerializeField] int columns;
        [SerializeField] string atlasName;
    }

    [CustomEditor(typeof(AtlasGeneratorObject))]
    public class AtlasGeneratorEditor : Editor
    {
        SerializedProperty textures;
        SerializedProperty rows;
        SerializedProperty columns;
        SerializedProperty atlasName;

        private void OnEnable()
        {
            textures = serializedObject.FindProperty(nameof(textures));
            rows = serializedObject.FindProperty(nameof(rows));
            columns = serializedObject.FindProperty(nameof(columns));
            atlasName = serializedObject.FindProperty(nameof(atlasName));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            GUILayout.Label("It is mandatory that all textures share the same dimensions, format, etc");

            EditorGUILayout.PropertyField(textures);
            if(textures.arraySize == 0)
            {
                serializedObject.ApplyModifiedProperties();
                return;
            }

            EditorGUILayout.PropertyField(columns);

            if(columns.intValue < 1)
            {
                columns.intValue = 1;
            }

            Texture2D prototype = textures.GetArrayElementAtIndex(0).objectReferenceValue as Texture2D;

            if(prototype != null)
            {
                rows.intValue = ((textures.arraySize - 1) / columns.intValue) + 1;
            }

            GUI.enabled = false;
            EditorGUILayout.PropertyField(rows);
            GUI.enabled = true;

            EditorGUILayout.PropertyField(atlasName);
            serializedObject.ApplyModifiedProperties();


            if(atlasName.stringValue.Trim().Length != 0)
            {
                if (GUILayout.Button("GENERATE ATLAS"))
                {
                    string prototypePath = AssetDatabase.GetAssetPath(prototype);

                    string format = prototypePath.EndsWith("png") ? "png" :
                        prototypePath.EndsWith("jpg") ? "jpg" :
                        prototypePath.EndsWith("exr") ? "exr" :
                        prototypePath.EndsWith("tga") ? "tga" : 
                        "png";

                    string folderPath = Path.GetDirectoryName(prototypePath);
                    string newPath = Path.Combine(folderPath, atlasName.stringValue + "." + format);
                    Debug.Log(newPath);

                    AssetDatabase.CopyAsset(prototypePath, newPath);
                    GenerateAtlas(newPath, format, prototype);
                    AssetDatabase.Refresh();
                    Texture2D atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(newPath);

                    EditorGUIUtility.PingObject(atlas);
                }
            }
            
            

        }

        private void GenerateAtlas(string path, string format, Texture2D prototype)
        {
            int width = columns.intValue * prototype.width;
            int height = rows.intValue * prototype.height;

            Texture2D atlas = new Texture2D(width, height, prototype.format, false);

            for(int i = 0; i < textures.arraySize; i++)
            {
                Texture2D tex = textures.GetArrayElementAtIndex(i).objectReferenceValue as Texture2D;

                int startX = (i % columns.intValue) * prototype.width;
                int startY = (i / columns.intValue) * prototype.height;

                for(int y = 0; y < prototype.height; y++)
                {
                    for (int x = 0; x < prototype.height; x++)
                    {
                        Color color = tex.GetPixel(x, y);
                        atlas.SetPixel(startX + x, startY + y, color);
                    }
                }
            }
            atlas.Apply();

            byte[] bytes;
            switch (format)
            {
                default:
                case "png": bytes = atlas.EncodeToPNG(); break;
                case "jpg": bytes = atlas.EncodeToJPG(); break;
                case "exr": bytes = atlas.EncodeToEXR(); break;
                case "tga": bytes = atlas.EncodeToTGA(); break;
            }

            File.WriteAllBytes(path, bytes);
        }
    }
}


