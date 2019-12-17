using UnityEngine;
using UnityEditor;

//[CustomPropertyDrawer(typeof(Ocean.WaveSettings))]
public class WaveSettingsDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        //base.OnGUI(position, property, label);
        //EditorGUI.LabelField(position, "hello");
        EditorGUI.PropertyField(position, property, label, true);

        Rect labelRect = new Rect(position.x, position.y + 10, position.width, position.height);
        EditorGUI.LabelField(labelRect, "Hello");
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property) + 20;
    }
}
