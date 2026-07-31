using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;


public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Portal order")]
    [Tooltip("When enabled, portals launch in ascending Queue Order from their own PortalTarget component.")]
    [SerializeField] private bool usePortalSequence = true;
    [SerializeField] private bool loopPortalSequence = true;

    [Header("Black hole control")]
    [Tooltip("Off: Space/button toggles every black hole together. On: click a hole to toggle it; Space/button toggles the hole closest to the current star.")]
    [SerializeField] private bool useIndividualBlackHoleControl;

    [Header("Balance")]
    [SerializeField, Min(1)] private int deliveriesToWin = 5;
    [SerializeField, Min(1)] private int maximumMisses = 3;
    [SerializeField, Min(0.1f)] private float launchSpeed = 4.8f;
    [SerializeField, Min(0f)] private float delayBeforeNextStar = 0.7f;
    [SerializeField] private float outOfBoundsMargin = 0.01f;
    [SerializeField] private string levelNameText = "LevelName";

    [Header("Automatic names")]
    [Tooltip("Put the StarProjectile prefab at Assets/Resources/Star.prefab.")]
    [SerializeField] private string levelNamePath = "LevelName";
    [SerializeField] private string starPrefabResourcesPath = "Star";
    [SerializeField] private string launchPointName = "LaunchPoint";
    [SerializeField] private string scoreTextName = "ScoreText";
    [SerializeField] private string livesTextName = "LivesText";
    [SerializeField] private string targetTextName = "TargetText";
    [SerializeField] private string gravityButtonTextName = "GravityButtonText";
    [SerializeField] private string statusTextName = "StatusText";
    [SerializeField] private string gamePanelName = "GamePanel";
    [SerializeField] private string winPanelName = "WinPanel";
    [SerializeField] private string losePanelName = "LosePanel";

    private Camera mainCamera;
    private StarProjectile starPrefab;
    private Transform launchPoint;
    private PortalTarget[] portals;
    private TextMeshProUGUI levelName;
    private TextMeshProUGUI scoreText;
    private TextMeshProUGUI livesText;
    private TextMeshProUGUI targetText;
    private TextMeshProUGUI gravityButtonText;
    private TextMeshProUGUI statusText;
    private GameObject gamePanel;
    private GameObject winPanel;
    private GameObject losePanel;
    private StarProjectile activeStar;
    private PortalTarget activeTarget;
    private PortalTarget pendingTarget;
    private int portalSequenceIndex;
    private int deliveries;
    private int misses;
    private bool finished;
    private readonly Collider2D[] holeClickResults = new Collider2D[32];
    private readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();

    private void Awake()
    {
        Instance = this;
        mainCamera = Camera.main;
        FindSceneReferences();
        if (gamePanel != null) gamePanel.SetActive(true);
        if (winPanel != null) winPanel.SetActive(false);
        if (losePanel != null) losePanel.SetActive(false);
    }

    private void Start()
    {
        if (!ValidateLevel()) return;

        BlackHoleController.SetAllGravity(false);
        SetAllPortalsHighlighted(false);
        RefreshUI();
        StartCoroutine(LaunchNextStar());
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            RestartLevel();
            return;
        }

        if (finished) return;

        if (useIndividualBlackHoleControl)
            HandleIndividualHoleClick();

        if (Input.GetKeyDown(KeyCode.Space))
            ToggleGravityForCurrentControlMode();
    }

    public void ToggleGravityFromButton()
    {
        if (!finished) ToggleGravityForCurrentControlMode();
    }

    private void ToggleGravityForCurrentControlMode()
    {
        if (!useIndividualBlackHoleControl)
        {
            BlackHoleController.ToggleAllGravity();
            return;
        }

        Vector2 referencePoint = activeStar != null
            ? activeStar.transform.position
            : launchPoint != null ? launchPoint.position : Vector2.zero;
        BlackHoleController closestHole = BlackHoleController.GetClosestTo(referencePoint, true);
        if (closestHole != null)
        {
            closestHole.ToggleGravity();
            RefreshUI();
        }
    }

    private void HandleIndividualHoleClick()
    {
        if (!Input.GetMouseButtonDown(0) || mainCamera == null) return;
        if (IsPointerOverInteractiveUi()) return;

        Vector2 worldPosition = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        BlackHoleController hole = FindSwitchableHoleAt(worldPosition);
        if (hole == null) return;

        hole.ToggleGravity();
        RefreshUI();
    }

    private BlackHoleController FindSwitchableHoleAt(Vector2 worldPosition)
    {
        ContactFilter2D filter = new ContactFilter2D();
        filter.useLayerMask = false;
        filter.useDepth = false;
        filter.useTriggers = true;
        int hitCount = Physics2D.OverlapPoint(worldPosition, filter, holeClickResults);

        BlackHoleController closestHit = null;
        float closestDistanceSqr = float.PositiveInfinity;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = holeClickResults[i];
            if (hit == null) continue;

            BlackHoleController hole = hit.GetComponentInParent<BlackHoleController>();
            if (hole == null || !hole.IsSwitchable) continue;

            float distanceSqr = ((Vector2)hole.transform.position - worldPosition).sqrMagnitude;
            if (distanceSqr >= closestDistanceSqr) continue;

            closestDistanceSqr = distanceSqr;
            closestHit = hole;
        }

        if (closestHit != null) return closestHit;

        // Supports previously made black-hole prefabs even if they did not yet
        // have a collider at the time the level was saved.
        BlackHoleController closestHole = BlackHoleController.GetClosestTo(worldPosition, true);
        return closestHole != null && closestHole.IsInsideClickRadius(worldPosition)
            ? closestHole
            : null;
    }

    private bool IsPointerOverInteractiveUi()
    {
        if (EventSystem.current == null) return false;

        PointerEventData pointer = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };

        uiRaycastResults.Clear();
        EventSystem.current.RaycastAll(pointer, uiRaycastResults);

        foreach (RaycastResult result in uiRaycastResults)
        {
            if (result.gameObject.GetComponentInParent<Selectable>() != null)
                return true;
        }

        return false;
    }

    private IEnumerator LaunchNextStar()
    {
        yield return new WaitForSeconds(delayBeforeNextStar);
        if (finished) yield break;

        activeTarget = GetPendingTarget();
        if (activeTarget == null)
        {
            Finish(false, "NO PORTAL CONFIGURED");
            yield break;
        }

        SetAllPortalsHighlighted(false);
        activeTarget.SetHighlighted(true);
        Vector2 velocity = launchPoint.right * launchSpeed;
        activeStar = Instantiate(starPrefab, launchPoint.position, Quaternion.identity);
        activeStar.Launch(this, activeTarget, velocity);
        RefreshUI();
    }

    public void StarDelivered(StarProjectile star, PortalTarget portal)
    {
        if (finished || star != activeStar) return;

        deliveries++;
        activeStar = null;
        portal.SetHighlighted(false);
        AdvancePortalSequence();
        pendingTarget = null;

        if (deliveries >= deliveriesToWin)
        {
            Finish(true, "CONSTELLATION RESTORED");
            return;
        }

        StartCoroutine(LaunchNextStar());
        RefreshUI();
    }

    public void StarFailed(StarProjectile star, string reason)
    {
        if (finished || star != activeStar) return;

        misses++;
        activeStar = null;
        // pendingTarget and queue index deliberately remain untouched: retry same portal.
        if (misses >= maximumMisses)
        {
            Finish(false, reason);
            return;
        }

        if (statusText != null) statusText.text = reason + " — TRY AGAIN";
        StartCoroutine(LaunchNextStar());
        RefreshUI();
    }

    public bool IsOutsidePlayArea(Vector2 worldPosition)
    {
        Vector3 viewportPosition = mainCamera.WorldToViewportPoint(worldPosition);
        return viewportPosition.x < -outOfBoundsMargin || viewportPosition.x > 1f + outOfBoundsMargin
            || viewportPosition.y < -outOfBoundsMargin || viewportPosition.y > 1f + outOfBoundsMargin;
    }

    private void FindSceneReferences()
    {
        starPrefab = Resources.Load<StarProjectile>(starPrefabResourcesPath);
        launchPoint = FindSceneComponentByName<Transform>(launchPointName);
        portals = FindSceneComponents<PortalTarget>();
        System.Array.Sort(portals, ComparePortalsByQueue);

        levelName = FindSceneComponentByName<TextMeshProUGUI>(levelNamePath);
        scoreText = FindSceneComponentByName<TextMeshProUGUI>(scoreTextName);
        livesText = FindSceneComponentByName<TextMeshProUGUI>(livesTextName);
        targetText = FindSceneComponentByName<TextMeshProUGUI>(targetTextName);
        gravityButtonText = FindSceneComponentByName<TextMeshProUGUI>(gravityButtonTextName);
        statusText = FindSceneComponentByName<TextMeshProUGUI>(statusTextName);
        gamePanel = FindSceneObjectByName(gamePanelName);
        winPanel = FindSceneObjectByName(winPanelName);
        losePanel = FindSceneObjectByName(losePanelName);
        levelName.text = levelNameText;
    }

    private bool ValidateLevel()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return ConfigurationError("NO MAIN CAMERA");
        if (BlackHoleController.ActiveHoleCount == 0) return ConfigurationError("NO BLACK HOLE");
        if (starPrefab == null) return ConfigurationError("MISSING RESOURCES/STAR PREFAB");
        if (launchPoint == null) return ConfigurationError("MISSING LAUNCHPOINT");
        if (portals == null || portals.Length == 0) return ConfigurationError("NO PORTALS");
        return true;
    }

    private bool ConfigurationError(string error)
    {
        Debug.LogError("GameManager: " + error, this);
        if (statusText != null) statusText.text = error;
        finished = true;
        return false;
    }

    private PortalTarget GetPendingTarget()
    {
        if (pendingTarget != null) return pendingTarget;
        if (usePortalSequence)
        {
            int index = Mathf.Clamp(portalSequenceIndex, 0, portals.Length - 1);
            pendingTarget = portals[index];
        }
        else
        {
            pendingTarget = portals[Random.Range(0, portals.Length)];
        }
        return pendingTarget;
    }

    private void AdvancePortalSequence()
    {
        if (!usePortalSequence) return;
        if (loopPortalSequence)
            portalSequenceIndex = (portalSequenceIndex + 1) % portals.Length;
        else
            portalSequenceIndex = Mathf.Min(portalSequenceIndex + 1, portals.Length - 1);
    }

    private void Finish(bool won, string message)
    {
        finished = true;
        SetAllPortalsHighlighted(false);
        if (statusText != null) statusText.text = message;

        if (won && winPanel != null) {
            gamePanel.SetActive(false); 
            winPanel.SetActive(true); 
        }

        if (!won && losePanel != null)
        {
            gamePanel.SetActive(false);
            losePanel.SetActive(true);
        }
    }

    private void SetAllPortalsHighlighted(bool highlighted)
    {
        if (portals == null) return;
        foreach (PortalTarget portal in portals)
            if (portal != null) portal.SetHighlighted(highlighted);
    }

    public void RefreshUI()
    {
        if (scoreText != null) scoreText.text = $"{deliveries} / {deliveriesToWin}";
        if (livesText != null) livesText.text = $"{misses} / {maximumMisses}";
        if (targetText != null)
            targetText.text = activeTarget == null ? "—" : activeTarget.DisplayName;
        if (gravityButtonText != null)
        {
            if (useIndividualBlackHoleControl)
            {
                Vector2 referencePoint = activeStar != null
                    ? activeStar.transform.position
                    : launchPoint != null ? launchPoint.position : Vector2.zero;
                BlackHoleController closestHole = BlackHoleController.GetClosestTo(referencePoint, true);

                gravityButtonText.text = closestHole == null
                    ? "TOGGLE NEAREST HOLE"
                    : closestHole.GravityEnabled ? "NEAREST HOLE: ON" : "NEAREST HOLE: OFF";
            }
            else
            {
                gravityButtonText.text = BlackHoleController.AnySwitchableGravityEnabled ? "Active" : "Disabled";
            }
        }
        if (statusText != null && activeStar != null)
            statusText.text = useIndividualBlackHoleControl
                ? "CLICK SPACE — TOGGLE NEAREST"
                : "SPACE — SWITCH GRAVITY";
    }

    public void RestartLevel()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }

    private static int ComparePortalsByQueue(PortalTarget a, PortalTarget b)
    {
        int compare = a.QueueOrder.CompareTo(b.QueueOrder);
        return compare != 0 ? compare : a.GetInstanceID().CompareTo(b.GetInstanceID());
    }

    private static T FindSceneComponentByName<T>(string objectName) where T : Component
    {
        foreach (T component in Resources.FindObjectsOfTypeAll<T>())
        {
            if (component != null && component.gameObject.scene.IsValid()
                && component.gameObject.name == objectName)
                return component;
        }
        return null;
    }

    private static T[] FindSceneComponents<T>() where T : Component
    {
        List<T> found = new List<T>();
        foreach (T component in Resources.FindObjectsOfTypeAll<T>())
        {
            if (component != null && component.gameObject.scene.IsValid()) found.Add(component);
        }
        return found.ToArray();
    }

    private static GameObject FindSceneObjectByName(string objectName)
    {
        foreach (GameObject gameObject in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (gameObject != null && gameObject.scene.IsValid() && gameObject.name == objectName)
                return gameObject;
        }
        return null;
    }

    public static void LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }
}
