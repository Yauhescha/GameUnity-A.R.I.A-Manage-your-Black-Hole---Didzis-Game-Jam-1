using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Runtime level builder. Add this single component to an empty object in a 2D scene.
/// It creates its own UI, lets the player place objects, and can endlessly launch
/// preview stars through the level without requiring GameManager or prefabs.
/// </summary>
public class MapBuilderController : MonoBehaviour
{
    private enum PlacementTool
    {
        None,
        SpawnPoint,
        BlackHole,
        Quasar,
        Portal,
        Obstacle
    }

    [Header("Preview run")]
    [SerializeField, Min(0.1f)] private float previewStarSpeed = 4.8f;
    [SerializeField, Min(0.1f)] private float secondsBetweenStars = 1.3f;
    [SerializeField, Min(1f)] private float previewStarLifetime = 14f;

    [Header("Builder look")]
    [SerializeField] private Color spawnColor = new Color(0.2f, 0.9f, 1f);
    [SerializeField] private Color portalColor = new Color(1f, 0.3f, 0.65f);
    [SerializeField] private Color holeHaloColor = new Color(0.45f, 0.28f, 1f);

    private PlacementTool activeTool;
    private MapEditableObject selectedObject;
    private Camera mainCamera;
    private bool isPreviewRunning;
    private Coroutine previewRoutine;
    private MapPreviewStar activePreviewStar;
    private int previewSpawnIndex;

    private Font uiFont;
    private Text toolHintText;
    private Text statusText;
    private Text inspectorTitle;
    private Text sizeValueText;
    private Text rotationValueText;
    private Text strengthValueText;
    private Text playButtonText;
    private Text previewGravityButtonText;
    private Slider sizeSlider;
    private Slider rotationSlider;
    private Slider strengthSlider;
    private GameObject inspectorPanel;
    private GameObject strengthRow;
    private GameObject obstacleGravityRow;
    private Button gravityToggleButton;
    private Toggle obstacleGravityToggle;
    private readonly Dictionary<PlacementTool, Button> toolButtons = new Dictionary<PlacementTool, Button>();

    private static Sprite discSprite;
    private static Sprite ringSprite;
    private static Sprite squareSprite;

    private void Awake()
    {
        mainCamera = Camera.main;
        uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        CreateBuilderUi();
        UpdateToolUi();
        SetStatus("SELECT AN OBJECT TO PLACE");
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace))
            DeleteSelectedObject();

        if (isPreviewRunning && Input.GetKeyDown(KeyCode.Space) && !IsPointerOverUi())
            TogglePreviewGravity();

        if (!Input.GetMouseButtonDown(0) || IsPointerOverUi() || mainCamera == null) return;

        Vector2 worldPosition = GetMouseWorldPosition();
        Collider2D hit = Physics2D.OverlapPoint(worldPosition);
        MapEditableObject editable = hit != null ? hit.GetComponentInParent<MapEditableObject>() : null;

        if (editable != null)
        {
            SelectObject(editable);
            return;
        }

        DeselectObject();
        if (!isPreviewRunning && activeTool != PlacementTool.None)
            PlaceObject(activeTool, worldPosition);
        else if (!isPreviewRunning)
            SetStatus("SELECT AN OBJECT TO PLACE");
    }

    private Vector2 GetMouseWorldPosition()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        Plane mapPlane = new Plane(Vector3.forward, Vector3.zero);
        return mapPlane.Raycast(ray, out float distance)
            ? (Vector2)ray.GetPoint(distance)
            : (Vector2)mainCamera.ScreenToWorldPoint(Input.mousePosition);
    }

    private bool IsPointerOverUi()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    private MapEditableObject PlaceObject(PlacementTool tool, Vector2 position, bool selectAfterPlacement = true)
    {
        GameObject root = new GameObject(tool.ToString());
        root.transform.position = position;
        root.transform.rotation = Quaternion.identity;

        MapEditableObject editable = root.AddComponent<MapEditableObject>();

        switch (tool)
        {
            case PlacementTool.SpawnPoint:
                root.name = "StarSpawnPoint";
                AddClickCircle(root);
                CreateSpawnVisual(root.transform);
                root.AddComponent<MapSpawnPoint>();
                editable.Configure(MapEditableObject.ObjectType.SpawnPoint, null);
                break;

            case PlacementTool.BlackHole:
                root.name = "BlackHole";
                AddClickCircle(root);
                BlackHoleController hole = CreateGravityVisual(root, Color.black, holeHaloColor);
                hole.SetGravity(false);
                editable.Configure(MapEditableObject.ObjectType.BlackHole, hole);
                break;

            case PlacementTool.Quasar:
                root.name = "Quasar";
                AddClickCircle(root);
                BlackHoleController quasar = CreateGravityVisual(root, Color.white, Color.white);
                root.AddComponent<MapQuasar>();
                quasar.SetGravity(true);
                editable.Configure(MapEditableObject.ObjectType.Quasar, quasar);
                break;

            case PlacementTool.Portal:
                root.name = "Portal";
                AddClickCircle(root);
                CreatePortalVisual(root);
                root.AddComponent<MapPortal>();
                editable.Configure(MapEditableObject.ObjectType.Portal, null);
                break;

            case PlacementTool.Obstacle:
                root.name = "GravityObstacle";
                root.transform.localScale = new Vector3(2.4f, 0.45f, 1f);
                CreateObstacleVisual(root);
                BoxCollider2D obstacleCollider = root.AddComponent<BoxCollider2D>();
                obstacleCollider.size = Vector2.one;
                MapGravityObstacle obstacle = root.AddComponent<MapGravityObstacle>();
                obstacle.SetAffectedByGravity(false);
                editable.Configure(MapEditableObject.ObjectType.Obstacle, null);
                break;
        }

        if (selectAfterPlacement)
        {
            SelectObject(editable);
            SetStatus(tool + " PLACED — CLICK IT TO EDIT");
        }
        UpdatePreviewGravityUi();
        return editable;
    }

    private static void AddClickCircle(GameObject root)
    {
        CircleCollider2D clickCollider = root.AddComponent<CircleCollider2D>();
        clickCollider.isTrigger = true;
        clickCollider.radius = 0.5f;
    }

    private BlackHoleController CreateGravityVisual(GameObject root, Color coreColor, Color haloColor)
    {
        // The sprite is a child because the size slider owns the root scale.
        CreateSpriteChild("Core", root.transform, GetDiscSprite(), coreColor, Vector3.one, 3);

        GameObject halo = CreateSpriteChild("Halo", root.transform, GetDiscSprite(), WithAlpha(haloColor, 0.32f),
            Vector3.one * 1.85f, 2);
        CreateSpriteChild("AccretionRing", root.transform, GetRingSprite(), WithAlpha(haloColor, 0.75f),
            Vector3.one * 1.25f, 4);
        halo.transform.localPosition = Vector3.zero;

        return root.AddComponent<BlackHoleController>();
    }

    private void CreatePortalVisual(GameObject root)
    {
        SpriteRenderer rim = root.AddComponent<SpriteRenderer>();
        rim.sprite = GetRingSprite();
        rim.color = Color.white;
        rim.sortingOrder = 3;

        CreateSpriteChild("ColorFill", root.transform, GetDiscSprite(), portalColor,
            Vector3.one * 0.74f, 4);
        CreateSpriteChild("PortalGlow", root.transform, GetDiscSprite(), WithAlpha(portalColor, 0.28f),
            Vector3.one * 1.75f, 1);
    }

    private void CreateSpawnVisual(Transform root)
    {
        CreateSpriteChild("SpawnCore", root, GetDiscSprite(), spawnColor, Vector3.one * 0.42f, 4);
        CreateSpriteChild("SpawnGlow", root, GetDiscSprite(), WithAlpha(spawnColor, 0.24f), Vector3.one, 2);
        GameObject arrow = CreateSpriteChild("LaunchDirection", root, GetDiscSprite(), Color.white,
            new Vector3(1.1f, 0.13f, 1f), 3);
        arrow.transform.localPosition = new Vector3(0.68f, 0f, 0f);
    }

    private void CreateObstacleVisual(GameObject root)
    {
        SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = GetSquareSprite();
        renderer.color = new Color(0.38f, 0.42f, 0.48f, 1f);
        renderer.sortingOrder = 3;
    }

    private GameObject CreateSpriteChild(string objectName, Transform parent, Sprite sprite, Color color,
        Vector3 localScale, int sortingOrder)
    {
        GameObject child = new GameObject(objectName);
        child.transform.SetParent(parent, false);
        child.transform.localScale = localScale;
        SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        return child;
    }

    private void SelectTool(PlacementTool tool)
    {
        DeselectObject();
        activeTool = tool;
        UpdateToolUi();
        SetStatus(tool == PlacementTool.None ? "SELECT AN OBJECT TO PLACE" : "CLICK EMPTY SPACE TO PLACE " + tool.ToString().ToUpperInvariant());
    }

    private void UpdateToolUi()
    {
        foreach (KeyValuePair<PlacementTool, Button> pair in toolButtons)
        {
            ColorBlock colors = pair.Value.colors;
            colors.normalColor = pair.Key == activeTool
                ? new Color(0.18f, 0.82f, 1f, 1f)
                : new Color(0.14f, 0.18f, 0.26f, 0.96f);
            pair.Value.colors = colors;
        }

        if (toolHintText != null)
            toolHintText.text = activeTool == PlacementTool.None ? "TOOL: NONE" : "TOOL: " + activeTool.ToString().ToUpperInvariant();
    }

    private void SelectObject(MapEditableObject editable)
    {
        selectedObject = editable;
        inspectorPanel.SetActive(true);
        inspectorTitle.text = editable.DisplayName.ToUpperInvariant();

        sizeSlider.SetValueWithoutNotify(editable.Size);
        rotationSlider.SetValueWithoutNotify(editable.RotationDegrees);
        sizeValueText.text = editable.Size.ToString("0.00");
        rotationValueText.text = editable.RotationDegrees.ToString("0") + "°";

        bool hasGravity = editable.HasGravitySource;
        strengthRow.SetActive(hasGravity);
        gravityToggleButton.gameObject.SetActive(hasGravity && !editable.IsQuasar);
        obstacleGravityRow.SetActive(editable.IsObstacle);
        if (hasGravity)
        {
            strengthSlider.SetValueWithoutNotify(editable.GravityStrength);
            strengthValueText.text = editable.GravityStrength.ToString("0.0");
            SetGravityButtonLabel(editable.GravityEnabled);
        }

        if (editable.IsObstacle)
            obstacleGravityToggle.SetIsOnWithoutNotify(editable.ObstacleAffectedByGravity);

        SetStatus("EDITING " + editable.DisplayName.ToUpperInvariant());
    }

    private void DeselectObject()
    {
        selectedObject = null;
        if (inspectorPanel != null) inspectorPanel.SetActive(false);
    }

    private void OnSizeChanged(float value)
    {
        if (selectedObject == null) return;
        selectedObject.SetSize(value);
        sizeValueText.text = value.ToString("0.00");
    }

    private void OnRotationChanged(float value)
    {
        if (selectedObject == null) return;
        selectedObject.SetRotation(value);
        rotationValueText.text = value.ToString("0") + "°";
    }

    private void OnStrengthChanged(float value)
    {
        if (selectedObject == null || !selectedObject.HasGravitySource) return;
        selectedObject.SetGravityStrength(value);
        strengthValueText.text = value.ToString("0.0");
    }

    private void OnObstacleGravityChanged(bool affectedByGravity)
    {
        if (selectedObject != null && selectedObject.IsObstacle)
            selectedObject.SetObstacleAffectedByGravity(affectedByGravity);
    }

    private void ToggleSelectedGravity()
    {
        if (selectedObject == null || !selectedObject.HasGravitySource || selectedObject.IsQuasar) return;
        selectedObject.ToggleGravity();
        SetGravityButtonLabel(selectedObject.GravityEnabled);
        UpdatePreviewGravityUi();
    }

    private void SetGravityButtonLabel(bool enabled)
    {
        Text label = gravityToggleButton != null ? gravityToggleButton.GetComponentInChildren<Text>() : null;
        if (label != null) label.text = enabled ? "GRAVITY: ON" : "GRAVITY: OFF";
    }

    private void DeleteSelectedObject()
    {
        if (selectedObject == null || isPreviewRunning) return;
        Destroy(selectedObject.gameObject);
        selectedObject = null;
        inspectorPanel.SetActive(false);
        SetStatus("OBJECT DELETED");
    }

    private void TogglePreviewRun()
    {
        if (isPreviewRunning) StopPreviewRun();
        else StartPreviewRun();
    }

    private void StartPreviewRun()
    {
        if (FindObjectsOfType<MapSpawnPoint>().Length == 0)
        {
            SetStatus("PLACE A STAR SPAWN POINT FIRST");
            return;
        }

        isPreviewRunning = true;
        playButtonText.text = "STOP";
        activeTool = PlacementTool.None;
        UpdateToolUi();
        activePreviewStar = null;
        previewSpawnIndex = 0;
        previewRoutine = StartCoroutine(PreviewStarLoop());
        SetStatus("PREVIEW RUNNING — INFINITE STAR LAUNCH");
    }

    private void StopPreviewRun()
    {
        isPreviewRunning = false;
        if (previewRoutine != null) StopCoroutine(previewRoutine);
        previewRoutine = null;
        playButtonText.text = "PLAY";
        activePreviewStar = null;

        foreach (MapPreviewStar star in FindObjectsOfType<MapPreviewStar>())
            Destroy(star.gameObject);

        SetStatus("PREVIEW STOPPED");
    }

    private IEnumerator PreviewStarLoop()
    {
        while (isPreviewRunning)
        {
            MapSpawnPoint[] spawnPoints = FindObjectsOfType<MapSpawnPoint>();
            if (spawnPoints.Length == 0)
            {
                StopPreviewRun();
                yield break;
            }

            MapSpawnPoint spawn = spawnPoints[previewSpawnIndex % spawnPoints.Length];
            previewSpawnIndex++;
            activePreviewStar = CreatePreviewStar(spawn.transform);

            yield return new WaitUntil(() => !isPreviewRunning || activePreviewStar == null);
            if (!isPreviewRunning) yield break;
            yield return new WaitForSeconds(secondsBetweenStars);
        }
    }

    private MapPreviewStar CreatePreviewStar(Transform spawn)
    {
        GameObject star = new GameObject("PreviewStar");
        star.transform.position = spawn.position;
        star.transform.localScale = Vector3.one * 0.19f;

        SpriteRenderer renderer = star.AddComponent<SpriteRenderer>();
        renderer.sprite = GetDiscSprite();
        renderer.color = Color.white;
        renderer.sortingOrder = 10;

        Rigidbody2D body = star.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.linearDamping = 0f;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        CircleCollider2D collider = star.AddComponent<CircleCollider2D>();
        collider.radius = 0.5f;

        MapPreviewStar previewStar = star.AddComponent<MapPreviewStar>();
        previewStar.Launch((Vector2)spawn.right * previewStarSpeed, previewStarLifetime);
        return previewStar;
    }

    private void TogglePreviewGravity()
    {
        bool anyNormalHoleEnabled = false;
        bool hasNormalHole = false;
        foreach (BlackHoleController hole in FindObjectsOfType<BlackHoleController>())
        {
            if (hole == null || hole.GetComponent<MapQuasar>() != null) continue;
            hasNormalHole = true;
            if (hole.GravityEnabled) anyNormalHoleEnabled = true;
        }

        if (!hasNormalHole)
        {
            SetStatus("NO SWITCHABLE BLACK HOLES");
            return;
        }

        bool enable = !anyNormalHoleEnabled;
        foreach (BlackHoleController hole in FindObjectsOfType<BlackHoleController>())
            if (hole != null && hole.GetComponent<MapQuasar>() == null) hole.SetGravity(enable);

        UpdatePreviewGravityUi();
    }

    private void UpdatePreviewGravityUi()
    {
        if (previewGravityButtonText == null) return;

        bool anyNormalHoleEnabled = false;
        foreach (BlackHoleController hole in FindObjectsOfType<BlackHoleController>())
        {
            if (hole != null && hole.GetComponent<MapQuasar>() == null && hole.GravityEnabled)
            {
                anyNormalHoleEnabled = true;
                break;
            }
        }
        previewGravityButtonText.text = anyNormalHoleEnabled ? "GRAVITY ON" : "GRAVITY OFF";
    }

    private void ExportMap()
    {
        MapSaveData map = new MapSaveData();
        foreach (MapEditableObject editable in FindObjectsOfType<MapEditableObject>())
        {
            if (editable == null) continue;
            map.objects.Add(editable.ToSaveData());
        }

        string folder = GetMapFolderPath();
        Directory.CreateDirectory(folder);
        string fileName = "NexusMap_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".json";
        string path = Path.Combine(folder, fileName);

        try
        {
            File.WriteAllText(path, JsonUtility.ToJson(map, true));
            SetStatus("EXPORTED: " + path);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            SetStatus("EXPORT FAILED — SEE CONSOLE");
        }
    }

    private void ImportNewestMap()
    {
        string folder = GetMapFolderPath();
        Directory.CreateDirectory(folder);

        string[] files = Directory.GetFiles(folder, "*.json");
        if (files.Length == 0)
        {
            SetStatus("COPY A JSON MAP INTO: " + folder);
            return;
        }

        Array.Sort(files, (left, right) => File.GetLastWriteTime(right).CompareTo(File.GetLastWriteTime(left)));
        try
        {
            MapSaveData map = JsonUtility.FromJson<MapSaveData>(File.ReadAllText(files[0]));
            if (map == null || map.objects == null)
            {
                SetStatus("INVALID MAP FILE");
                return;
            }

            StartCoroutine(ReplaceMapOnNextFrame(map, Path.GetFileName(files[0])));
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            SetStatus("IMPORT FAILED — SEE CONSOLE");
        }
    }

    private IEnumerator ReplaceMapOnNextFrame(MapSaveData map, string fileName)
    {
        if (isPreviewRunning) StopPreviewRun();
        DeselectObject();

        foreach (MapEditableObject editable in FindObjectsOfType<MapEditableObject>())
            Destroy(editable.gameObject);
        yield return null;

        foreach (MapObjectSaveData saved in map.objects)
        {
            if (!Enum.TryParse(saved.type, true, out PlacementTool tool) || tool == PlacementTool.None) continue;

            MapEditableObject editable = PlaceObject(tool, new Vector2(saved.x, saved.y), false);
            editable.SetBaseScale(new Vector3(saved.baseScaleX, saved.baseScaleY, 1f));
            editable.SetSize(saved.size);
            editable.SetRotation(saved.rotation);
            editable.SetGravityStrength(saved.gravityStrength);
            editable.SetGravityEnabled(saved.gravityEnabled);
            editable.SetObstacleAffectedByGravity(saved.obstacleAffectedByGravity);
        }

        activeTool = PlacementTool.None;
        UpdateToolUi();
        SetStatus("IMPORTED: " + fileName);
    }

    private static string GetMapFolderPath()
    {
        return Path.Combine(Application.persistentDataPath, "Nexus9Maps");
    }

    private void SetStatus(string message)
    {
        if (statusText != null) statusText.text = message;
    }

    private void CreateBuilderUi()
    {
        if (FindObjectOfType<EventSystem>() == null)
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        GameObject canvasObject = new GameObject("MapBuilderUI");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasObject.AddComponent<GraphicRaycaster>();

        RectTransform toolbar = CreatePanel("Toolbar", canvasObject.transform, new Color(0.025f, 0.045f, 0.09f, 0.96f));
        toolbar.anchorMin = new Vector2(0f, 1f);
        toolbar.anchorMax = new Vector2(1f, 1f);
        toolbar.pivot = new Vector2(0.5f, 1f);
        toolbar.sizeDelta = new Vector2(0f, 94f);
        HorizontalLayoutGroup toolbarLayout = toolbar.gameObject.AddComponent<HorizontalLayoutGroup>();
        toolbarLayout.padding = new RectOffset(24, 24, 16, 16);
        toolbarLayout.spacing = 10f;
        toolbarLayout.childAlignment = TextAnchor.MiddleLeft;

        toolHintText = CreateText("ToolHint", toolbar, "TOOL: NONE", 18, Color.white, 150f);
        AddToolButton(toolbar, "SPAWN", PlacementTool.SpawnPoint);
        AddToolButton(toolbar, "BLACK HOLE", PlacementTool.BlackHole);
        AddToolButton(toolbar, "QUASAR", PlacementTool.Quasar);
        AddToolButton(toolbar, "PORTAL", PlacementTool.Portal);
        AddToolButton(toolbar, "OBSTACLE", PlacementTool.Obstacle);

        Button clearToolButton = CreateButton("ClearTool", toolbar, "CANCEL", 112f, () => SelectTool(PlacementTool.None));
        clearToolButton.colors = GetButtonColors(new Color(0.22f, 0.18f, 0.25f, 0.96f));

        Button playButton = CreateButton("PlayButton", toolbar, "PLAY", 115f, TogglePreviewRun);
        playButton.colors = GetButtonColors(new Color(0.12f, 0.55f, 0.38f, 1f));
        playButtonText = playButton.GetComponentInChildren<Text>();

        Button exportButton = CreateButton("ExportButton", toolbar, "EXPORT", 112f, ExportMap);
        exportButton.colors = GetButtonColors(new Color(0.13f, 0.4f, 0.58f, 1f));
        Button importButton = CreateButton("ImportButton", toolbar, "IMPORT NEWEST", 148f, ImportNewestMap);
        importButton.colors = GetButtonColors(new Color(0.13f, 0.4f, 0.58f, 1f));

        RectTransform statusPanel = CreatePanel("Status", canvasObject.transform, new Color(0.025f, 0.045f, 0.09f, 0.9f));
        statusPanel.anchorMin = new Vector2(0.5f, 0f);
        statusPanel.anchorMax = new Vector2(0.5f, 0f);
        statusPanel.pivot = new Vector2(0.5f, 0f);
        statusPanel.anchoredPosition = new Vector2(0f, 22f);
        statusPanel.sizeDelta = new Vector2(700f, 52f);
        statusText = CreateText("StatusText", statusPanel, string.Empty, 18, new Color(0.65f, 0.9f, 1f), -1f);
        Stretch(statusText.rectTransform, 12f);
        statusText.alignment = TextAnchor.MiddleCenter;

        Button previewGravityButton = CreateButton("PreviewGravityButton", canvasObject.transform as RectTransform,
            "GRAVITY OFF", 330f, TogglePreviewGravity);
        previewGravityButton.colors = GetButtonColors(new Color(0.18f, 0.32f, 0.65f, 1f));
        RectTransform gravityButtonRect = previewGravityButton.GetComponent<RectTransform>();
        gravityButtonRect.anchorMin = new Vector2(0.5f, 0f);
        gravityButtonRect.anchorMax = new Vector2(0.5f, 0f);
        gravityButtonRect.pivot = new Vector2(0.5f, 0f);
        gravityButtonRect.anchoredPosition = new Vector2(0f, 88f);
        gravityButtonRect.sizeDelta = new Vector2(330f, 76f);
        previewGravityButtonText = previewGravityButton.GetComponentInChildren<Text>();
        UpdatePreviewGravityUi();

        RectTransform inspector = CreatePanel("ObjectInspector", canvasObject.transform, new Color(0.025f, 0.045f, 0.09f, 0.97f));
        inspector.anchorMin = new Vector2(1f, 1f);
        inspector.anchorMax = new Vector2(1f, 1f);
        inspector.pivot = new Vector2(1f, 1f);
        inspector.anchoredPosition = new Vector2(-22f, -118f);
        inspector.sizeDelta = new Vector2(315f, 500f);
        VerticalLayoutGroup inspectorLayout = inspector.gameObject.AddComponent<VerticalLayoutGroup>();
        inspectorLayout.padding = new RectOffset(18, 18, 18, 18);
        inspectorLayout.spacing = 10f;
        inspectorLayout.childControlWidth = true;
        inspectorLayout.childForceExpandWidth = true;
        inspectorPanel = inspector.gameObject;

        inspectorTitle = CreateText("InspectorTitle", inspector, "OBJECT", 22, Color.white, -1f);
        CreateText("InspectorHelp", inspector, "Size and rotation update immediately.", 14, new Color(0.65f, 0.75f, 0.85f), -1f);
        sizeSlider = CreateSliderRow(inspector, "SIZE", 0.2f, 3f, OnSizeChanged, out sizeValueText);
        rotationSlider = CreateSliderRow(inspector, "ROTATION", 0f, 360f, OnRotationChanged, out rotationValueText);

        strengthSlider = CreateSliderRow(inspector, "GRAVITY STRENGTH", 0.01f, 90f,
            OnStrengthChanged, out strengthValueText);
        strengthRow = strengthSlider.transform.parent.gameObject;

        obstacleGravityToggle = CreateToggleRow(inspector, "AFFECTED BY GRAVITY", OnObstacleGravityChanged);
        obstacleGravityRow = obstacleGravityToggle.gameObject;

        gravityToggleButton = CreateButton("GravityToggle", inspector, "GRAVITY: OFF", -1f, ToggleSelectedGravity);
        gravityToggleButton.colors = GetButtonColors(new Color(0.34f, 0.18f, 0.7f, 1f));
        Button deleteButton = CreateButton("Delete", inspector, "DELETE OBJECT", -1f, DeleteSelectedObject);
        deleteButton.colors = GetButtonColors(new Color(0.62f, 0.16f, 0.24f, 1f));

        inspectorPanel.SetActive(false);
    }

    private void AddToolButton(RectTransform parent, string label, PlacementTool tool)
    {
        Button button = CreateButton(label, parent, label, 138f, () => SelectTool(tool));
        toolButtons.Add(tool, button);
    }

    private RectTransform CreatePanel(string name, Transform parent, Color color)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        panel.GetComponent<Image>().color = color;
        return panel.GetComponent<RectTransform>();
    }

    private Button CreateButton(string name, RectTransform parent, string label, float preferredWidth, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);
        Image image = buttonObject.GetComponent<Image>();
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.colors = GetButtonColors(new Color(0.14f, 0.18f, 0.26f, 0.96f));
        button.onClick.AddListener(onClick);

        LayoutElement element = buttonObject.GetComponent<LayoutElement>();
        element.preferredWidth = preferredWidth;
        element.preferredHeight = 48f;

        Text text = CreateText("Label", buttonObject.transform as RectTransform, label, 16, Color.white, -1f);
        Stretch(text.rectTransform, 3f);
        text.alignment = TextAnchor.MiddleCenter;
        return button;
    }

    private Text CreateText(string name, RectTransform parent, string content, int fontSize, Color color, float preferredWidth)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(LayoutElement));
        textObject.transform.SetParent(parent, false);
        Text text = textObject.GetComponent<Text>();
        text.font = uiFont;
        text.text = content;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = TextAnchor.MiddleLeft;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;

        LayoutElement element = textObject.GetComponent<LayoutElement>();
        element.preferredWidth = preferredWidth;
        element.preferredHeight = 30f;
        return text;
    }

    private Slider CreateSliderRow(RectTransform parent, string label, float min, float max,
        UnityEngine.Events.UnityAction<float> onChanged, out Text valueText)
    {
        GameObject row = new GameObject(label + "Row", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        VerticalLayoutGroup layout = row.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 2f;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
        row.GetComponent<LayoutElement>().preferredHeight = 62f;

        GameObject header = new GameObject("Header", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        header.transform.SetParent(row.transform, false);
        header.GetComponent<HorizontalLayoutGroup>().childControlWidth = true;
        header.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
        header.GetComponent<HorizontalLayoutGroup>().spacing = 6f;
        header.AddComponent<LayoutElement>().preferredHeight = 24f;

        CreateText("Label", header.transform as RectTransform, label, 14, new Color(0.7f, 0.82f, 0.95f), 205f);
        valueText = CreateText("Value", header.transform as RectTransform, "—", 14, Color.white, 55f);
        valueText.alignment = TextAnchor.MiddleRight;

        GameObject sliderObject = new GameObject("Slider", typeof(RectTransform), typeof(Slider), typeof(LayoutElement));
        sliderObject.transform.SetParent(row.transform, false);
        sliderObject.GetComponent<LayoutElement>().preferredHeight = 26f;
        Slider slider = sliderObject.GetComponent<Slider>();
        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = false;

        Image background = sliderObject.AddComponent<Image>();
        background.color = new Color(0.12f, 0.16f, 0.24f, 1f);
        GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(sliderObject.transform, false);
        Stretch(fillArea.GetComponent<RectTransform>(), 4f);
        GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(fillArea.transform, false);
        fill.GetComponent<Image>().color = new Color(0.17f, 0.8f, 1f, 1f);
        Stretch(fill.GetComponent<RectTransform>(), 0f);
        slider.fillRect = fill.GetComponent<RectTransform>();

        GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(sliderObject.transform, false);
        Stretch(handleArea.GetComponent<RectTransform>(), 0f);
        GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handle.transform.SetParent(handleArea.transform, false);
        handle.GetComponent<Image>().color = Color.white;
        handle.GetComponent<RectTransform>().sizeDelta = new Vector2(18f, 26f);
        slider.handleRect = handle.GetComponent<RectTransform>();
        slider.targetGraphic = handle.GetComponent<Image>();
        slider.onValueChanged.AddListener(onChanged);
        return slider;
    }

    private Toggle CreateToggleRow(RectTransform parent, string label, UnityEngine.Events.UnityAction<bool> onChanged)
    {
        GameObject row = new GameObject(label + "Toggle", typeof(RectTransform), typeof(Image), typeof(Toggle), typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        row.GetComponent<LayoutElement>().preferredHeight = 38f;
        Image background = row.GetComponent<Image>();
        background.color = new Color(0.12f, 0.16f, 0.24f, 1f);

        Toggle toggle = row.GetComponent<Toggle>();
        toggle.targetGraphic = background;

        GameObject check = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
        check.transform.SetParent(row.transform, false);
        RectTransform checkRect = check.GetComponent<RectTransform>();
        checkRect.anchorMin = new Vector2(0f, 0.5f);
        checkRect.anchorMax = new Vector2(0f, 0.5f);
        checkRect.pivot = new Vector2(0f, 0.5f);
        checkRect.anchoredPosition = new Vector2(12f, 0f);
        checkRect.sizeDelta = new Vector2(18f, 18f);
        Image checkImage = check.GetComponent<Image>();
        checkImage.color = new Color(0.2f, 0.86f, 1f, 1f);
        toggle.graphic = checkImage;

        Text text = CreateText("Label", row.transform as RectTransform, label, 14, Color.white, -1f);
        text.rectTransform.anchorMin = new Vector2(0f, 0f);
        text.rectTransform.anchorMax = new Vector2(1f, 1f);
        text.rectTransform.offsetMin = new Vector2(42f, 0f);
        text.rectTransform.offsetMax = new Vector2(-8f, 0f);
        text.alignment = TextAnchor.MiddleLeft;
        toggle.onValueChanged.AddListener(onChanged);
        return toggle;
    }

    private static ColorBlock GetButtonColors(Color normal)
    {
        return new ColorBlock
        {
            normalColor = normal,
            highlightedColor = Color.Lerp(normal, Color.white, 0.18f),
            pressedColor = Color.Lerp(normal, Color.black, 0.25f),
            selectedColor = normal,
            disabledColor = new Color(0.2f, 0.2f, 0.2f, 0.5f),
            colorMultiplier = 1f,
            fadeDuration = 0.08f
        };
    }

    private static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.one * inset;
        rect.offsetMax = Vector2.one * -inset;
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    public static Sprite GetDiscSprite()
    {
        if (discSprite == null) discSprite = CreateCircleSprite("RuntimeDisc", false);
        return discSprite;
    }

    private static Sprite GetRingSprite()
    {
        if (ringSprite == null) ringSprite = CreateCircleSprite("RuntimeRing", true);
        return ringSprite;
    }

    private static Sprite GetSquareSprite()
    {
        if (squareSprite != null) return squareSprite;

        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        texture.name = "RuntimeSquareTexture";
        texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
        texture.Apply();
        squareSprite = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f), 2f);
        return squareSprite;
    }

    private static Sprite CreateCircleSprite(string spriteName, bool ring)
    {
        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = spriteName + "Texture";
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float px = (x + 0.5f) / size * 2f - 1f;
            float py = (y + 0.5f) / size * 2f - 1f;
            float distance = Mathf.Sqrt(px * px + py * py);
            float alpha = ring
                ? Mathf.Clamp01((1f - Mathf.Abs(distance - 0.77f)) * 12f)
                : Mathf.Clamp01((1f - distance) * 15f);
            texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
    }
}

/// <summary>Metadata and editable values for an object made by MapBuilderController.</summary>
public class MapEditableObject : MonoBehaviour
{
    public enum ObjectType { SpawnPoint, BlackHole, Quasar, Portal, Obstacle }

    private ObjectType type;
    private BlackHoleController gravitySource;
    private MapGravityObstacle obstacle;
    private float size = 1f;
    private Vector3 baseScale = Vector3.one;

    public string DisplayName => type == ObjectType.SpawnPoint ? "Star Respawn" : type.ToString();
    public bool HasGravitySource => gravitySource != null;
    public bool IsQuasar => type == ObjectType.Quasar;
    public bool IsObstacle => type == ObjectType.Obstacle;
    public float Size => size;
    public float RotationDegrees => transform.eulerAngles.z;
    public float GravityStrength => gravitySource != null ? gravitySource.gravityStrength : 0f;
    public bool GravityEnabled => gravitySource != null && gravitySource.GravityEnabled;
    public bool ObstacleAffectedByGravity => obstacle != null && obstacle.AffectedByGravity;

    public void Configure(ObjectType objectType, BlackHoleController source)
    {
        type = objectType;
        gravitySource = source;
        obstacle = GetComponent<MapGravityObstacle>();
        baseScale = transform.localScale;
    }

    public void SetSize(float value)
    {
        size = Mathf.Clamp(value, 0.2f, 3f);
        transform.localScale = baseScale * size;
    }

    public void SetRotation(float zAngle)
    {
        transform.rotation = Quaternion.Euler(0f, 0f, zAngle);
    }

    public void SetGravityStrength(float value)
    {
        if (gravitySource != null) gravitySource.gravityStrength = Mathf.Max(0.01f, value);
    }

    public void ToggleGravity()
    {
        if (gravitySource != null && !IsQuasar) gravitySource.ToggleGravity();
    }

    public void SetGravityEnabled(bool enabled)
    {
        if (gravitySource != null && !IsQuasar) gravitySource.SetGravity(enabled);
    }

    public void SetObstacleAffectedByGravity(bool affectedByGravity)
    {
        if (obstacle != null) obstacle.SetAffectedByGravity(affectedByGravity);
    }

    public void SetBaseScale(Vector3 value)
    {
        baseScale = new Vector3(Mathf.Max(0.01f, value.x), Mathf.Max(0.01f, value.y), 1f);
        transform.localScale = baseScale * size;
    }

    public MapObjectSaveData ToSaveData()
    {
        return new MapObjectSaveData
        {
            type = type.ToString(),
            x = transform.position.x,
            y = transform.position.y,
            rotation = RotationDegrees,
            size = size,
            baseScaleX = baseScale.x,
            baseScaleY = baseScale.y,
            gravityStrength = GravityStrength,
            gravityEnabled = GravityEnabled,
            obstacleAffectedByGravity = ObstacleAffectedByGravity
        };
    }
}

/// <summary>Marker component used by the preview launcher.</summary>
public class MapSpawnPoint : MonoBehaviour { }

/// <summary>Marker component for a portal placed by the runtime builder.</summary>
public class MapPortal : MonoBehaviour { }

/// <summary>Rectangular obstacle that optionally receives the combined gravity of every active source.</summary>
[RequireComponent(typeof(BoxCollider2D))]
public class MapGravityObstacle : MonoBehaviour
{
    private Rigidbody2D body;
    public bool AffectedByGravity { get; private set; }

    public void SetAffectedByGravity(bool value)
    {
        AffectedByGravity = value;
        if (value)
        {
            if (body == null) body = gameObject.GetComponent<Rigidbody2D>() ?? gameObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;
            body.linearDamping = 0f;
            body.angularDamping = 0f;
        }
        else if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.bodyType = RigidbodyType2D.Static;
        }
    }

    private void FixedUpdate()
    {
        if (AffectedByGravity && body != null)
            body.AddForce(BlackHoleController.GetCombinedAcceleration(body.position), ForceMode2D.Force);
    }
}

[Serializable]
public class MapSaveData
{
    public List<MapObjectSaveData> objects = new List<MapObjectSaveData>();
}

[Serializable]
public class MapObjectSaveData
{
    public string type;
    public float x;
    public float y;
    public float rotation;
    public float size = 1f;
    public float baseScaleX = 1f;
    public float baseScaleY = 1f;
    public float gravityStrength = 24f;
    public bool gravityEnabled;
    public bool obstacleAffectedByGravity;
}

/// <summary>A white gravity source that cannot be switched off.</summary>
[RequireComponent(typeof(BlackHoleController))]
public class MapQuasar : MonoBehaviour
{
    private BlackHoleController source;
    private SpriteRenderer core;
    private SpriteRenderer halo;

    private void Awake()
    {
        source = GetComponent<BlackHoleController>();
        Transform coreTransform = transform.Find("Core");
        if (coreTransform != null) core = coreTransform.GetComponent<SpriteRenderer>();
        Transform haloTransform = transform.Find("Halo");
        if (haloTransform != null) halo = haloTransform.GetComponent<SpriteRenderer>();
    }

    private void OnEnable()
    {
        if (source == null) source = GetComponent<BlackHoleController>();
        SetAlwaysOnLook();
    }

    private void Update()
    {
        if (source != null && !source.GravityEnabled) SetAlwaysOnLook();
        if (core != null) core.color = Color.white;
    }

    private void SetAlwaysOnLook()
    {
        if (source != null) source.SetGravity(true);
        if (core != null) core.color = Color.white;
        if (halo != null) halo.color = new Color(1f, 1f, 1f, 0.46f);
    }
}

/// <summary>Simple physics-only star used by the builder's endless preview mode.</summary>
[RequireComponent(typeof(Rigidbody2D))]
public class MapPreviewStar : MonoBehaviour
{
    private Rigidbody2D body;
    private float destroyAt;

    private void Awake() => body = GetComponent<Rigidbody2D>();

    public void Launch(Vector2 velocity, float lifetime)
    {
        body.linearVelocity = velocity;
        destroyAt = Time.time + lifetime;
    }

    private void FixedUpdate()
    {
        body.AddForce(BlackHoleController.GetCombinedAcceleration(body.position), ForceMode2D.Force);

        // Same rule as the playable scene: entering the swallow radius of any
        // black hole or always-on quasar destroys the stellar core immediately.
        if (BlackHoleController.IsInsideAnySwallowRadius(body.position))
            Destroy(gameObject);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.collider.GetComponentInParent<MapGravityObstacle>() != null)
            Destroy(gameObject);
    }

    private void Update()
    {
        if (Time.time >= destroyAt) Destroy(gameObject);
    }
}
