using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(MeshBuoyancy))]
public class MeshBuoyancyEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        MeshBuoyancy script = (MeshBuoyancy)target;
        Quaternion rot = script.transform.rotation;
        Matrix4x4 local2World = script.transform.localToWorldMatrix;

        if (GUILayout.Button("Push From Back"))
        {
            Vector3 force = Vector3.forward * 5f + Vector3.up;
            Vector3 position = Vector3.back * 1.35f;
            script.GetComponent<Rigidbody>().AddForceAtPosition(rot * force * 5f, local2World.MultiplyPoint3x4(position));
        }

        if (GUILayout.Button("Push From Front"))
        {
            Vector3 force = Vector3.back * 5f + Vector3.up;
            Vector3 position = Vector3.forward * 1.6f;
            script.GetComponent<Rigidbody>().AddForceAtPosition(rot * force * 5f, local2World.MultiplyPoint3x4(position));
        }

        if (GUILayout.Button("Push from Left Side"))
        {
            Vector3 force = Vector3.right * 5f + Vector3.up;
            Vector3 position = Vector3.left * 0.4f;
            script.GetComponent<Rigidbody>().AddForceAtPosition(rot * force * 5f, local2World.MultiplyPoint3x4(position));
        }
    }
}
