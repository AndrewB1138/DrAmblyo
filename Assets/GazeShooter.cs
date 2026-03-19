using UnityEngine;

public class GazeShooter : MonoBehaviour
{
    [Header("Raycast settings")]
    public float maxDistance = 20f;

    [Header("Colors")]
    public Color normalColor = Color.white;
    public Color hitColor = Color.green;

    private Renderer lastRenderer;

    void Update()
    {
        Shoot();
    }

    void Shoot()
    {
        Ray ray = new Ray(transform.position, transform.forward);

        // Debug ray in Scene view
        Debug.DrawRay(ray.origin, ray.direction * maxDistance, Color.red, 0.1f);

        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance))
        {
            Renderer r = hit.collider.GetComponent<Renderer>();
            if (r != null)
            {
                if (r != lastRenderer)
                {
                    // restore previous
                    RestoreLastRenderer();

                    // set new one
                    SetBaseColor(r, hitColor);
                    lastRenderer = r;
                }
            }
        }
        else
        {
            RestoreLastRenderer();
            lastRenderer = null;
        }
    }

    void SetBaseColor(Renderer r, Color c)
    {
        var mat = r.material;
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", c);
        else
            mat.color = c;
    }

    void RestoreLastRenderer()
    {
        if (lastRenderer == null) return;
        SetBaseColor(lastRenderer, normalColor);
    }
}
