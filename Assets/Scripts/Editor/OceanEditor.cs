using UnityEditor;
using UnityEngine;

//[CustomEditor(typeof(Ocean))]
public class OceanEditor : Editor
{
    private SerializedProperty[] _waveSteepnessProperties;

    private void OnEnable()
    {
        var wavesProperty = serializedObject.FindProperty("Waves");

        _waveSteepnessProperties = new SerializedProperty[wavesProperty.arraySize];
        for (int i = 0; i < wavesProperty.arraySize; i++)
        {
            var steepnessProperty = wavesProperty.GetArrayElementAtIndex(i).FindPropertyRelative("Steepness");
            _waveSteepnessProperties[i] = steepnessProperty;
        }
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        //_waveSteepnessProperties[0].displayName = "X";

        serializedObject.Update();
        EditorGUILayout.PropertyField(_waveSteepnessProperties[0]);
        serializedObject.ApplyModifiedProperties();
        EditorGUILayout.LabelField("label");
    }
}
