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

    [Tooltip("If this is checked, the ocean will resend shader values every 3 seconds. This is useful when editing " +
        "shaders during runtime because the wave settings inside the shader are reset when the shader is compiled.")]
    public bool ResendShaderValues;

    private Mesh _mesh;
    private float _lastSendTime;

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
        int[] triangles = new int[(VertexSize - 1) * (VertexSize - 1) * 2 * 3];
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


                    //triangles[nextTriangleIndex++] = index;
                    //triangles[nextTriangleIndex++] = index + 1;
                    //triangles[nextTriangleIndex++] = index + VertexSize;

                    //triangles[nextTriangleIndex++] = index + 1;
                    //triangles[nextTriangleIndex++] = index + VertexSize + 1;
                    //triangles[nextTriangleIndex++] = index + VertexSize;
                }
            }
        }
        _mesh.vertices = vertices;
        _mesh.triangles = triangles;
        _mesh.normals = normals;
        _mesh.bounds = new Bounds(Vector3.zero, new Vector3(WorldSize, 10, WorldSize));

        Filter.mesh = _mesh;

        UpdateMaterialParameters();
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            UpdateMaterialParameters();
        }
    }

    private void Update()
    {
        if (ResendShaderValues && Time.time - _lastSendTime > 3)
        {
            UpdateMaterialParameters();
        }
    }

    public Vector3 TransformVertex(Vector3 vertex, float time)
    {
        Vector3 xz = new Vector3(vertex.x, 0, vertex.z);

        // TODO Actually calculate the vertex position, right now we are acting as if the ocean is a flat plane (which it is not).
        // The actual calculation is somewhat computationally expensive if we are running it multiple times for all triangles on
        // the hull, so in the future we should find some way to optimize/cache this data.
        //return xz;

        float sumX = vertex.x;
        float sumZ = vertex.z;
        float sumY = 0;
        foreach (WaveSettings wave in Waves)
        {
            sumX += wave.Steepness * wave.Amplitude * wave.Direction.x * (float)Math.Cos(Vector3.Dot(wave.Frequency * wave.Direction, xz) + wave.PhaseConstant * time);
            sumZ += wave.Steepness * wave.Amplitude * wave.Direction.z * (float)Math.Cos(Vector3.Dot(wave.Frequency * wave.Direction, xz) + wave.PhaseConstant * time);
            sumY += wave.Amplitude * (float)Math.Sin(Vector3.Dot(wave.Frequency * wave.Direction, xz) + wave.PhaseConstant * time);
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

        //return new Vector3(sumX, sumY, sumZ);
        return Vector3.up;
    }

    private Vector3 GetVertexWorldPosition(int vertexX, int vertexZ)
    {
        return new Vector3(vertexX / VerticesPerUnit - WorldSize / 2, 0, vertexZ / VerticesPerUnit - WorldSize / 2);
    }

    private void UpdateMaterialParameters()
    {
        Material material = GetComponent<MeshRenderer>().material;

        Vector4[] waveData = new Vector4[Waves.Length];
        Vector4[] waveDirections = new Vector4[Waves.Length];
        for (int i = 0; i < Waves.Length; i++)
        {
            WaveSettings wave = Waves[i];
            waveData[i] = new Vector4(wave.Steepness, wave.Amplitude, wave.Frequency, wave.Speed);
            waveDirections[i] = new Vector4(wave.Direction.x, wave.Direction.y, wave.Direction.z, wave.PhaseConstant);
        }

        material.SetInt("_WavesLength", Waves.Length);
        material.SetVectorArray("_WavesData", waveData);
        material.SetVectorArray("_WavesDirection", waveDirections);

        _lastSendTime = Time.time;
    }

    [Serializable]
    public struct WaveSettings
    {
        [Tooltip("This can be used to add a comment to this wave. Its value does not affect anything else.")]
        [SerializeField]
#pragma warning disable IDE0051 // Remove unused private members
        private string Comment;
#pragma warning restore IDE0051

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
