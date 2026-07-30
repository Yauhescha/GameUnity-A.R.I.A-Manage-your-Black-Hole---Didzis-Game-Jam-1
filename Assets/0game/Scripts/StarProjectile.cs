using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class StarProjectile : MonoBehaviour
{
    [Header("Trail")]
    [Tooltip("Create a URP Sprite-Unlit material or a built-in Sprites/Default material and assign it here.")]
    [SerializeField] private Material trailMaterial;
    [SerializeField, Range(0f, 0.08f)] private float pulseAmount = 0.015f;

    private Rigidbody2D body;
    private SpriteRenderer spriteRenderer;
    private SpriteRenderer[] visualRenderers;
    private TrailRenderer trailRenderer;
    private GameManager gameManager;
    private PortalTarget target;
    private bool resolved;
    private Vector3 baseScale;
    private float pulseSeed;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        visualRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        trailRenderer = GetComponent<TrailRenderer>();
        baseScale = transform.localScale;
        pulseSeed = Random.value * 10f;
        ConfigureTrail();
    }

    private void Update()
    {
        float pulse = 1f + Mathf.Sin(Time.time * 10f + pulseSeed) * pulseAmount;
        transform.localScale = baseScale * pulse;
    }

    public void Launch(GameManager manager, PortalTarget destination, Vector2 initialVelocity)
    {
        gameManager = manager;
        target = destination;
        body.linearVelocity = initialVelocity;

        SetVisualColor(destination.PortalColor);
        ConfigureTrailColor(destination.PortalColor);
    }

    private void FixedUpdate()
    {
        if (resolved || gameManager == null) return;

        body.AddForce(BlackHoleController.GetCombinedAcceleration(body.position), ForceMode2D.Force);

        if (BlackHoleController.IsInsideAnySwallowRadius(body.position))
        {
            Fail("SWALLOWED");
            return;
        }

        if (gameManager.IsOutsidePlayArea(body.position))
            Fail("MISSED");
    }

    public void TryEnterPortal(PortalTarget portal)
    {
        if (resolved) return;
        if (portal == target) Deliver();
        else Fail("WRONG PORTAL");
    }

    private void Deliver()
    {
        resolved = true;
        gameManager.StarDelivered(this, target);
        Destroy(gameObject);
    }

    private void Fail(string reason)
    {
        resolved = true;
        gameManager.StarFailed(this, reason);
        Destroy(gameObject);
    }

    private void ConfigureTrail()
    {
        if (trailRenderer == null || spriteRenderer == null) return;
        if (trailMaterial != null) trailRenderer.sharedMaterial = trailMaterial;

        trailRenderer.time = 3.5f;
        trailRenderer.minVertexDistance = 0.035f;
        trailRenderer.widthMultiplier = spriteRenderer.bounds.size.x * 0.5f;
        trailRenderer.widthCurve = new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(1f, 0.03f));
    }

    private void ConfigureTrailColor(Color color)
    {
        if (trailRenderer == null) return;

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
            new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0f, 1f) });
        trailRenderer.colorGradient = gradient;
    }

    private void SetVisualColor(Color color)
    {
        if (spriteRenderer != null) spriteRenderer.color = color;

        foreach (SpriteRenderer renderer in visualRenderers)
        {
            if (renderer == null || renderer == spriteRenderer) continue;
            if (renderer.gameObject.name == "HotCore") continue;

            Color radiation = color;
            radiation.a = renderer.color.a;
            renderer.color = radiation;
        }
    }
}
