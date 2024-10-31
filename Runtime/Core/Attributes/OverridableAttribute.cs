//https://vintay.medium.com/creating-custom-unity-attributes-readonly-d279e1e545c9
#if UNITY_EDITOR
using UnityEditor;
#endif
using System;
using UnityEngine;

[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public class OverridableAttribute : PropertyAttribute
{
    public string overrideBoolName;

    public OverridableAttribute(string overrideBoolName)
    {
        this.overrideBoolName = overrideBoolName;
    }
}

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(OverridableAttribute))]
public class OverridablePropertyDrawer : PropertyDrawer
{

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        OverridableAttribute overridableAttrib = (OverridableAttribute)attribute;
        string boolName = PropertyDrawerUtil.GetRelativePropertyPath(property, overridableAttrib.overrideBoolName);
        SerializedProperty overridableBoolProperty = property.serializedObject.FindProperty(boolName);
        if (overridableBoolProperty == null)
        {
            Debug.LogError($"Could not find bool property {boolName} for property {property.name} at object {property.serializedObject.targetObject.name}");
            return;
        }

        const float toggleWidth = 18f;
        const float toggleLeftMargin = 6f;
        position.width -= toggleWidth + toggleLeftMargin;

        GUI.enabled = overridableBoolProperty.boolValue;
        EditorGUI.PropertyField(position, property, label);
        GUI.enabled = true;

        position.x += position.width + toggleLeftMargin;
        position.width = toggleWidth;

        overridableBoolProperty.boolValue = GUI.Toggle(position, overridableBoolProperty.boolValue, "");

        //if (IsOverriden(property))
        //{
        //    
        //}



    }
}
#endif
