//https://vintay.medium.com/creating-custom-unity-attributes-readonly-d279e1e545c9
#if UNITY_EDITOR
using UnityEditor;
#endif
using System;
using UnityEngine;


[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public class RangeEnumAttribute : PropertyAttribute
{
}

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(RangeEnumAttribute))]
public class RangeEnumPropertyDrawer : PropertyDrawer
{
    const float suffixMarginLeft = 6;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        position = EditorGUI.PrefixLabel(position, label);
        float suffixSize = GetMaxSuffixWidth(property);
        position.width -= suffixSize;
        position.width -= suffixMarginLeft;
        property.enumValueIndex = Mathf.RoundToInt(GUI.HorizontalSlider(position, property.enumValueIndex, 0f, property.enumNames.Length - 1));
        position.x += position.width;
        position.x += suffixMarginLeft;
        position.width = suffixSize;

        GUI.Label(position, property.enumDisplayNames[property.enumValueIndex]);
    }

    float GetMaxSuffixWidth(SerializedProperty property)
    {
        string[] names = property.enumDisplayNames;
        GUIContent content = new GUIContent();
        float maxWidth = 0f;

        for (int i = 0; i < names.Length; ++i)
        {
            content.text = names[i];
            float width = GUI.skin.label.CalcSize(content).x;
            if (width > maxWidth) maxWidth = width;
        }

        return Mathf.Max(maxWidth, 50);

    }
}
#endif

