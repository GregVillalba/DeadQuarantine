using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Aplica el personaje elegido al jugador: apariencia (cuerpo + accesorios) y estadísticas (vida, velocidades).
/// Va en la raíz del Player (el que tiene el NetworkObject).
///
/// CÓMO CAMBIA LA APARIENCIA
/// Todos los personajes de Polygon comparten el MISMO esqueleto. El Player ya tiene uno (con Animator, NetworkAnimator,
/// scripts de sync y la pistola en la mano), así que no se duplica nada: del prefab del personaje elegido solo se
/// "trasplantan" el cuerpo (SkinnedMeshRenderer, con sus huesos remapeados por ruta) y los accesorios que estén
/// ACTIVOS en ese prefab (cascos, mochilas, bolsillos...), que se cuelgan de los mismos huesos que tenían allá.
/// Lo que está apagado en el prefab (las variantes que trae Synty) se ignora.
///
/// RED: el número de personaje es un NetworkVariable que escribe el dueño. Todos los peers aplican la apariencia
/// y las estadísticas; el servidor además llena la vida con el máximo nuevo.
/// </summary>
public class PlayerCharacter : NetworkBehaviour
{
    [Header("Datos")]
    [SerializeField] private CharacterCatalog catalog;

    [Header("Modelo de tercera persona")]
    [Tooltip("Raíz del modelo que tiene el esqueleto y el Animator (el objeto SM_Chr_Soldier_Male_01 dentro del Player). Vacío = se busca solo.")]
    [SerializeField] private Transform modelRoot;
    [Tooltip("Nombre del hueso raíz del esqueleto.")]
    [SerializeField] private string skeletonRootName = "Root";
    [Tooltip("Los objetos cuyo nombre empiece con esto (armas de tercera persona en la mano) NO se ocultan ni se reemplazan.")]
    [SerializeField] private string[] keepNamePrefixes = { "SM_Wep" };

    [Header("Referencias (si están vacías se buscan solas)")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerHealth playerHealth;

    [Header("Pruebas (sin lobby)")]
    [Tooltip("Mientras esté tildado se ignora la selección del lobby y se usa Test Index. En Play podés cambiarlo en vivo.")]
    [SerializeField] private bool useTestIndex;
    [Tooltip("0 Soldado M, 1 Soldado F, 2 Tanque M, 3 Tanque F, 4 Explorador M, 5 Explorador F (el orden de Variants del catálogo).")]
    [SerializeField] private int testIndex;

    /// <summary>Número de personaje del jugador. Lo escribe el dueño; lo leen todos.</summary>
    public NetworkVariable<int> CharacterIndex = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    public CharacterCatalog Catalog => catalog;
    public int AppliedIndex => appliedIndex;

    private readonly List<GameObject> spawnedParts = new List<GameObject>();
    private bool baseHidden;
    private int appliedIndex = -1;

    // =========================================================
    // UNITY / NETCODE
    // =========================================================

    private void Awake()
    {
        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();

        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealth>();

        if (modelRoot == null)
            modelRoot = FindModelRoot();
    }

    public override void OnNetworkSpawn()
    {
        CharacterIndex.OnValueChanged += OnIndexChanged;

        if (IsOwner)
        {
            int wanted = useTestIndex ? testIndex : CharacterSelection.SelectedIndex;
            CharacterIndex.Value = ClampIndex(wanted);
        }

        Apply(CharacterIndex.Value);
    }

    public override void OnNetworkDespawn()
    {
        CharacterIndex.OnValueChanged -= OnIndexChanged;
        ClearSpawnedParts();
    }

    private void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Para probar sin lobby: cambiá Test Index en el Inspector durante el Play.
        if (IsSpawned && IsOwner && useTestIndex)
        {
            int wanted = ClampIndex(testIndex);

            if (CharacterIndex.Value != wanted)
                CharacterIndex.Value = wanted;
        }
#endif
    }

    private void OnIndexChanged(int previous, int current)
    {
        Apply(current);
    }

    // =========================================================
    // APLICAR PERSONAJE
    // =========================================================

    private void Apply(int index)
    {
        if (catalog == null || modelRoot == null)
        {
            Debug.LogWarning("[PlayerCharacter] Falta asignar el catálogo o no se encontró el modelo (Model Root).");
            return;
        }

        index = ClampIndex(index);

        if (index == appliedIndex)
            return;

        CharacterCatalog.Variant variant = catalog.GetVariant(index);

        if (variant == null || variant.modelPrefab == null)
        {
            Debug.LogWarning("[PlayerCharacter] El personaje " + index + " no tiene Model Prefab en el catálogo.");
            return;
        }

        HideBaseVisuals();
        ClearSpawnedParts();
        TransplantAppearance(variant.modelPrefab);
        ApplyStats(index);

        appliedIndex = index;
    }

    private void ApplyStats(int index)
    {
        CharacterCatalog.ClassData data = catalog.GetClassDataForVariant(index);

        if (data == null)
            return;

        if (playerMovement != null)
            playerMovement.SetMovementSpeeds(data.walkSpeed, data.sprintSpeed, data.crouchSpeed);

        if (playerHealth != null)
            playerHealth.SetMaxHealth(data.maxHealth, true);
    }

    // =========================================================
    // APARIENCIA
    // =========================================================

    // La apariencia que ya trae el Player (el soldado base) se oculta una sola vez. Se apaga el renderer, no el objeto:
    // la jerarquía no se toca. Las armas en la mano (keepNamePrefixes) se dejan como están.
    private void HideBaseVisuals()
    {
        if (baseHidden)
            return;

        baseHidden = true;

        Renderer[] renderers = modelRoot.GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];

            if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer))
                continue;

            if (IsKept(r.transform, modelRoot))
                continue;

            r.enabled = false;
        }
    }

    private void ClearSpawnedParts()
    {
        for (int i = 0; i < spawnedParts.Count; i++)
        {
            if (spawnedParts[i] != null)
                Destroy(spawnedParts[i]);
        }

        spawnedParts.Clear();
    }

    private void TransplantAppearance(GameObject prefab)
    {
        Transform dstSkeleton = modelRoot.Find(skeletonRootName);

        if (dstSkeleton == null)
        {
            Debug.LogError("[PlayerCharacter] No encuentro el hueso '" + skeletonRootName + "' dentro de " + modelRoot.name + ".");
            return;
        }

        // El modelo origen se crea dentro de un contenedor APAGADO: no despierta, no anima, no se ve.
        GameObject holder = new GameObject("CharacterTransplantHolder");
        holder.SetActive(false);

        GameObject source = Instantiate(prefab, holder.transform);
        Transform srcRoot = source.transform;

        if (srcRoot.Find(skeletonRootName) == null)
        {
            Debug.LogError("[PlayerCharacter] El prefab '" + prefab.name + "' no tiene el hueso '" + skeletonRootName + "'.");
            Destroy(holder);
            return;
        }

        // 1) Candidatas: renderers realmente activos en el prefab (las variantes apagadas de Synty se ignoran).
        HashSet<Transform> candidates = new HashSet<Transform>();
        Renderer[] renderers = source.GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];

            if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer))
                continue;

            if (!IsActiveInSource(r.transform, srcRoot))
                continue;

            if (IsKept(r.transform, srcRoot))
                continue;

            candidates.Add(r.transform);
        }

        // 2) Solo las piezas "de más arriba": el cristal de unas gafas viaja con las gafas.
        List<Transform> pieces = new List<Transform>();

        foreach (Transform candidate in candidates)
        {
            bool hasParentPiece = false;

            for (Transform p = candidate.parent; p != null && p != srcRoot; p = p.parent)
            {
                if (candidates.Contains(p))
                {
                    hasParentPiece = true;
                    break;
                }
            }

            if (!hasParentPiece)
                pieces.Add(candidate);
        }

        // 3) Mover cada pieza al esqueleto del Player (mismo hueso, misma posición local).
        int layer = modelRoot.gameObject.layer;

        for (int i = 0; i < pieces.Count; i++)
        {
            Transform piece = pieces[i];
            Transform dstParent = MapTransform(piece.parent, srcRoot, modelRoot);

            if (dstParent == null)
            {
                Debug.LogWarning("[PlayerCharacter] No encuentro dónde colgar '" + piece.name + "' (hueso '" + piece.parent.name + "').");
                continue;
            }

            SkinnedMeshRenderer smr = piece.GetComponent<SkinnedMeshRenderer>();

            if (smr != null)
                RemapBones(smr, srcRoot, modelRoot, dstSkeleton);

            piece.SetParent(dstParent, false);
            SetLayerRecursively(piece.gameObject, layer);
            spawnedParts.Add(piece.gameObject);
        }

        // Lo que sobra (esqueleto origen, Animator, variantes apagadas) se destruye.
        Destroy(holder);
    }

    // Huesos del cuerpo: se busca el equivalente en el esqueleto del Player por RUTA ("Root/Hips/Spine_01/...").
    private static void RemapBones(SkinnedMeshRenderer smr, Transform srcRoot, Transform dstRoot, Transform dstSkeleton)
    {
        Transform[] srcBones = smr.bones;
        Transform[] dstBones = new Transform[srcBones.Length];

        for (int i = 0; i < srcBones.Length; i++)
        {
            if (srcBones[i] == null)
                continue;

            dstBones[i] = MapTransform(srcBones[i], srcRoot, dstRoot);

            if (dstBones[i] == null)
                Debug.LogWarning("[PlayerCharacter] Hueso sin equivalente: " + srcBones[i].name + " (el cuerpo puede deformarse).");
        }

        smr.bones = dstBones;

        Transform mappedRootBone = smr.rootBone != null ? MapTransform(smr.rootBone, srcRoot, dstRoot) : null;
        smr.rootBone = mappedRootBone != null ? mappedRootBone : dstSkeleton;
    }

    private static Transform MapTransform(Transform source, Transform sourceRoot, Transform targetRoot)
    {
        if (source == null)
            return null;

        if (source == sourceRoot)
            return targetRoot;

        return targetRoot.Find(GetRelativePath(source, sourceRoot));
    }

    private static string GetRelativePath(Transform t, Transform root)
    {
        string path = t.name;

        for (Transform p = t.parent; p != null && p != root; p = p.parent)
            path = p.name + "/" + path;

        return path;
    }

    // Activo "de verdad" en el prefab: el objeto y todos sus padres hasta la raíz están encendidos.
    private static bool IsActiveInSource(Transform t, Transform root)
    {
        for (Transform p = t; p != null && p != root; p = p.parent)
        {
            if (!p.gameObject.activeSelf)
                return false;
        }

        return true;
    }

    private bool IsKept(Transform t, Transform root)
    {
        if (keepNamePrefixes == null || keepNamePrefixes.Length == 0)
            return false;

        for (Transform p = t; p != null && p != root; p = p.parent)
        {
            for (int i = 0; i < keepNamePrefixes.Length; i++)
            {
                if (!string.IsNullOrEmpty(keepNamePrefixes[i]) &&
                    p.name.StartsWith(keepNamePrefixes[i], System.StringComparison.Ordinal))
                    return true;
            }
        }

        return false;
    }

    private static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;

        foreach (Transform child in go.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    private Transform FindModelRoot()
    {
        Animator[] animators = GetComponentsInChildren<Animator>(true);

        for (int i = 0; i < animators.Length; i++)
        {
            if (animators[i].transform.Find(skeletonRootName) != null &&
                animators[i].GetComponentInParent<Camera>() == null)
                return animators[i].transform;
        }

        return null;
    }

    private int ClampIndex(int index)
    {
        if (catalog == null)
            return 0;

        return Mathf.Clamp(index, 0, Mathf.Max(0, catalog.VariantCount - 1));
    }
}
