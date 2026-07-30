using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Editor helper: creates a showcase of reusable black-hole, portal and star variants.
/// Add it to any empty scene object, use the component menu (⋮) → Build UI Showcase,
/// then drag generated roots to the Project window to make prefabs.
/// </summary>
public class BootstrapUi : MonoBehaviour
{
    private const string GeneratedRootName = "Generated_UI_Showcase";
    private const string AssetFolder = "Assets/GeneratedGravityUi";
    private const string CircleAssetPath = AssetFolder + "/SoftCircle.asset";
    private const string GlowAssetPath = AssetFolder + "/RadialGlow.asset";

    [SerializeField] private int sortingLayerOrder = 5;
    [SerializeField] private bool addGameplayComponents = true;

    [ContextMenu("Build UI Showcase")]
    public void BuildUiShowcase()
    {
#if UNITY_EDITOR
        ClearGenerated();
        Sprite circle = GetOrCreateCircleSprite();
        Sprite glow = GetOrCreateGlowSprite();
        if (circle == null || glow == null) return;

        GameObject root = new GameObject(GeneratedRootName);
        Undo.RegisterCreatedObjectUndo(root, "Build gravity UI showcase");

        Transform holes = CreateGroup("Black_Holes", root.transform);
        CreateBlackHole(holes, "BlackHole_CyanPulse", new Vector2(-6f, 2.8f), circle,
            new Color(0.12f, 0.82f, 1f), 1.00f, 2);
        CreateBlackHole(holes, "BlackHole_VioletAccretion", new Vector2(-2f, 2.8f), circle,
            new Color(0.57f, 0.31f, 1f), 1.25f, 3);
        CreateBlackHole(holes, "BlackHole_PinkCore", new Vector2(2f, 2.8f), circle,
            new Color(1f, 0.22f, 0.62f), 0.88f, 2);
        CreateBlackHole(holes, "BlackHole_IceMinimal", new Vector2(6f, 2.8f), circle,
            new Color(0.68f, 0.92f, 1f), 1.1f, 1);

        Transform portals = CreateGroup("Portals", root.transform);
        CreatePortal(portals, "Portal_Cyan", new Vector2(-6f, -1.4f), circle, glow,
            new Color(0.1f, 0.83f, 1f), 0);
        CreatePortal(portals, "Portal_Violet", new Vector2(-2f, -1.4f), circle, glow,
            new Color(0.65f, 0.36f, 1f), 1);
        CreatePortal(portals, "Portal_Pink", new Vector2(2f, -1.4f), circle, glow,
            new Color(1f, 0.25f, 0.56f), 2);
        CreatePortal(portals, "Portal_Gold", new Vector2(6f, -1.4f), circle, glow,
            new Color(1f, 0.74f, 0.12f), 3);

        Transform stars = CreateGroup("Stars", root.transform);
        CreateStar(stars, "Star_Cyan", new Vector2(-4.5f, -4.3f), circle, glow, new Color(0.1f, 0.83f, 1f));
        CreateStar(stars, "Star_Violet", new Vector2(-1.5f, -4.3f), circle, glow, new Color(0.65f, 0.36f, 1f));
        CreateStar(stars, "Star_Pink", new Vector2(1.5f, -4.3f), circle, glow, new Color(1f, 0.25f, 0.56f));
        CreateStar(stars, "Star_Gold", new Vector2(4.5f, -4.3f), circle, glow, new Color(1f, 0.74f, 0.12f));

        Selection.activeGameObject = root;
        EditorUtility.SetDirty(root);
#else
        Debug.LogWarning("BootstrapUi can build the showcase only in the Unity Editor.");
#endif
    }

    [ContextMenu("Clear Generated UI Showcase")]
    public void ClearGenerated()
    {
#if UNITY_EDITOR
        GameObject existing = GameObject.Find(GeneratedRootName);
        if (existing != null) Undo.DestroyObjectImmediate(existing);
#endif
    }

#if UNITY_EDITOR
    private Transform CreateGroup(string groupName, Transform parent)
    {
        GameObject group = new GameObject(groupName);
        Undo.RegisterCreatedObjectUndo(group, "Create UI group");
        group.transform.SetParent(parent, false);
        return group.transform;
    }

    private void CreateBlackHole(Transform parent, string objectName, Vector2 position, Sprite circle,
        Color glow, float coreScale, int ringCount)
    {
        GameObject hole = CreateSpriteObject(objectName, parent, position, circle, Color.black,
            Vector3.one * coreScale, sortingLayerOrder + 3);

        CircleCollider2D collider = hole.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        if (addGameplayComponents) hole.AddComponent<BlackHoleController>();

        GameObject halo = CreateSpriteObject("Halo", hole.transform, Vector2.zero, circle,
            WithAlpha(glow, 0.2f), Vector3.one * 1.5f, sortingLayerOrder + 2);
        halo.transform.localPosition = Vector3.zero;

        for (int i = 0; i < ringCount; i++)
        {
            float size = 1.75f + i * 0.32f;
            CreateSpriteObject("AccretionRing_" + (i + 1), hole.transform, Vector2.zero, circle,
                WithAlpha(glow, 0.12f - i * 0.025f), Vector3.one * size, sortingLayerOrder + 1 - i);
        }
    }

    private void CreatePortal(Transform parent, string objectName, Vector2 position, Sprite circle, Sprite glow,
        Color color, int queueOrder)
    {
        // A portal is deliberately built from a white rim and a coloured window,
        // so it never reads as another black hole.
        GameObject portal = CreateSpriteObject(objectName, parent, position, circle, Color.white,
            Vector3.one * 0.9f, sortingLayerOrder + 3);
        CircleCollider2D collider = portal.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;

        CreateSpriteObject("ColorFill", portal.transform, Vector2.zero, circle, color,
            Vector3.one * 0.73f, sortingLayerOrder + 4);
        CreateSpriteObject("InnerLight", portal.transform, Vector2.zero, glow, new Color(1f, 1f, 1f, 0.32f),
            Vector3.one * 0.55f, sortingLayerOrder + 5);
        CreateSpriteObject("PortalGlow", portal.transform, Vector2.zero, glow, WithAlpha(color, 0.46f),
            Vector3.one * 2.35f, sortingLayerOrder + 1);

        if (addGameplayComponents)
        {
            PortalTarget target = portal.AddComponent<PortalTarget>();
            target.ConfigureBootstrap(objectName.Replace("Portal_", "").ToUpperInvariant(), color, queueOrder);
        }
    }

    private void CreateStar(Transform parent, string objectName, Vector2 position, Sprite circle, Sprite glow, Color color)
    {
        GameObject star = CreateSpriteObject(objectName, parent, position, circle, color,
            Vector3.one * 0.22f, sortingLayerOrder + 6);

        // A warm white core plus several transparent rays makes the object read as
        // an emitting star rather than a coloured disc that is being scaled.
        CreateSpriteObject("RadiationAura", star.transform, Vector2.zero, glow, WithAlpha(color, 0.58f),
            Vector3.one * 8f, sortingLayerOrder + 2);
        CreateRay(star.transform, "RadiationRay_H", glow, color, new Vector3(8f, 0.48f, 1f), 0f);
        CreateRay(star.transform, "RadiationRay_V", glow, color, new Vector3(0.48f, 8f, 1f), 0f);
        CreateRay(star.transform, "RadiationRay_D1", glow, color, new Vector3(5.6f, 0.30f, 1f), 45f);
        CreateRay(star.transform, "RadiationRay_D2", glow, color, new Vector3(5.6f, 0.30f, 1f), -45f);
        CreateSpriteObject("HotCore", star.transform, Vector2.zero, circle, new Color(1f, 0.98f, 0.88f, 1f),
            Vector3.one * 0.58f, sortingLayerOrder + 7);
    }

    private void CreateRay(Transform parent, string name, Sprite glow, Color color, Vector3 scale, float angle)
    {
        GameObject ray = CreateSpriteObject(name, parent, Vector2.zero, glow, WithAlpha(color, 0.30f),
            scale, sortingLayerOrder + 3);
        ray.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    private GameObject CreateSpriteObject(string objectName, Transform parent, Vector2 localPosition,
        Sprite sprite, Color color, Vector3 localScale, int order)
    {
        GameObject gameObject = new GameObject(objectName);
        Undo.RegisterCreatedObjectUndo(gameObject, "Create UI variant");
        gameObject.transform.SetParent(parent, false);
        gameObject.transform.localPosition = localPosition;
        gameObject.transform.localScale = localScale;

        SpriteRenderer renderer = gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = order;
        return gameObject;
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    private static Sprite GetOrCreateCircleSprite()
    {
        if (!AssetDatabase.IsValidFolder(AssetFolder))
            AssetDatabase.CreateFolder("Assets", "GeneratedGravityUi");

        Object[] existing = AssetDatabase.LoadAllAssetsAtPath(CircleAssetPath);
        foreach (Object asset in existing)
            if (asset is Sprite sprite) return sprite;

        Texture2D texture = new Texture2D(128, 128, TextureFormat.RGBA32, false)
        {
            name = "SoftCircleTexture",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        for (int y = 0; y < texture.height; y++)
        for (int x = 0; x < texture.width; x++)
        {
            float nx = (x + 0.5f) / texture.width * 2f - 1f;
            float ny = (y + 0.5f) / texture.height * 2f - 1f;
            float distance = Mathf.Sqrt(nx * nx + ny * ny);
            float alpha = Mathf.Clamp01((1f - distance) * 14f);
            texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }
        texture.Apply();

        AssetDatabase.CreateAsset(texture, CircleAssetPath);
        Sprite generatedSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
            new Vector2(0.5f, 0.5f), texture.width);
        generatedSprite.name = "SoftCircle";
        AssetDatabase.AddObjectToAsset(generatedSprite, texture);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return generatedSprite;
    }

    private static Sprite GetOrCreateGlowSprite()
    {
        if (!AssetDatabase.IsValidFolder(AssetFolder))
            AssetDatabase.CreateFolder("Assets", "GeneratedGravityUi");

        Object[] existing = AssetDatabase.LoadAllAssetsAtPath(GlowAssetPath);
        foreach (Object asset in existing)
            if (asset is Sprite sprite) return sprite;

        Texture2D texture = new Texture2D(128, 128, TextureFormat.RGBA32, false)
        {
            name = "RadialGlowTexture",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        for (int y = 0; y < texture.height; y++)
        for (int x = 0; x < texture.width; x++)
        {
            float nx = (x + 0.5f) / texture.width * 2f - 1f;
            float ny = (y + 0.5f) / texture.height * 2f - 1f;
            float distance = Mathf.Sqrt(nx * nx + ny * ny);
            float alpha = Mathf.Pow(Mathf.Clamp01(1f - distance), 2.8f);
            texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }
        texture.Apply();

        AssetDatabase.CreateAsset(texture, GlowAssetPath);
        Sprite generatedSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
            new Vector2(0.5f, 0.5f), texture.width);
        generatedSprite.name = "RadialGlow";
        AssetDatabase.AddObjectToAsset(generatedSprite, texture);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return generatedSprite;
    }
#endif
}
