using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Un casillero del HUD de armas: icono + texto de la tecla. Lo maneja WeaponInventoryHUD.
/// Los campos image, nullWeaponIndicator y numKeyText conservan el nombre que ya tiene el prefab
/// InventoryContainer, así las referencias no se pierden.
///
/// El icono es un OBJETO de UI (arma + accesorios, con un ImageWeapon): se instancia dentro de un contenedor
/// propio ("IconRoot") que llena el casillero. El objeto conserva el tamaño y la escala que tiene en su prefab
/// (se ve igual que en el panel de munición) y vos lo acomodás con Icon Position e Icon Scale Xy.
/// Mientras haya un objeto, la imagen de fondo del casillero ('image') se oculta.
/// </summary>
public class WeaponsInventoryUISlot : MonoBehaviour
{
    [SerializeField] private Image image;
    [Tooltip("Imagen que se muestra cuando el casillero no tiene arma.")]
    [SerializeField] private Sprite nullWeaponIndicator;
    [SerializeField] private TextMeshProUGUI numKeyText;

    [Header("Posición y tamaño del icono (valen para todas las armas de este casillero)")]
    [Tooltip("Zona donde va el icono (opcional). Si queda vacía, el icono se coloca sobre la imagen 'image' del casillero.")]
    [SerializeField] private RectTransform iconArea;
    [Tooltip("Desplazamiento del icono desde el centro del casillero, en píxeles (X, Y).")]
    [SerializeField] private Vector2 iconPosition = Vector2.zero;
    [Tooltip("Multiplica la escala X e Y que el icono tiene en su prefab. (1, 1) = igual que en el prefab. " +
             "Se puede cambiar con el juego corriendo y se ve al instante.")]
    [SerializeField] private Vector2 iconScaleXy = Vector2.one;
    [Tooltip("Usa el diseño estándar en vez del encuadre automático: todas las capas 300 x 100, posición (50, 0, 0). " +
             "Con esto Icon Scale Xy tiene que ser chico (ej.: 0.15) para que entre en el casillero.")]
    [SerializeField] private bool useStandardLayout = false;

    [Header("Diagnóstico")]
    [Tooltip("Escribe en la consola qué imágenes tiene el icono, si están activas, su sprite y si caen dentro del casillero.")]
    [SerializeField] private bool debugLog = false;

    [Header("Selección")]
    [SerializeField] private float selectedScale = 1.2f;
    [SerializeField] private float deselectedScale = 1f;
    [SerializeField, Range(0f, 1f)] private float selectedAlpha = 1f;
    [SerializeField, Range(0f, 1f)] private float deselectedAlpha = 0.2f;
    [Tooltip("Qué tan rápido pasa de un estado al otro. Usa tiempo real, así anima aunque el juego esté en pausa.")]
    [SerializeField] private float transitionSpeed = 14f;

    private CanvasGroup canvasGroup;
    private bool initialized;
    private bool animating;

    private float currentScale;
    private float currentAlpha;
    private float targetScale;
    private float targetAlpha;

    // Un objeto de icono por cada prefab que pasó por acá: se crea una vez y después solo se prende / apaga.
    private readonly Dictionary<GameObject, GameObject> iconInstances = new Dictionary<GameObject, GameObject>();
    private GameObject shownIcon;
    private RectTransform iconContainer;

    // Se prepara en el primer uso y no en Awake: el HUD puede llamar antes de que este objeto despierte.
    private void EnsureInit()
    {
        if (initialized)
            return;

        initialized = true;

        canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        currentScale = targetScale = deselectedScale;
        currentAlpha = targetAlpha = deselectedAlpha;

        ApplyVisuals();
    }

    /// <summary>
    /// Contenedor donde viven los iconos ("IconRoot"). Se coloca exactamente sobre el casillero visible
    /// (la imagen 'image' / SlotDisplay), justo después de ella en la jerarquía, así el texto de la tecla
    /// sigue dibujándose encima. Si se asigna Icon Area, se estira sobre esa zona.
    /// </summary>
    private RectTransform GetIconContainer()
    {
        if (iconContainer == null)
        {
            GameObject go = new GameObject("IconRoot", typeof(RectTransform));
            iconContainer = go.GetComponent<RectTransform>();

            RectTransform imageRect = image != null ? image.rectTransform : null;
            bool useImageRect = iconArea == null && imageRect != null &&
                                imageRect.transform != transform && imageRect.parent is RectTransform;

            if (iconArea != null)
            {
                iconContainer.SetParent(iconArea, false);
            }
            else if (useImageRect)
            {
                iconContainer.SetParent(imageRect.parent, false);
                iconContainer.SetSiblingIndex(imageRect.GetSiblingIndex() + 1);
            }
            else
            {
                iconContainer.SetParent(transform, false);
            }
        }

        ApplyContainerRect();
        return iconContainer;
    }

    // Se reaplica siempre: así el icono sigue al casillero si lo mueven o le cambian el tamaño.
    private void ApplyContainerRect()
    {
        if (iconContainer == null)
            return;

        RectTransform imageRect = image != null ? image.rectTransform : null;
        bool useImageRect = iconArea == null && imageRect != null &&
                            imageRect.transform != transform && iconContainer.parent == imageRect.parent;

        iconContainer.localScale = Vector3.one;
        iconContainer.localRotation = Quaternion.identity;
        iconContainer.pivot = new Vector2(0.5f, 0.5f);
        iconContainer.localPosition = new Vector3(iconContainer.localPosition.x, iconContainer.localPosition.y, 0f);

        if (useImageRect)
        {
            // Misma zona que el casillero visible. Si 'image' tiene una escala distinta de 1 (por ejemplo un
            // WeaponIcon viejo), se respeta el tamaño que se ve en pantalla.
            Vector3 scale = imageRect.localScale;
            Vector2 sizeFactor = new Vector2(Mathf.Abs(scale.x), Mathf.Abs(scale.y));

            iconContainer.anchorMin = imageRect.anchorMin;
            iconContainer.anchorMax = imageRect.anchorMax;

            if (imageRect.anchorMin == imageRect.anchorMax)
            {
                iconContainer.sizeDelta = new Vector2(imageRect.sizeDelta.x * sizeFactor.x, imageRect.sizeDelta.y * sizeFactor.y);

                Vector2 pivotShift = new Vector2(
                    (0.5f - imageRect.pivot.x) * iconContainer.sizeDelta.x,
                    (0.5f - imageRect.pivot.y) * iconContainer.sizeDelta.y);

                iconContainer.anchoredPosition = imageRect.anchoredPosition + pivotShift;
            }
            else
            {
                iconContainer.offsetMin = imageRect.offsetMin;
                iconContainer.offsetMax = imageRect.offsetMax;
            }
        }
        else
        {
            iconContainer.anchorMin = Vector2.zero;
            iconContainer.anchorMax = Vector2.one;
            iconContainer.offsetMin = Vector2.zero;
            iconContainer.offsetMax = Vector2.zero;
        }

        iconContainer.anchoredPosition3D = new Vector3(iconContainer.anchoredPosition.x, iconContainer.anchoredPosition.y, 0f);
    }

    /// <summary>Pasa Icon Position e Icon Scale Xy a los iconos de este casillero.</summary>
    private void ApplyIconTransform()
    {
        foreach (KeyValuePair<GameObject, GameObject> pair in iconInstances)
        {
            if (pair.Value == null)
                continue;

            ImageWeapon imageWeapon = pair.Value.GetComponentInChildren<ImageWeapon>(true);

            if (imageWeapon != null)
                imageWeapon.SetManualAdjust(iconPosition, iconScaleXy, useStandardLayout);
        }
    }

    // Cambiar los valores en el Inspector con el juego corriendo se ve al instante.
    private void OnValidate()
    {
        if (Application.isPlaying && initialized)
            ApplyIconTransform();
    }

    /// <summary>
    /// Muestra el objeto de icono (arma + accesorios). null = casillero vacío (se muestra nullWeaponIndicator).
    /// Pasar el PREFAB: acá se instancia una sola vez.
    /// attachments = accesorios del arma (opcional): el icono muestra los que tenga puestos y se actualiza solo
    /// cuando cambian. Si el prefab no trae un ImageWeapon, se le agrega uno (busca sus imágenes por nombre).
    /// </summary>
    public void SetIcon(GameObject iconPrefab, WeaponAttachments attachments = null)
    {
        EnsureInit();

        if (shownIcon != null)
        {
            shownIcon.SetActive(false);
            shownIcon = null;
        }

        if (iconPrefab == null)
        {
            if (image != null)
            {
                image.enabled = true;
                image.preserveAspect = true;
                image.sprite = nullWeaponIndicator;
            }

            return;
        }

        RectTransform container = GetIconContainer();
        GameObject instance;

        if (!iconInstances.TryGetValue(iconPrefab, out instance) || instance == null)
        {
            instance = Instantiate(iconPrefab, container, false);
            instance.name = iconPrefab.name;

            // El icono llena el casillero; el encuadre del arma lo hace ImageWeapon con Icon Position / Icon Scale Xy.
            RectTransform rect = instance.transform as RectTransform;

            if (rect != null)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.localScale = Vector3.one;
                rect.localRotation = Quaternion.identity;
                rect.anchoredPosition3D = new Vector3(rect.anchoredPosition.x, rect.anchoredPosition.y, 0f);
            }

            iconInstances[iconPrefab] = instance;
        }

        instance.SetActive(true);
        shownIcon = instance;

        // Los accesorios se vuelven a pasar cada vez: dos armas pueden compartir el mismo prefab de icono.
        ImageWeapon imageWeapon = instance.GetComponentInChildren<ImageWeapon>(true);

        if (imageWeapon == null)
            imageWeapon = instance.AddComponent<ImageWeapon>();

        imageWeapon.SetManualAdjust(iconPosition, iconScaleXy, useStandardLayout);
        imageWeapon.Bind(attachments, ImageWeapon.FindBodySprite(iconPrefab));

        // El objeto reemplaza a la imagen del casillero.
        if (image != null)
        {
            image.enabled = false;
            HideLeftoverChildren();
        }

        if (debugLog)
            LogIcon(instance, attachments);
    }

    // Si el objeto 'image' del casillero tiene hijos (por ejemplo un WeaponIcon viejo con sus capas), seguirían
    // dibujándose aunque la imagen esté apagada y taparían o duplicarían el icono nuevo. Se apagan.
    private void HideLeftoverChildren()
    {
        Transform imageTransform = image.transform;

        if (iconContainer != null && iconContainer.IsChildOf(imageTransform))
            return;

        for (int i = 0; i < imageTransform.childCount; i++)
        {
            Transform child = imageTransform.GetChild(i);

            if (child.gameObject.activeSelf)
            {
                child.gameObject.SetActive(false);

                if (debugLog)
                    Debug.Log("[WeaponsInventoryUISlot " + name + "] Se apagó el hijo '" + child.name +
                              "' de la imagen del casillero (era un icono viejo).", this);
            }
        }
    }

    private void LogIcon(GameObject instance, WeaponAttachments attachments)
    {
        RectTransform slotRect = (iconContainer != null ? iconContainer : transform) as RectTransform;

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        RectTransform instanceRect = instance.transform as RectTransform;

        sb.AppendLine("[WeaponsInventoryUISlot " + name + "] icono '" + instance.name + "' | WeaponAttachments: " +
                      (attachments != null ? "sí" : "NO (solo se usa el cuerpo del prefab)"));
        sb.AppendLine("  casillero (IconRoot) tamaño=" + (slotRect != null ? slotRect.rect.size.ToString() : "?") +
                      " | icono: posición=" + (instanceRect != null ? instanceRect.anchoredPosition.ToString() : "?") +
                      " escala=" + (instanceRect != null ? instanceRect.localScale.ToString() : "?") +
                      " | Icon Position=" + iconPosition + " Icon Scale Xy=" + iconScaleXy);

        Image[] images = instance.GetComponentsInChildren<Image>(true);

        for (int i = 0; i < images.Length; i++)
        {
            Image img = images[i];
            Vector3 world = img.rectTransform.TransformPoint(img.rectTransform.rect.center);
            bool inside = slotRect != null && slotRect.rect.Contains(slotRect.InverseTransformPoint(world));

            sb.AppendLine("  - " + img.gameObject.name +
                          " | enabled=" + img.enabled +
                          " | activo=" + img.gameObject.activeInHierarchy +
                          " | sprite=" + (img.sprite != null ? img.sprite.name : "NINGUNO") +
                          " | alpha=" + img.color.a.ToString("0.##") +
                          " | centro dentro del casillero=" + inside);
        }

        Debug.Log(sb.ToString(), this);
    }

    /// <summary>Texto de la tecla (ej.: "1").</summary>
    public void SetKeyLabel(string label)
    {
        EnsureInit();

        if (numKeyText != null)
            numKeyText.text = label;
    }

    /// <summary>Marca o desmarca el casillero. instant = sin animación.</summary>
    public void SetSelected(bool selected, bool instant = false)
    {
        EnsureInit();

        targetScale = selected ? selectedScale : deselectedScale;
        targetAlpha = selected ? selectedAlpha : deselectedAlpha;

        if (instant || !isActiveAndEnabled)
        {
            currentScale = targetScale;
            currentAlpha = targetAlpha;
            animating = false;
            ApplyVisuals();
            return;
        }

        animating = true;
    }

    private void Update()
    {
        if (!animating)
            return;

        float t = 1f - Mathf.Exp(-transitionSpeed * Time.unscaledDeltaTime);

        currentScale = Mathf.Lerp(currentScale, targetScale, t);
        currentAlpha = Mathf.Lerp(currentAlpha, targetAlpha, t);

        if (Mathf.Abs(currentScale - targetScale) < 0.001f && Mathf.Abs(currentAlpha - targetAlpha) < 0.001f)
        {
            currentScale = targetScale;
            currentAlpha = targetAlpha;
            animating = false;
        }

        ApplyVisuals();
    }

    private void ApplyVisuals()
    {
        transform.localScale = Vector3.one * currentScale;
        canvasGroup.alpha = currentAlpha;
    }
}