//https://vintay.medium.com/creating-custom-unity-attributes-readonly-d279e1e545c9
#if UNITY_EDITOR
using UnityEditor;
#endif
using System;
using UnityEngine;

[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public class HideIfAttribute : PropertyAttribute
{
    public string boolName;

    public HideIfAttribute(string boolName)
    {
        this.boolName = boolName;
    }
}

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(HideIfAttribute))]
public class HideIfPropertyDrawer : PropertyDrawer
{
    public bool CanBeShown(SerializedProperty property)
    {
        HideIfAttribute showIfAttrib = (HideIfAttribute)attribute;
        string boolName = PropertyDrawerUtil.GetRelativePropertyPath(property, showIfAttrib.boolName);
        SerializedProperty boolProperty = property.serializedObject.FindProperty(boolName);
        bool canBeShown;
        if (boolProperty == null)
        {
            Debug.LogError($"Could not find bool property {boolName} for property {property.name} at object {property.serializedObject.targetObject.name}");
            canBeShown = true;
        }
        else canBeShown = !boolProperty.boolValue;
        return canBeShown;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (CanBeShown(property))
        {
            EditorGUI.PropertyField(position, property, label, true);
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (CanBeShown(property))
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }
        else return -EditorGUIUtility.standardVerticalSpacing;

    }
}
#endif
