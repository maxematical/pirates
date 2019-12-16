using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VoxelSystem;

public class ShipBuoyancy : MonoBehaviour
{
    private const float EPSILON = 0.001f;

    [Header("Components")]
    public WaterPatch _WaterPatch;
    public Rigidbody _Rigidbody;

    [Header("Center of Mass (optional)")]
    public GameObject _CustomCenterOfMass;

    [Header("Voxelization Settings")]
    public MeshFilter _Hull;
    public int _VoxelResolution;
    public float _SampleSizeMultiplier;
    
    [Header("Physics Settings")]
    public float _Gravity;
    public float _Density;
    public float _DragCoefficient;
    public float _AngularDragCoefficient;
    public float _AverageWidth;

    [Tooltip("The amount of time to estimate y-position in the past. Larger values will make the slamming action more spread out over time.")]
    public float _SlamEstimationTime;
    [Tooltip("The multiplier for the slamming force.")]
    public float _SlammingForce;

    [SerializeField]
    [HideInInspector]
    private List<Vector3> _samplePositions;

    [SerializeField]
    [HideInInspector]
    private float _sampleRadius;

    private Vector3 _gizmosBuoyancyCenter;
    private Vector3 _gizmosBuoyancyForce;

    private void Start()
    {
        if (_CustomCenterOfMass != null)
        {
            Vector3 worldPos = _CustomCenterOfMass.transform.position;
            Matrix4x4 mat = transform.worldToLocalMatrix;
            _Rigidbody.centerOfMass = mat.MultiplyPoint3x4(worldPos);
        }
    }

    void FixedUpdate()
    {
        _WaterPatch._Center = transform.position;
        _WaterPatch.UpdatePatch(Time.time);

        float buoyantTotalWeight = 0;
        Vector3 buoyantCenter = Vector3.zero;
        Vector3 buoyantForce = Vector3.zero;

        float dragTotalWeight = 0;
        Vector3 dragCenter = Vector3.zero;
        Vector3 dragForce = Vector3.zero;

        float slammingTotalWeight = 0;
        Vector3 slammingCenter = Vector3.zero;
        Vector3 slammingForce = Vector3.zero;
        
        Vector3 totalTorque = Vector3.zero;

        foreach (Vector3 localCenter in _samplePositions)
        {
            Vector3 sphereCenter = transform.localToWorldMatrix.MultiplyPoint3x4(localCenter);

            float waterHeight = ComputeWaterHeight(sphereCenter, Time.time);
            float sphereBottom = sphereCenter.y - _sampleRadius;
            Vector3 filledCenter = CalculateFilledSphereCenter(sphereCenter, _sampleRadius, waterHeight - sphereBottom);

            float filledVolume = CalculateFilledSphereVolume(_sampleRadius, waterHeight - sphereBottom);
            float sphereVolume = CalculateFilledSphereVolume(_sampleRadius, 2 * _sampleRadius);
            float filledRatio = filledVolume / sphereVolume;

            if (filledVolume > 0)
            {
                buoyantForce += Vector3.up * _Gravity * _Density * filledRatio;
                buoyantCenter += filledCenter * filledRatio;
                buoyantTotalWeight += filledRatio;
            }

            Vector3 sphereVelocity = _Rigidbody.velocity + Vector3.Cross(_Rigidbody.angularVelocity, sphereCenter - _Rigidbody.worldCenterOfMass);
            Vector3 waterVelocity = Vector3.zero;

            Vector3 estimatedLastPosition = sphereCenter - sphereVelocity * _SlamEstimationTime;
            float estimatedLastBottom = estimatedLastPosition.y - _sampleRadius;
            if (waterHeight >= sphereBottom && waterHeight < estimatedLastBottom)
            {
                Vector3 slam = Vector3.up * Mathf.Sqrt(-sphereVelocity.y) * _SlammingForce;
                slammingForce += slam;
                slammingTotalWeight += filledVolume;
                slammingCenter += filledCenter * filledVolume;
            }

            dragForce += _DragCoefficient * _Rigidbody.mass * filledRatio * (waterVelocity - sphereVelocity);
            dragCenter += filledCenter * filledRatio;
            dragTotalWeight += filledRatio;

            totalTorque += _AngularDragCoefficient * _Rigidbody.mass * filledRatio * _AverageWidth * _AverageWidth * -_Rigidbody.angularVelocity;
        }

        // Apply buoyant force
        if (buoyantTotalWeight > 0 && _samplePositions.Count > 0)
        {
            // The center of the force is the average position
            buoyantCenter /= buoyantTotalWeight;
            // Note that we do not divide by buoyantCount here because then buoyantForce would always have a magnitude
            // of (_Gravity*_Density). Instead we divide by BuoyancySpheres.Count, so that its magnitude varies but is
            // never greater than (_Gravity*_Density).
            buoyantForce /= _samplePositions.Count;
            _Rigidbody.AddForceAtPosition(buoyantForce, buoyantCenter);
        }

        // Apply slamming force
        if (slammingTotalWeight > 0 && _samplePositions.Count > 0)
        {
            slammingCenter /= slammingTotalWeight;
            slammingForce /= _samplePositions.Count;
            _Rigidbody.AddForceAtPosition(slammingForce, slammingCenter);
        }

        // Apply drag force
        if (dragTotalWeight > 0 && _samplePositions.Count > 0)
        {
            dragCenter /= dragTotalWeight;
            dragForce /= _samplePositions.Count;
            _Rigidbody.AddForceAtPosition(dragForce, dragCenter);
        }

        // Apply angular drag
        if (_samplePositions.Count > 0)
        {
            totalTorque /= _samplePositions.Count;
            _Rigidbody.AddTorque(totalTorque);
        }

        // Apply gravity
        _Rigidbody.AddForce(Vector3.down * _Gravity);

        // Save forces for gizmos
        _gizmosBuoyancyCenter = buoyantCenter;
        _gizmosBuoyancyForce = buoyantForce;

        //_gizmosBuoyancyCenter = slammingCenter;
        //_gizmosBuoyancyForce = slammingForce;
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


        // Draw water levels
        Gizmos.color = Color.red;
        if (_samplePositions != null)
        {
            float time = Application.isPlaying ? Time.time : 0;
            foreach (Vector3 localCenter in _samplePositions)
            {
                Vector3 sphereCenter = transform.localToWorldMatrix.MultiplyPoint3x4(localCenter);
                float waterHeight = ComputeWaterHeight(sphereCenter, time);

                Vector3 waterLevelCenter = sphereCenter;
                waterLevelCenter.y = waterHeight;
                Vector3 waterLevelSize = Vector3.one * _sampleRadius * 2;
                waterLevelSize.y = 0;
                Gizmos.DrawCube(waterLevelCenter, waterLevelSize);

                Gizmos.DrawWireSphere(sphereCenter, _sampleRadius);

                Vector3 filledCenter = CalculateFilledSphereCenter(sphereCenter, _sampleRadius, waterHeight - sphereCenter.y + _sampleRadius);
                Gizmos.DrawSphere(filledCenter, 0.25f * _sampleRadius);
            }
        }

        // Draw things from FixedUpdate
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(_gizmosBuoyancyCenter, 0.2f);
        Gizmos.DrawRay(_gizmosBuoyancyCenter, _gizmosBuoyancyForce);

        Gizmos.color = Color.magenta;
        Gizmos.DrawSphere(_Rigidbody.worldCenterOfMass, 0.3f);
    }

    public void VoxelizeHull()
    {
        Mesh mesh = _Hull.sharedMesh;

        // Apply transform to the hull mesh
        Matrix4x4 mat = this.transform.worldToLocalMatrix * _Hull.gameObject.transform.localToWorldMatrix;
        Vector3[] transformedVertices = mesh.vertices;
        for (int i = 0; i < transformedVertices.Length; i++)
        {
            transformedVertices[i] = mat.MultiplyPoint3x4(transformedVertices[i]);
        }

        Mesh transformedMesh = new Mesh();
        transformedMesh.vertices = transformedVertices;
        transformedMesh.triangles = mesh.triangles;
        transformedMesh.normals = mesh.normals;

        // Voxelize the mesh
        List<Voxel_t> voxels;
        CPUVoxelizer.Voxelize(transformedMesh, _VoxelResolution, out voxels, out float voxelSize);

        // Save voxels to samples
        Vector3 sampleCenter = Vector3.zero;
        _samplePositions.Clear();
        foreach (Voxel_t voxel in voxels)
        {
            _samplePositions.Add(voxel.position);
            sampleCenter += voxel.position;
        }

        // Save sample radius and center
        _sampleRadius = voxelSize * _SampleSizeMultiplier * 2.4f;

        sampleCenter /= _samplePositions.Count;
        _Rigidbody.centerOfMass = transform.worldToLocalMatrix.MultiplyPoint3x4(sampleCenter);

        // Calculate average length along axis
        transformedMesh.RecalculateBounds();
        Vector3 size = transformedMesh.bounds.size;
        _AverageWidth = (size.x + size.y + size.z) / 3f;
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
}
