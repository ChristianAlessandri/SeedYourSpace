using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Procedurally generates a flat-shaded low-poly icosphere.
/// Replaces the default sphere mesh to achieve a stylized aesthetic and lower vertex count.
/// </summary>
[RequireComponent(typeof(MeshFilter))]
public class LowPolyMeshGenerator : MonoBehaviour
{
    [Header("Mesh Settings")]
    [Tooltip("0 = 20 faces, 1 = 80 faces, 2 = 320 faces. Keep it low (0-2) for the flat-shaded look.")]
    [Range(0, 3)]
    public int subdivisions = 1;

    private void Awake()
    {
        GenerateFlatIcosphere();
    }

    /// <summary>
    /// Generates the icosphere geometry and unwelds vertices for flat shading.
    /// </summary>
    public void GenerateFlatIcosphere()
    {
        float t = (1f + Mathf.Sqrt(5f)) / 2f; // Golden ratio

        // Base Icosahedron Vertices
        List<Vector3> vertices = new List<Vector3>()
        {
            new Vector3(-1,  t,  0).normalized,
            new Vector3( 1,  t,  0).normalized,
            new Vector3(-1, -t,  0).normalized,
            new Vector3( 1, -t,  0).normalized,
            new Vector3( 0, -1,  t).normalized,
            new Vector3( 0,  1,  t).normalized,
            new Vector3( 0, -1, -t).normalized,
            new Vector3( 0,  1, -t).normalized,
            new Vector3( t,  0, -1).normalized,
            new Vector3( t,  0,  1).normalized,
            new Vector3(-t,  0, -1).normalized,
            new Vector3(-t,  0,  1).normalized
        };

        // Base Icosahedron Triangles
        List<int> triangles = new List<int>()
        {
            0, 11, 5,  0, 5, 1,  0, 1, 7,  0, 7, 10,  0, 10, 11,
            1, 5, 9,  5, 11, 4,  11, 10, 2,  10, 7, 6,  7, 1, 8,
            3, 9, 4,  3, 4, 2,  3, 2, 6,  3, 6, 8,  3, 8, 9,
            4, 9, 5,  2, 4, 11,  6, 2, 10,  8, 6, 7,  9, 8, 1
        };

        // Subdivision
        Dictionary<long, int> midpointCache = new Dictionary<long, int>();
        for (int i = 0; i < subdivisions; i++)
        {
            List<int> newTriangles = new List<int>();
            for (int j = 0; j < triangles.Count; j += 3)
            {
                int v1 = triangles[j];
                int v2 = triangles[j + 1];
                int v3 = triangles[j + 2];

                int a = GetMidPoint(vertices, midpointCache, v1, v2);
                int b = GetMidPoint(vertices, midpointCache, v2, v3);
                int c = GetMidPoint(vertices, midpointCache, v3, v1);

                newTriangles.AddRange(new int[] { v1, a, c });
                newTriangles.AddRange(new int[] { v2, b, a });
                newTriangles.AddRange(new int[] { v3, c, b });
                newTriangles.AddRange(new int[] { a, b, c });
            }
            triangles = newTriangles;
        }

        // Unweld vertices for Flat Shading
        Vector3[] flatVertices = new Vector3[triangles.Count];
        int[] flatTriangles = new int[triangles.Count];

        for (int i = 0; i < triangles.Count; i++)
        {
            flatVertices[i] = vertices[triangles[i]];
            flatTriangles[i] = i; 
        }

        // Apply to MeshFilter
        Mesh mesh = new Mesh();
        mesh.name = "Procedural_LowPoly_Sphere";
        mesh.vertices = flatVertices;
        mesh.triangles = flatTriangles;
        mesh.RecalculateNormals(); // Calculates sharp normals for flat shading
        mesh.RecalculateBounds();

        GetComponent<MeshFilter>().mesh = mesh;
    }

    /// <summary>
    /// Finds or creates a normalized midpoint between two vertices.
    /// Uses a dictionary cache to prevent generating duplicate vertices during subdivision.
    /// </summary>
    /// <param name="vertices">The list of current vertices.</param>
    /// <param name="cache">A dictionary to cache midpoints for edges.</param>
    /// <param name="v1">Index of the first vertex.</param>
    /// <param name="v2">Index of the second vertex.</param>
    /// <returns>The index of the midpoint vertex.</returns>
    private int GetMidPoint(List<Vector3> vertices, Dictionary<long, int> cache, int v1, int v2)
    {
        long smallerIndex = Mathf.Min(v1, v2);
        long greaterIndex = Mathf.Max(v1, v2);
        long key = (smallerIndex << 32) + greaterIndex; // Unique hash for the edge

        if (cache.TryGetValue(key, out int index))
        {
            return index;
        }

        Vector3 middle = ((vertices[v1] + vertices[v2]) / 2f).normalized;
        vertices.Add(middle);
        index = vertices.Count - 1;
        cache.Add(key, index);
        
        return index;
    }
}