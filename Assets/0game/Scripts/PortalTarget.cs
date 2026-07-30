using UnityEngine;

/// <summary>Большая цветная цель для текущей звезды.</summary>
[RequireComponent(typeof(Collider2D))]
public class PortalTarget : MonoBehaviour
{
    [Header("Queue")]
    [Tooltip("Used only when GameManager → Use Portal Sequence is enabled. Lower number launches first.")]
    [SerializeField, Min(0)] private int queueOrder;

    [Header("Look")]
    [SerializeField] private string displayName = "PINK PORTAL";
    [SerializeField] private Color portalColor = new Color(1f, 0.25f, 0.55f);
    [Tooltip("The coloured centre. Leave empty for legacy single-sprite portals.")]
    [SerializeField] private SpriteRenderer fillRenderer;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField, Range(0.05f, 1f)] private float inactiveAlpha = 0.22f;
    [SerializeField, Min(1f)] private float highlightScale = 1.15f;
    [SerializeField, Range(0f, 0.2f)] private float pulseAmount = 0.07f;

    public string DisplayName => displayName;
    public Color PortalColor => portalColor;
    public int QueueOrder => queueOrder;

    private Vector3 baseScale;
    private bool highlighted;
    private float pulseSeed;

    private void Awake()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (fillRenderer == null)
        {
            Transform fill = transform.Find("ColorFill");
            if (fill != null) fillRenderer = fill.GetComponent<SpriteRenderer>();
        }
        baseScale = transform.localScale;
        pulseSeed = Random.value * 10f;
        SetHighlighted(false);
    }

    private void Update()
    {
        float strength = highlighted ? pulseAmount : pulseAmount * 0.2f;
        float wave = 1f + Mathf.Sin(Time.time * (highlighted ? 7f : 2f) + pulseSeed) * strength;
        transform.localScale = baseScale * (highlighted ? highlightScale : 1f) * wave;

        if (spriteRenderer != null)
        {
            Color color = fillRenderer == null ? portalColor : Color.white;
            float alphaBase = highlighted ? 1f : inactiveAlpha;
            color.a = alphaBase * (highlighted ? 0.82f + wave * 0.18f : 1f);
            spriteRenderer.color = color;
        }

        if (fillRenderer != null)
        {
            Color fill = portalColor;
            float alphaBase = highlighted ? 1f : inactiveAlpha;
            fill.a = alphaBase * (highlighted ? 0.82f + wave * 0.18f : 1f);
            fillRenderer.color = fill;
        }
    }

    public void SetHighlighted(bool value) => highlighted = value;

    /// <summary>Called by BootstrapUi so generated objects are immediately usable as portal prefabs.</summary>
    public void ConfigureBootstrap(string portalName, Color color, int order)
    {
        displayName = portalName;
        portalColor = color;
        queueOrder = order;
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (fillRenderer == null)
        {
            Transform fill = transform.Find("ColorFill");
            if (fill != null) fillRenderer = fill.GetComponent<SpriteRenderer>();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        StarProjectile star = other.GetComponent<StarProjectile>();
        if (star != null) star.TryEnterPortal(this);
    }
}
