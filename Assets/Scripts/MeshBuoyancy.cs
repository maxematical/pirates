using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MeshBuoyancy : MonoBehaviour
{
    public Ocean _Ocean;
    public Mesh _HullPhysicsMesh;
    public GameObject _HullPhysicsObject;

    private void OnDrawGizmosSelected()
    {
        var hullTransform = _HullPhysicsObject.transform;
        Gizmos.color = Color.red;
        Gizmos.DrawWireMesh(_HullPhysicsMesh, hullTransform.position, hullTransform.rotation, hullTransform.lossyScale);

        float time = Application.isPlaying ? Time.time : 0;

        Gizmos.color = Color.blue;
        Vector3[] vertices = _HullPhysicsMesh.vertices;
        int[] triangles = _HullPhysicsMesh.triangles;
        Vector3[] normals = _HullPhysicsMesh.normals;

        for (int i = 0; i < triangles.Length; i += 3)
        {
            int i1 = triangles[i];
            int i2 = triangles[i + 1];  
            int i3 = triangles[i + 2];
            Vector3 v1 = hullTransform.localToWorldMatrix.MultiplyPoint3x4(vertices[i1]);
            Vector3 v2 = hullTransform.localToWorldMatrix.MultiplyPoint3x4(vertices[i2]);
            Vector3 v3 = hullTransform.localToWorldMatrix.MultiplyPoint3x4(vertices[i3]);

            Vector3 triangleNormal = (hullTransform.rotation * normals[i1] +
                hullTransform.rotation * normals[i2] +
                hullTransform.rotation * normals[i3]) / 3f;
            triangleNormal = triangleNormal.normalized;

            bool hasIntersectA;
            Vector3 intersectA1;
            Vector3 intersectA2;
            Vector3 intersectA3;
            bool hasIntersectB;
            Vector3 intersectB1;
            Vector3 intersectB2;
            Vector3 intersectB3;
            ComputeTriangleWaterIntersection(v1, v2, v3, time, triangleNormal,
                out hasIntersectA, out intersectA1, out intersectA2, out intersectA3,
                out hasIntersectB, out intersectB1, out intersectB2, out intersectB3);

            if (hasIntersectA || hasIntersectB)
            {
                int numberVertices = (hasIntersectA ? 3 : 0) + (hasIntersectB ? 3 : 0);

                Vector3[] drawVertices = new Vector3[numberVertices];
                int[] drawTriangles = new int[numberVertices];
                Vector3[] drawNormals = new Vector3[numberVertices];

                Vector3 normal = normals[i1];

                for (int j = 0; j < numberVertices; j++)
                {
                    drawTriangles[j] = j;
                    drawNormals[j] = normal;
                }

                int k = 0;
                if (hasIntersectA)
                {
                    drawVertices[k++] = intersectA1;
                    drawVertices[k++] = intersectA2;
                    drawVertices[k++] = intersectA3;
                }
                if (hasIntersectB)
                {
                    drawVertices[k++] = intersectB1;
                    drawVertices[k++] = intersectB2;
                    drawVertices[k++] = intersectB3;
                }

                if (hasIntersectA && !hasIntersectB)
                {
                    //Debug.Log(string.Join(", ", drawVertices));
                    //Debug.Log(string.Join(", ", drawTriangles));
                    //Debug.Log(string.Join(", ", drawNormals));
                }

                Mesh toDraw = new Mesh();
                toDraw.vertices = drawVertices;
                toDraw.normals = drawNormals;
                toDraw.triangles = drawTriangles;
                Gizmos.DrawMesh(toDraw);
            }
        }
    }

    public float ComputeWaterHeight(Vector3 position, float time)
    {
        // Note: this is an estimate! However, it is usually very accurate.
        Vector3 xzOffset = _Ocean.TransformVertex(position, time) - position;
        xzOffset.y = 0;

        return _Ocean.TransformVertex(position - xzOffset, time).y;
    }

    private void ComputeTriangleWaterIntersection(Vector3 point1, Vector3 point2, Vector3 point3, float time, Vector3 normal,
        out bool hasIntersectA, out Vector3 intersectA1, out Vector3 intersectA2, out Vector3 intersectA3,
        out bool hasIntersectB, out Vector3 intersectB1, out Vector3 intersectB2, out Vector3 intersectB3)
    {
        List<Vector3> sorted = new Vector3[] { point1, point2, point3 }.OrderBy(v => v.y - ComputeWaterHeight(v, time)).ToList();
        Vector3 L = sorted[0]; // Lowest vertex
        Vector3 M = sorted[1]; // Middle vertex
        Vector3 H = sorted[2]; // Highest vertex

        float hL = L.y - ComputeWaterHeight(L, time);
        float hM = M.y - ComputeWaterHeight(M, time);
        float hH = H.y - ComputeWaterHeight(H, time);

        //if (hL <= 0 || true)
        //{
        //    hasIntersectA = true;
        //    intersectA3 = H;
        //    intersectA1 = L;
        //    intersectA2 = M;
        //    hasIntersectB = false;
        //    intersectB1 = Vector3.zero;
        //    intersectB2 = Vector3.zero;
        //    intersectB3 = Vector3.zero;
        //    //return;
        //} else
        //{
        //    hasIntersectA = false;
        //    intersectA3 = Vector3.zero;
        //    intersectA1 = Vector3.zero;
        //    intersectA2 = Vector3.zero;
        //    hasIntersectB = false;
        //    intersectB1 = Vector3.zero;
        //    intersectB2 = Vector3.zero;
        //    intersectB3 = Vector3.zero;
        //}

        // Case 1: H is above the water but M and L are below
        if (hL <= 0 && hM <= 0 && hH >= 0)
        {
            float tM = -hM / (hH - hM);
            float tL = -hL / (hH - hL);

            // IM - M = tM * (H - M)
            //

            Vector3 IM = M + tM * (H - M);
            Vector3 IL = L + tL * (H - L);

            hasIntersectA = true;
            intersectA1 = IM;
            intersectA2 = M;
            intersectA3 = L;
            hasIntersectB = true;
            intersectB1 = IM;
            intersectB2 = L;
            intersectB3 = IL;

            //intersectA1 = L;
            //intersectA2 = M;
            //intersectA3 = H;
        }
        else if (hL <= 0 && hM >= 0 && hL >= 0)
        {
            float tM = -hL / (hM - hL);
            float tH = -hL / (hH - hL);

            Vector3 JM = L + tM * (M - L);
            Vector3 JH = L + tH * (H - L);

            hasIntersectA = true;
            intersectA1 = JH;
            intersectA2 = L;
            intersectA3 = JM;
            hasIntersectB = false;
            intersectB1 = Vector3.zero;
            intersectB2 = Vector3.zero;
            intersectB3 = Vector3.zero;
        }
        else if (hL <= 0 && hM <= 0 && hL <= 0)
        {
            hasIntersectA = true;
            intersectA1 = H;
            intersectA2 = L;
            intersectA3 = M;
            hasIntersectB = false;
            intersectB1 = Vector3.zero;
            intersectB2 = Vector3.zero;
            intersectB3 = Vector3.zero;
        }
        else
        {
            hasIntersectA = false;
            intersectA1 = Vector3.zero;
            intersectA2 = Vector3.zero;
            intersectA3 = Vector3.zero;
            hasIntersectB = false;
            intersectB1 = Vector3.zero;
            intersectB2 = Vector3.zero;
            intersectB3 = Vector3.zero;
        }
        
        // The last thing we need to do is ensure the vertices are in the right order (counterclockwise)
        // If they're in the wrong order, when we try to render the triangle(s) for debug purposes, the triangle would
        // only display from the opposite side we want it to
        if (hasIntersectA)
        {
            float currentDot = Vector3.Dot(normal, GetNormal(intersectA1, intersectA2, intersectA3));
            float otherDot = Vector3.Dot(normal, GetNormal(intersectA1, intersectA3, intersectA2));
            if (Mathf.Abs(otherDot - 1) < Mathf.Abs(currentDot - 1))
            {
                Vector3 lastA2 = intersectA2;
                intersectA2 = intersectA3;
                intersectA3 = lastA2;
            }
        }
        if (hasIntersectB)
        {
            float currentDot = Vector3.Dot(normal, GetNormal(intersectB1, intersectB2, intersectB3));
            float otherDot = Vector3.Dot(normal, GetNormal(intersectB1, intersectB3, intersectB2));
            if (Mathf.Abs(otherDot - 1) < Mathf.Abs(currentDot - 1))
            {
                Vector3 lastB2 = intersectB2;
                intersectB2 = intersectB3;
                intersectB3 = lastB2;
            }
        }
    }

    private Vector3 GetNormal(Vector3 vertex1, Vector3 vertex2, Vector3 vertex3)
    {
        Vector3 v21 = vertex2 - vertex1;
        Vector3 v31 = vertex3 - vertex1;
        return Vector3.Cross(v21, v31).normalized;
    }
}
