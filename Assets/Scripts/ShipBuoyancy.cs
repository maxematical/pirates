using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShipBuoyancy : MonoBehaviour
{
    public Bounds ScanHullBounds;
    public float SamplesResolution = 0.25f;

    public MeshCollider HullCollider;
    public string RaycastLayer;

    [SerializeField]
    [HideInInspector]
    private List<Vector3> _hullSamplePositions;

    public ShipBuoyancy()
    {

    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blue;

        if (_hullSamplePositions != null)
        {
            Gizmos.matrix *= transform.localToWorldMatrix;
            foreach (Vector3 v in _hullSamplePositions)
            {
                Gizmos.DrawSphere(v, 0.05f);
            }
            Gizmos.matrix *= transform.localToWorldMatrix.inverse;
        }

        if (ScanHullBounds != null)
        {
            Gizmos.matrix *= transform.localToWorldMatrix;
            Gizmos.DrawWireCube(ScanHullBounds.center, ScanHullBounds.extents * 2);
            Gizmos.matrix *= transform.localToWorldMatrix.inverse;
        }
    }

    public void ComputeHullSamples()
    {
        int mask = LayerMask.GetMask(RaycastLayer);
        int prevLayer = HullCollider.gameObject.layer;
        bool prevEnabled = HullCollider.enabled;
        HullCollider.gameObject.layer = LayerMask.NameToLayer(RaycastLayer);
        HullCollider.enabled = true;

        int amountX = Mathf.FloorToInt(ScanHullBounds.extents.x * 2 / SamplesResolution) + 1;
        int amountZ = Mathf.FloorToInt(ScanHullBounds.extents.z * 2 / SamplesResolution) + 1;
        _hullSamplePositions = new List<Vector3>(amountX * amountZ);
        for (int z = 0; z < amountZ; z++)
        {
            for (int x = 0; x < amountX; x++)
            {
                Vector3 offset = new Vector3(x * SamplesResolution, 0, z * SamplesResolution);
                Vector3 localPosition = offset + ScanHullBounds.min;
                Vector3 worldPosition = transform.localToWorldMatrix.MultiplyPoint3x4(localPosition);

                // Perform raycast up from this position to try to hit the hull
                RaycastHit hit;
                if (Physics.Raycast(worldPosition, transform.up, out hit, ScanHullBounds.extents.y * 2, mask))
                {
                    Vector3 localHit = transform.worldToLocalMatrix.MultiplyPoint3x4(hit.point);
                    _hullSamplePositions.Add(localHit);
                }
            }
        }

        HullCollider.gameObject.layer = prevLayer;
        HullCollider.enabled = prevEnabled;
    }
}
