using UnityEngine;

/// <summary>
/// A permanently active gravity source. Add it to the same root object as
/// BlackHoleController. It uses the normal swallow-radius logic, so stars are
/// absorbed exactly like by a regular black hole.
/// </summary>
[RequireComponent(typeof(BlackHoleController))]
public class QuasarController : MonoBehaviour
{
    [Header("Visual parts")]
    [SerializeField] private SpriteRenderer core;
    [SerializeField] private Transform accretionRing;
    [SerializeField] private SpriteRenderer accretionRingRenderer;
    [SerializeField] private Transform outerGlow;
    [SerializeField] private SpriteRenderer outerGlowRenderer;
    [SerializeField] private Transform[] jets = new Transform[0];

    [Header("Animation")]
    [SerializeField, Min(0.01f)] private float ringRotationSpeed = 105f;
    [SerializeField, Min(0f)] private float glowPulseAmount = 0.16f;
    [SerializeField, Min(0.01f)] private float glowPulseSpeed = 3.2f;
    [SerializeField] private Color coreColor = Color.white;
    [SerializeField] private Color ringColor = new Color(0.55f, 0.9f, 1f, 1f);
    [SerializeField] private Color glowColor = new Color(0.25f, 0.72f, 1f, 0.36f);

    private BlackHoleController gravitySource;
    private Vector3 outerGlowBaseScale;
    private Vector3[] jetBaseScales;
    private float seed;

    private void Awake()
    {
        gravitySource = GetComponent<BlackHoleController>();
        if (jets == null) jets = new Transform[0];
        if (jets.Length == 0)
        {
            Transform topJet = transform.Find("JetTop");
            Transform bottomJet = transform.Find("JetBottom");
            if (topJet != null && bottomJet != null) jets = new[] { topJet, bottomJet };
            else if (topJet != null) jets = new[] { topJet };
            else if (bottomJet != null) jets = new[] { bottomJet };
        }

        if (core == null)
        {
            Transform coreTransform = transform.Find("Core");
            if (coreTransform != null) core = coreTransform.GetComponent<SpriteRenderer>();
        }

        if (accretionRing == null)
        {
            Transform ringTransform = transform.Find("AccretionRing");
            if (ringTransform != null) accretionRing = ringTransform;
        }

        if (accretionRingRenderer == null && accretionRing != null)
            accretionRingRenderer = accretionRing.GetComponent<SpriteRenderer>();

        if (outerGlow == null)
        {
            Transform glowTransform = transform.Find("Halo");
            if (glowTransform != null) outerGlow = glowTransform;
        }

        if (outerGlowRenderer == null && outerGlow != null)
            outerGlowRenderer = outerGlow.GetComponent<SpriteRenderer>();

        outerGlowBaseScale = outerGlow != null ? outerGlow.localScale : Vector3.one;
        jetBaseScales = new Vector3[jets.Length];
        for (int i = 0; i < jets.Length; i++)
            if (jets[i] != null) jetBaseScales[i] = jets[i].localScale;

        seed = Random.value * 100f;
        ForceAlwaysOn();

        if (core != null) core.color = coreColor;
        ApplyColors();
    }

    private void OnEnable() => ForceAlwaysOn();

    private void LateUpdate()
    {
        // Keeps the quasar active even if another script tried to switch every
        // gravity source off. It is also excluded from normal controls.
        ForceAlwaysOn();

        float wave = Mathf.Sin(Time.time * glowPulseSpeed + seed) * 0.5f + 0.5f;

        if (accretionRing != null)
            accretionRing.Rotate(0f, 0f, -ringRotationSpeed * Time.deltaTime);

        if (outerGlow != null)
            outerGlow.localScale = outerGlowBaseScale * (1f + wave * glowPulseAmount);

        if (outerGlowRenderer != null)
        {
            Color color = glowColor;
            color.a *= 0.7f + wave * 0.3f;
            outerGlowRenderer.color = color;
        }

        for (int i = 0; i < jets.Length; i++)
        {
            if (jets[i] == null) continue;
            float jetWave = Mathf.Sin(Time.time * (glowPulseSpeed * 1.45f) + seed + i) * 0.5f + 0.5f;
            jets[i].localScale = jetBaseScales[i] * (1f + jetWave * 0.18f);
        }
    }

    private void ForceAlwaysOn()
    {
        if (gravitySource == null) gravitySource = GetComponent<BlackHoleController>();
        if (gravitySource == null) return;

        gravitySource.SetSwitchable(false);
        if (!gravitySource.GravityEnabled) gravitySource.SetGravity(true);
    }

    private void ApplyColors()
    {
        if (core != null) core.color = coreColor;
        if (accretionRingRenderer != null) accretionRingRenderer.color = ringColor;
        if (outerGlowRenderer != null) outerGlowRenderer.color = glowColor;
    }
}
