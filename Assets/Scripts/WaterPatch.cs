using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaterPatch : MonoBehaviour
{
    public Ocean _Ocean;
    public int _VertexWidth;
    public int _VertexLength;

    public Vector3 _Center;
    public float _Scale;

    public float WorldWidth { get => (_VertexWidth - 1) * _Scale; }
    public float WorldLength { get => (_VertexLength - 1) * _Scale; }

    public float SqrDrift {
        get
        {
            Vector3 diff = _Center - _dataCenter;
            diff.y = 0;
            return diff.sqrMagnitude;
        }
    }

    public float LastUpdateTime { get; private set; }

    // EVENLY SPACED vertices representing the height at a position
    private float[] _heightValues;
    private Vector3 _dataCenter;

    private void Start()
    {
        _heightValues = new float[_VertexWidth * _VertexLength];
        _dataCenter = Vector3.zero;
        LastUpdateTime = 0;
    }

    public void UpdatePatch(float time)
    {
        float timeStart = Time.realtimeSinceStartup;

        _dataCenter = _Center;
        for (int z = 0; z < _VertexLength; z++)
        {
            for (int x = 0; x < _VertexWidth; x++)
            {
                var (worldX, worldZ) = IndexToWorld(x, z);

                int index = x + z * _VertexWidth;
                _heightValues[index] = EstimateHeight(new Vector3(worldX, 0, worldZ), time);
            }
        }

        LastUpdateTime = time;

        //Debug.Log(1000 * (Time.realtimeSinceStartup - timeStart) + "ms to update patch");
    }

    public float GetWaterHeight(float x, float z)
    {
        CheckPatchInitialized();

        // Use bilinear interpolation to sample height values
        int xInt = Mathf.FloorToInt(x);
        int zInt = Mathf.FloorToInt(z);
        var (xIdx, zIdx) = WorldToIndex(x, z);

        if (!Application.isPlaying && (xIdx < 0 || xIdx >= _VertexWidth || zIdx < 0 || zIdx >= _VertexLength))
            return 0;

        float h00 = _heightValues[xIdx + zIdx * _VertexWidth];
        float h01 = zIdx + 1 < _VertexLength ? _heightValues[xIdx + (zIdx + 1) * _VertexWidth] : h00;
        float h10 = xIdx + 1 < _VertexWidth ? _heightValues[(xIdx + 1) + zIdx * _VertexWidth] : h00;
        float h11 = xIdx + 1 < _VertexWidth && zIdx + 1 < _VertexLength ? _heightValues[(xIdx + 1) + (zIdx + 1) * _VertexWidth] : h01;

        float xF = x - xInt;
        float zF = z - zInt;

        return h00 * (1 - xF) * (1 - zF) + h10 * xF * (1 - zF) + h01 * (1 - xF) * zF + h11 * xF * zF;
    }

    public void OnDrawGizmosSelected()
    {
        CheckPatchInitialized();

        if (_VertexWidth <= 1 || _VertexLength <= 1)
            return;

        Vector3[] vertices = new Vector3[_VertexWidth * _VertexLength];
        Vector3[] normals = new Vector3[_VertexWidth * _VertexLength];

        int[] triangles = new int[6 * (_VertexWidth - 1) * (_VertexLength - 1)];
        int nextTriangleIndex = 0;

        for (int z = 0; z < _VertexLength; z++)
        {
            for (int x = 0; x < _VertexWidth; x++)
            {
                int index = x + z * _VertexWidth;
                var (worldX, worldZ) = IndexToWorld(x, z);
                float worldY = _heightValues[index];
                
                vertices[index] = new Vector3(worldX, worldY, worldZ);
                normals[index] = Vector3.up;

                if (x + 1 < _VertexWidth && z + 1 < _VertexLength)
                {
                    triangles[nextTriangleIndex++] = index;
                    triangles[nextTriangleIndex++] = index + 1;
                    triangles[nextTriangleIndex++] = index + _VertexWidth;

                    triangles[nextTriangleIndex++] = index + 1;
                    triangles[nextTriangleIndex++] = index + _VertexWidth + 1;
                    triangles[nextTriangleIndex++] = index + _VertexWidth;
                }
            }
        }

        Mesh mesh = new Mesh();
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.triangles = triangles;

        Gizmos.color = Color.blue;
        Gizmos.DrawWireMesh(mesh);
    }

    private (float, float) IndexToWorld(int indexX, int indexZ)
    {
        float worldX = indexX * _Scale + _dataCenter.x - WorldWidth * 0.5f;
        float worldZ = indexZ * _Scale + _dataCenter.z - WorldLength * 0.5f;
        return (worldX, worldZ);
    }

    private (int, int) WorldToIndex(float worldX, float worldZ)
    {
        int indexX = Mathf.FloorToInt((worldX - _dataCenter.x + WorldWidth * 0.5f) / _Scale);
        int indexZ = Mathf.FloorToInt((worldZ - _dataCenter.z + WorldLength * 0.5f) / _Scale);
        return (indexX, indexZ);
    }

    private float EstimateHeight(Vector3 position, float time)
    {
        //return 0;

        // Note: this is an estimate! However, it is usually very accurate.
        Vector3 xzOffset = _Ocean.TransformVertex(position, time) - position;
        xzOffset.y = 0;

        return _Ocean.TransformVertex(position - xzOffset, time).y;
    }

    private void CheckPatchInitialized()
    {
        if (_heightValues == null || _heightValues.Length != _VertexWidth * _VertexLength)
        {
            _heightValues = new float[_VertexWidth * _VertexLength];
        }
    }
}
