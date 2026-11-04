using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Muestra en el lobby UNA copia VISUAL del Player.prefab real para UN slot.
/// - La variante se la indica quien lo maneja (LobbyCinematicPartyPresenter) con SetPlayer(visible, indice);
///   NUNCA lee CharacterSelection.SelectedIndex.
/// - Usa PlayerCharacter para aplicar la variante y NetworkWeaponPresentation para el cuerpo, el controller y el arma.
/// - No ejecuta movimiento, red, disparo ni gameplay: apaga todo menos el cuerpo de tercera persona.
/// - Solo construye el modelo cuando el slot está conectado (no hay personajes fantasma).
/// </summary>
public class LobbyCinematicPlayerDisplay : MonoBehaviour
{
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private Transform anchor;
    [SerializeField] private string weaponId = "Handgun";

    [Header("Idle")]
    [SerializeField] private float animatorSpeed = 0.92f;
    [SerializeField] private float subtleTurnDegrees = 1.2f;
    [SerializeField] private float subtleTurnSpeed = 0.16f;

    [Header("Acomodo en escena")]
    [Tooltip("Tiempo que tarda en deslizarse a su nueva posición cuando entra o sale otro jugador.")]
    [SerializeField] private float moveSmoothTime = 0.35f;

    [Header("Nombre sobre la cabeza")]
    [Tooltip("Altura sobre el hueso de la cabeza donde se ubica el nombre (cubre el casco).")]
    [SerializeField] private float headTopOffset = 0.30f;
    [Tooltip("Altura desde los pies si no se encuentra el hueso de la cabeza.")]
    [SerializeField] private float fallbackHeight = 1.95f;

    private GameObject current;
    private Animator bodyAnimator;
    private Transform headBone;
    private int currentWeaponAnimatorId = 1;
    private int builtIndex = -1;

    private bool visible;
    private Vector3 targetPosition;
    private float targetYaw = 180f;
    private float currentYaw = 180f;
    private Vector3 moveVelocity;
    private float yawVelocity;
    private float swayPhase;

    [Header("Diagnóstico")]
    [Tooltip("Escribe en la consola qué renderers quedan activos en cada personaje (sirve para detectar skins mezcladas).")]
    [SerializeField] private bool logDiagnostics = true;

    // Nodo "Visual": el Player real se arma ahi adentro mientras esta APAGADO.
    private Transform visualRoot;
    private Coroutine buildRoutine;
    private int buildToken;
    private bool building;

    private static MethodInfo applyMethod;
    private static bool applySearched;

    private static readonly int TP_Speed = Animator.StringToHash("TP_Speed");
    private static readonly int TP_IsAiming = Animator.StringToHash("TP_IsAiming");
    private static readonly int TP_MoveX = Animator.StringToHash("TP_MoveX");
    private static readonly int TP_MoveZ = Animator.StringToHash("TP_MoveZ");
    private static readonly int TP_MoveAmount = Animator.StringToHash("TP_MoveAmount");
    private static readonly int TP_WeaponID = Animator.StringToHash("TP_WeaponID");
    private static readonly int TP_IsAlive = Animator.StringToHash("TP_IsAlive");
    private static readonly int TP_IsGrounded = Animator.StringToHash("TP_IsGrounded");
    private static readonly int TP_IsReloading = Animator.StringToHash("TP_IsReloading");
    private static readonly int TP_IsSprinting = Animator.StringToHash("TP_IsSprinting");
    private static readonly int TP_IsCrouching = Animator.StringToHash("TP_IsCrouching");
    private static readonly int TP_AimWeight = Animator.StringToHash("TP_AimWeight");

    // Parámetros reales del animator del cuerpo (se leen UNA vez por modelo, no cada frame).
    private readonly Dictionary<int, AnimatorControllerParameterType> animatorParams =
        new Dictionary<int, AnimatorControllerParameterType>();

    public Transform Anchor => anchor;
    public bool IsVisible => visible;
    public int BuiltCharacterIndex => builtIndex;

    public void Configure(GameObject prefab, Transform newAnchor, string preferredWeaponId)
    {
        playerPrefab = prefab;
        anchor = newAnchor;

        if (!string.IsNullOrWhiteSpace(preferredWeaponId))
            weaponId = preferredWeaponId;

        // Fase distinta por jugador usando su orden en la jerarquía (GetInstanceID está obsoleto en Unity 6).
        swayPhase = newAnchor != null ? newAnchor.GetSiblingIndex() * 1.3f : 0f;
    }

    /// <summary>Posición y orientación (yaw) donde tiene que pararse este jugador.</summary>
    public void SetPose(Vector3 worldPosition, float yaw)
    {
        targetPosition = worldPosition;
        targetYaw = yaw;
    }

    /// <summary>Muestra u oculta el personaje de este slot y le indica qué variante dibujar.</summary>
    public void SetPlayer(bool show, int characterIndex)
    {
        if (anchor == null)
            return;

        if (!show)
        {
            Hide();
            return;
        }

        bool wasHidden = !visible;
        visible = true;

        if (wasHidden)
        {
            // Entra de golpe a su lugar (no viaja desde la posición vieja).
            anchor.position = targetPosition;
            currentYaw = targetYaw;
            moveVelocity = Vector3.zero;
            yawVelocity = 0f;
            anchor.rotation = Quaternion.Euler(0f, currentYaw, 0f);
        }

        if (!anchor.gameObject.activeSelf)
            anchor.gameObject.SetActive(true);

        characterIndex = Mathf.Max(0, characterIndex);

        if (current == null || builtIndex != characterIndex)
        {
            RebuildPlayer(characterIndex);
        }
        else if (wasHidden)
        {
            RestartAnimator();
        }
    }

    private void Hide()
    {
        visible = false;

        if (anchor != null && anchor.gameObject.activeSelf)
            anchor.gameObject.SetActive(false);
    }

    /// <summary>Punto del mundo justo encima de la cabeza (para ubicar el nombre).</summary>
    public bool TryGetHeadWorldPosition(out Vector3 position)
    {
        position = Vector3.zero;

        if (!visible || building || anchor == null || current == null)
            return false;

        if (headBone != null)
        {
            position = headBone.position + Vector3.up * headTopOffset;
            return true;
        }

        position = anchor.position + Vector3.up * fallbackHeight;
        return true;
    }

    private void OnDisable()
    {
        // El mundo se apaga cuando el panel del lobby se cierra: se libera el modelo.
        if (buildRoutine != null)
        {
            StopCoroutine(buildRoutine);
            buildRoutine = null;
        }

        buildToken++;
        building = false;

        if (current != null)
        {
            Destroy(current);
            current = null;
        }

        bodyAnimator = null;
        headBone = null;
        builtIndex = -1;
        visible = false;
        animatorParams.Clear();
    }

    private void Update()
    {
        if (!visible || anchor == null)
            return;

        float dt = Time.unscaledDeltaTime;

        anchor.position = Vector3.SmoothDamp(
            anchor.position,
            targetPosition,
            ref moveVelocity,
            Mathf.Max(0.01f, moveSmoothTime),
            Mathf.Infinity,
            dt);

        currentYaw = Mathf.SmoothDampAngle(
            currentYaw,
            targetYaw,
            ref yawVelocity,
            Mathf.Max(0.01f, moveSmoothTime),
            Mathf.Infinity,
            dt);

        float sway = Mathf.Sin(Time.unscaledTime * subtleTurnSpeed + swayPhase) * subtleTurnDegrees;
        anchor.rotation = Quaternion.Euler(0f, currentYaw + sway, 0f);
    }

    // =========================================================
    // CONSTRUCCIÓN DEL MODELO
    // =========================================================

    private void EnsureVisualRoot()
    {
        if (visualRoot != null)
            return;

        GameObject go = new GameObject("Visual");
        go.transform.SetParent(anchor, false);
        go.SetActive(false);
        visualRoot = go.transform;
    }

    private void DestroyCurrentModel()
    {
        if (current == null)
            return;

        // Destroy es diferido: se apaga ya para que no se vea ni un cuadro.
        current.SetActive(false);
        Destroy(current);
        current = null;
        bodyAnimator = null;
        headBone = null;
        animatorParams.Clear();
    }

    private void RebuildPlayer(int characterIndex)
    {
        buildToken++;

        if (buildRoutine != null)
        {
            StopCoroutine(buildRoutine);
            buildRoutine = null;
        }

        DestroyCurrentModel();
        builtIndex = -1;
        building = false;

        if (playerPrefab == null || anchor == null)
            return;

        EnsureVisualRoot();

        // El Player real se arma con su nodo APAGADO: asi no corren Awake / OnEnable / Start de sus scripts
        // (HUD, cartel de nombre, red...) hasta que el modelo esta limpio.
        visualRoot.gameObject.SetActive(false);
        building = true;

        // Foto de la UI existente: lo que el Player cree por su cuenta se apaga despues.
        HashSet<Component> uiBefore = SnapshotUi();

        current = Instantiate(playerPrefab, visualRoot);
        current.name = "Lobby_Player_Visual";
        current.transform.localPosition = Vector3.zero;
        current.transform.localRotation = Quaternion.identity;
        current.transform.localScale = Vector3.one;

        builtIndex = characterIndex;

        // El Player trae su propio cartel de nombre ("JUGADOR"): en el lobby no existe.
        RemoveNameplates(current);

        PlayerCharacter playerCharacter = current.GetComponent<PlayerCharacter>();
        NetworkWeaponPresentation presentation = current.GetComponent<NetworkWeaponPresentation>();

        Transform bodyRoot = presentation != null ? presentation.Body : null;

        // Piezas del cuerpo que existen ANTES de aplicar la variante (el soldado por defecto del prefab).
        HashSet<Renderer> bodyBefore = CollectRenderers(bodyRoot);

        // 1) Aplicar exactamente la apariencia que usa PlayerCharacter.
        if (playerCharacter != null)
            ApplyCharacter(playerCharacter, characterIndex);

        // 2) Cuerpo, animator y arma desde NetworkWeaponPresentation.
        GameObject heldWeapon = null;

        if (presentation != null)
        {
            if (presentation.Body != null)
                bodyRoot = presentation.Body;

            bodyAnimator = presentation.BodyAnimator;

            NetworkWeaponPresentation.Definition[] defs = presentation.Weapons;

            if (defs != null && defs.Length > 0)
            {
                int index = FindWeaponIndex(defs, weaponId);

                if (index < 0)
                    index = 0;

                currentWeaponAnimatorId = index + 1;

                NetworkWeaponPresentation.Definition def = defs[index];

                if (bodyAnimator != null && def.Controller != null)
                    bodyAnimator.runtimeAnimatorController = def.Controller;

                if (bodyRoot != null && def.Model != null)
                {
                    Transform hand = FindDeep(bodyRoot, "Hand_R");

                    if (hand != null)
                    {
                        heldWeapon = Instantiate(def.Model, hand);
                        heldWeapon.name = "Lobby_" + def.Id;
                        heldWeapon.transform.localPosition = def.GripPosition;
                        heldWeapon.transform.localRotation = Quaternion.Euler(def.GripRotation);
                        heldWeapon.transform.localScale = Vector3.one;
                        ForceRenderers(heldWeapon);
                    }
                }
            }
        }

        // 3) Lo que NO es el cuerpo de tercera persona (brazos en primera persona, inventario...) se apaga.
        //    OJO: a las piezas del cuerpo NO se les toca "enabled": se respeta lo que hizo Apply.
        if (bodyRoot != null)
        {
            Renderer[] renderers = current.GetComponentsInChildren<Renderer>(true);

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];

                bool belongsToBody =
                    renderer.transform == bodyRoot ||
                    renderer.transform.IsChildOf(bodyRoot);

                if (!belongsToBody)
                {
                    renderer.enabled = false;
                    continue;
                }

                SkinnedMeshRenderer skinned = renderer as SkinnedMeshRenderer;

                if (skinned != null)
                    skinned.updateWhenOffscreen = true;
            }
        }
        else
        {
            ForceRenderers(current);
        }

        // 4) Apagar gameplay, HUD, camaras, audio y luces del prefab.
        DisableGameplay(current, bodyAnimator);
        HideInterface(current, bodyRoot);

        // 5) El resto se termina el cuadro siguiente: los Destroy de Apply recien ahi se efectuan.
        buildRoutine = StartCoroutine(FinishBuild(buildToken, bodyRoot, bodyBefore, heldWeapon, uiBefore, characterIndex));
    }

    private IEnumerator FinishBuild(
        int token,
        Transform bodyRoot,
        HashSet<Renderer> bodyBefore,
        GameObject heldWeapon,
        HashSet<Component> uiBefore,
        int characterIndex)
    {
        // Deja que PlayerCharacter.Apply termine sus Destroy diferidos.
        yield return null;

        if (token != buildToken || current == null)
            yield break;

        if (bodyRoot != null)
            CleanLeftoverDefaultSkin(bodyRoot, bodyBefore, heldWeapon, characterIndex);

        headBone = FindHead(bodyRoot);

        // Recien ahora el Player existe "de verdad" (sus scripts ya estan apagados y sin cartel).
        building = false;
        visualRoot.gameObject.SetActive(true);

        if (bodyAnimator != null)
        {
            bodyAnimator.enabled = true;
            bodyAnimator.applyRootMotion = false;
            bodyAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            bodyAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
            bodyAnimator.speed = animatorSpeed;

            CacheAnimatorParameters();
            RestartAnimator();
        }

        // Red de seguridad: si el Player crea UI propia al activarse (tambien en cuadros posteriores), se apaga.
        DeactivateNewUi(uiBefore);
        yield return null;

        if (token == buildToken && current != null)
            DeactivateNewUi(uiBefore);

        yield return null;

        if (token == buildToken && current != null)
            DeactivateNewUi(uiBefore);

        buildRoutine = null;
    }

    /// <summary>
    /// Si Apply CREO piezas nuevas para la variante elegida, las piezas del soldado por defecto que sobrevivieron
    /// se apagan (eso es la "skin mezclada"). Si Apply modifico las piezas en el lugar, no se toca nada.
    /// Nunca se enciende un renderer que Apply haya apagado.
    /// </summary>
    private void CleanLeftoverDefaultSkin(
        Transform bodyRoot,
        HashSet<Renderer> before,
        GameObject heldWeapon,
        int characterIndex)
    {
        Renderer[] after = bodyRoot.GetComponentsInChildren<Renderer>(true);

        int added = 0;
        int hidden = 0;
        System.Text.StringBuilder hiddenNames = new System.Text.StringBuilder();

        for (int i = 0; i < after.Length; i++)
        {
            Renderer r = after[i];

            if (r == null || before.Contains(r))
                continue;

            if (IsUnderOrSelf(r.transform, heldWeapon))
                continue;

            added++;
        }

        if (added > 0)
        {
            for (int i = 0; i < after.Length; i++)
            {
                Renderer r = after[i];

                if (r == null || !before.Contains(r))
                    continue;

                if (IsUnderOrSelf(r.transform, heldWeapon))
                    continue;

                // Las armas que PlayerCharacter conserva a proposito (keepNamePrefixes = SM_Wep).
                if (r.name.StartsWith("SM_Wep", StringComparison.Ordinal))
                    continue;

                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer)
                    continue;

                if (r.enabled)
                {
                    r.enabled = false;
                    hidden++;

                    if (hiddenNames.Length > 0)
                        hiddenNames.Append(", ");

                    hiddenNames.Append(r.name);
                }
            }
        }

        // Red de seguridad: si por algun motivo no quedo nada visible, se muestra el cuerpo tal como esta.
        int visibleCount = 0;

        for (int i = 0; i < after.Length; i++)
        {
            Renderer r = after[i];

            if (r != null && r.enabled && ActiveInPrefab(r.transform, current.transform))
                visibleCount++;
        }

        if (visibleCount == 0)
        {
            for (int i = 0; i < after.Length; i++)
            {
                if (after[i] == null)
                    continue;

                after[i].enabled = true;
                after[i].forceRenderingOff = false;
            }
        }

        if (logDiagnostics)
        {
            Debug.Log(
                "[LobbyCinematic] Variante " + characterIndex + ": Apply creo " + added +
                " pieza(s) nueva(s); piezas del soldado por defecto ocultadas: " + hidden +
                (hidden > 0 ? " (" + hiddenNames + ")" : "") + ".");
        }
    }

    private static bool IsUnderOrSelf(Transform t, GameObject root)
    {
        return root != null && t != null && (t == root.transform || t.IsChildOf(root.transform));
    }

    private static HashSet<Renderer> CollectRenderers(Transform root)
    {
        HashSet<Renderer> set = new HashSet<Renderer>();

        if (root == null)
            return set;

        Renderer[] all = root.GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < all.Length; i++)
            set.Add(all[i]);

        return set;
    }

    /// <summary>Quita el cartel de nombre propio del Player (PlayerNameplate / DisplayName) antes de que corra.</summary>
    private static void RemoveNameplates(GameObject root)
    {
        MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);

        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour b = behaviours[i];

            if (b != null && b.GetType().Name.IndexOf("Nameplate", StringComparison.OrdinalIgnoreCase) >= 0)
                DestroyImmediate(b);
        }

        Transform[] all = root.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i] != root.transform && all[i].name == "DisplayName")
                all[i].gameObject.SetActive(false);
        }
    }

    private void RestartAnimator()
    {
        if (bodyAnimator == null || building || visualRoot == null || !visualRoot.gameObject.activeInHierarchy)
            return;

        bodyAnimator.Rebind();
        ApplyIdlePose();
        bodyAnimator.Update(0f);
    }

    /// <summary>Llama al método de PlayerCharacter que aplica una variante (es privado en el proyecto).</summary>
    private static void ApplyCharacter(PlayerCharacter target, int characterIndex)
    {
        if (!applySearched)
        {
            applySearched = true;

            string[] candidates = { "Apply", "ApplyCharacter", "ApplyAppearance", "ApplyVariant", "ApplyIndex" };
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            for (int c = 0; c < candidates.Length && applyMethod == null; c++)
            {
                MethodInfo[] methods = typeof(PlayerCharacter).GetMethods(flags);

                for (int m = 0; m < methods.Length; m++)
                {
                    if (methods[m].Name != candidates[c])
                        continue;

                    ParameterInfo[] pars = methods[m].GetParameters();

                    if (pars.Length == 1 && pars[0].ParameterType == typeof(int))
                    {
                        applyMethod = methods[m];
                        break;
                    }
                }
            }

            if (applyMethod == null)
            {
                Debug.LogWarning(
                    "[LobbyCinematicPlayerDisplay] No encontré en PlayerCharacter un método Apply(int). " +
                    "Los personajes se verán con la variante por defecto del prefab.");
            }
        }

        if (applyMethod == null)
            return;

        try
        {
            applyMethod.Invoke(target, new object[] { characterIndex });
        }
        catch (Exception ex)
        {
            Exception real = ex.InnerException != null ? ex.InnerException : ex;

            Debug.LogWarning(
                "[LobbyCinematicPlayerDisplay] No pude aplicar la variante " + characterIndex + ": " + real.Message);
        }
    }

    // =========================================================
    // POSE IDLE
    // =========================================================

    private void CacheAnimatorParameters()
    {
        animatorParams.Clear();

        if (bodyAnimator == null)
            return;

        AnimatorControllerParameter[] pars = bodyAnimator.parameters;

        for (int i = 0; i < pars.Length; i++)
            animatorParams[pars[i].nameHash] = pars[i].type;
    }

    private void ApplyIdlePose()
    {
        if (bodyAnimator == null)
            return;

        SetFloat(TP_Speed, 0f);
        SetFloat(TP_MoveX, 0f);
        SetFloat(TP_MoveZ, 0f);
        SetFloat(TP_MoveAmount, 0f);
        SetFloat(TP_WeaponID, currentWeaponAnimatorId);
        SetFloat(TP_AimWeight, 0f);

        SetBool(TP_IsAlive, true);
        SetBool(TP_IsGrounded, true);
        SetBool(TP_IsAiming, false);
        SetBool(TP_IsReloading, false);
        SetBool(TP_IsSprinting, false);
        SetBool(TP_IsCrouching, false);
    }

    private void SetBool(int hash, bool value)
    {
        AnimatorControllerParameterType type;

        if (animatorParams.TryGetValue(hash, out type) && type == AnimatorControllerParameterType.Bool)
            bodyAnimator.SetBool(hash, value);
    }

    private void SetFloat(int hash, float value)
    {
        AnimatorControllerParameterType type;

        if (animatorParams.TryGetValue(hash, out type) && type == AnimatorControllerParameterType.Float)
            bodyAnimator.SetFloat(hash, value);
    }

    // =========================================================
    // SKINS Y UI QUE NO DEBEN VERSE
    // =========================================================

    private static bool ActiveInPrefab(Transform t, Transform root)
    {
        while (t != null)
        {
            if (!t.gameObject.activeSelf)
                return false;

            if (t == root)
                break;

            t = t.parent;
        }

        return true;
    }

    /// <summary>Foto de los Canvas y textos que existen ahora (para detectar los que cree el Player al nacer).</summary>
    private static HashSet<Component> SnapshotUi()
    {
        HashSet<Component> set = new HashSet<Component>();

        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            set.Add(canvas);

        foreach (TMP_Text text in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            set.Add(text);

        return set;
    }

    /// <summary>Apaga cualquier Canvas o texto que el Player haya creado FUERA de su propia jerarquía.</summary>
    private void DeactivateNewUi(HashSet<Component> before)
    {
        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (before.Contains(canvas) || canvas.transform.IsChildOf(current.transform))
                continue;

            Debug.Log("[LobbyCinematic] El Player creo un Canvas fuera de si mismo y se apago: " + canvas.name);
            canvas.gameObject.SetActive(false);
        }

        foreach (TMP_Text text in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (before.Contains(text) || text.transform.IsChildOf(current.transform))
                continue;

            Debug.Log("[LobbyCinematic] El Player creo un texto fuera de si mismo y se apago: " + text.name);
            text.gameObject.SetActive(false);
        }
    }

    /// <summary>Desactiva por completo cualquier texto o imagen de UI del Player (HUD, nombre, marcadores).</summary>
    private static void HideInterface(GameObject root, Transform bodyRoot)
    {
        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text != null)
                text.gameObject.SetActive(false);
        }

        foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true))
        {
            if (graphic == null || graphic.gameObject == root)
                continue;

            if (bodyRoot != null && bodyRoot.IsChildOf(graphic.transform))
                continue;

            graphic.gameObject.SetActive(false);
        }
    }

    // =========================================================
    // UTILIDADES
    // =========================================================

    private static void DisableGameplay(GameObject root, Animator keepAnimator)
    {
        foreach (Camera cam in root.GetComponentsInChildren<Camera>(true))
            cam.enabled = false;

        foreach (AudioListener listener in root.GetComponentsInChildren<AudioListener>(true))
            listener.enabled = false;

        foreach (AudioSource source in root.GetComponentsInChildren<AudioSource>(true))
        {
            source.Stop();
            source.enabled = false;
        }

        foreach (Light light in root.GetComponentsInChildren<Light>(true))
            light.enabled = false;

        foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
            canvas.enabled = false;

        foreach (Collider col in root.GetComponentsInChildren<Collider>(true))
            col.enabled = false;

        foreach (Rigidbody rb in root.GetComponentsInChildren<Rigidbody>(true))
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        foreach (Animator animator in root.GetComponentsInChildren<Animator>(true))
        {
            if (animator != keepAnimator)
                animator.enabled = false;
        }

        foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour != null)
                behaviour.enabled = false;
        }
    }

    private static Transform FindHead(Transform bodyRoot)
    {
        if (bodyRoot == null)
            return null;

        Animator animator = bodyRoot.GetComponentInChildren<Animator>(true);

        if (animator != null && animator.isHuman)
        {
            Transform bone = animator.GetBoneTransform(HumanBodyBones.Head);

            if (bone != null)
                return bone;
        }

        return FindDeep(bodyRoot, "Head");
    }

    private static int FindWeaponIndex(NetworkWeaponPresentation.Definition[] defs, string wanted)
    {
        if (defs == null)
            return -1;

        for (int i = 0; i < defs.Length; i++)
        {
            if (defs[i] == null)
                continue;

            if (string.Equals(defs[i].Id, wanted, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root == null)
            return null;

        if (root.name == name)
            return root;

        foreach (Transform child in root)
        {
            Transform found = FindDeep(child, name);

            if (found != null)
                return found;
        }

        return null;
    }

    private static void ForceRenderers(GameObject root)
    {
        if (root == null)
            return;

        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            renderer.enabled = true;
            renderer.forceRenderingOff = false;

            SkinnedMeshRenderer skinned = renderer as SkinnedMeshRenderer;
            if (skinned != null)
                skinned.updateWhenOffscreen = true;
        }
    }
}
