using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MeshBuoyancy : MonoBehaviour
{
    private const bool DEBUG_NORMALS = false;
    private const bool DEBUG_SUBMERGED_TRIANGLES = true;
    private const bool DEBUG_SUBMERGED_CENTERS = true;
    private const bool LOG_UPDATE_TIME = false;

    public Ocean _Ocean;
    public Mesh _HullPhysicsMesh;
    public GameObject _HullPhysicsObject;
    public Rigidbody _Rigidbody;

    private Vector3[] _meshVertices;
    private Vector3[] _meshNormals;
    private int[] _meshTriangles;

    private void Start()
    {
        _meshVertices = _HullPhysicsMesh.vertices;
        _meshNormals = _HullPhysicsMesh.normals;
        _meshTriangles = _HullPhysicsMesh.triangles;
    }

    private void FixedUpdate()
    {
        float startTime = Time.realtimeSinceStartup;

        Transform hullTransform = _HullPhysicsObject.transform;
        float time = Application.isPlaying ? Time.time : 0;

        float density = 0.25f;
        float g = 9.81f;

        float dampingConstant = 15f;
        Vector3 dampingVector = -_Rigidbody.velocity.sqrMagnitude * _Rigidbody.velocity * dampingConstant * Time.deltaTime;

        for (int i = 0; i < _meshTriangles.Length; i += 3)
        {
            int i1 = _meshTriangles[i];
            int i2 = _meshTriangles[i + 1];
            int i3 = _meshTriangles[i + 2];
            Vector3 v1 = hullTransform.localToWorldMatrix.MultiplyPoint3x4(_meshVertices[i1]);
            Vector3 v2 = hullTransform.localToWorldMatrix.MultiplyPoint3x4(_meshVertices[i2]);
            Vector3 v3 = hullTransform.localToWorldMatrix.MultiplyPoint3x4(_meshVertices[i3]);
            Vector3 center = (v1 + v2 + v3) / 3f;

            Vector3 n1 = hullTransform.rotation * _meshNormals[i1];
            Vector3 n2 = hullTransform.rotation * _meshNormals[i2];
            Vector3 n3 = hullTransform.rotation * _meshNormals[i3];
            Vector3 normal = (n1 + n2 + n3).normalized;

            bool hasIntersectA;
            Vector3 intersectA1;
            Vector3 intersectA2;
            Vector3 intersectA3;
            bool hasIntersectB;
            Vector3 intersectB1;
            Vector3 intersectB2;
            Vector3 intersectB3;
            ComputeTriangleWaterIntersection(v1, v2, v3, time, normal,
                out hasIntersectA, out intersectA1, out intersectA2, out intersectA3,
                out hasIntersectB, out intersectB1, out intersectB2, out intersectB3);

            normal = Vector3.down;
            if (hasIntersectA)
            {
                GetTriangleCenters(intersectA1, intersectA2, intersectA3, time,
                    out Vector3 center1, out float area1,
                    out bool has2, out Vector3 center2, out float area2);

                float waterHeight1 = ComputeWaterHeight(center1, time) - center1.y;
                Vector3 force1 = -density * g * waterHeight1 * normal;
                force1.x = force1.z = 0;
                _Rigidbody.AddForceAtPosition(force1 * area1, center1);

                _Rigidbody.AddForceAtPosition(dampingVector * area1, center1);

                if (has2)
                {
                    float waterHeight2 = ComputeWaterHeight(center2, time) - center2.y;
                    Vector3 force2 = -density * g * waterHeight2 * normal;
                    force2.x = force2.z = 0;
                    _Rigidbody.AddForceAtPosition(force2 * area2, center2);

                    _Rigidbody.AddForceAtPosition(dampingVector * area2, center2);
                }
            }

            if (hasIntersectB)
            {
                GetTriangleCenters(intersectB1, intersectB2, intersectB3, time,
                    out Vector3 center1, out float area1,
                    out bool has2, out Vector3 center2, out float area2);

                float waterHeight1 = ComputeWaterHeight(center1, time) - center1.y;
                Vector3 force1 = -density * g * waterHeight1 * normal;
                force1.x = force1.z = 0;
                _Rigidbody.AddForceAtPosition(force1 * area1, center1);

                _Rigidbody.AddForceAtPosition(dampingVector * area1, center1);

                if (has2)
                {
                    float waterHeight2 = ComputeWaterHeight(center2, time) - center2.y;
                    Vector3 force2 = -density * g * waterHeight2 * normal;
                    force2.x = force2.z = 0;
                    _Rigidbody.AddForceAtPosition(force2 * area2, center2);

                    _Rigidbody.AddForceAtPosition(dampingVector * area2, center2);
                }
            }
        }

        if (LOG_UPDATE_TIME)
            Debug.Log("Time taken to perform fixed update: " + ((Time.realtimeSinceStartup - startTime) * 1000) + "ms");
    }

    private void OnDrawGizmosSelected()
    {
        var hullTransform = _HullPhysicsObject.transform;

        float time = Application.isPlaying ? Time.time : 0;

        Vector3[] vertices = _HullPhysicsMesh.vertices;
        int[] triangles = _HullPhysicsMesh.triangles;
        Vector3[] normals = _HullPhysicsMesh.normals;

        List<Vector3> centersToDraw = new List<Vector3>(triangles.Length / 3 * 2);

        for (int i = 0; i < triangles.Length; i += 3)
        {
            Gizmos.color = Color.blue;
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

            ComputeTriangleWaterIntersection(v1, v2, v3, time, triangleNormal,
                out bool hasIntersectA, out Vector3 intersectA1, out Vector3 intersectA2, out Vector3 intersectA3,
                out bool hasIntersectB, out Vector3 intersectB1, out Vector3 intersectB2, out Vector3 intersectB3);

            if (DEBUG_NORMALS)
                Gizmos.DrawRay((v1 + v2 + v3) / 3f, triangleNormal * 0.25f);

            if (DEBUG_SUBMERGED_TRIANGLES && (hasIntersectA || hasIntersectB))
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

                Mesh toDraw = new Mesh();
                toDraw.vertices = drawVertices;
                toDraw.normals = drawNormals;
                toDraw.triangles = drawTriangles;
                Gizmos.DrawMesh(toDraw);

                if (DEBUG_SUBMERGED_CENTERS && hasIntersectA)
                {
                    GetTriangleCenters(intersectA1, intersectA2, intersectA3, time, out Vector3 centerA, out _, out bool hasCenterB, out Vector3 centerB, out _);
                    centersToDraw.Add(centerA);
                    if (hasCenterB) centersToDraw.Add(centerB);
                }
                if (DEBUG_SUBMERGED_CENTERS && hasIntersectB)
                {
                    GetTriangleCenters(intersectB1, intersectB2, intersectB3, time, out Vector3 centerA, out _, out bool hasCenterB, out Vector3 centerB, out _);
                    centersToDraw.Add(centerA);
                    if (hasCenterB) centersToDraw.Add(centerB);
                }
            }
        }

        Gizmos.color = Color.cyan;
        foreach (Vector3 center in centersToDraw)
        {
            Gizmos.DrawSphere(center, 0.025f);
        }

        Gizmos.color = Color.red;
        Gizmos.DrawWireMesh(_HullPhysicsMesh, hullTransform.position, hullTransform.rotation, hullTransform.lossyScale);

        {
            Gizmos.color = Color.magenta;
            Vector3[] triangle = new Vector3[]
            {
                new Vector3(4, 4, 4),
                new Vector3(4, 5, 3),
                new Vector3(3, 4, 4)
                //new Vector3(-4, 4, 4),
                //new Vector3(-4, 6, 2),
                //new Vector3(-3, 4, 4)
            };
            Vector3 triangleNormal = GetNormal(triangle[0], triangle[1], triangle[2]);
            {
                Mesh toDraw = new Mesh();
                toDraw.vertices = triangle;
                toDraw.normals = new Vector3[] { triangleNormal, triangleNormal, triangleNormal };
                toDraw.triangles = new int[] { 0, 1, 2 };
                Gizmos.DrawMesh(toDraw);

                Mesh toDrawBack = new Mesh();
                toDrawBack.vertices = triangle;
                toDrawBack.normals = new Vector3[] { -triangleNormal, -triangleNormal, -triangleNormal };
                toDrawBack.triangles = new int[] { 2, 1, 0 };
                Gizmos.DrawMesh(toDrawBack);
            }
            {
                Gizmos.color = Color.cyan;
                Quaternion triangleTransform = Quaternion.FromToRotation(triangleNormal, Vector3.forward);
                Vector3[] transformedTriangle = triangle.Select(x => triangleTransform * x).ToArray();
                Vector3 transformedNormal = GetNormal(transformedTriangle[0], transformedTriangle[1], transformedTriangle[2]);

                Mesh toDraw = new Mesh();
                toDraw.vertices = transformedTriangle;
                toDraw.normals = new Vector3[] { transformedNormal, transformedNormal, transformedNormal };
                toDraw.triangles = new int[] { 0, 1, 2 };
                Gizmos.DrawMesh(toDraw);

                Mesh toDrawBack = new Mesh();
                toDrawBack.vertices = transformedTriangle;
                toDrawBack.normals = new Vector3[] { -transformedNormal, -transformedNormal, -transformedNormal };
                toDrawBack.triangles = new int[] { 2, 1, 0 };
                Gizmos.DrawMesh(toDrawBack);

                Vector3 hydroforceCenter;
                //GetTriangleCenters(transformedTriangle[0], transformedTriangle[1], transformedTriangle[2], time, out hydroforceCenter, out _, out _);
                //Gizmos.DrawSphere(hydroforceCenter, 0.2f);
                //Gizmos.DrawSphere(Quaternion.Inverse(triangleTransform) * hydroforceCenter, 0.2f);
                GetTriangleCenters(triangle[0], triangle[1], triangle[2], 0, out Vector3 centerA, out float areaA, out bool hasCenterB, out Vector3 centerB, out float areaB);
                Gizmos.DrawSphere(centerA, 0.2f * areaA);
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

        if (hL <= 0 && hM <= 0 && hH >= 0)
        {
            // Case 1: One vertex above the water
            float tM = -hM / (hH - hM);
            float tL = -hL / (hH - hL);

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
        }
        else if (hL <= 0 && hM >= 0 && hL >= 0)
        {
            // Case 2: Two vertices above the water
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
            // Case 3: All vertices below the water (completely submerged)
            hasIntersectA = true;
            intersectA1 = L;
            intersectA2 = M;
            intersectA3 = H;
            hasIntersectB = false;
            intersectB1 = Vector3.zero;
            intersectB2 = Vector3.zero;
            intersectB3 = Vector3.zero;
        }
        else
        {
            // Case 4: All vertices above the water (completely dry)
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

    /// <summary>
    /// Calculates the center(s) of the hydroforce(s) acting on the triangle defined by the given three points.
    /// There may be one or two forces acting upon the triangle, depending on whether it has a horizontal base or not.
    /// <para>
    /// This performs a rotation on the vertices, so it is not necessary for the vertices to lie on the XY plane.
    /// </para>
    /// </summary>
    /// <param name="vertex1">the first vertex of the triangle</param>
    /// <param name="vertex2">the second vertex of the triangle</param>
    /// <param name="vertex3">the third vertex of the triangle</param>
    /// <param name="centerA">the center of the first force acting on the triangle</param>
    /// <param name="areaA">the area for which the first force is being applied</param>
    /// <param name="hasCenterB">whether there is a second force on the triangle</param>
    /// <param name="centerB">the center of the second force acting on the triangle (if it exists)</param>
    /// <param name="areaB">the area for which the second force is being applied (if it exists)</param>
    private void GetTriangleCenters(Vector3 vertex1, Vector3 vertex2, Vector3 vertex3, float time,
        out Vector3 centerA, out float areaA,
        out bool hasCenterB, out Vector3 centerB, out float areaB)
    {
        Vector3 normal = GetNormal(vertex1, vertex2, vertex3);
        Quaternion q = Quaternion.FromToRotation(normal, Vector3.forward);
        Quaternion qInv = Quaternion.Inverse(q);

        GetTriangleCentersXY(q * vertex1, q * vertex2, q * vertex3, out centerA, out areaA, out hasCenterB, out centerB, out areaB);
        centerA = qInv * centerA;
        centerB = qInv * centerB;
    }

    /// <summary>
    /// Assuming the vertices lie on the XY plane, calculates the center(s) of the hydroforce(s) acting on the triangle defined by the given three points.
    /// There may be one or two forces acting upon the triangle, depending on whether it has a horizontal base or not.
    /// </summary>
    /// <param name="vertex1">the first vertex of the triangle</param>
    /// <param name="vertex2">the second vertex of the triangle</param>
    /// <param name="vertex3">the third vertex of the triangle</param>
    /// <param name="centerA">the center of the first force acting on the triangle</param>
    /// <param name="areaA">the area for which the first force is being applied</param>
    /// <param name="hasCenterB">whether there is a second force on the triangle</param>
    /// <param name="centerB">the center of the second force acting on the triangle (if it exists)</param>
    /// <param name="areaB">the area for which the second force is being applied (if it exists)</param>
    private void GetTriangleCentersXY(Vector3 vertex1, Vector3 vertex2, Vector3 vertex3,
        out Vector3 centerA, out float areaA,
        out bool hasCenterB, out Vector3 centerB, out float areaB)
    {
        var (L, M, H) = SortByHeight(vertex1, vertex2, vertex3);

        // We can proceed if there is a horizontal edge for the triangle. (i.e. two vertices have the same y-position).
        // Otherwise, we will split the triangle into two different triangles such that they both have one
        // horizontal edge.
        if (Eq(vertex1.y, vertex2.y) || Eq(vertex2.y, vertex3.y) || Eq(vertex1.y, vertex3.y))
        {
            //float y0 = H.y - ComputeWaterHeight(H, time);
            float y0 = H.y;
            float y = L.y;
            float h = y0 - y;

            // The base is lower than the lone vertex if the 2 lowest vertices have the same vertical position
            bool isBaseLower = Eq(L.y, M.y);

            if (isBaseLower)
            {
                float tc = (4 * y0 + 3 * h) / (6 * y0 + 4 * h);
                centerA = Vector3.Lerp(H, 0.5f * (L + M), tc);
            }
            else
            {
                float tc = (2 * y0 + h) / (6 * y0 + 2 * h);
                centerA = Vector3.Lerp(0.5f * (H + M), L, tc);
            }

            // Use a modified version of Heron's Formula to find the area
            // https://www.iquilezles.org/blog/?p=1579
            float a = (L - M).sqrMagnitude;
            float b = (M - H).sqrMagnitude;
            float c = (H - L).sqrMagnitude;
            areaA = Mathf.Sqrt((2 * a * b + 2 * b * c + 2 * c * a - a * a - b * b - c * c) / 16);

            hasCenterB = false;
            centerB = Vector3.zero;
            areaB = 0;
        }
        else
        {
            // Split the triangle into two sub-triangles, such that each sub-triangle has one horizontal base

            // P is the midpoint between L and H, where its y-position is the same as M's y-position
            float t = (M.y - L.y) / (H.y - L.y);
            Vector3 P = Vector3.Lerp(L, H, t);

            if (!Eq(P.y, M.y))
                Debug.Log($"Warning: unable to compute a midpoint P between L and H such that (P.y == M.y): " +
                    $"L{L} M{M} H{H} P{P}");

            // Now calculate the centers of triangles LPM and MPH
            // These recursive calls will hit the base case where there's one horizontal side, meaning there won't be any recursive calls after this
            // (see what I did there? BASE CASE???)
            GetTriangleCentersXY(L, P, M, out centerA, out areaA, out _, out _, out _);
            GetTriangleCentersXY(M, P, H, out centerB, out areaB, out _, out _, out _);
            hasCenterB = true;
        }
    }

    private (Vector3, Vector3, Vector3) SortByHeight(Vector3 point1, Vector3 point2, Vector3 point3)
    {
        List<Vector3> sorted = new Vector3[] { point1, point2, point3 }.OrderBy(v => v.y).ToList();
        Vector3 L = sorted[0];
        Vector3 M = sorted[1];
        Vector3 H = sorted[2];
        return (L, M, H);
    }

    /// <summary>
    /// Returns true if the two values are approximately equal (less than one thousandth from each other).
    /// This is useful when accounting for floating point precision error.
    /// </summary>
    /// <param name="a">the first number</param>
    /// <param name="b">the second number</param>
    /// <returns>whether they are approximately equal</returns>
    private bool Eq(float a, float b)
    {
        return Mathf.Abs(a - b) < 0.001f;
    }
}
