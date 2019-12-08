using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class ShipBuoyancy : MonoBehaviour
{
    public Bounds ScanHullBounds;
    public float SamplesResolution = 0.25f;

    public Bounds OceanBounds;
    public float OceanResolution = 0.5f;
    public Ocean Ocean;

    public MeshCollider HullCollider;
    public string RaycastLayer;

    public List<BuoyancySphere> BuoyancySpheres;
    public float BuoyancyForce;
    public float GravityForce;
    public float Damping;

    public Rigidbody Rigidbody;

    [SerializeField]
    [HideInInspector]
    private List<Vector3> _hullSamplePositions;

    public ShipBuoyancy()
    {

    }

    void FixedUpdate()
    {
        foreach (BuoyancySphere sphere in BuoyancySpheres)
        {
            Vector3 samplePos = sphere.WorldPosition;
            float waterHeight = ComputeWaterHeight(samplePos, Time.time);
            float sphereBottom = samplePos.y - sphere.radius;

            //Vector3 forcePos = transform.position;
            Vector3 forcePos = samplePos;
            
            float volumeSubmerged = CalculateFilledSphereVolume(sphere.radius, waterHeight - sphereBottom);
            if (volumeSubmerged > 0)
            {
                Rigidbody.AddForceAtPosition(Vector3.up * BuoyancyForce * Time.fixedDeltaTime * volumeSubmerged, forcePos);
            }

            Rigidbody.AddForceAtPosition(Vector3.down * GravityForce * Time.fixedDeltaTime, forcePos);

            Vector3 v = Rigidbody.velocity;
            float density = sphereBottom < waterHeight ? 100f : 10f;

            Rigidbody.AddForce(-Damping * volumeSubmerged * v.sqrMagnitude * v.normalized);
        }


        //(-normalize(vel) * length(vel) * length(vel)) * (float DynPressure = (0.5 * (v * v) * dens * 100.0);

        //Vector3 velocity = Rigidbody.velocity;
        //velocity.y *= 1 - 0.1f * Time.fixedDeltaTime;
        //Rigidbody.velocity = velocity;
    }

    private void OnDrawGizmos()
    {
        // Check that this object is selected, or one of its children is selected
        Transform parent = Selection.activeGameObject?.transform;
        while (parent != null && parent != this.transform)
        {
            parent = parent.transform.parent;
        }
        if (parent == null)
        {
            return;
        }

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

        Gizmos.color = Color.red;

        if (false && OceanBounds != null)
        {
            Gizmos.matrix *= transform.localToWorldMatrix;
            Gizmos.DrawWireCube(OceanBounds.center, OceanBounds.extents * 2);
            Gizmos.matrix *= transform.localToWorldMatrix.inverse;

            int amountX = Mathf.FloorToInt(OceanBounds.extents.x * 2 / OceanResolution) + 1;
            int amountZ = Mathf.FloorToInt(OceanBounds.extents.z * 2 / OceanResolution) + 1;
            for (int z = 0; z < amountZ; z++)
            {
                for (int x = 0; x < amountX; x++)
                {
                    Vector3 offset = new Vector3(x * OceanResolution, 0, z * OceanResolution);
                    Vector3 localPosition = offset + OceanBounds.min;
                    Vector3 worldPosition = transform.localToWorldMatrix.MultiplyPoint3x4(localPosition);
                    Vector3 transformed = Ocean?.TransformVertex(worldPosition, Time.time) ?? worldPosition;
                    Gizmos.DrawSphere(transformed, 0.075f);
                }
            }

            Vector3 v = transform.position;
            v.y = ComputeWaterHeight(v, Application.isPlaying ? Time.time : 0);
            Gizmos.DrawCube(v, Vector3.one * 0.35f);
        }

        if (BuoyancySpheres != null)
        {
            foreach (BuoyancySphere sphere in BuoyancySpheres)
            {
                if (sphere.Initialized)
                    Gizmos.DrawWireSphere(sphere.WorldPosition, sphere.radius);
            }
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

    public float ComputeWaterHeight(Vector3 position, float time)
    {
        // Note: this is an estimate! However, it is usually very accurate.
        Vector3 xzOffset = Ocean.TransformVertex(position, time) - position;
        xzOffset.y = 0;

        return Ocean.TransformVertex(position - xzOffset, time).y;
    }

    /// <summary>
    /// Calculates the volume of a sphere partially filled from the bottom.
    /// </summary>
    /// <param name="radius">the radius of the sphere</param>
    /// <param name="filledAmount">the height of the sphere that is filled,
    /// measured from the bottom of the sphere. (this would range from 0 to 2*radius)</param>
    /// <returns></returns>
    public float CalculateFilledSphereVolume(float radius, float filledAmount)
    {
        filledAmount = Mathf.Clamp(filledAmount, 0, 2 * radius);

        // The derivation is a modified version of this
        // https://medium.com/@andrew.chamberlain/45434f2231e9
        // Just change the upper bound of the integral from "radius" to "-radius + filledAmount"

        // Use single letter variables to make the math formula less long
        float r = radius;
        float x = filledAmount;
        float r2 = r * r;
        float r3 = r2 * r;

        float sum = r - x;
        float factor = x * r2 - r3 / 3 - Mathf.Pow(sum, 3) / 3;
        float max = 4f / 3f * r3;
        return Mathf.PI * Mathf.Clamp(factor, 0, max);
    }

    [Serializable]
    public struct BuoyancySphere
    {
        public GameObject positionObject;
        public float radius;

        public Vector3 WorldPosition { get => positionObject.transform.position; }
        public bool Initialized { get => positionObject != null; }
    }
}
