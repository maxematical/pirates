using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VoxelSystem;

public class ShipBuoyancy : MonoBehaviour
{
    private const float EPSILON = 0.001f;

    public Bounds ScanHullBounds;
    public float SamplesResolution = 0.25f;
    
    public MeshCollider HullCollider;
    public string RaycastLayer;

    public WaterPatch _WaterPatch;

    public Mesh _HullMesh;
    public int _voxelResolution;
    public float _voxelSphereSize;

    public List<BuoyancySphere> BuoyancySpheres;
    public float _Gravity;
    public float _Density;
    public float _DragCoefficient;
    public float _AngularDragCoefficient;
    public float _AverageWidth;

    private float _totalVolume;

    public Rigidbody Rigidbody;

    [SerializeField]
    [HideInInspector]
    private List<Vector3> _hullSamplePositions;

    private GameObject _emptyGameobject;

    private Vector3 _gizmosForceCenter;
    private Vector3 _gizmosTotalForce;

    private void Start()
    {
        _emptyGameobject = new GameObject();

        BuoyancySpheres.Clear();
        foreach (Vector3 samplePosition in _hullSamplePositions)
        {
            GameObject obj = Instantiate(_emptyGameobject, transform);
            obj.transform.localPosition = samplePosition;

            BuoyancySphere sphere = new BuoyancySphere();
            sphere.positionObject = obj;
            sphere.radius = _voxelSphereSize;
            BuoyancySpheres.Add(sphere);
        }

        _totalVolume = 0;
        foreach (BuoyancySphere sphere in BuoyancySpheres)
        {
            _totalVolume += CalculateFilledSphereVolume(sphere.radius, 2 * sphere.radius);
        }
    }

    void FixedUpdate()
    {
        _WaterPatch._Center = transform.position;
        _WaterPatch.UpdatePatch(Time.time);

        int forceCount = 0;
        Vector3 forceCenter = Vector3.zero;
        Vector3 totalForce = Vector3.zero;

        int torqueCount = 0;
        Vector3 torqueCenter = Vector3.zero;
        Vector3 totalTorque = Vector3.zero;

        foreach (BuoyancySphere sphere in BuoyancySpheres)
        {
            Vector3 sphereCenter = sphere.WorldPosition;
            float waterHeight = ComputeWaterHeight(sphereCenter, Time.time);
            float sphereBottom = sphereCenter.y - sphere.radius;
            Vector3 filledCenter = CalculateFilledSphereCenter(sphereCenter, sphere.radius, waterHeight - sphereBottom);

            float filledVolume = CalculateFilledSphereVolume(sphere.radius, waterHeight - sphereBottom);
            float sphereVolume = CalculateFilledSphereVolume(sphere.radius, 2 * sphere.radius);
            float filledRatio = filledVolume / sphereVolume;

            if (filledVolume > 0)
            {
                totalForce += Vector3.up * _Gravity * _Density * filledRatio;
                forceCenter += filledCenter;
                forceCount++;
            }

            //totalForce += Vector3.down * _Gravity * forceProportion;
            //forceCenter += filledCenter;
            //forceCount++;

            Vector3 v = Rigidbody.velocity + Vector3.Cross(Rigidbody.angularVelocity, sphereCenter - Rigidbody.worldCenterOfMass);//not causing shaking

            Vector3 waterCurrentVelocity = Vector3.zero;
            //Rigidbody.AddForceAtPosition(_DragCoefficient * Rigidbody.mass * filledRatio * (waterCurrentVelocity - v), filledCenter);
            totalForce += _DragCoefficient * Rigidbody.mass * filledRatio * (waterCurrentVelocity - v);
            forceCenter += filledCenter;
            forceCount++;

            totalTorque += _AngularDragCoefficient * Rigidbody.mass * filledRatio * _AverageWidth * _AverageWidth * -Rigidbody.angularVelocity;
            torqueCenter += filledCenter;
            torqueCount++;
        }

        Rigidbody.AddForce(Vector3.down * _Gravity);

        if (forceCount > 0)
        {
            forceCenter /= forceCount;
            totalForce /= BuoyancySpheres.Count;
            Rigidbody.AddForce(totalForce);

            Vector3 x = forceCenter - Rigidbody.worldCenterOfMass;
            Vector3 torqueFromForces = x.sqrMagnitude > 0.1f || true ? Vector3.Cross(x, totalForce) : Vector3.zero;
            Rigidbody.AddTorque(torqueFromForces);
        }
        //Rigidbody.AddForceAtPosition(totalForce, forceCenter);

        if (torqueCount > 0)
        {
            torqueCenter /= torqueCount;
            totalTorque /= BuoyancySpheres.Count;
            Rigidbody.AddTorque(totalTorque);
        }

        _gizmosForceCenter = forceCenter;
        _gizmosTotalForce = totalForce - Vector3.down * _Gravity;
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

        // Other gizmos
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

        if (BuoyancySpheres != null)
        {
            float time = Application.isPlaying ? Time.time : 0;
            foreach (BuoyancySphere sphere in BuoyancySpheres)
            {
                if (sphere.Initialized)
                {
                    float waterHeight = ComputeWaterHeight(sphere.WorldPosition, time);

                    Vector3 waterLevelCenter = sphere.WorldPosition;
                    waterLevelCenter.y = waterHeight;
                    Vector3 waterLevelSize = Vector3.one * sphere.radius * 2;
                    waterLevelSize.y = 0;
                    Gizmos.DrawCube(waterLevelCenter, waterLevelSize);
                    
                    Gizmos.DrawWireSphere(sphere.WorldPosition, sphere.radius);

                    Gizmos.DrawSphere(CalculateFilledSphereCenter(sphere.WorldPosition, sphere.radius, waterHeight - sphere.WorldPosition.y + sphere.radius), 0.075f);
                }
            }
        }

        // Draw things from FixedUpdate
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(_gizmosForceCenter, 0.1f);
        Gizmos.DrawRay(_gizmosForceCenter, _gizmosTotalForce);
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

    public void VoxelizeHull()
    {
        // Apply transform to the hull mesh
        Matrix4x4 mat = this.transform.worldToLocalMatrix * HullCollider.gameObject.transform.localToWorldMatrix;
        Vector3[] transformedVertices = _HullMesh.vertices;
        for (int i = 0; i < transformedVertices.Length; i++)
        {
            transformedVertices[i] = mat.MultiplyPoint3x4(transformedVertices[i]);
        }

        Mesh transformedMesh = new Mesh();
        transformedMesh.vertices = transformedVertices;
        transformedMesh.triangles = _HullMesh.triangles;
        transformedMesh.normals = _HullMesh.normals;

        // Voxelize the mesh
        List<Voxel_t> voxels;
        CPUVoxelizer.Voxelize(transformedMesh, _voxelResolution, out voxels, out _);

        // Save voxels to samples
        _hullSamplePositions.Clear();
        foreach (Voxel_t voxel in voxels)
        {
            _hullSamplePositions.Add((voxel.position));
        }
    }

    public float ComputeWaterHeight(Vector3 position, float time)
    {
        return _WaterPatch?.GetWaterHeight(position.x, position.z) ?? 0;
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
        float R = radius;
        float h = filledAmount;
        float R2 = R * R;
        float R3 = R2 * R;
        float d = h - R;

        // The volume as a multiple of pi
        float factor = R2 * h - R3 / 3 - (d * d * d) / 3;
        
        // Return the actual volume
        return Mathf.PI * factor;
    }

    public Vector3 CalculateFilledSphereCenter(Vector3 center, float radius, float filledAmount)
    {
        filledAmount = Mathf.Clamp(filledAmount, 0, 2 * radius);

        float volume = CalculateFilledSphereVolume(radius, filledAmount);

        float R = radius;
        float V = volume;
        float h = filledAmount;
        float d = h - R;

        float R2 = R * R;
        float R3 = R2 * R;
        float R4 = R3 * R;

        float d2 = d * d;
        float d3 = d2 * d;
        float d4 = d3 * d;

        float t;

        if (Eq(V, 0) || R == 0)
        {
            t = 0;
        }
        else
        {
            float coeff = Mathf.PI * 0.5f;
            float div = R * V;
            //Debug.Log("div = " + div);
            t = coeff * (h * R3 - R * d3 / 3f - R4 / 3f + 0.5f * R2 * d2 - 0.5f * R4 - 0.25f * d4 + 0.25f * R4) / div;
        }

        //Debug.Log("t: " + t);
        //Debug.Log($"R:{R}, V:{V}, h:{h}, d:{d}");

        Vector3 volumeCenter = center;
        volumeCenter.y += 2 * R * t - R;
        return volumeCenter;
    }

    private static bool Eq(float x, float y)
    {
        return Mathf.Abs(x - y) < EPSILON;
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
