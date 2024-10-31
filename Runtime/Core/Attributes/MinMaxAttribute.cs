//https://vintay.medium.com/creating-custom-unity-attributes-readonly-d279e1e545c9
#if UNITY_EDITOR
using UnityEditor;
#endif
using System;
using UnityEngine;

namespace Architecture
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class MinMaxAttribute : PropertyAttribute
    {
        public float minLimit;
        public float maxLimit;

        public MinMaxAttribute(float minLimit, float maxLimit)
        {
            this.minLimit = minLimit;
            this.maxLimit = maxLimit;
        }
    }

#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(MinMaxAttribute))]
    public class MinMaxPropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if(property.propertyType != SerializedPropertyType.Vector2)
            {
                GUI.Label(position, $"{property.name} is not a Vector2");
                return;
            }
            MinMaxAttribute minMaxAttribute = (MinMaxAttribute)attribute;
            float minValue = property.vector2Value.x;
            float maxValue = property.vector2Value.y;
            position = EditorGUI.PrefixLabel(position, label);
            position.x -= 18f;
            position.width += 18f;

            Rect floatPosition = position;
            floatPosition.width = 66f;

            minValue = EditorGUI.FloatField(floatPosition, minValue);
            position.x += floatPosition.width;
            position.width -= floatPosition.width * 2;

            Rect minMaxPosition = position;
            minMaxPosition.width += 10f;
            EditorGUI.MinMaxSlider(minMaxPosition, ref minValue, ref maxValue, minMaxAttribute.minLimit, minMaxAttribute.maxLimit);

            floatPosition.x = position.x + position.width;
            maxValue = EditorGUI.FloatField(floatPosition, maxValue);

            if (minValue > maxValue) minValue = maxValue;

            property.vector2Value = new Vector2(minValue, maxValue);
            //EditorGUI.PropertyField(position, property, label, true);
        }
    }
#endif
}
