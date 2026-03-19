using UnityEngine;

public class OrbitingCubes : MonoBehaviour
{
    [Header("Assign your dichoptic material here")]
    public Material stimulusMaterial;
    [Header("Tag for shootable cubes")]
    public string cubeTag = "TargetCube";

    [Header("Orbit settings")]
    public int cubeCount = 8;
    public float radius = 0.8f;
    public float rotationSpeed = 30f; // degrees per second
    public float verticalBobAmplitude = 0.1f;
    public float verticalBobSpeed = 1.5f;

    [Header("Cube size")]
    public float cubeScale = 0.2f;

    private Transform[] cubes;
    private Vector3[] basePositions;
    private float bobOffset;

    void Start()
    {
        if (cubeCount <= 0) return;

        cubes = new Transform[cubeCount];
        basePositions = new Vector3[cubeCount];

        // Create cubes in a circle around this object
        for (int i = 0; i < cubeCount; i++)
        {
            float angle = (Mathf.PI * 2f / cubeCount) * i;
            Vector3 localPos = new Vector3(
                Mathf.Cos(angle) * radius,
                0f,
                Mathf.Sin(angle) * radius
            );

            GameObject c = GameObject.CreatePrimitive(PrimitiveType.Cube);
            c.name = "OrbitCube_" + i;
            c.tag = cubeTag;
            c.transform.SetParent(this.transform, false);
            c.transform.localPosition = localPos;
            c.transform.localRotation = Quaternion.identity;
            c.transform.localScale = Vector3.one * cubeScale;

            if (stimulusMaterial != null)
            {
                c.GetComponent<Renderer>().material = stimulusMaterial;
            }

            cubes[i] = c.transform;
            basePositions[i] = localPos;
        }

        // random starting point for bobbing so it's not too "synchronised"
        bobOffset = Random.Range(0f, 100f);
    }

    void Update()
    {
        if (cubes == null) return;

        // Rotate the whole ring
        transform.Rotate(0f, rotationSpeed * Time.deltaTime, 0f, Space.World);

        // Add a little vertical bobbing to each cube
        float t = Time.time * verticalBobSpeed + bobOffset;
        for (int i = 0; i < cubes.Length; i++)
        {
            if (cubes[i] == null) continue;
            Vector3 baseLocal = basePositions[i];
            float bob = Mathf.Sin(t + i * 0.5f) * verticalBobAmplitude;
            cubes[i].localPosition = baseLocal + new Vector3(0f, bob, 0f);
        }
    }
}
