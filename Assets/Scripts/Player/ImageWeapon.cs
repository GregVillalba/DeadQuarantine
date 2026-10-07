using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Calcula, una sola vez por sprite, qué parte del lienzo ocupa realmente el dibujo (el rectángulo que contiene
/// todos los píxeles no transparentes). Sirve para encuadrar cada arma sin importar en qué lugar del sprite
/// esté dibujada. Funciona aunque la textura NO tenga activado Read/Write.
/// </summary>
public static class WeaponIconBounds
{
    private const byte AlphaThreshold = 16;
    private static readonly Dictionary<Sprite, Rect> cache = new Dictionary<Sprite, Rect>();

    /// <summary>Rectángulo en píxeles del dibujo, dentro del sprite (origen abajo a la izquierda).</summary>
    public static Rect Get(Sprite sprite)
    {
        if (sprite == null)
            return new Rect(0f, 0f, 1f, 1f);

        Rect bounds;

        if (cache.TryGetValue(sprite, out bounds))
            return bounds;

        bounds = Compute(sprite);
        cache[sprite] = bounds;
        return bounds;
    }

    private static Rect Compute(Sprite sprite)
    {
        Rect full = new Rect(0f, 0f, sprite.rect.width, sprite.rect.height);

        try
        {
            Texture2D tex = sprite.texture;
            Rect tr = sprite.textureRect;
            int x = Mathf.RoundToInt(tr.x);
            int y = Mathf.RoundToInt(tr.y);
            int w = Mathf.RoundToInt(tr.width);
            int h = Mathf.RoundToInt(tr.height);

            if (tex == null || w <= 0 || h <= 0)
                return full;

            byte[] alpha = ReadAlpha(tex, x, y, w, h);

            if (alpha == null || alpha.Length < w * h)
                return full;

            int minX = w, minY = h, maxX = -1, maxY = -1;

            for (int j = 0; j < h; j++)
            {
                int row = j * w;

                for (int i = 0; i < w; i++)
                {
                    if (alpha[row + i] >= AlphaThreshold)
                    {
                        if (i < minX) minX = i;
                        if (i > maxX) maxX = i;
                        if (j < minY) minY = j;
                        if (j > maxY) maxY = j;
                    }
                }
            }

            if (maxX < minX || maxY < minY)
                return full;

            return new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[WeaponIconBounds] No se pudo medir el sprite '" + sprite.name + "': " + e.Message);
            return full;
        }
    }

    private static byte[] ReadAlpha(Texture2D tex, int x, int y, int w, int h)
    {
        if (tex.isReadable)
        {
            Color[] colors = tex.GetPixels(x, y, w, h);
            byte[] result = new byte[colors.Length];

            for (int i = 0; i < colors.Length; i++)
                result[i] = (byte)Mathf.RoundToInt(colors[i].a * 255f);

            return result;
        }

        // Textura sin Read/Write: se copia a una RenderTexture y se lee de ahí.
        RenderTexture rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32);
        RenderTexture previous = RenderTexture.active;
        Texture2D readable = null;

        try
        {
            Graphics.Blit(tex, rt);
            RenderTexture.active = rt;

            readable = new Texture2D(w, h, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(x, y, w, h), 0, 0);

            Color32[] pixels = readable.GetPixels32();
            byte[] result = new byte[pixels.Length];

            for (int i = 0; i < pixels.Length; i++)
                result[i] = pixels[i].a;

            return result;
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);

            if (readable != null)
                Object.Destroy(readable);
        }
    }
}

/// <summary>
/// Icono de un arma en el HUD: cuerpo + accesorios (mira, cargador, láser, grip, silenciador).
/// Va en la raíz de un objeto de UI. Sus capas son imágenes hijas llamadas:
///     Body, Scope_Default, Scope, Magazine, Laser, Grip, Muzzle
/// (las que falten se crean solas).
///
/// ENCUADRE AUTOMÁTICO: el script mide dónde está realmente dibujada el arma dentro del sprite y coloca todas las
/// capas para que el arma quede centrada y entre justo en el rectángulo de este objeto. Ya no importa dónde
/// esté el arma dentro del lienzo ni cómo estén puestas las capas en el prefab. Se ajusta con:
///     Frame Fill   - cuánto del rectángulo ocupa el arma (1 = todo).
///     Frame Scale  - multiplica el ancho (X) y el alto (Y) del dibujo.
///     Frame Offset - mueve el dibujo (X, Y) en unidades del icono.
/// En un casillero del inventario esos valores los pone WeaponsInventoryUISlot (Icon Position / Icon Scale Xy).
///
/// Dos formas de usarlo:
///  1) En un casillero del inventario: el slot le pasa los accesorios con Bind(...).
///  2) En el panel de munición: sigue solo al arma que tenés en la mano.
/// </summary>
public class ImageWeapon : MonoBehaviour
{
    [Header("Color")]
    [SerializeField] private Color imageColor = Color.white;
    [Tooltip("Aplica 'Image Color' a todas las capas con sprite.")]
    [SerializeField] private bool applyColor = false;

    [Header("Capas (si están vacías se buscan solas por el nombre del hijo, o se crean)")]
    [SerializeField] private Image imageWeaponBody;
    [SerializeField] private Image imageWeaponGrip;
    [SerializeField] private Image imageWeaponLaser;
    [SerializeField] private Image imageWeaponMuzzle;
    [SerializeField] private Image imageWeaponMagazine;
    [SerializeField] private Image imageWeaponScope;
    [SerializeField] private Image imageWeaponScopeDefault;

    [Header("Encuadre automático")]
    [Tooltip("Solo para el icono que NO es de un casillero (por ejemplo el del AmmoPanel): centra y ajusta el arma al rectángulo de este objeto. " +
             "Si lo apagás, las capas quedan como las armaste en el prefab. Los iconos de los casilleros se manejan desde WeaponsInventoryUISlot.")]
    [SerializeField] private bool autoFrame = true;
    [Tooltip("Cuánto del rectángulo ocupa el arma (1 = todo el rectángulo).")]
    [SerializeField, Range(0.2f, 1f)] private float frameFill = 0.92f;
    [Tooltip("Multiplica el ancho (X) y el alto (Y) del dibujo.")]
    [SerializeField] private Vector2 frameScale = Vector2.one;
    [Tooltip("Mueve el dibujo (X, Y), en unidades del icono.")]
    [SerializeField] private Vector2 frameOffset = Vector2.zero;

    [Header("Seguir al arma equipada")]
    [Tooltip("El icono muestra siempre el arma que tenés en la mano. No hace falta marcarlo: se activa solo " +
             "en el icono que NO es de un casillero (por ejemplo, el del panel de munición).")]
    [SerializeField] private bool followEquippedWeapon = false;

    private WeaponAttachments attachments;
    private WeaponSwitcher switcher;
    private Sprite authoredBodySprite;
    private Sprite fallbackBodySprite;
    private bool allowAuthoredBody = true;
    private bool initialized;
    private bool subscribedAttachments;
    private bool subscribedSwitcher;
    private bool externallyBound;
    private bool layoutDirty;
    private bool slotControlled;     // true solo en los iconos que genera WeaponsInventoryUISlot
    private bool slotSetupDone;
    private bool slotStandardLayout;
    private int emptyFrameCount;

    private bool ShouldFollow
    {
        get
        {
            if (externallyBound)
                return false;

            return followEquippedWeapon || GetComponentInParent<WeaponsInventoryUISlot>(true) == null;
        }
    }

    // ---------- Arranque ----------

    private void EnsureInit()
    {
        if (initialized)
            return;

        initialized = true;

        FindImages();

        if (authoredBodySprite == null && imageWeaponBody != null)
            authoredBodySprite = imageWeaponBody.sprite;
    }

    // Solo para los iconos que genera un casillero del inventario (IconRoot). El icono del AmmoPanel NO pasa por acá.
    private void EnsureSlotSetup()
    {
        EnsureInit();

        if (slotSetupDone)
            return;

        slotSetupDone = true;

        // La imagen de la raíz no es una capa: si no tiene sprite sería un rectángulo blanco, se apaga.
        Image rootImage = GetComponent<Image>();

        if (rootImage != null)
        {
            if (authoredBodySprite == null && rootImage.sprite != null)
                authoredBodySprite = rootImage.sprite;

            rootImage.enabled = false;
        }

        CreateMissingLayers();
        DisableStrayImages();
    }

    private void Awake()
    {
        EnsureInit();
    }

    private void OnEnable()
    {
        EnsureInit();
        SubscribeAttachments();

        if (ShouldFollow)
        {
            SubscribeSwitcher();
            FollowSwitcher();
        }
        else
        {
            Refresh();
        }
    }

    private void Start()
    {
        // El WeaponSwitcher termina de equipar el arma inicial en su Start.
        if (ShouldFollow)
            FollowSwitcher();
    }

    private void OnDisable()
    {
        UnsubscribeAttachments();
        UnsubscribeSwitcher();
    }

    private void OnRectTransformDimensionsChange()
    {
        layoutDirty = true;
    }

    private void OnValidate()
    {
        frameScale = new Vector2(Mathf.Max(0.01f, frameScale.x), Mathf.Max(0.01f, frameScale.y));
        layoutDirty = true;
    }

    private void LateUpdate()
    {
        if (layoutDirty)
            Layout();
    }

    // ---------- Conectar con un arma ----------

    /// <summary>
    /// Lo llama el casillero del inventario: le pasa los accesorios del arma de ese icono y el sprite del cuerpo
    /// que trae el prefab (por si el arma no tiene WeaponAttachments). A partir de acá el icono deja de seguir
    /// al arma equipada.
    /// </summary>
    public void Bind(WeaponAttachments newAttachments, Sprite bodyFromPrefab = null)
    {
        slotControlled = true;
        EnsureSlotSetup();

        externallyBound = true;
        UnsubscribeSwitcher();

        BindInternal(newAttachments, bodyFromPrefab, true);
    }

    /// <summary>
    /// Posición y escala del dibujo dentro del rectángulo. Lo usa WeaponsInventoryUISlot, y marca a este icono
    /// como "de casillero": solo en ese caso se fuerza Z = 0 y se puede usar el diseño estándar.
    /// </summary>
    public void SetManualAdjust(Vector2 offset, Vector2 scale, bool standardLayout = false)
    {
        slotControlled = true;
        EnsureSlotSetup();

        slotStandardLayout = standardLayout;
        frameOffset = offset;
        frameScale = new Vector2(Mathf.Max(0.01f, scale.x), Mathf.Max(0.01f, scale.y));
        layoutDirty = true;

        if (isActiveAndEnabled)
            Layout();
    }

    private void BindInternal(WeaponAttachments newAttachments, Sprite bodyFromPrefab, bool useAuthoredBody)
    {
        fallbackBodySprite = bodyFromPrefab;
        allowAuthoredBody = useAuthoredBody;

        if (newAttachments != attachments)
        {
            UnsubscribeAttachments();
            attachments = newAttachments;

            if (isActiveAndEnabled)
                SubscribeAttachments();
        }

        Refresh();
    }

    private void SubscribeAttachments()
    {
        if (attachments != null && !subscribedAttachments)
        {
            attachments.Changed += Refresh;
            subscribedAttachments = true;
        }
    }

    private void UnsubscribeAttachments()
    {
        if (attachments != null && subscribedAttachments)
            attachments.Changed -= Refresh;

        subscribedAttachments = false;
    }

    private void SubscribeSwitcher()
    {
        if (switcher == null)
            switcher = transform.root.GetComponentInChildren<WeaponSwitcher>(true);

        if (switcher != null && !subscribedSwitcher)
        {
            switcher.InventoryChanged += OnSwitcherChanged;
            subscribedSwitcher = true;
        }
    }

    private void UnsubscribeSwitcher()
    {
        if (switcher != null && subscribedSwitcher)
            switcher.InventoryChanged -= OnSwitcherChanged;

        subscribedSwitcher = false;
    }

    private void OnSwitcherChanged()
    {
        if (!externallyBound)
            FollowSwitcher();
    }

    private void FollowSwitcher()
    {
        if (switcher == null || !switcher.IsReady)
            return;

        int index = switcher.SelectedIndex;
        GameObject icon = switcher.GetIconObject(index);
        bool ownIcon = icon == null || icon == gameObject || icon.transform.IsChildOf(transform);

        BindInternal(switcher.GetAttachments(index), ownIcon ? null : FindBodySprite(icon), ownIcon);
    }

    // ---------- Pintar ----------

    /// <summary>Vuelve a pintar el icono con los accesorios actuales.</summary>
    public void Refresh()
    {
        EnsureInit();

        Sprite body = attachments != null ? attachments.BodySprite : null;

        if (body == null)
            body = fallbackBodySprite;

        if (body == null && allowAuthoredBody)
            body = authoredBodySprite;

        SetLayer(imageWeaponBody, body);
        SetLayer(imageWeaponScopeDefault, attachments != null ? attachments.ScopeDefaultSprite : null);
        SetLayer(imageWeaponScope, attachments != null ? attachments.ScopeSprite : null);
        SetLayer(imageWeaponMagazine, attachments != null ? attachments.MagazineSprite : null);
        SetLayer(imageWeaponLaser, attachments != null ? attachments.LaserSprite : null);
        SetLayer(imageWeaponGrip, attachments != null ? attachments.GripSprite : null);
        SetLayer(imageWeaponMuzzle, attachments != null ? attachments.MuzzleSprite : null);

        layoutDirty = true;
        Layout();
    }

    private void SetLayer(Image img, Sprite sprite)
    {
        if (img == null)
            return;

        img.sprite = sprite;
        img.raycastTarget = false;
        img.preserveAspect = false;

        bool show = sprite != null;
        img.enabled = show;

        if (!show)
            return;

        // Un objeto apagado o con alpha 0 no se vería aunque tenga sprite.
        if (!img.gameObject.activeSelf)
            img.gameObject.SetActive(true);

        Color c = applyColor ? imageColor : img.color;

        if (c.a < 1f)
            c.a = applyColor ? imageColor.a : 1f;

        img.color = c;
    }

    // ---------- Encuadre ----------

    private void Layout()
    {
        layoutDirty = false;

        if (slotControlled)
        {
            // Icono de un casillero: diseño estándar (300 x 100, posición 50, 0, 0) o encuadre automático.
            if (slotStandardLayout)
            {
                ApplyLayers(
                    new Vector2(StandardSize.x * frameScale.x, StandardSize.y * frameScale.y),
                    new Vector2(StandardPosition.x + frameOffset.x, StandardPosition.y + frameOffset.y),
                    true);
                return;
            }
        }
        else if (!autoFrame)
        {
            // Icono fuera de los casilleros (AmmoPanel) con Auto Frame apagado: se deja como está armado en el prefab.
            return;
        }

        if (imageWeaponBody == null || imageWeaponBody.sprite == null)
            return;

        RectTransform root = transform as RectTransform;

        if (root == null)
            return;

        Vector2 frame = root.rect.size;

        // Todavía no tiene tamaño (jerarquía recién creada o apagada): se reintenta en el próximo LateUpdate.
        if (frame.x <= 0.01f || frame.y <= 0.01f)
        {
            layoutDirty = true;
            emptyFrameCount++;

            if (emptyFrameCount == 30)
                Debug.LogWarning("[ImageWeapon] El icono '" + name + "' tiene tamaño 0, por eso no se ve. " +
                                 "Revisá el tamaño del casillero (o de este objeto) en el Rect Transform.", this);

            return;
        }

        emptyFrameCount = 0;

        Sprite body = imageWeaponBody.sprite;
        Vector2 canvas = new Vector2(body.rect.width, body.rect.height);
        Rect content = WeaponIconBounds.Get(body);

        if (canvas.x <= 0.5f || canvas.y <= 0.5f || content.width <= 0.5f || content.height <= 0.5f)
            return;

        // La escala de los padres (si no es pareja) se compensa para que el dibujo no se deforme.
        Vector3 ls = root.lossyScale;
        float sx = Mathf.Max(0.0001f, Mathf.Abs(ls.x));
        float sy = Mathf.Max(0.0001f, Mathf.Abs(ls.y));

        // Píxeles del sprite -> unidades visibles, parejo en X e Y.
        float k = Mathf.Min(frame.x * sx / content.width, frame.y * sy / content.height) * frameFill;

        float kx = k * frameScale.x / sx;   // unidades locales por píxel (X)
        float ky = k * frameScale.y / sy;   // unidades locales por píxel (Y)

        Vector2 contentCenter = content.center;
        Vector2 canvasCenter = canvas * 0.5f;

        Vector2 size = new Vector2(canvas.x * kx, canvas.y * ky);
        Vector2 position = new Vector2(
            -(contentCenter.x - canvasCenter.x) * kx + frameOffset.x,
            -(contentCenter.y - canvasCenter.y) * ky + frameOffset.y);

        ApplyLayers(size, position, slotControlled);
    }

    private static readonly Vector2 StandardSize = new Vector2(300f, 100f);
    private static readonly Vector2 StandardPosition = new Vector2(50f, 0f);

    /// <summary>
    /// Pone todas las capas con el mismo tamaño y posición. En los iconos de casillero (forceZeroZ) también fuerza Z = 0:
    /// con un Z distinto de 0 el dibujo puede salirse de la cámara y desaparecer.
    /// </summary>
    private void ApplyLayers(Vector2 size, Vector2 position, bool forceZeroZ)
    {
        Image[] layers = AllLayers();

        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i] == null)
                continue;

            RectTransform r = layers[i].rectTransform;
            r.anchorMin = new Vector2(0.5f, 0.5f);
            r.anchorMax = new Vector2(0.5f, 0.5f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.localRotation = Quaternion.identity;
            r.localScale = Vector3.one;
            r.sizeDelta = size;

            if (forceZeroZ)
                r.anchoredPosition3D = new Vector3(position.x, position.y, 0f);
            else
                r.anchoredPosition = position;
        }
    }

    // ---------- Búsqueda y creación de capas ----------

    private Image[] AllLayers()
    {
        return new[]
        {
            imageWeaponBody, imageWeaponScopeDefault, imageWeaponScope, imageWeaponMagazine,
            imageWeaponLaser, imageWeaponGrip, imageWeaponMuzzle
        };
    }

    private static int LayerOf(string lowerName)
    {
        switch (lowerName)
        {
            case "body":
            case "weapon":
                return 0;
            case "scope_default":
            case "scopedefault":
                return 1;
            case "scope":
                return 2;
            case "magazine":
                return 3;
            case "laser":
                return 4;
            case "grip":
                return 5;
            case "muzzle":
            case "silencer":
                return 6;
            default:
                return -1;
        }
    }

    private void FindImages()
    {
        Image[] all = GetComponentsInChildren<Image>(true);

        for (int i = 0; i < all.Length; i++)
        {
            Image image = all[i];

            if (image.gameObject == gameObject)
                continue;

            switch (LayerOf(image.gameObject.name.ToLowerInvariant()))
            {
                case 0: if (imageWeaponBody == null) imageWeaponBody = image; break;
                case 1: if (imageWeaponScopeDefault == null) imageWeaponScopeDefault = image; break;
                case 2: if (imageWeaponScope == null) imageWeaponScope = image; break;
                case 3: if (imageWeaponMagazine == null) imageWeaponMagazine = image; break;
                case 4: if (imageWeaponLaser == null) imageWeaponLaser = image; break;
                case 5: if (imageWeaponGrip == null) imageWeaponGrip = image; break;
                case 6: if (imageWeaponMuzzle == null) imageWeaponMuzzle = image; break;
            }
        }

        // Sin capa "Body": se usa la primera imagen con sprite que no sea otra capa (por si el cuerpo tiene otro nombre).
        if (imageWeaponBody == null)
        {
            for (int i = 0; i < all.Length; i++)
            {
                Image image = all[i];

                if (image.gameObject == gameObject || image.sprite == null || IsAssignedLayer(image))
                    continue;

                imageWeaponBody = image;
                break;
            }
        }
    }

    private bool IsAssignedLayer(Image image)
    {
        Image[] layers = AllLayers();

        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i] == image)
                return true;
        }

        return false;
    }

    private void CreateMissingLayers()
    {
        if (imageWeaponBody == null)
        {
            imageWeaponBody = CreateLayer("Body");
            imageWeaponBody.transform.SetAsFirstSibling();
        }

        if (imageWeaponScopeDefault == null) imageWeaponScopeDefault = CreateLayer("Scope_Default");
        if (imageWeaponScope == null) imageWeaponScope = CreateLayer("Scope");
        if (imageWeaponMagazine == null) imageWeaponMagazine = CreateLayer("Magazine");
        if (imageWeaponLaser == null) imageWeaponLaser = CreateLayer("Laser");
        if (imageWeaponGrip == null) imageWeaponGrip = CreateLayer("Grip");
        if (imageWeaponMuzzle == null) imageWeaponMuzzle = CreateLayer("Muzzle");
    }

    private Image CreateLayer(string layerName)
    {
        GameObject go = new GameObject(layerName, typeof(RectTransform), typeof(Image));
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);

        Image image = go.GetComponent<Image>();
        image.raycastTarget = false;
        image.enabled = false;
        return image;
    }

    // Cualquier otra imagen sin sprite que haya dentro del icono se dibujaría como un cuadrado blanco.
    private void DisableStrayImages()
    {
        Image[] all = GetComponentsInChildren<Image>(true);

        for (int i = 0; i < all.Length; i++)
        {
            Image image = all[i];

            if (image.gameObject == gameObject || IsAssignedLayer(image))
                continue;

            if (image.sprite == null)
                image.enabled = false;
        }
    }

    /// <summary>
    /// Sprite del cuerpo que trae un prefab de icono: la capa Body; si no tiene ese nombre, la primera imagen con sprite.
    /// </summary>
    public static Sprite FindBodySprite(GameObject icon)
    {
        if (icon == null)
            return null;

        Image[] all = icon.GetComponentsInChildren<Image>(true);
        Sprite other = null;

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].sprite == null)
                continue;

            int layer = LayerOf(all[i].gameObject.name.ToLowerInvariant());

            if (layer == 0)
                return all[i].sprite;

            if (layer < 0 && other == null)
                other = all[i].sprite;
        }

        return other;
    }
}