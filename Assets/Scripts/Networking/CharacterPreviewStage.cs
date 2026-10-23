using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Escenario 3D del lobby con VARIOS modelos (uno por slot de jugador). Una sola cámara los renderiza a una
/// RenderTexture que se muestra en un RawImage detrás de los paneles (como el lobby de referencia: los personajes
/// "paran" sobre su columna y el panel de datos queda abajo).
///
/// Montaje: objeto vacío ALEJADO del lobby (ej: 0,-200,0) con
///  - Slot Anchors: un hijo vacío por columna, en fila sobre el eje X (ej: x = -4.5, -1.5, 1.5, 4.5), a ras del piso.
///  - Preview Camera: mirando la fila de frente (ej: pos 0,1.2,9 / rot Y = 180). Ajustá distancia/FOV hasta que cada
///    modelo caiga sobre su columna de UI.
///  - Target: el RawImage (a pantalla completa o sobre la zona de modelos) donde se muestra la textura.
///    La proporción de Texture Size debe ser la del RawImage.
/// Layer "CharacterPreview" exclusivo: la cámara del preview solo ve ese layer; la cámara del lobby lo desmarca.
/// </summary>
public class CharacterPreviewStage : MonoBehaviour
{
    [SerializeField] private CharacterCatalog catalog;
    [SerializeField] private Camera previewCamera;
    [SerializeField] private Transform[] slotAnchors;
    [SerializeField] private RawImage target;
    [SerializeField] private string previewLayerName = "CharacterPreview";
    [SerializeField] private Vector2Int textureSize = new Vector2Int(1920, 720);
    [SerializeField] private float startYaw = 0f;

    private RenderTexture texture;
    private GameObject[] models;
    private int[] shownIndex;
    private float[] yaw;
    private int layer = -1;
    private bool initialized;

    public int SlotCount => slotAnchors != null ? slotAnchors.Length : 0;

    private void Awake()
    {
        EnsureInit();
    }

    private void OnDestroy()
    {
        if (texture != null)
        {
            texture.Release();
            Destroy(texture);
        }
    }

    private void EnsureInit()
    {
        if (initialized)
            return;

        initialized = true;

        int n = SlotCount;
        models = new GameObject[n];
        shownIndex = new int[n];
        yaw = new float[n];

        for (int i = 0; i < n; i++)
        {
            shownIndex[i] = -1;
            yaw[i] = startYaw;
        }

        layer = LayerMask.NameToLayer(previewLayerName);

        if (layer < 0)
            Debug.LogWarning("[CharacterPreviewStage] No existe el layer '" + previewLayerName + "'. Crealo en Tags & Layers.");

        texture = new RenderTexture(textureSize.x, textureSize.y, 24, RenderTextureFormat.ARGB32);
        texture.name = "CharacterPreviewRT";

        if (previewCamera != null)
        {
            previewCamera.targetTexture = texture;
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);

            if (layer >= 0)
                previewCamera.cullingMask = 1 << layer;
        }

        if (target != null)
            target.texture = texture;
    }

    /// <summary>Muestra el personaje "characterIndex" del catálogo en la columna "slot". Si ya está, no hace nada.</summary>
    public void Show(int slot, int characterIndex)
    {
        EnsureInit();

        if (slot < 0 || slot >= SlotCount || catalog == null)
            return;

        if (models[slot] != null && shownIndex[slot] == characterIndex)
            return;

        CharacterCatalog.Variant variant = catalog.GetVariant(characterIndex);

        if (variant == null || variant.modelPrefab == null)
        {
            Debug.LogWarning("[CharacterPreviewStage] El personaje " + characterIndex + " no tiene Model Prefab en el catálogo.");
            return;
        }

        Clear(slot);

        GameObject model = Instantiate(variant.modelPrefab, slotAnchors[slot]);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.Euler(0f, yaw[slot], 0f);

        if (layer >= 0)
            SetLayerRecursively(model, layer);

        models[slot] = model;
        shownIndex[slot] = characterIndex;
    }

    /// <summary>Saca el modelo de esa columna (jugador no conectado).</summary>
    public void Clear(int slot)
    {
        EnsureInit();

        if (slot < 0 || slot >= SlotCount)
            return;

        if (models[slot] != null)
            Destroy(models[slot]);

        models[slot] = null;
        shownIndex[slot] = -1;
        yaw[slot] = startYaw;
    }

    /// <summary>Gira el modelo de una columna (grados). Lo llama CharacterPreviewDrag.</summary>
    public void Rotate(int slot, float degrees)
    {
        EnsureInit();

        if (slot < 0 || slot >= SlotCount)
            return;

        yaw[slot] += degrees;

        if (models[slot] != null)
            models[slot].transform.localRotation = Quaternion.Euler(0f, yaw[slot], 0f);
    }

    private static void SetLayerRecursively(GameObject go, int l)
    {
        go.layer = l;

        foreach (Transform child in go.transform)
            SetLayerRecursively(child.gameObject, l);
    }
}