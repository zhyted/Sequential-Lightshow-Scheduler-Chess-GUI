using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
#if UNITY_EDITOR
#endif

public class Board : MonoBehaviour
{
    [SerializeField] private GameObject squarePrefab;


    [SerializeProperty("lightColor")]
    public Color _lightColor;

    [SerializeProperty("darkColor")]
    public Color _darkColor;
    public Color lightColor { get { return _lightColor; } set { ColorRefresh(); _lightColor = value; } }
    public Color darkColor { get { return _darkColor; } set { ColorRefresh(); _darkColor = value; } }

    public Color legalMovesColor;
    public Color selectColor;


    public Dictionary<Vector2, Renderer> squares = new Dictionary<Vector2, Renderer>();

    public void ColorRefresh()
    {
        foreach(var square in squares)
        {
            Color color = (square.Key[0] + square.Key[1]) % 2 != 0 ? lightColor : darkColor;
            square.Value.material.color = color;
        }

        //Debug.Log($"Light Colour: ({lightColor.r*255},{lightColor.g*255},{lightColor.b*255}) Dark Colour: ({darkColor.r * 255},{darkColor.g * 255},{darkColor.b * 255})");
    }

    void CreateBoard()
    {
        //generate board squares
        for (int file = 0; file < 8; file++)
        {
            for (int rank = 0; rank < 8; rank++)
            {
                Vector2 index = new Vector2(file+1, rank+1);

                if (squares.ContainsKey(index)) Destroy(squares[index]);

                Color color = (file + rank) % 2 != 0 ? lightColor : darkColor;

                squares[index] = DrawSquare(new Vector3(file+1, rank+1, 0), color);
            }
        }
    }
    Renderer DrawSquare(Vector3 position, Color color)
    {
        var Square = Instantiate(squarePrefab, position, Quaternion.identity).GetComponent<Renderer>();

        Square.material.color = color;
        return Square;
    }

    void Start()
    {
        CreateBoard();
    }
}




































//Serialized properties by arkano22 on unity forums
//
//
[System.AttributeUsage(System.AttributeTargets.Field)]
public class SerializeProperty : PropertyAttribute
{
    public string PropertyName { get; private set; }

    public SerializeProperty(string propertyName)
    {
        PropertyName = propertyName;
    }
}

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(SerializeProperty))]
public class SerializePropertyAttributeDrawer : PropertyDrawer
{
    private PropertyInfo propertyFieldInfo = null;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        UnityEngine.Object target = property.serializedObject.targetObject;

        // Find the property field using reflection, in order to get access to its getter/setter.
        if (propertyFieldInfo == null)
            propertyFieldInfo = target.GetType().GetProperty(((SerializeProperty)attribute).PropertyName,
                                                 BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (propertyFieldInfo != null)
        {

            // Retrieve the value using the property getter:
            object value = propertyFieldInfo.GetValue(target, null);

            // Draw the property, checking for changes:
            EditorGUI.BeginChangeCheck();
            value = DrawProperty(position, property.propertyType, propertyFieldInfo.PropertyType, value, label);

            // If any changes were detected, call the property setter:
            if (EditorGUI.EndChangeCheck() && propertyFieldInfo != null)
            {

                // Record object state for undo:
                Undo.RecordObject(target, "Inspector");

                // Call property setter:
                propertyFieldInfo.SetValue(target, value, null);
            }

        }
        else
        {
            EditorGUI.LabelField(position, "Error: could not retrieve property.");
        }
    }

    private object DrawProperty(Rect position, SerializedPropertyType propertyType, Type type, object value, GUIContent label)
    {
        switch (propertyType)
        {
            case SerializedPropertyType.Integer:
                return EditorGUI.IntField(position, label, (int)value);
            case SerializedPropertyType.Boolean:
                return EditorGUI.Toggle(position, label, (bool)value);
            case SerializedPropertyType.Float:
                return EditorGUI.FloatField(position, label, (float)value);
            case SerializedPropertyType.String:
                return EditorGUI.TextField(position, label, (string)value);
            case SerializedPropertyType.Color:
                return EditorGUI.ColorField(position, label, (Color)value);
            case SerializedPropertyType.ObjectReference:
                return EditorGUI.ObjectField(position, label, (UnityEngine.Object)value, type, true);
            case SerializedPropertyType.ExposedReference:
                return EditorGUI.ObjectField(position, label, (UnityEngine.Object)value, type, true);
            case SerializedPropertyType.LayerMask:
                return EditorGUI.LayerField(position, label, (int)value);
            case SerializedPropertyType.Enum:
                return EditorGUI.EnumPopup(position, label, (Enum)value);
            case SerializedPropertyType.Vector2:
                return EditorGUI.Vector2Field(position, label, (Vector2)value);
            case SerializedPropertyType.Vector3:
                return EditorGUI.Vector3Field(position, label, (Vector3)value);
            case SerializedPropertyType.Vector4:
                return EditorGUI.Vector4Field(position, label, (Vector4)value);
            case SerializedPropertyType.Rect:
                return EditorGUI.RectField(position, label, (Rect)value);
            case SerializedPropertyType.AnimationCurve:
                return EditorGUI.CurveField(position, label, (AnimationCurve)value);
            case SerializedPropertyType.Bounds:
                return EditorGUI.BoundsField(position, label, (Bounds)value);
            default:
                throw new NotImplementedException("Unimplemented propertyType " + propertyType + ".");
        }
    }

}
#endif
