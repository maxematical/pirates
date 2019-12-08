using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(CaravelModelController))]
public class CaravelModelControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (GUILayout.Button("Find Cannons"))
        {
            CaravelModelController script = (CaravelModelController)target;
            Transform transform = script.transform;

            List<CaravelModelController.CannonSettings> cannons = new List<CaravelModelController.CannonSettings>();
            foreach (Transform child in transform)
            {
                if (child.name.Contains("Cannon"))
                {
                    GameObject barrel = FindChildWithName(child.gameObject, "Barrel");
                    GameObject spawn = FindChildWithName(child.gameObject, "Spawn");

                    if (barrel == null)
                    {
                        Debug.Log("Could not find a barrel object for cannon " + child +
                            "; this cannon will be ignored");
                        continue;
                    }
                    if (spawn == null)
                    {
                        Debug.Log("Could not find a spawn position object for cannon " + child +
                           "; this cannon will be ignored");
                        continue;
                    }

                    var settings = new CaravelModelController.CannonSettings();
                    settings.Base = child.gameObject;
                    settings.Barrel = barrel;
                    settings.SpawnPos = spawn;
                    settings.IsRightSide = child.name.Contains("R");
                    cannons.Add(settings);
                }
            }

            script.Cannons = cannons;
        }
    }

    private GameObject FindChildWithName(GameObject obj, string nameContains)
    {
        foreach (Transform child in obj.transform)
        {
            if (child.name.Contains(nameContains))
                return child.gameObject;

            GameObject grandchild = FindChildWithName(child.gameObject, nameContains);
            if (grandchild != null)
                return grandchild;
        }

        return null;
    }
}
