using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Extracts a multi-part prefab, combines it into a single mesh, and renders thousands 
/// of instances via GPU Instancing. Handles both the global orbit of the belt and 
/// the individual local rotation (spin) of each asteroid without floating-point drift.
/// </summary>
public class AsteroidBeltRenderer : MonoBehaviour
{
    private struct AsteroidState
    {
        public Vector3 localPosition;
        public Quaternion baseRotation; // Stored permanently to prevent floating-point drift
        public Vector3 localScale;
        public Vector3 spinAxis;
        public float spinSpeed;
        public float currentAngle;
    }

    private AsteroidState[] asteroids;
    private List<Matrix4x4[]> matrixBatches = new List<Matrix4x4[]>();
    private int[] batchCounts;

    private Mesh instancedMesh;
    private Material instancedMaterial;
    private float orbitSpeed;

    public void InitializeBelt(AsteroidBeltData beltData, float distanceMultiplier, GameObject prefab)
    {
        if (prefab == null) return;

        ExtractSingleAsteroidMesh(prefab, out instancedMesh, out instancedMaterial);

        if (instancedMesh == null || instancedMaterial == null) return;
        instancedMaterial.enableInstancing = true;

        System.Random prng = new System.Random(beltData.seed);
        int count = Mathf.Clamp(beltData.asteroidCount, 100, 3000);

        asteroids = new AsteroidState[count];
        int numBatches = Mathf.CeilToInt(count / 1023f);
        batchCounts = new int[numBatches];

        int index = 0;
        for (int b = 0; b < numBatches; b++)
        {
            int batchSize = Mathf.Min(1023, count - index);
            batchCounts[b] = batchSize;
            matrixBatches.Add(new Matrix4x4[1023]);

            for (int i = 0; i < batchSize; i++)
            {
                float angle = (float)prng.NextDouble() * Mathf.PI * 2f;
                float radius = Mathf.Lerp(beltData.innerRadius, beltData.outerRadius, (float)prng.NextDouble()) * distanceMultiplier;
                
                float verticalThickness = (beltData.outerRadius - beltData.innerRadius) * distanceMultiplier * 0.08f;
                float yOffset = ((float)prng.NextDouble() * 2f - 1f) * verticalThickness;

                Vector3 pos = new Vector3(Mathf.Cos(angle) * radius, yOffset, Mathf.Sin(angle) * radius);
                Quaternion rot = Random.rotation;
                Vector3 sca = Vector3.one * ((float)prng.NextDouble() * 6.0f + 2.5f);

                asteroids[index] = new AsteroidState
                {
                    localPosition = pos,
                    baseRotation = rot,
                    localScale = sca,
                    spinAxis = Random.onUnitSphere,
                    spinSpeed = (float)prng.NextDouble() * 80f + 10f,
                    currentAngle = 0f
                };

                index++;
            }
        }

        float beltTilt = ((float)prng.NextDouble() * 10f) - 5f;
        transform.rotation = Quaternion.Euler(beltTilt, 0, beltTilt * 0.5f);
        
        orbitSpeed = 10f / Mathf.Max(beltData.innerRadius, 0.1f);
        if (prng.NextDouble() > 0.5) orbitSpeed *= -1f;
    }

    private void ExtractSingleAsteroidMesh(GameObject prefab, out Mesh mergedMesh, out Material mat)
    {
        GameObject tempObj = Instantiate(prefab, Vector3.zero, Quaternion.identity);
        MeshFilter[] filters = tempObj.GetComponentsInChildren<MeshFilter>();
        MeshRenderer[] renderers = tempObj.GetComponentsInChildren<MeshRenderer>();

        if (filters.Length == 0)
        {
            mergedMesh = null; mat = null;
            Destroy(tempObj);
            return;
        }

        mat = renderers.Length > 0 ? renderers[0].sharedMaterial : null;

        CombineInstance[] combine = new CombineInstance[filters.Length];
        for (int i = 0; i < filters.Length; i++)
        {
            combine[i].mesh = filters[i].sharedMesh;
            combine[i].transform = filters[i].transform.localToWorldMatrix;
        }

        mergedMesh = new Mesh();
        mergedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mergedMesh.CombineMeshes(combine, true, true);
        mergedMesh.RecalculateNormals();

        Destroy(tempObj);
    }

    private void Update()
    {
        if (instancedMesh == null || instancedMaterial == null || asteroids == null) return;

        transform.Rotate(Vector3.up, orbitSpeed * Time.deltaTime, Space.Self);

        Matrix4x4 parentMatrix = transform.localToWorldMatrix;
        float dt = Time.deltaTime;
        int index = 0;
        
        for (int b = 0; b < matrixBatches.Count; b++)
        {
            Matrix4x4[] batch = matrixBatches[b];
            int batchSize = batchCounts[b];

            for (int i = 0; i < batchSize; i++)
            {
                asteroids[index].currentAngle += asteroids[index].spinSpeed * dt;
                
                // Calculate pristine rotation
                Quaternion currentRot = asteroids[index].baseRotation * Quaternion.AngleAxis(asteroids[index].currentAngle, asteroids[index].spinAxis);

                Matrix4x4 localMatrix = Matrix4x4.TRS(asteroids[index].localPosition, currentRot, asteroids[index].localScale);
                batch[i] = parentMatrix * localMatrix;

                index++;
            }

            Graphics.DrawMeshInstanced(instancedMesh, 0, instancedMaterial, batch, batchSize);
        }
    }
}