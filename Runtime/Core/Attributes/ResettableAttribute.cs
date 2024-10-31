//https://vintay.medium.com/creating-custom-unity-attributes-readonly-d279e1e545c9
#if UNITY_EDITOR
using UnityEditor;
#endif
using System;
using UnityEngine;

namespace Architecture
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class ResettableAttribute : PropertyAttribute
    {
        public object resetValue;

        public ResettableAttribute(object resetValue, object resetValue1 = null, object resetValue2 = null, object resetValue3 = null)
        {
            if(resetValue is float)
            {
                if(resetValue1 != null && resetValue2 != null && resetValue3 != null)
                {
                    resetValue = new Vector4((float)resetValue, (float)resetValue1, (float)resetValue2, (float)resetValue3);
                }
                else if (resetValue1 != null && resetValue2 != null)
                {
                    resetValue = new Vector3((float)resetValue, (float)resetValue1, (float)resetValue2);
                }
                else if(resetValue1 != null)
                {
                    resetValue = new Vector2((float)resetValue, (float)resetValue1);
                }
            }
            this.resetValue = resetValue;
        }
    }

#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(ResettableAttribute))]
    public class ResettablePropertyDrawer : PropertyDrawer
    {
        const float RESET_BUTTON_WIDTH = 50f;
        const float RESET_BUTTON_MARGIN_LEFT = 2f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            position.width -= RESET_BUTTON_WIDTH + RESET_BUTTON_MARGIN_LEFT;
            EditorGUI.PropertyField(position, property, label, true);
            position.x += position.width + RESET_BUTTON_MARGIN_LEFT;
            position.width = RESET_BUTTON_WIDTH;

            object resetValue = (attribute as ResettableAttribute).resetValue;

            bool isResetValue = false;
            bool invalid = false;

            if (property.propertyType == SerializedPropertyType.Generic) invalid = true;
            else if (property.propertyType == SerializedPropertyType.Integer) isResetValue = property.intValue == (int)resetValue;
            else if (property.propertyType == SerializedPropertyType.Boolean) isResetValue = property.boolValue == (bool)resetValue;
            else if (property.propertyType == SerializedPropertyType.Float) isResetValue = property.floatValue == (float)resetValue;
            else if (property.propertyType == SerializedPropertyType.String) isResetValue = property.stringValue == (string)resetValue;
            else if (property.propertyType == SerializedPropertyType.Color) isResetValue = property.colorValue == (Color)resetValue;
            else if (property.propertyType == SerializedPropertyType.ObjectReference) invalid = true;
            else if (property.propertyType == SerializedPropertyType.LayerMask) isResetValue = property.intValue == (LayerMask)resetValue;
            else if (property.propertyType == SerializedPropertyType.Enum) isResetValue = ((System.Enum)resetValue).ToString().Equals(property.enumNames[property.enumValueIndex]);
            else if (property.propertyType == SerializedPropertyType.Vector2) isResetValue = property.vector2Value == (Vector2)resetValue;
            else if (property.propertyType == SerializedPropertyType.Vector3) isResetValue = property.vector3Value == (Vector3)resetValue;
            else if (property.propertyType == SerializedPropertyType.Vector4) isResetValue = property.vector4Value == (Vector4)resetValue;
            else if (property.propertyType == SerializedPropertyType.Vector4) isResetValue = property.vector4Value == (Vector4)resetValue;
            else if (property.propertyType == SerializedPropertyType.Rect) isResetValue = property.rectValue == (Rect)resetValue;
            else if (property.propertyType == SerializedPropertyType.ArraySize) invalid = true;
            else if (property.propertyType == SerializedPropertyType.Character) isResetValue = property.stringValue == (string)resetValue;
            else if (property.propertyType == SerializedPropertyType.AnimationCurve) invalid = true;
            else if (property.propertyType == SerializedPropertyType.Bounds) isResetValue = property.boundsValue == (Bounds)resetValue;
            else if (property.propertyType == SerializedPropertyType.Gradient) isResetValue = property.gradientValue == (Gradient)resetValue;
            else if (property.propertyType == SerializedPropertyType.Quaternion) isResetValue = property.quaternionValue == (Quaternion)resetValue;
            else if (property.propertyType == SerializedPropertyType.ExposedReference) invalid = true;
            else if (property.propertyType == SerializedPropertyType.FixedBufferSize) invalid = true;
            else if (property.propertyType == SerializedPropertyType.Vector2Int) isResetValue = property.vector2IntValue == (Vector2Int)resetValue;
            else if (property.propertyType == SerializedPropertyType.Vector3Int) isResetValue = property.vector3IntValue == (Vector3Int)resetValue;
            else if (property.propertyType == SerializedPropertyType.RectInt) isResetValue = property.rectIntValue == (RectInt)resetValue;
            else if (property.propertyType == SerializedPropertyType.BoundsInt) isResetValue = property.boundsIntValue == (BoundsInt)resetValue;
            else if (property.propertyType == SerializedPropertyType.ManagedReference) invalid = true;
            else if (property.propertyType == SerializedPropertyType.Hash128) invalid = true;
            else if (property.propertyType == SerializedPropertyType.RenderingLayerMask) isResetValue = property.uintValue == (RenderingLayerMask)resetValue;
            else invalid = true;

            if (invalid) Debug.LogError($"Invalid property type for resettable property named {property.name} at {property.propertyPath}");


            GUI.enabled = !isResetValue && !invalid;
            if(GUI.Button(position, "Reset"))
            {
                if (property.propertyType == SerializedPropertyType.Generic) invalid = true;
                else if (property.propertyType == SerializedPropertyType.Integer) property.intValue = (int)resetValue;
                else if (property.propertyType == SerializedPropertyType.Boolean) property.boolValue = (bool)resetValue;
                else if (property.propertyType == SerializedPropertyType.Float) property.floatValue = (float)resetValue;
                else if (property.propertyType == SerializedPropertyType.String) property.stringValue = (string)resetValue;
                else if (property.propertyType == SerializedPropertyType.Color) property.colorValue = (Color)resetValue;
                else if (property.propertyType == SerializedPropertyType.LayerMask) property.intValue = (LayerMask)resetValue;
                else if (property.propertyType == SerializedPropertyType.Enum)
                {
                    int index = Array.IndexOf(property.enumNames, ((System.Enum)resetValue).ToString());
                    if(index < 0) Debug.LogError($"Couldn't find enum match for property {property.name} and reset value {((System.Enum)resetValue)}");
                    else property.enumValueIndex = index;
                }
                else if (property.propertyType == SerializedPropertyType.Vector2) property.vector2Value = (Vector2)resetValue;
                else if (property.propertyType == SerializedPropertyType.Vector3) property.vector3Value = (Vector3)resetValue;
                else if (property.propertyType == SerializedPropertyType.Vector4) property.vector4Value = (Vector4)resetValue;
                else if (property.propertyType == SerializedPropertyType.Vector4) property.vector4Value = (Vector4)resetValue;
                else if (property.propertyType == SerializedPropertyType.Rect) property.rectValue = (Rect)resetValue;
                else if (property.propertyType == SerializedPropertyType.Character) property.stringValue = (string)resetValue;
                else if (property.propertyType == SerializedPropertyType.Bounds) property.boundsValue = (Bounds)resetValue;
                else if (property.propertyType == SerializedPropertyType.Gradient) property.gradientValue = (Gradient)resetValue;
                else if (property.propertyType == SerializedPropertyType.Quaternion) property.quaternionValue = (Quaternion)resetValue;
                else if (property.propertyType == SerializedPropertyType.Vector2Int) property.vector2IntValue = (Vector2Int)resetValue;
                else if (property.propertyType == SerializedPropertyType.Vector3Int) property.vector3IntValue = (Vector3Int)resetValue;
                else if (property.propertyType == SerializedPropertyType.RectInt) property.rectIntValue = (RectInt)resetValue;
                else if (property.propertyType == SerializedPropertyType.BoundsInt) property.boundsIntValue = (BoundsInt)resetValue;
                else if (property.propertyType == SerializedPropertyType.RenderingLayerMask) property.uintValue = (RenderingLayerMask)resetValue;
            }
            GUI.enabled = true;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }
    }
#endif
}
