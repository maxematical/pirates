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
    }
}
