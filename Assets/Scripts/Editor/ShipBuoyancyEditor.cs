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

        if (GUILayout.Button("Voxelize Hull"))
        {
            Undo.RecordObject(script, "Voxelize Hull");
            script.VoxelizeHull();
        }

        if (GUILayout.Button("Reset Center of Mass"))
        {
            Undo.RecordObject(script, "Reset Center of Mass");

            script._Rigidbody.ResetCenterOfMass();
        }

        EditorGUI.BeginDisabledGroup(!Application.isPlaying);
        if (GUILayout.Button("Sink"))
        {
            script._Density = 0.95f;
            script._DragCoefficient = 2f;
            script._AngularDragCoefficient = 0.5f;
        }
        EditorGUI.EndDisabledGroup();
    }
}
