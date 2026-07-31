using UnityEngine;

/// <summary>
/// Static colourful nebula. It is never affected by black-hole gravity and
/// destroys a StarProjectile on contact.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class NebulaObstacle : MonoBehaviour
{
    [Header("Cloud layers")]
    [Tooltip("Leave empty to animate every SpriteRenderer on this object and its children.")]
    [SerializeField] private SpriteRenderer[] cloudLayers;

    [Header("Colour animation")]
    [SerializeField] private Color[] palette =
    {
        new Color(0.18f, 0.88f, 1f, 0.65f),
        new Color(0.46f, 0.25f, 1f, 0.65f),
        new Color(1f, 0.2f, 0.66f, 0.65f),
        new Color(1f, 0.52f, 0.16f, 0.65f),
        new Color(0.38f, 1f, 0.74f, 0.65f)
    };
    [SerializeField, Min(0.01f)] private float colourCycleDuration = 8f;
    [SerializeField, Range(0f, 0.3f)] private float breathingAmount = 0.08f;
    [SerializeField, Min(0.01f)] private float breathingSpeed = 1.4f;
    [SerializeField] private string failReason = "NEBULA COLLISION";

    private Vector3[] baseScales;
    private float[] baseAlphas;
    private float[] layerOffsets;

    private void Awake()
    {
        if (cloudLayers == null || cloudLayers.Length == 0)
            cloudLayers = GetComponentsInChildren<SpriteRenderer>(true);

        baseScales = new Vector3[cloudLayers.Length];
        baseAlphas = new float[cloudLayers.Length];
        layerOffsets = new float[cloudLayers.Length];

        for (int i = 0; i < cloudLayers.Length; i++)
        {
            if (cloudLayers[i] == null) continue;
            baseScales[i] = cloudLayers[i].transform.localScale;
            baseAlphas[i] = cloudLayers[i].color.a;
            layerOffsets[i] = Random.value;
        }
    }

    private void Update()
    {
        if (palette == null || palette.Length == 0) return;

        for (int i = 0; i < cloudLayers.Length; i++)
        {
            SpriteRenderer layer = cloudLayers[i];
            if (layer == null) continue;

            float phase = Mathf.Repeat(Time.time / colourCycleDuration + layerOffsets[i], 1f);
            Color colour = EvaluatePalette(phase);
            colour.a *= baseAlphas[i];
            layer.color = colour;

            float breath = Mathf.Sin(Time.time * breathingSpeed + layerOffsets[i] * Mathf.PI * 2f) * 0.5f + 0.5f;
            layer.transform.localScale = baseScales[i] * (1f + breath * breathingAmount);
        }
    }

    private void OnTriggerEnter2D(Collider2D other) => DestroyStar(other);

    private void OnCollisionEnter2D(Collision2D collision) => DestroyStar(collision.collider);

    private void DestroyStar(Collider2D other)
    {
        StarProjectile star = other.GetComponentInParent<StarProjectile>();
        if (star != null) star.DestroyByHazard(failReason);
    }

    private Color EvaluatePalette(float time)
    {
        if (palette.Length == 1) return palette[0];

        float scaledTime = time * palette.Length;
        int from = Mathf.FloorToInt(scaledTime) % palette.Length;
        int to = (from + 1) % palette.Length;
        return Color.Lerp(palette[from], palette[to], scaledTime - Mathf.Floor(scaledTime));
    }
}
