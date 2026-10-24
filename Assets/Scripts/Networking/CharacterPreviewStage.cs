using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.UI;

/// <summary>
/// Escenario 3D de selección de personaje / lobby. Muestra un modelo por slot, en POSE (no T-pose) y con ARMA en la mano,
/// y los renderiza con una cámara a una RenderTexture que se muestra en un RawImage.
///
/// POSE: asigná el Animator Controller de tu Player (arranca en idle) en "Pose Controller", o un clip suelto en "Pose Clip"
///       (ej: idle con rifle / apuntado). Tiene que ser de la misma clase de rig que el modelo (el de tu Player sirve).
/// ARMA: arrastrá el prefab del arma (SM_Wep_...) y el hueso de la mano. Los valores de posición/rotación/escala te los
///       copiás del arma que ya tenés en la mano dentro del prefab del Player.
/// </summary>
public class CharacterPreviewStage : MonoBehaviour
{
    [SerializeField] private CharacterCatalog catalog;
    [SerializeField] private Camera previewCamera;
    [SerializeField] private Transform[] slotAnchors;
    [SerializeField] private RawImage target;
    [SerializeField] private string previewLayerName = "CharacterPreview";
    [SerializeField] private Vector2Int textureSize = new Vector2Int(900, 1000);
    [Tooltip("Giro inicial del modelo. 0 = de frente a la cámara. 20-30 da una vista de 3/4, más natural.")]
    [SerializeField] private float startYaw = 20f;

    [Header("Pose (para que no quede en T-pose)")]
    [Tooltip("Animator Controller (el de tu Player sirve). Tiene prioridad sobre Pose Clip.")]
    [SerializeField] private RuntimeAnimatorController poseController;
    [Tooltip("Alternativa: un solo clip (idle con arma o apuntando) que se reproduce en loop.")]
    [SerializeField] private AnimationClip poseClip;
    [Tooltip("Peso que se le da a las capas extra del controller (aditivo de la pistola, etc.). En el juego las maneja tu script; acá nadie, por eso se fuerzan.")]
    [SerializeField, Range(0f, 1f)] private float extraLayersWeight = 1f;

    [Header("Arma en la mano")]
    [SerializeField] private GameObject weaponPrefab;
    [Tooltip("Nombre del hueso de la mano derecha en el esqueleto.")]
    [SerializeField] private string weaponBoneName = "Hand_R";
    [SerializeField] private Vector3 weaponLocalPosition = Vector3.zero;
    [SerializeField] private Vector3 weaponLocalEuler = Vector3.zero;
    [SerializeField] private Vector3 weaponLocalScale = Vector3.one;

    private RenderTexture texture;
    private GameObject[] models;
    private GameObject[] weapons;
    private PlayableGraph[] graphs;
    private Animator[] animators;
    private int[] shownIndex;
    private float[] yaw;
    private int layer = -1;
    private bool initialized;
    private bool warnedNoPose;

    public int SlotCount => slotAnchors != null ? slotAnchors.Length : 0;

    /// <summary>Prende o apaga la cámara del escenario (una ventana cerrada no tiene por qué seguir renderizando).</summary>
    public void SetRendering(bool on)
    {
        if (previewCamera != null)
            previewCamera.enabled = on;
    }

    private void Awake()
    {
        EnsureInit();
    }

    private void OnDestroy()
    {
        if (graphs != null)
        {
            for (int i = 0; i < graphs.Length; i++)
            {
                if (graphs[i].IsValid())
                    graphs[i].Destroy();
            }
        }

        if (texture != null)
        {
            texture.Release();
            Destroy(texture);
        }
    }

#if UNITY_EDITOR
    // Solo en el editor: si movés los valores del arma durante el Play, se ve el cambio en vivo.
    private void Update()
    {
        if (weapons == null)
            return;

        for (int i = 0; i < weapons.Length; i++)
        {
            if (weapons[i] != null)
                ApplyWeaponOffset(weapons[i].transform);
        }
    }
#endif

    private void EnsureInit()
    {
        if (initialized)
            return;

        initialized = true;

        int n = SlotCount;
        models = new GameObject[n];
        weapons = new GameObject[n];
        graphs = new PlayableGraph[n];
        animators = new Animator[n];
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

    /// <summary>Muestra el personaje "characterIndex" en la columna "slot". Si ya está, no hace nada.</summary>
    public void Show(int slot, int characterIndex)
    {
        EnsureInit();

        if (slot < 0 || slot >= SlotCount || catalog == null)
            return;

        // Una variante sin modelo (ej: Tanque mujer) se resuelve a la jugable de su clase.
        characterIndex = catalog.ResolveIndex(characterIndex);

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

        AttachWeapon(slot, model);

        if (layer >= 0)
            SetLayerRecursively(model, layer);

        ApplyPose(slot, model);

        models[slot] = model;
        shownIndex[slot] = characterIndex;
    }

    /// <summary>Saca el modelo de esa columna (jugador no conectado).</summary>
    public void Clear(int slot)
    {
        EnsureInit();

        if (slot < 0 || slot >= SlotCount)
            return;

        if (graphs[slot].IsValid())
            graphs[slot].Destroy();

        if (models[slot] != null)
            Destroy(models[slot]);

        models[slot] = null;
        weapons[slot] = null;
        animators[slot] = null;
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

    // =========================================================
    // POSE Y ARMA
    // =========================================================

    private void ApplyPose(int slot, GameObject model)
    {
        if (poseController == null && poseClip == null)
        {
            if (!warnedNoPose)
            {
                warnedNoPose = true;
                Debug.LogWarning("[CharacterPreviewStage] Sin Pose Controller ni Pose Clip: el personaje queda en T-pose.");
            }

            return;
        }

        Animator anim = model.GetComponentInChildren<Animator>(true);

        if (anim == null)
        {
            Debug.LogWarning("[CharacterPreviewStage] El prefab '" + model.name + "' no tiene Animator, no se puede aplicar la pose.");
            return;
        }

        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        if (poseController != null)
        {
            anim.runtimeAnimatorController = poseController;
            animators[slot] = anim;
            ApplyLayerWeights(anim);
        }
        else
        {
            anim.runtimeAnimatorController = null;
            AnimationPlayableUtilities.PlayClip(anim, poseClip, out graphs[slot]);
        }
    }

    // Las capas extra (aditivo de la pistola) arrancan con peso 0 si nadie las sube: acá se fuerzan a Extra Layers Weight.
    private void LateUpdate()
    {
        if (animators == null)
            return;

        for (int i = 0; i < animators.Length; i++)
        {
            if (animators[i] != null)
                ApplyLayerWeights(animators[i]);
        }
    }

    private void ApplyLayerWeights(Animator anim)
    {
        for (int layerIndex = 1; layerIndex < anim.layerCount; layerIndex++)
            anim.SetLayerWeight(layerIndex, extraLayersWeight);
    }

    private void AttachWeapon(int slot, GameObject model)
    {
        if (weaponPrefab == null)
            return;

        Transform bone = FindDeep(model.transform, weaponBoneName);

        if (bone == null)
        {
            Debug.LogWarning("[CharacterPreviewStage] No encuentro el hueso '" + weaponBoneName + "' para colgar el arma.");
            return;
        }

        GameObject weapon = Instantiate(weaponPrefab, bone);
        ApplyWeaponOffset(weapon.transform);
        weapons[slot] = weapon;
    }

    private void ApplyWeaponOffset(Transform weapon)
    {
        weapon.localPosition = weaponLocalPosition;
        weapon.localRotation = Quaternion.Euler(weaponLocalEuler);
        weapon.localScale = weaponLocalScale;
    }

    private static Transform FindDeep(Transform root, string boneName)
    {
        if (root.name == boneName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeep(root.GetChild(i), boneName);

            if (found != null)
                return found;
        }

        return null;
    }

    private static void SetLayerRecursively(GameObject go, int l)
    {
        go.layer = l;

        foreach (Transform child in go.transform)
            SetLayerRecursively(child.gameObject, l);
    }
}