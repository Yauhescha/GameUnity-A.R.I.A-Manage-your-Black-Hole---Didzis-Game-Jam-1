using System.Collections.Generic;
using UnityEngine;

/// <summary>Одна из нескольких гравитационных линз уровня.</summary>
public class BlackHoleController : MonoBehaviour
{
    private static readonly List<BlackHoleController> activeHoles = new List<BlackHoleController>();

    [Header("Gravity")]
    [Min(0.01f)] public float gravityStrength = 24f;
    [Min(0.01f)] public float softening = 0.3f;
    [Min(0.01f)] public float swallowRadius = 0.42f;
    public bool GravityEnabled { get; private set; }

    [Header("Look")]
    [SerializeField] private SpriteRenderer coreRenderer;
    [SerializeField] private Transform halo;
    [SerializeField] private Color enabledHalo = new Color(0.38f, 0.2f, 1f, 0.42f);
    [SerializeField] private Color disabledHalo = new Color(0.1f, 0.1f, 0.1f, 0.18f);
    [SerializeField, Range(0f, 0.15f)] private float corePulse = 0.035f;

    public static bool AnyGravityEnabled
    {
        get
        {
            foreach (BlackHoleController hole in activeHoles)
                if (hole != null && hole.GravityEnabled) return true;
            return false;
        }
    }

    public static int ActiveHoleCount => activeHoles.Count;

    private Vector3 coreScale;

    private void Awake()
    {
        if (coreRenderer == null) coreRenderer = GetComponent<SpriteRenderer>();
        if (halo == null)
        {
            Transform haloTransform = transform.Find("Halo");
            if (haloTransform != null) halo = haloTransform;
        }
        if (coreRenderer != null) coreScale = coreRenderer.transform.localScale;
        SetGravity(false);
    }

    private void OnEnable()
    {
        if (!activeHoles.Contains(this)) activeHoles.Add(this);
    }

    private void OnDisable() => activeHoles.Remove(this);

    private void Update()
    {
        float wave = Mathf.Sin(Time.time * (GravityEnabled ? 6f : 2f) + transform.position.x) * 0.5f + 0.5f;
        if (halo != null)
        {
            halo.localScale = Vector3.one * (GravityEnabled ? 1.11f + wave * 0.1f : 1f + wave * 0.02f);
            halo.Rotate(0f, 0f, GravityEnabled ? -40f * Time.deltaTime : -8f * Time.deltaTime);
        }
        if (coreRenderer != null)
            coreRenderer.transform.localScale = coreScale * (1f + wave * corePulse);
    }

    public void SetGravity(bool enabled)
    {
        GravityEnabled = enabled;
        if (coreRenderer != null) coreRenderer.color = Color.black;
        if (halo != null && halo.TryGetComponent(out SpriteRenderer haloRenderer))
            haloRenderer.color = enabled ? enabledHalo : disabledHalo;
    }

    public void ToggleGravity() => SetGravity(!GravityEnabled);

    public Vector2 GetAcceleration(Vector2 worldPosition)
    {
        if (!GravityEnabled) return Vector2.zero;
        Vector2 toHole = (Vector2)transform.position - worldPosition;
        float distanceSqr = toHole.sqrMagnitude;
        if (distanceSqr < 0.0001f) return Vector2.zero;
        return toHole.normalized * gravityStrength / (distanceSqr + softening * softening);
    }

    public static void ToggleAllGravity()
    {
        SetAllGravity(!AnyGravityEnabled);
        GameManager.Instance?.RefreshUI();
    }

    public static BlackHoleController GetClosestTo(Vector2 worldPosition)
    {
        BlackHoleController closest = null;
        float closestDistanceSqr = float.PositiveInfinity;

        foreach (BlackHoleController hole in activeHoles)
        {
            if (hole == null) continue;
            float distanceSqr = ((Vector2)hole.transform.position - worldPosition).sqrMagnitude;
            if (distanceSqr >= closestDistanceSqr) continue;

            closestDistanceSqr = distanceSqr;
            closest = hole;
        }

        return closest;
    }

    public static void SetAllGravity(bool enabled)
    {
        foreach (BlackHoleController hole in activeHoles)
            if (hole != null) hole.SetGravity(enabled);
    }

    public static Vector2 GetCombinedAcceleration(Vector2 worldPosition)
    {
        Vector2 acceleration = Vector2.zero;
        foreach (BlackHoleController hole in activeHoles)
            if (hole != null) acceleration += hole.GetAcceleration(worldPosition);
        return acceleration;
    }

    public static bool IsInsideAnySwallowRadius(Vector2 worldPosition)
    {
        foreach (BlackHoleController hole in activeHoles)
        {
            if (hole != null && Vector2.Distance(worldPosition, hole.transform.position) <= hole.swallowRadius)
                return true;
        }
        return false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.25f, 0.25f, 0.85f);
        Gizmos.DrawWireSphere(transform.position, swallowRadius);
    }
}
