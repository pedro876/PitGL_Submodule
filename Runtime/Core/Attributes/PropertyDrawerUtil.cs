#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class PropertyDrawerUtil
{
    public static string GetRelativePropertyPath(SerializedProperty property, string fieldName)
    {

        int index = property.propertyPath.LastIndexOf('.');
        if (index == -1) return fieldName;

        fieldName = $"{property.propertyPath.Substring(0, index)}.{fieldName}";

        // If this property is part of a serialized class, adjust the path
        //string[] pathParts = property.propertyPath.Split('.');
        //if (pathParts.Length > 1)
        //{
        //    // Remove the last part (current property) and append the field name
        //    string basePath = string.Join(".", pathParts, 0, pathParts.Length - 1);
        //    return $"{basePath}.{fieldName}";
        //}

        return fieldName;
    }

    public static void PropertyField(Rect position, SerializedProperty property, GUIContent label)
    {
        if(property.propertyType == SerializedPropertyType.Generic)
        {
                
        }
        else
        {
            EditorGUI.PropertyField(position, property);
        }
    }
}


#endif