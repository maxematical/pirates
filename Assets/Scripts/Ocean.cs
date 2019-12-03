using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Ocean : MonoBehaviour
{
    public MeshFilter Filter;
    public float WorldSize;
    public float VerticesPerUnit;

    public int VertexSize { get => Mathf.FloorToInt(WorldSize * VerticesPerUnit) + 1; }

    public WaveSettings[] Waves;

    private Mesh _mesh;

    void Start()
    {
        _mesh = new Mesh();
        _mesh.name = "GeneratedOcean";

        Vector3[] vertices = new Vector3[VertexSize * VertexSize];
        Vector3[] normals = new Vector3[vertices.Length];

        // The size we need for the triangles array is determined by the following:
        // (VertexSize-1)^2 : the number of squares we will use
        // x2: two triangles per square
        // x3: three array indices per triangle
        // x2: both sides of the triangles
        int[] triangles = new int[(VertexSize - 1) * (VertexSize - 1) * 2 * 3 * 2];
        int nextTriangleIndex = 0;
        for (int z = 0; z < VertexSize; z++)
        {
            for (int x = 0; x < VertexSize; x++)
            {
                int index = z * VertexSize + x;
                vertices[index] = GetVertexWorldPosition(x, z);
                normals[index] = GetVertexNormal(vertices[index], vertices[index], 0);

                if (x < VertexSize - 1 && z < VertexSize - 1)
                {
                    triangles[nextTriangleIndex++] = index;
                    triangles[nextTriangleIndex++] = index + VertexSize;
                    triangles[nextTriangleIndex++] = index + 1;

                    triangles[nextTriangleIndex++] = index + 1;
                    triangles[nextTriangleIndex++] = index + VertexSize;
                    triangles[nextTriangleIndex++] = index + VertexSize + 1;


                    triangles[nextTriangleIndex++] = index;
                    triangles[nextTriangleIndex++] = index + 1;
                    triangles[nextTriangleIndex++] = index + VertexSize;

                    triangles[nextTriangleIndex++] = index + 1;
                    triangles[nextTriangleIndex++] = index + VertexSize + 1;
                    triangles[nextTriangleIndex++] = index + VertexSize;
                }
            }
        }
        _mesh.vertices = vertices;
        _mesh.triangles = triangles;
        _mesh.normals = normals;

        Filter.mesh = _mesh;
    }

    void Update()
    {
        Vector3[] vertices = _mesh.vertices;
        Vector3[] normals = _mesh.normals;
        for (int z = 0; z < VertexSize; z++)
        {
            for (int x = 0; x < VertexSize; x++)
            {
                int index = z * VertexSize + x;
                Vector3 initial = GetVertexWorldPosition(x, z);
                vertices[index] = TransformVertex(initial, Time.time);
                normals[index] = GetVertexNormal(initial, vertices[index], Time.time);
            }
        }
        _mesh.vertices = vertices;
        _mesh.normals = normals;
    }

    private Vector3 TransformVertex(Vector3 vertex, float time)
    {
        Vector3 xz = new Vector3(vertex.x, 0, vertex.z);

        float sumX = vertex.x;
        float sumZ = vertex.z;
        float sumY = 0;
        foreach (WaveSettings wave in Waves)
        {
            sumX += wave.Steepness * wave.Amplitude * wave.Direction.x * Mathf.Cos(Vector3.Dot(wave.Frequency * wave.Direction, xz) + wave.PhaseConstant * time);
            sumZ += wave.Steepness * wave.Amplitude * wave.Direction.z * Mathf.Cos(Vector3.Dot(wave.Frequency * wave.Direction, xz) + wave.PhaseConstant * time);
            sumY += wave.Amplitude * Mathf.Sin(Vector3.Dot(wave.Frequency * wave.Direction, xz) + wave.PhaseConstant * time);
        }

        return new Vector3(sumX, sumY, sumZ);
    }

    private Vector3 GetVertexNormal(Vector3 initial, Vector3 transformed, float time)
    {
        // Eqn. 12
        float sumX = 0;
        float sumZ = 0;
        float sumY = 1;
        foreach (WaveSettings wave in Waves)
        {
            float wa = wave.Frequency * wave.Amplitude;
            float s = Mathf.Sin(Vector3.Dot(wave.Frequency * wave.Direction, transformed) + wave.PhaseConstant * time);
            float c = Mathf.Cos(Vector3.Dot(wave.Frequency * wave.Direction, transformed) + wave.PhaseConstant * time);
            sumX -= wave.Direction.x * wa * c;
            sumZ -= wave.Direction.z * wa * c;
            sumY -= wave.Steepness * wa * s;
        }

        return new Vector3(sumX, sumY, sumZ);
    }

    private Vector3 GetVertexWorldPosition(int vertexX, int vertexZ)
    {
        return new Vector3(vertexX / VerticesPerUnit - WorldSize / 2, 0, vertexZ / VerticesPerUnit - WorldSize / 2);
    }

    [Serializable]
    public struct WaveSettings
    {
        public float Steepness; // Q
        public float Amplitude; // A
        public float Wavelength; // L
        public Vector2 DirectionXZ; // D
        public float Speed;

        /// <summary>
        /// Normalized direction vector of the wave
        /// </summary>
        public Vector3 Direction { get => new Vector3(DirectionXZ.x, 0, DirectionXZ.y).normalized; }

        public float PhaseConstant { get => Speed * 2 / Wavelength; }
        public float Frequency { get => 1f / Wavelength; } // w
    }
}
