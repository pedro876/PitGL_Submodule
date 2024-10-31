//https://vintay.medium.com/creating-custom-unity-attributes-readonly-d279e1e545c9
#if UNITY_EDITOR
using UnityEditor;
#endif
using System;
using UnityEngine;

[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public class EnableIfAttribute : PropertyAttribute
{
    public string boolName;

    public EnableIfAttribute(string boolName)
    {
        this.boolName = boolName;
    }
}
#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(EnableIfAttribute))]
public class EnableIfPropertyDrawer : PropertyDrawer
{
    public bool ShouldBeInteractable(SerializedProperty property)
    {
        EnableIfAttribute disableifAttribute = (EnableIfAttribute)attribute;
        string boolName = PropertyDrawerUtil.GetRelativePropertyPath(property, disableifAttribute.boolName);
        SerializedProperty boolProperty = property.serializedObject.FindProperty(boolName);
        bool shouldBeInteractable;
        if (boolProperty == null)
        {
            Debug.LogError($"Could not find bool property {boolName} for property {property.name} at object {property.serializedObject.targetObject.name}");
            shouldBeInteractable = true;
        }
        else shouldBeInteractable = boolProperty.boolValue;
        return shouldBeInteractable;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        GUI.enabled = ShouldBeInteractable(property);
        EditorGUI.PropertyField(position, property, label, true);
        GUI.enabled = true;
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, label, true);
    }
}
#endif