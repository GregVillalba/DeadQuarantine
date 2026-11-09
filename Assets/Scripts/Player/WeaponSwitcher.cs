
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>Casillero del HUD de armas al que pertenece cada arma.</summary>
public enum WeaponSlotCategory
{
    Primary = 0,
    Secondary = 1
}

public partial class WeaponSwitcher : MonoBehaviour
{
    [System.Serializable]
    public class WeaponSlot
    {
        public string weaponId;
        public GameObject weaponRoot;
        public Weapon weaponComponent;
        public AnimatorOverrideController overrideController;
        public AnimatorOverrideController tpOverrideController;
        [Tooltip("Familia del Animator TP: 1=Pistola, 2=Rifle/SMG, 3=Escopeta. 0=índice histórico.")]
        public int tpWeaponId;
        public bool unlockedByDefault;

        [Tooltip("Objeto de UI del icono del slot.")]
        public GameObject weaponIcon;

        [Header("Inventario (HUD)")]
        public WeaponSlotCategory category;

        [Header("Casquillo")]
        public GameObject casingPrefab;
        public Transform casingEjectPoint;
    }

    [Header("Referencias")]
    [SerializeField] private Animator weaponAnimator;
    [SerializeField] private RuntimeAnimatorController baseController;
    [SerializeField] private Animator thirdPersonAnimator;
    [SerializeField] private RuntimeAnimatorController tpBaseController;
    [SerializeField] private HUDController hudController;
    [SerializeField] private WeaponAnimationEvents weaponAnimationEvents;
    [SerializeField] private WeaponSway weaponSway;

    [Header("Animator")]
    [SerializeField] private string baseIdleStateName = "";

    [Header("Armas")]
    [SerializeField] private WeaponSlot[] slots;
    [SerializeField] private int startingSlotIndex = 0;

    [Header("Diagnóstico")]
    [SerializeField] private bool debugInventory = false;

    private int[] inventory;

    private readonly List<int> queuedUnlocks = new List<int>();
    private readonly List<bool> queuedEquip = new List<bool>();
    private bool queueRunning;

    private int currentIndex;
    private int pendingIndex = -1;
    private bool isSwitching;
    private int[] layerIdleHashes;
    private bool unholsterPhase;
    private int holsterLayerIndex = -1;

    private PlayerHealth saludJugador;
    private int armaInicialIndex = -1;
    private int armaAntesDeAbatido = -1;
    private bool abatido;
    private Coroutine rutinaAbatido;

    public Weapon CurrentWeapon =>
        slots != null && currentIndex >= 0 && currentIndex < slots.Length
            ? slots[currentIndex].weaponComponent
            : null;

    public int CurrentWeaponIndex => currentIndex;

    /// <summary>Familia visual de tercera persona para el arma equipada.</summary>
    public int CurrentThirdPersonWeaponId
    {
        get
        {
            if (slots == null || currentIndex < 0 || currentIndex >= slots.Length ||
                slots[currentIndex] == null)
                return 0;

            int configured = slots[currentIndex].tpWeaponId;
            return configured > 0 ? configured : currentIndex + 1;
        }
    }

    public bool IsSwitching => isSwitching;
    public bool IsHolstering => isSwitching && !unholsterPhase;
    public bool IsUnholstering => isSwitching && unholsterPhase;

    public event System.Action InventoryChanged;
    public event System.Action BindingsChanged;

    private PlayerControls controls;

    private InputAction selectPrimaryAction;
    private InputAction selectSecondaryAction;
    private InputAction nextWeaponAction;
    private InputAction previousWeaponAction;

    private readonly List<int> cycleBuffer = new List<int>();

    public bool IsInitialized { get; private set; }

    private void Awake()
    {
        BuildInitialInventory();

        controls = new PlayerControls();
        ConfiguracionesJuego.CargarRebinds(controls.asset);

        selectPrimaryAction = FindAction("SelectPrimary");
        selectSecondaryAction = FindAction("SelectSecondary");
        nextWeaponAction = FindAction("NextWeapon");
        previousWeaponAction = FindAction("PreviousWeapon");

        if (weaponAnimator != null)
            holsterLayerIndex = weaponAnimator.GetLayerIndex("Holster");

        ConfiguracionesJuego.CambiosAplicados += OnCambiosAplicados;
    }

    private void OnCambiosAplicados()
    {
        BindingsChanged?.Invoke();
    }

    private void OnEnable()
    {
        if (controls == null)
            return;

        controls.Player.Enable();

        if (selectPrimaryAction != null)
            selectPrimaryAction.performed += OnSelectPrimary;

        if (selectSecondaryAction != null)
            selectSecondaryAction.performed += OnSelectSecondary;

        if (nextWeaponAction != null)
            nextWeaponAction.performed += OnNextWeapon;

        if (previousWeaponAction != null)
            previousWeaponAction.performed += OnPreviousWeapon;
    }

    private void OnDisable()
    {
        if (selectPrimaryAction != null)
            selectPrimaryAction.performed -= OnSelectPrimary;

        if (selectSecondaryAction != null)
            selectSecondaryAction.performed -= OnSelectSecondary;

        if (nextWeaponAction != null)
            nextWeaponAction.performed -= OnNextWeapon;

        if (previousWeaponAction != null)
            previousWeaponAction.performed -= OnPreviousWeapon;

        queueRunning = false;

        if (controls != null)
            controls.Player.Disable();
    }

    private void OnDestroy()
    {
        ConfiguracionesJuego.CambiosAplicados -= OnCambiosAplicados;

        if (saludJugador != null)
            saludJugador.OnStateChanged -= OnEstadoJugadorCambio;

        controls?.Dispose();
    }

    private InputAction FindAction(string actionName)
    {
        InputAction action =
            controls.asset.FindAction("Player/" + actionName, false);

        if (action == null)
        {
            Debug.LogWarning(
                "[WeaponSwitcher] Falta la acción '" + actionName +
                "' en PlayerControls.inputactions (mapa Player)."
            );
        }

        return action;
    }

    private void Start()
    {
        if (slots == null || slots.Length == 0)
        {
            Debug.LogError("[WeaponSwitcher] No hay armas configuradas.");
            return;
        }

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null && slots[i].weaponRoot != null)
                slots[i].weaponRoot.SetActive(false);
        }

        int index = Mathf.Clamp(startingSlotIndex, 0, slots.Length - 1);

        if (!IsInInventory(index))
        {
            Debug.LogWarning(
                "[WeaponSwitcher] El slot inicial (" + index +
                ") no está desbloqueado. Revisá 'Unlocked By Default'."
            );

            index = inventory[0] >= 0 ? inventory[0] : 0;
        }

        EquipWeaponImmediate(index);

        StartCoroutine(CaptureBaseIdleState());

        armaInicialIndex = index;
        SuscribirEstadoJugador();

        if (CurrentWeapon != null)
            CurrentWeapon.InputLocked = true;

        IsInitialized = true;

        if (debugInventory)
            Debug.Log("[WeaponSwitcher] Inicialización completada.");
    }

    private void OnSelectPrimary(InputAction.CallbackContext context)
    {
        TrySelect(WeaponSlotCategory.Primary);
    }

    private void OnSelectSecondary(InputAction.CallbackContext context)
    {
        TrySelect(WeaponSlotCategory.Secondary);
    }

    private void OnNextWeapon(InputAction.CallbackContext context)
    {
        if (PlayerInputGate.ActionsBlocked)
            return;

        CycleWeapon(1);
    }

    private void OnPreviousWeapon(InputAction.CallbackContext context)
    {
        if (PlayerInputGate.ActionsBlocked)
            return;

        CycleWeapon(-1);
    }

    private void CycleWeapon(int direction)
    {
        if (isSwitching)
            return;

        cycleBuffer.Clear();

        for (int c = 0; c < inventory.Length; c++)
        {
            if (inventory[c] >= 0)
                cycleBuffer.Add(inventory[c]);
        }

        if (cycleBuffer.Count < 2)
            return;

        int position = cycleBuffer.IndexOf(currentIndex);

        if (position < 0)
            position = 0;

        int next = cycleBuffer[
            (position + direction + cycleBuffer.Count) % cycleBuffer.Count
        ];

        RequestEquip(next);
    }

    private void TrySelect(WeaponSlotCategory category)
    {
        if (PlayerInputGate.ActionsBlocked)
            return;

        SelectCategory(category);
    }

    // =========================================================
    // INVENTARIO
    // =========================================================

    public bool IsReady => inventory != null;
    public int SlotCount => slots != null ? slots.Length : 0;

    public int SelectedIndex =>
        (isSwitching && pendingIndex >= 0) ? pendingIndex : currentIndex;

    public WeaponSlotCategory SelectedCategory => GetCategory(SelectedIndex);

    public bool IsUnlocked(int index)
    {
        return index >= 0 &&
               index < slots.Length &&
               (IsInInventory(index) || queuedUnlocks.Contains(index));
    }

    public GameObject GetIconObject(int index)
    {
        return (index >= 0 && index < slots.Length)
            ? slots[index].weaponIcon
            : null;
    }

    public WeaponAttachments GetAttachments(int index)
    {
        if (index < 0 || index >= slots.Length)
            return null;

        WeaponSlot slot = slots[index];
        WeaponAttachments found = null;

        if (slot.weaponRoot != null)
        {
            found = slot.weaponRoot
                .GetComponentInChildren<WeaponAttachments>(true);
        }

        if (found == null && slot.weaponComponent != null)
        {
            found = slot.weaponComponent
                .GetComponentInChildren<WeaponAttachments>(true);
        }

        return found;
    }

    public string GetKeyLabel(WeaponSlotCategory category)
    {
        InputAction action =
            category == WeaponSlotCategory.Primary
                ? selectPrimaryAction
                : selectSecondaryAction;

        string fallback =
            category == WeaponSlotCategory.Primary ? "1" : "2";

        if (action == null || action.bindings.Count == 0)
            return fallback;

        string label = action.GetBindingDisplayString(0);

        return string.IsNullOrEmpty(label) ? fallback : label;
    }

    public void RefreshBindings()
    {
        if (controls == null)
            return;

        bool wasEnabled = controls.Player.enabled;

        if (wasEnabled)
            controls.Player.Disable();

        ConfiguracionesJuego.CargarRebinds(controls.asset);

        if (wasEnabled)
            controls.Player.Enable();

        BindingsChanged?.Invoke();
    }

    private static Sprite GetIconSprite(GameObject icon)
    {
        if (icon == null)
            return null;

        UnityEngine.UI.Image[] images =
            icon.GetComponentsInChildren<UnityEngine.UI.Image>(true);

        for (int i = 0; i < images.Length; i++)
        {
            if (string.Equals(
                images[i].gameObject.name,
                "Weapon",
                System.StringComparison.OrdinalIgnoreCase))
            {
                return images[i].sprite;
            }
        }

        // Los iconos por capas pueden traer Scope/Magazine antes que Body.
        for (int i = 0; i < images.Length; i++)
        {
            if (string.Equals(images[i].gameObject.name, "Body",
                System.StringComparison.OrdinalIgnoreCase) && images[i].sprite != null)
                return images[i].sprite;
        }

        for (int i = 0; i < images.Length; i++)
        {
            if (images[i].sprite != null)
                return images[i].sprite;
        }

        return null;
    }

    public WeaponSlotCategory GetCategory(int index)
    {
        int slot = InventorySlotOf(index);
        return (WeaponSlotCategory)Mathf.Max(0, slot);
    }

    public int GetIndexForCategory(WeaponSlotCategory category)
    {
        return inventory[(int)category];
    }

    public void SelectCategory(WeaponSlotCategory category)
    {
        if (isSwitching)
            return;

        int target = inventory[(int)category];

        if (target >= 0 && target != currentIndex)
            RequestEquip(target);
    }

    private void BuildInitialInventory()
    {
        inventory = new int[] { -1, -1 };

        if (slots == null)
            return;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null || !slots[i].unlockedByDefault)
                continue;

            int free = FirstFreeInventorySlot();

            if (free < 0)
            {
                Debug.LogWarning(
                    "[WeaponSwitcher] Hay más de 2 armas con " +
                    "'Unlocked By Default'. Se ignora '" +
                    slots[i].weaponId + "'."
                );

                continue;
            }

            inventory[free] = i;
        }
    }

    private int FirstFreeInventorySlot()
    {
        for (int c = 0; c < inventory.Length; c++)
        {
            if (inventory[c] < 0)
                return c;
        }

        return -1;
    }

    private int InventorySlotOf(int index)
    {
        if (inventory == null || index < 0)
            return -1;

        for (int c = 0; c < inventory.Length; c++)
        {
            if (inventory[c] == index)
                return c;
        }

        return -1;
    }

    private bool IsInInventory(int index)
    {
        return InventorySlotOf(index) >= 0;
    }

    private int FindSlotIndex(string weaponId)
    {
        if (slots == null || string.IsNullOrEmpty(weaponId))
            return -1;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null && slots[i].weaponId == weaponId)
                return i;
        }

        return -1;
    }

    // =========================================================
    // ESTADO ABATIDO
    // =========================================================

    private void SuscribirEstadoJugador()
    {
        saludJugador =
            transform.root.GetComponentInChildren<PlayerHealth>(true);

        if (saludJugador == null)
            return;

        saludJugador.OnStateChanged += OnEstadoJugadorCambio;

        if (saludJugador.IsOwner && saludJugador.IsDowned)
            EntrarEnAbatido();
    }

    private void OnEstadoJugadorCambio(
        PlayerHealth.PlayerState anterior,
        PlayerHealth.PlayerState nuevo
    )
    {
        if (saludJugador == null || !saludJugador.IsOwner)
            return;

        if (nuevo == PlayerHealth.PlayerState.Downed)
            EntrarEnAbatido();
        else if (abatido)
            SalirDeAbatido();
    }

    private void EntrarEnAbatido()
    {
        if (abatido)
            return;

        abatido = true;
        armaAntesDeAbatido = SelectedIndex;

        ForzarEquipo(armaInicialIndex, true);
    }

    private void SalirDeAbatido()
    {
        abatido = false;

        int destino = armaAntesDeAbatido;
        armaAntesDeAbatido = -1;

        if (destino >= 0 && IsInInventory(destino))
            ForzarEquipo(destino, false);
    }

    private void ForzarEquipo(int index, bool liberarInput)
    {
        if (rutinaAbatido != null)
            StopCoroutine(rutinaAbatido);

        rutinaAbatido =
            StartCoroutine(ForzarEquipoRoutine(index, liberarInput));
    }

    private IEnumerator ForzarEquipoRoutine(
        int index,
        bool liberarInput
    )
    {
        while (isSwitching)
            yield return null;

        if (index < 0 || index >= slots.Length)
        {
            rutinaAbatido = null;
            yield break;
        }

        if (index != currentIndex)
            RequestEquip(index, true);

        float limite = Time.time + 3f;

        while (isSwitching && Time.time < limite)
            yield return null;

        if (isSwitching && currentIndex == index)
            NotifyUnholsterFinished();

        Weapon arma = slots[index].weaponComponent;

        if (arma != null && !isSwitching)
        {
            if (liberarInput)
                arma.InputLocked = false;

            if (!arma.enabled)
                arma.enabled = true;

            if (debugInventory)
            {
                Debug.Log(
                    "[WeaponSwitcher] Arma forzada '" +
                    slots[index].weaponId +
                    "' | enabled=" + arma.enabled +
                    " | InputLocked=" + arma.InputLocked
                );
            }
        }

        rutinaAbatido = null;
    }

    // =========================================================
    // EQUIPAR ARMA
    // =========================================================

    private void EquipWeaponImmediate(int index)
    {
        if (index < 0 || index >= slots.Length)
            return;

        currentIndex = index;
        WeaponSlot slot = slots[currentIndex];

        ApplyControllerAndVisuals(slot);

        if (weaponAnimator != null)
            weaponAnimator.SetBool("Holstered", false);
    }

    public void RequestEquip(int index, bool forzar = false)
    {
        if (index < 0 || index >= slots.Length)
            return;

        if (!PermitidaEnClase(index)) return;

        if (abatido && !forzar)
            return;

        if (!forzar && !IsInInventory(index))
            return;

        if (index == currentIndex || isSwitching)
            return;

        pendingIndex = index;
        InventoryChanged?.Invoke();

        StartCoroutine(SwitchWeaponRoutine());
    }

    private IEnumerator SwitchWeaponRoutine()
    {
        isSwitching = true;
        unholsterPhase = false;

        WeaponSlot current = slots[currentIndex];

        // Cambiar de arma cancela la recarga en curso y bloquea disparo/recarga/apuntado
        // sin ocultar el modelo todavía.
        if (current.weaponComponent != null)
        {
            current.weaponComponent.CancelReload();
            current.weaponComponent.enabled = false;
        }

        BeginPerkSwitchSpeed();
        if (weaponAnimator != null)
        {
            if (IsNewIntegratedWeapon(currentIndex) && holsterLayerIndex >= 0) weaponAnimator.SetLayerWeight(holsterLayerIndex, 1f);
            weaponAnimator.SetBool("Holstered", true);
            if (IsNewIntegratedWeapon(currentIndex)) weaponAnimator.Play("Holster.Holster", holsterLayerIndex, 0f);
        }

        // Protección para clips importados de las nuevas armas que no traen Animation Events.
        // Las tres armas originales mantienen su flujo previo sin cambios.
        bool requiresSafeCompletion = IsNewIntegratedWeapon(currentIndex) || IsNewIntegratedWeapon(pendingIndex);

        // Esperar a que termine el clip de Holster antes de tapar el modelo.
        if (weaponAnimator != null && holsterLayerIndex >= 0)
        {
            yield return null; // Deja que el Animator entre en la transición.
            float startWaiting = Time.unscaledTime;

            while (true)
            {
                if (requiresSafeCompletion && Time.unscaledTime - startWaiting >= 2.5f / PerkSwitchMultiplier)
                {
                    Debug.LogWarning("[WeaponSwitcher] Holster sin finalización en arma integrada: sigo al arma siguiente para evitar bloqueo.");
                    break;
                }
                bool inTransition = weaponAnimator.IsInTransition(holsterLayerIndex);
                AnimatorStateInfo info = weaponAnimator.GetCurrentAnimatorStateInfo(holsterLayerIndex);

                if (!inTransition && info.shortNameHash == Animator.StringToHash("Holster") && info.normalizedTime >= 1f)
                    break;

                yield return null;
            }
        }

        // Se corta cualquier animación que haya quedado a medias (por ejemplo una recarga) antes de cambiar de arma.
        ResetBaseLayerToIdle();

        if (current.weaponRoot != null)
            current.weaponRoot.SetActive(false);

        currentIndex = pendingIndex;
        WeaponSlot next = slots[currentIndex];

        // El arma nueva se apaga ANTES de activar su modelo: si nunca se había usado, su componente
        // seguía encendido y habría podido disparar/recargar/apuntar durante la animación de sacarla.
        // NotifyUnholsterFinished() la vuelve a encender al terminar el Unholster.
        if (next.weaponComponent != null)
            next.weaponComponent.enabled = false;

        ApplyControllerAndVisuals(next);
        RefreshPerkSwitchSpeed();

        // El Animator es el mismo para todas las armas: sin esto el arma nueva continuaba el estado
        // (por ejemplo una recarga) del arma anterior.
        ResetBaseLayerToIdle();

        if (weaponAnimator != null)
            weaponAnimator.SetBool("Holstered", false);

        if (weaponAnimator != null && IsNewIntegratedWeapon(currentIndex))
        {
            weaponAnimator.SetLayerWeight(holsterLayerIndex, 1f);
            weaponAnimator.Play("Holster.Unholster", holsterLayerIndex, 0f);
        }
        unholsterPhase = true;

        // Un cuadro después: cambiar el controller puede reconstruir el Animator y perder el reset anterior.
        yield return null;
        ResetBaseLayerToIdle();

        // NotifyUnholsterFinished() (llamado por el Animation Event al
        // terminar el Unholster) reactiva el disparo y baja isSwitching.
        // Algunos FBX importados (AR_02/AR_03) no contienen ese evento.
        // Solo para las nuevas armas, cerramos por estado de Animator o por timeout.
        if (requiresSafeCompletion)
        {
            float startUnholster = Time.unscaledTime;
            int expectedIndex = currentIndex;
            while (isSwitching && unholsterPhase && currentIndex == expectedIndex)
            {
                if (weaponAnimator != null && holsterLayerIndex >= 0 &&
                    !weaponAnimator.IsInTransition(holsterLayerIndex))
                {
                    AnimatorStateInfo state = weaponAnimator.GetCurrentAnimatorStateInfo(holsterLayerIndex);
                    if (IsNewIntegratedWeapon(currentIndex) ? state.shortNameHash == Animator.StringToHash("Ready") : state.IsName("Unholster") && state.normalizedTime >= 0.98f)
                    {
                        NotifyUnholsterFinished();
                        break;
                    }
                }
                if (Time.unscaledTime - startUnholster > 2.5f / PerkSwitchMultiplier)
                {
                    Debug.LogWarning("[WeaponSwitcher] Unholster sin Animation Event: desbloqueo seguro del arma integrada " +
                                     slots[currentIndex].weaponId);
                    NotifyUnholsterFinished();
                    break;
                }
                yield return null;
            }
        }
    }

    private bool IsNewIntegratedWeapon(int index)
    {
        if (slots == null || index < 0 || index >= slots.Length || slots[index] == null)
            return false;
        string id = slots[index].weaponId;
        return id == "AutoPistol" || id == "SemiRifle" || id == "BurstRifle" ||
               id == "P90" || id == "Vector" || id == "MAC10" ||
               id == "Handgun01" || id == "Handgun02" || id == "SMG02" || id == "SMG05" ||
               id == "Sniper01" || id == "Sniper02" || id == "Sniper03";
    }

    private void ApplyControllerAndVisuals(WeaponSlot slot)
    {
        if (slot == null)
            return;

        if (weaponAnimator != null)
        {
            weaponAnimator.runtimeAnimatorController =
                slot.overrideController != null
                    ? slot.overrideController
                    : baseController;
            holsterLayerIndex = weaponAnimator.GetLayerIndex("Holster");
            var profile=slot.weaponComponent?slot.weaponComponent.GetComponent<PackWeaponPresentation>():null;
            if(profile&&!profile.preserveLegacyAnimation){weaponAnimator.SetFloat("Aiming",0);weaponAnimator.SetBool("IsAiming",false);ResetBaseLayerToIdle();}
        }

        if (thirdPersonAnimator != null)
        {
            thirdPersonAnimator.runtimeAnimatorController =
                slot.tpOverrideController != null
                    ? slot.tpOverrideController
                    : tpBaseController;
        }

        if (slot.weaponRoot != null)
            slot.weaponRoot.SetActive(true);

        if (hudController != null)
        {
            hudController.SetWeapon(
                slot.weaponComponent,
                GetIconSprite(slot.weaponIcon)
            );
        }

        if (weaponAnimationEvents != null)
        {
            weaponAnimationEvents.SetWeapon(slot.weaponComponent);

            weaponAnimationEvents.SetCasingData(
                slot.casingPrefab,
                slot.casingEjectPoint
            );
        }

        if (weaponSway != null)
            weaponSway.SetWeapon(slot.weaponComponent);

        InventoryChanged?.Invoke();
    }

    private IEnumerator CaptureBaseIdleState()
    {
        for (int i = 0; i < 10 && weaponAnimator != null; i++)
        {
            yield return null;

            int count = weaponAnimator.layerCount;
            bool estable = true;

            for (int l = 0; l < count; l++)
            {
                if (weaponAnimator.IsInTransition(l))
                    estable = false;
            }

            if (!estable)
                continue;

            int[] hashes = new int[count];

            for (int l = 0; l < count; l++)
            {
                hashes[l] =
                    weaponAnimator.GetCurrentAnimatorStateInfo(l)
                        .fullPathHash;
            }

            if (!string.IsNullOrEmpty(baseIdleStateName))
            {
                hashes[0] =
                    Animator.StringToHash(baseIdleStateName);
            }

            if (hashes[0] == 0)
                continue;

            layerIdleHashes = hashes;
            yield break;
        }

        Debug.LogWarning(
            "[WeaponSwitcher] No se pudo detectar el estado de reposo " +
            "del Animator. Completá 'Base Idle State Name'."
        );
    }

    public void ResetBaseLayerToIdle()
    {
        if (weaponAnimator == null) return;
        var presentation = CurrentWeapon ? CurrentWeapon.GetComponent<PackWeaponPresentation>() : null;
        // Hashes belong to this controller, not to the pistol used at startup.
        int[] rest = presentation && presentation.firstPersonRestStates.Length == weaponAnimator.layerCount
            ? presentation.firstPersonRestStates : layerIdleHashes;
        if (rest == null) return;

        weaponAnimator.ResetTrigger("Reload");
        weaponAnimator.SetBool("ShellLoading", false);
        weaponAnimator.ResetTrigger("Fire");
        weaponAnimator.ResetTrigger("WeaponPartsFire");
        weaponAnimator.ResetTrigger("WeaponPartsReload");
        weaponAnimator.SetBool("BoltAction", false);

        int count = Mathf.Min(
            rest.Length,
            weaponAnimator.layerCount
        );

        for (int l = 0; l < count; l++)
        {
            if (l == holsterLayerIndex || rest[l] == 0)
                continue;

            if (weaponAnimator.HasState(l, rest[l])) weaponAnimator.Play(rest[l], l, 0f);
        }
    }

    public void NotifyUnholsterFinished()
    {
        if (isSwitching && !unholsterPhase)
            return;

        unholsterPhase = false;
        EndPerkSwitchSpeed();

        WeaponSlot current = slots[currentIndex];

        if (current.weaponComponent != null)
            current.weaponComponent.enabled = true;

        isSwitching = false;
        pendingIndex = -1;
    }

    // =========================================================
    // GUARDADO DE ARMAS
    // =========================================================

    /// <summary>
    /// Obtiene los datos de las armas que están actualmente en los
    /// dos casilleros del inventario.
    /// </summary>
    public List<ArmaSaveData> ObtenerDatosArmas()
    {
        List<ArmaSaveData> datos = new List<ArmaSaveData>();

        if (inventory == null || slots == null)
            return datos;

        for (int casillero = 0; casillero < inventory.Length; casillero++)
        {
            int index = inventory[casillero];

            if (index < 0 || index >= slots.Length)
                continue;

            WeaponSlot slot = slots[index];

            if (slot == null || slot.weaponComponent == null)
                continue;

            ArmaSaveData arma = new ArmaSaveData
            {
                identificador = slot.weaponId,
                casillero = casillero,
                municionCargador = slot.weaponComponent.CurrentAmmo,
                municionReserva = slot.weaponComponent.ReserveAmmo
            };

            WeaponAttachments accesorios = GetAttachments(index);

            if (accesorios != null)
            {
                arma.mira = accesorios.ScopeIndex;
                arma.cargador = accesorios.MagazineIndex;
                arma.laser = accesorios.LaserIndex;
                arma.empunadura = accesorios.GripIndex;
                arma.boca = accesorios.MuzzleIndex;
            }

            datos.Add(arma);
        }

        return datos;
    }

    /// <summary>
    /// Devuelve el identificador del arma que está equipada actualmente.
    /// </summary>
    public string ObtenerArmaEquipadaId()
    {
        if (slots == null ||
            currentIndex < 0 ||
            currentIndex >= slots.Length ||
            slots[currentIndex] == null)
        {
            return "";
        }

        return slots[currentIndex].weaponId;
    }

    /// <summary>
    /// Restaura las armas, sus casilleros, munición y accesorios.
    /// Debe llamarse después de que Start haya terminado.
    /// </summary>
    public bool RestaurarArmas(
        List<ArmaSaveData> datos,
        string armaEquipada
    )
    {
        if (!IsInitialized)
        {
            Debug.LogWarning(
                "[WeaponSwitcher] Todavía no terminó la inicialización " +
                "del inventario."
            );

            return false;
        }

        if (datos == null || slots == null || inventory == null)
        {
            Debug.LogError(
                "[WeaponSwitcher] No se pudieron restaurar las armas: " +
                "faltan datos o referencias."
            );

            return false;
        }

        if (isSwitching)
        {
            Debug.LogWarning(
                "[WeaponSwitcher] No se pueden restaurar las armas " +
                "durante un cambio de arma."
            );

            return false;
        }

        // Si el guardado no contiene armas, conservar el inventario inicial.
        if (datos.Count == 0)
        {
            Debug.LogWarning(
                "[WeaponSwitcher] El guardado no contiene armas. " +
                "Se conserva el inventario inicial."
            );

            return false;
        }

        // Cancelar compras pendientes para evitar que alteren el inventario
        // después de la restauración.
        queuedUnlocks.Clear();
        queuedEquip.Clear();
        queueRunning = false;

        // Vaciar los dos casilleros.
        for (int i = 0; i < inventory.Length; i++)
            inventory[i] = -1;

        // Restaurar cada arma en su casillero correspondiente.
        foreach (ArmaSaveData dato in datos)
        {
            if (dato == null)
                continue;

            if (dato.casillero < 0 ||
                dato.casillero >= inventory.Length)
            {
                Debug.LogWarning(
                    "[WeaponSwitcher] Casillero inválido en el guardado."
                );

                continue;
            }

            int index = FindSlotIndex(dato.identificador);

            if (index < 0)
            {
                Debug.LogWarning(
                    "[WeaponSwitcher] No se encontró el arma guardada: " +
                    dato.identificador
                );

                continue;
            }

            if (inventory[dato.casillero] >= 0)
            {
                Debug.LogWarning(
                    "[WeaponSwitcher] El casillero " +
                    dato.casillero + " ya está ocupado."
                );

                continue;
            }

            inventory[dato.casillero] = index;

            WeaponSlot slot = slots[index];

            if (slot.weaponComponent != null)
            {
                slot.weaponComponent.RestaurarMunicion(
                    dato.municionCargador,
                    dato.municionReserva
                );
            }

            WeaponAttachments accesorios = GetAttachments(index);

            if (accesorios != null)
            {
                accesorios.SetScope(dato.mira);
                accesorios.SetMagazine(dato.cargador);
                accesorios.SetLaser(dato.laser);
                accesorios.SetGrip(dato.empunadura);
                accesorios.SetMuzzle(dato.boca);
            }
        }

        // Buscar el arma que estaba equipada al guardar.
        int indexEquipado = FindSlotIndex(armaEquipada);

        if (indexEquipado < 0 || !IsInInventory(indexEquipado))
        {
            indexEquipado =
                inventory[0] >= 0 ? inventory[0] : inventory[1];
        }

        if (indexEquipado < 0)
        {
            Debug.LogError(
                "[WeaponSwitcher] No hay armas válidas para equipar " +
                "después de restaurar el inventario."
            );

            return false;
        }

        // Apagar todos los modelos antes de activar el arma restaurada.
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null)
                continue;

            if (slots[i].weaponRoot != null)
                slots[i].weaponRoot.SetActive(false);

            if (slots[i].weaponComponent != null)
                slots[i].weaponComponent.enabled = false;
        }

        // Equipar directamente, sin animación de cambio.
        EquipWeaponImmediate(indexEquipado);

        if (slots[indexEquipado].weaponComponent != null)
        {
            slots[indexEquipado].weaponComponent.enabled = true;
            slots[indexEquipado].weaponComponent.InputLocked = true;
        }

        // Actualizar el arma inicial obligatoria para el estado abatido.
        armaInicialIndex = indexEquipado;

        pendingIndex = -1;
        isSwitching = false;
        unholsterPhase = false;

        InventoryChanged?.Invoke();

        Debug.Log(
            "[WeaponSwitcher] Inventario restaurado. Primaria: " +
            DescribeSlot(inventory[0]) +
            " | Secundaria: " +
            DescribeSlot(inventory[1]) +
            " | Equipada: " +
            slots[indexEquipado].weaponId
        );

        return true;
    }

    // =========================================================
    // DESBLOQUEO / WALLBUY
    // =========================================================

    public void UnlockWeapon(
        string weaponId,
        bool equipAfterUnlock = true
    )
    {
        int index = FindSlotIndex(weaponId);

        if (index < 0 || !PermitidaEnClase(index))
            return;

        if (IsInInventory(index))
        {
            ReleaseInput(index);

            if (equipAfterUnlock)
                RequestEquip(index);

            return;
        }

        if (isSwitching || queuedUnlocks.Count > 0)
        {
            if (!queuedUnlocks.Contains(index))
            {
                queuedUnlocks.Add(index);
                queuedEquip.Add(equipAfterUnlock);
            }

            StartQueueIfNeeded();
            return;
        }

        PlaceWeapon(index, equipAfterUnlock);
    }

    private void PlaceWeapon(int index, bool equip)
    {
        int target = FirstFreeInventorySlot();

        if (target < 0)
        {
            int equippedSlot = InventorySlotOf(currentIndex);

            if (equippedSlot < 0)
                equippedSlot = 0;

            target = equip ? equippedSlot : 1 - equippedSlot;
        }

        int replaced = inventory[target];
        inventory[target] = index;

        if (debugInventory)
        {
            Debug.Log(
                "[WeaponSwitcher] Compra de '" +
                slots[index].weaponId +
                "' -> casillero " +
                (target == 0 ? "primario" : "secundario") +
                (replaced >= 0
                    ? " (reemplaza a '" + slots[replaced].weaponId + "')"
                    : " (casillero libre)") +
                " | primario=" + DescribeSlot(inventory[0]) +
                ", secundario=" + DescribeSlot(inventory[1])
            );
        }

        ReleaseInput(index);
        InventoryChanged?.Invoke();

        if (equip)
            RequestEquip(index);
    }

    private string DescribeSlot(int index)
    {
        return index >= 0 ? slots[index].weaponId : "vacío";
    }

    private void ReleaseInput(int index)
    {
        if (slots[index].weaponComponent != null)
            slots[index].weaponComponent.InputLocked = false;
    }

    private void StartQueueIfNeeded()
    {
        if (queueRunning || queuedUnlocks.Count == 0)
            return;

        queueRunning = true;
        StartCoroutine(ProcessQueuedUnlocks());
    }

    private IEnumerator ProcessQueuedUnlocks()
    {
        while (queuedUnlocks.Count > 0)
        {
            while (isSwitching)
                yield return null;

            int index = queuedUnlocks[0];
            bool equip = queuedEquip[0];

            queuedUnlocks.RemoveAt(0);
            queuedEquip.RemoveAt(0);

            if (IsInInventory(index))
            {
                ReleaseInput(index);

                if (equip)
                    RequestEquip(index);
            }
            else
            {
                PlaceWeapon(index, equip);
            }
        }

        queueRunning = false;
    }

    public bool IsUnlocked(string weaponId)
    {
        int index = FindSlotIndex(weaponId);
        return index >= 0 && IsUnlocked(index);
    }
}
