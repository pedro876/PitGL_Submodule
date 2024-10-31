#if UNITY_EDITOR
using UnityEditor;
#endif
using System;
using UnityEngine;
using System.IO;

/// <summary>
/// Use this attribute to force an asset to be referenced. The path doesn't need to start in Assets, it can be a subfolder
/// and the editor will try to match the provided relative path to the asset database.
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public class ForceAssetAttribute : PropertyAttribute
{
    public string assetPath;
    public string assetName;

    public ForceAssetAttribute(string assetPath)
    {
        this.assetPath = assetPath;
        this.assetName = Path.GetFileNameWithoutExtension(assetPath);
    }

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(ForceAssetAttribute))]
public class ForceAssetPropertyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        ForceAssetAttribute forceAssetAttribute = attribute as ForceAssetAttribute;
        UnityEngine.Object currentAsset = property.objectReferenceValue;
        bool needsFind = (currentAsset == null || currentAsset.name != forceAssetAttribute.assetName)
            && forceAssetAttribute.assetPath != null && forceAssetAttribute.assetPath.Length > 0;

        if (needsFind)
        {
            string[] guids = AssetDatabase.FindAssets(forceAssetAttribute.assetName);

            for(int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if(path.Contains(forceAssetAttribute.assetPath))
                {
                    property.objectReferenceValue = AssetDatabase.LoadAssetAtPath(path, typeof(UnityEngine.Object));
                    break;
                }
            }
            
        }

        GUI.enabled = false;
        EditorGUI.PropertyField(position, property, label);
        GUI.enabled = true;
    }
}
#endif
}
