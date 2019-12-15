using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(ShipBuoyancy))]
public class ShipBuoyancyEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        ShipBuoyancy script = (ShipBuoyancy)target;

        // Later we can add a fancy layer mask dropdown like this:
        // https://answers.unity.com/questions/42996/how-to-create-layermask-field-in-a-custom-editorwi.html

        bool disableComputeSamples = false;
        if (LayerMask.NameToLayer(script.RaycastLayer) < 0)
        {
            GUILayout.Label("WARNING: No layer called '" + script.RaycastLayer + "'");
            disableComputeSamples = true;
        }
        else if (script.RaycastLayer == "")
        {
            GUILayout.Label("WARNING: Layer field is empty");
            disableComputeSamples = true;
        }

        EditorGUI.BeginDisabledGroup(disableComputeSamples);
        if (GUILayout.Button("Compute Samples"))
        {
            Undo.RecordObject(script, "Compute Hull Samples");
            script.ComputeHullSamples();
        }
        EditorGUI.EndDisabledGroup();

        if (GUILayout.Button("Voxelize Hull"))
        {
            Undo.RecordObject(script, "Voxelize Hull");
            script.VoxelizeHull();
        }
    }
}
