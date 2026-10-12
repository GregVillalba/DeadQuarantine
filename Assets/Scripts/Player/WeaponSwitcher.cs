using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>Casillero del HUD de armas al que pertenece cada arma.</summary>
public enum WeaponSlotCategory
{
    Primary = 0,    // tecla 1
    Secondary = 1   // tecla 2
}

public class WeaponSwitcher : MonoBehaviour
{
    [System.Serializable]
    public class WeaponSlot
    {
        public string weaponId;
        public GameObject weaponRoot;
        public Weapon weaponComponent;
        public AnimatorOverrideController overrideController;
        public AnimatorOverrideController tpOverrideController; // <- NUEVO: override del Animator de tercera persona
        public bool unlockedByDefault;
        [Tooltip("Objeto de UI del icono (prefab con RectTransform): contiene el arma y, si existen, sus accesorios. " +
                 "El hijo llamado 'Weapon' es el arma principal; el resto se muestra tal cual.")]
        public GameObject weaponIcon;

        [Header("Inventario (HUD)")]
        [Tooltip("YA NO SE USA. El inventario tiene 2 casilleros y se arma solo: el arma inicial ocupa el primario (tecla 1), " +
                 "la primera arma que comprás ocupa el secundario (tecla 2), y con ambos llenos la compra reemplaza el arma " +
                 "del casillero que tenés en la mano.")]
        public WeaponSlotCategory category;

        [Header("Casquillo")] // <- NUEVO
        public GameObject casingPrefab; // <- NUEVO
        public Transform casingEjectPoint; // <- NUEVO
    }

    [Header("Referencias")]
    [SerializeField] private Animator weaponAnimator;
    [SerializeField] private RuntimeAnimatorController baseController;
    [SerializeField] private Animator thirdPersonAnimator; // <- NUEVO
    [SerializeField] private RuntimeAnimatorController tpBaseController; // <- NUEVO: controller de la pistola (base)
    [SerializeField] private HUDController hudController;
    [SerializeField] private WeaponAnimationEvents weaponAnimationEvents;
    [SerializeField] private WeaponSway weaponSway; // <- NUEVO: para que el sway sepa si el arma actual está apuntando

    [Header("Animator")]
    [Tooltip("Opcional. Nombre del estado de reposo (Idle) de la capa base del Animator. Si se deja vacío se detecta solo " +
             "al empezar (en todas las capas). Se usa para cortar la animación de recarga al cambiar de arma o al usar cuchillo/granada.")]
    [SerializeField] private string baseIdleStateName = "";

    [Header("Armas")]
    [SerializeField] private WeaponSlot[] slots;
    [SerializeField] private int startingSlotIndex = 0;

    [Header("Diagnóstico")]
    [Tooltip("Escribe en la consola cómo queda el inventario cada vez que se compra o se reemplaza un arma.")]
    [SerializeField] private bool debugInventory = false;

    // Inventario: dos casilleros (0 = primario / tecla 1, 1 = secundario / tecla 2). Cada uno guarda el índice del arma
    // (su posición en 'slots') o -1 si está vacío. Un arma "desbloqueada" es la que está en alguno de los dos casilleros.
    private int[] inventory;

    // Compras que llegaron mientras se cambiaba de arma: esperan a que termine el cambio para no pisarlo.
    private readonly List<int> queuedUnlocks = new List<int>();
    private readonly List<bool> queuedEquip = new List<bool>();
    private bool queueRunning;

    private int currentIndex;
    private int pendingIndex = -1;
    private bool isSwitching;
    private int[] layerIdleHashes;   // estado de reposo (por defecto) de cada capa del Animator
    private bool unholsterPhase;   // true cuando ya se guardó el arma anterior y se está sacando la nueva
    private int holsterLayerIndex = -1;

    // Estado abatido: el jugador queda obligado a usar el arma inicial y al levantarse recupera la que tenía.
    private PlayerHealth saludJugador;
    private int armaInicialIndex = -1;
    private int armaAntesDeAbatido = -1;
    private bool abatido;
    private Coroutine rutinaAbatido;
    public Weapon CurrentWeapon => slots[currentIndex].weaponComponent;
    public int CurrentWeaponIndex => currentIndex;

    /// <summary>
    /// true mientras se guarda el arma actual y se saca la nueva (de que empieza la animación de guardar
    /// hasta que termina la de sacar). Durante ese tiempo no se puede disparar, recargar, apuntar,
    /// acuchillar ni tirar granadas.
    /// </summary>
    public bool IsSwitching => isSwitching;

    /// <summary>
    /// Se dispara cuando cambia el arma elegida, se equipa una o se desbloquea una.
    /// Lo escucha el HUD de inventario (WeaponInventoryHUD).
    /// </summary>
    public event System.Action InventoryChanged;

    /// <summary>Se dispara cuando se vuelven a leer las teclas (cambio de rebind). El HUD actualiza sus textos.</summary>
    public event System.Action BindingsChanged;

    private PlayerControls controls;

    // Acciones de PlayerControls.inputactions (mapa Player). Se buscan por nombre: si falta alguna, avisa en consola.
    private InputAction selectPrimaryAction;
    private InputAction selectSecondaryAction;
    private InputAction nextWeaponAction;
    private InputAction previousWeaponAction;

    private readonly List<int> cycleBuffer = new List<int>();

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

        // ConfiguracionesJuego ya recarga las teclas de este asset; solo falta avisar al HUD.
        ConfiguracionesJuego.CambiosAplicados += OnCambiosAplicados;
    }

    private void OnCambiosAplicados()
    {
        BindingsChanged?.Invoke();
    }

    private void OnEnable()
    {
        controls.Player.Enable();

        if (selectPrimaryAction != null) selectPrimaryAction.performed += OnSelectPrimary;
        if (selectSecondaryAction != null) selectSecondaryAction.performed += OnSelectSecondary;
        if (nextWeaponAction != null) nextWeaponAction.performed += OnNextWeapon;
        if (previousWeaponAction != null) previousWeaponAction.performed += OnPreviousWeapon;
    }

    private void OnDisable()
    {
        if (selectPrimaryAction != null) selectPrimaryAction.performed -= OnSelectPrimary;
        if (selectSecondaryAction != null) selectSecondaryAction.performed -= OnSelectSecondary;
        if (nextWeaponAction != null) nextWeaponAction.performed -= OnNextWeapon;
        if (previousWeaponAction != null) previousWeaponAction.performed -= OnPreviousWeapon;

        queueRunning = false;

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
        InputAction action = controls.asset.FindAction("Player/" + actionName, false);

        if (action == null)
            Debug.LogWarning("[WeaponSwitcher] Falta la acción '" + actionName + "' en PlayerControls.inputactions (mapa Player).");

        return action;
    }

    private void Start()
    {
        for (int i = 0; i < slots.Length; i++)
            slots[i].weaponRoot.SetActive(false);

        int index = Mathf.Clamp(startingSlotIndex, 0, slots.Length - 1);

        if (!IsInInventory(index))
        {
            Debug.LogWarning("[WeaponSwitcher] El slot inicial (" + index + ") no está desbloqueado. Revisá 'Unlocked By Default'.");
            index = inventory[0] >= 0 ? inventory[0] : 0;
        }

        EquipWeaponImmediate(index);

        StartCoroutine(CaptureBaseIdleState());

        // Esta es el "arma inicial" que se usa obligatoriamente al estar abatido.
        armaInicialIndex = index;
        SuscribirEstadoJugador();

        // Arranca SIEMPRE bloqueado; se libera explícitamente después.
        if (CurrentWeapon != null)
            CurrentWeapon.InputLocked = true;
    }

    private void OnSelectPrimary(InputAction.CallbackContext context)
    {
        TrySelect(WeaponSlotCategory.Primary);
    }

    private void OnSelectSecondary(InputAction.CallbackContext context)
    {
        TrySelect(WeaponSlotCategory.Secondary);
    }

    // Rueda hacia abajo = siguiente arma, rueda hacia arriba = anterior.
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

    /// <summary>
    /// Pasa a la siguiente (+1) o anterior (-1) arma del inventario, en el orden del HUD:
    /// primero el casillero primario y después el secundario. Da la vuelta al llegar al final.
    /// </summary>
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

        int next = cycleBuffer[(position + direction + cycleBuffer.Count) % cycleBuffer.Count];

        RequestEquip(next);
    }

    private void TrySelect(WeaponSlotCategory category)
    {
        // Pausa, tienda abierta, etc. (incluye PauseController.LocalPlayerPaused).
        if (PlayerInputGate.ActionsBlocked)
            return;

        SelectCategory(category);
    }

    // =========================================================
    // INVENTARIO: PRIMARIA / SECUNDARIA
    // =========================================================

    public bool IsReady => inventory != null;
    public int SlotCount => slots.Length;

    /// <summary>Slot elegido ahora. Durante el cambio de arma ya es el nuevo, así el HUD responde al instante.</summary>
    public int SelectedIndex => (isSwitching && pendingIndex >= 0) ? pendingIndex : currentIndex;
    public WeaponSlotCategory SelectedCategory => GetCategory(SelectedIndex);

    /// <summary>true si el arma está en alguno de los dos casilleros (o su compra está esperando en la cola).</summary>
    public bool IsUnlocked(int index) => index >= 0 && index < slots.Length && (IsInInventory(index) || queuedUnlocks.Contains(index));
    /// <summary>Objeto de UI del icono del slot (arma + accesorios), o null.</summary>
    public GameObject GetIconObject(int index) => (index >= 0 && index < slots.Length) ? slots[index].weaponIcon : null;

    /// <summary>
    /// Accesorios (mira, cargador, grip...) del arma de ese slot, o null si no tiene el componente WeaponAttachments.
    /// Se busca en el modelo del arma (Weapon Root, en la raíz o en un hijo) y, si no está ahí, en el objeto del Weapon.
    /// </summary>
    public WeaponAttachments GetAttachments(int index)
    {
        if (index < 0 || index >= slots.Length)
            return null;

        WeaponSlot slot = slots[index];
        WeaponAttachments found = null;

        if (slot.weaponRoot != null)
            found = slot.weaponRoot.GetComponentInChildren<WeaponAttachments>(true);

        if (found == null && slot.weaponComponent != null)
            found = slot.weaponComponent.GetComponentInChildren<WeaponAttachments>(true);

        return found;
    }

    /// <summary>Texto de la tecla de selección de esa categoría, leído de PlayerControls (ya con los rebinds aplicados).</summary>
    public string GetKeyLabel(WeaponSlotCategory category)
    {
        InputAction action = category == WeaponSlotCategory.Primary ? selectPrimaryAction : selectSecondaryAction;
        string fallback = category == WeaponSlotCategory.Primary ? "1" : "2";

        if (action == null || action.bindings.Count == 0)
            return fallback;

        string label = action.GetBindingDisplayString(0);

        return string.IsNullOrEmpty(label) ? fallback : label;
    }

    /// <summary>
    /// Vuelve a leer las teclas guardadas (ConfiguracionesJuego.CargarRebinds) y avisa al HUD.
    /// Llamalo cuando tu menú aplique un cambio de teclas. El HUD también lo llama solo cada vez que reaparece
    /// (por ejemplo al cerrar la pausa, que oculta y muestra el HUD).
    /// </summary>
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

    // El HUDController (panel de munición) sigue pidiendo un Sprite: se toma del hijo 'Weapon' del objeto de icono.
    private static Sprite GetIconSprite(GameObject icon)
    {
        if (icon == null)
            return null;

        UnityEngine.UI.Image[] images = icon.GetComponentsInChildren<UnityEngine.UI.Image>(true);

        for (int i = 0; i < images.Length; i++)
        {
            if (string.Equals(images[i].gameObject.name, "Weapon", System.StringComparison.OrdinalIgnoreCase))
                return images[i].sprite;
        }

        return images.Length > 0 ? images[0].sprite : null;
    }

    /// <summary>Casillero del HUD (primario / secundario) donde está el arma. Si no está en el inventario, devuelve Primary.</summary>
    public WeaponSlotCategory GetCategory(int index)
    {
        int slot = InventorySlotOf(index);
        return (WeaponSlotCategory)Mathf.Max(0, slot);
    }

    /// <summary>
    /// Arma que tiene ese casillero ahora mismo. -1 = vacío (el casillero va vacío en el HUD).
    /// </summary>
    public int GetIndexForCategory(WeaponSlotCategory category)
    {
        return inventory[(int)category];
    }

    /// <summary>
    /// Tecla 1 / 2: equipa el arma que tiene ese casillero. Si el casillero está vacío o ya la tenés en la mano, no hace nada.
    /// </summary>
    public void SelectCategory(WeaponSlotCategory category)
    {
        if (isSwitching)
            return;

        int target = inventory[(int)category];

        if (target >= 0 && target != currentIndex)
            RequestEquip(target);
    }

    // Arma inicial -> casillero primario (tecla 1). Si hubiera una segunda "Unlocked By Default", va al secundario.
    private void BuildInitialInventory()
    {
        inventory = new int[] { -1, -1 };

        for (int i = 0; i < slots.Length; i++)
        {
            if (!slots[i].unlockedByDefault)
                continue;

            int free = FirstFreeInventorySlot();

            if (free < 0)
            {
                Debug.LogWarning("[WeaponSwitcher] Hay más de 2 armas con 'Unlocked By Default' y el inventario tiene 2 casilleros. " +
                                 "Se ignora '" + slots[i].weaponId + "'.");
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

    // Casillero (0 o 1) que ocupa el arma, o -1 si no está en el inventario.
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
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].weaponId == weaponId)
                return i;
        }

        return -1;
    }

    // =========================================================
    // ESTADO ABATIDO: ARMA INICIAL OBLIGATORIA
    // =========================================================

    private void SuscribirEstadoJugador()
    {
        saludJugador = transform.root.GetComponentInChildren<PlayerHealth>(true);

        if (saludJugador == null)
            return;

        saludJugador.OnStateChanged += OnEstadoJugadorCambio;

        // Si el jugador ya estaba abatido cuando arranca este componente.
        if (saludJugador.IsOwner && saludJugador.IsDowned)
            EntrarEnAbatido();
    }

    private void OnEstadoJugadorCambio(PlayerHealth.PlayerState anterior, PlayerHealth.PlayerState nuevo)
    {
        // Solo el dueño del jugador cambia su arma.
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

        // Se recuerda el arma que tenía (o la que estaba sacando) para devolvérsela al levantarse.
        armaAntesDeAbatido = SelectedIndex;

        ForzarEquipo(armaInicialIndex, true);
    }

    private void SalirDeAbatido()
    {
        abatido = false;

        int destino = armaAntesDeAbatido;
        armaAntesDeAbatido = -1;

        // Vuelve al arma que tenía antes de caer (si sigue en el inventario).
        if (destino >= 0 && IsInInventory(destino))
            ForzarEquipo(destino, false);
    }

    // liberarInput = true: además se deja el arma lista para disparar y apuntar (al caer abatido).
    private void ForzarEquipo(int index, bool liberarInput)
    {
        if (rutinaAbatido != null)
            StopCoroutine(rutinaAbatido);

        rutinaAbatido = StartCoroutine(ForzarEquipoRoutine(index, liberarInput));
    }

    private IEnumerator ForzarEquipoRoutine(int index, bool liberarInput)
    {
        // Si justo está cambiando de arma, espera a que termine ese cambio.
        while (isSwitching)
            yield return null;

        if (index < 0 || index >= slots.Length)
        {
            rutinaAbatido = null;
            yield break;
        }

        if (index != currentIndex)
            RequestEquip(index, true);

        // Espera a que termine el cambio. Si el Animation Event de fin de Unholster no llega
        // (el arma quedaría desactivada y sin input), se da por terminado a mano pasado un tiempo.
        float limite = Time.time + 3f;

        while (isSwitching && Time.time < limite)
            yield return null;

        if (isSwitching && currentIndex == index)
            NotifyUnholsterFinished();

        Weapon arma = slots[index].weaponComponent;

        if (arma != null && !isSwitching)
        {
            // Una arma que quedó bloqueada (por ejemplo desde otra pantalla o ronda) no deja disparar ni apuntar.
            if (liberarInput)
                arma.InputLocked = false;

            if (!arma.enabled)
                arma.enabled = true;

            if (debugInventory)
            {
                Debug.Log("[WeaponSwitcher] Arma forzada '" + slots[index].weaponId + "' | enabled=" + arma.enabled +
                          " | InputLocked=" + arma.InputLocked);
            }
        }

        rutinaAbatido = null;
    }

    // =========================================================
    // PRIMER EQUIP (sin animación de holster, no hay nada que guardar)
    // =========================================================

    private void EquipWeaponImmediate(int index)
    {
        currentIndex = index;
        WeaponSlot slot = slots[currentIndex];

        ApplyControllerAndVisuals(slot);

        if (weaponAnimator != null)
            weaponAnimator.SetBool("Holstered", false);
    }

    // =========================================================
    // CAMBIO DE ARMA CON ANIMACIÓN
    // =========================================================

    // forzar = true: cambio obligatorio por el estado abatido (puede ir a un arma que ya no está en el inventario).
    public void RequestEquip(int index, bool forzar = false)
    {
        if (index < 0 || index >= slots.Length)
            return;

        // Abatido: el arma inicial es obligatoria, no se puede cambiar de arma.
        if (abatido && !forzar)
            return;

        if (!forzar && !IsInInventory(index))
            return;

        if (index == currentIndex || isSwitching)
            return;

        pendingIndex = index;
        InventoryChanged?.Invoke();   // el HUD marca el casillero nuevo al instante
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

        if (weaponAnimator != null)
            weaponAnimator.SetBool("Holstered", true);

        // Esperar a que termine el clip de Holster antes de tapar el modelo.
        if (weaponAnimator != null && holsterLayerIndex >= 0)
        {
            yield return null; // Deja que el Animator entre en la transición.

            while (true)
            {
                bool inTransition = weaponAnimator.IsInTransition(holsterLayerIndex);
                AnimatorStateInfo info = weaponAnimator.GetCurrentAnimatorStateInfo(holsterLayerIndex);

                if (!inTransition && info.IsName("Holster") && info.normalizedTime >= 1f)
                    break;

                yield return null;
            }
        }

        // Se corta cualquier animación que haya quedado a medias (por ejemplo una recarga) antes de cambiar de arma.
        ResetBaseLayerToIdle();

        current.weaponRoot.SetActive(false);

        currentIndex = pendingIndex;
        WeaponSlot next = slots[currentIndex];

        // El arma nueva se apaga ANTES de activar su modelo: si nunca se había usado, su componente
        // seguía encendido y habría podido disparar/recargar/apuntar durante la animación de sacarla.
        // NotifyUnholsterFinished() la vuelve a encender al terminar el Unholster.
        if (next.weaponComponent != null)
            next.weaponComponent.enabled = false;

        ApplyControllerAndVisuals(next);

        // El Animator es el mismo para todas las armas: sin esto el arma nueva continuaba el estado
        // (por ejemplo una recarga) del arma anterior.
        ResetBaseLayerToIdle();

        if (weaponAnimator != null)
            weaponAnimator.SetBool("Holstered", false);

        unholsterPhase = true;

        // Un cuadro después: cambiar el controller puede reconstruir el Animator y perder el reset anterior.
        yield return null;
        ResetBaseLayerToIdle();

        // NotifyUnholsterFinished() (llamado por el Animation Event al
        // terminar el Unholster) reactiva el disparo y baja isSwitching.
    }

    private void ApplyControllerAndVisuals(WeaponSlot slot)
    {
        if (weaponAnimator != null)
        {
            weaponAnimator.runtimeAnimatorController =
                slot.overrideController != null ? slot.overrideController : baseController;
        }

        // NUEVO: mismo mecanismo, aplicado al Animator de tercera persona.
        if (thirdPersonAnimator != null)
        {
            thirdPersonAnimator.runtimeAnimatorController =
                slot.tpOverrideController != null ? slot.tpOverrideController : tpBaseController;
        }

        slot.weaponRoot.SetActive(true);

        if (hudController != null)
        hudController.SetWeapon(
            slot.weaponComponent,
            GetIconSprite(slot.weaponIcon)
        );

        if (weaponAnimationEvents != null)
        {
            weaponAnimationEvents.SetWeapon(slot.weaponComponent);
            weaponAnimationEvents.SetCasingData(slot.casingPrefab, slot.casingEjectPoint); // <- NUEVO
        }

        if (weaponSway != null) // <- NUEVO
            weaponSway.SetWeapon(slot.weaponComponent);

        InventoryChanged?.Invoke();
    }

    // Guarda el estado de reposo (el que tiene cada capa al empezar) de TODAS las capas del Animator.
    // La animación de recarga puede estar en cualquier capa, así que se resetean todas menos la de Holster.
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
                hashes[l] = weaponAnimator.GetCurrentAnimatorStateInfo(l).fullPathHash;

            if (!string.IsNullOrEmpty(baseIdleStateName))
                hashes[0] = Animator.StringToHash(baseIdleStateName);

            if (hashes[0] == 0)
                continue;

            layerIdleHashes = hashes;
            yield break;
        }

        Debug.LogWarning("[WeaponSwitcher] No se pudo detectar el estado de reposo del Animator. " +
                         "Completá 'Base Idle State Name' para que se corte la animación de recarga al cambiar de arma.");
    }

    /// <summary>
    /// Devuelve todas las capas del Animator (menos Holster) a su estado de reposo y limpia los parámetros de recarga.
    /// Corta de golpe una recarga en curso (el Animator es el mismo para todas las armas).
    /// </summary>
    public void ResetBaseLayerToIdle()
    {
        if (weaponAnimator == null || layerIdleHashes == null)
            return;

        weaponAnimator.ResetTrigger("Reload");
        weaponAnimator.SetBool("ShellLoading", false);

        int count = Mathf.Min(layerIdleHashes.Length, weaponAnimator.layerCount);

        for (int l = 0; l < count; l++)
        {
            // La capa Holster no se toca: de ahí salen las animaciones de guardar y sacar.
            if (l == holsterLayerIndex || layerIdleHashes[l] == 0)
                continue;

            weaponAnimator.Play(layerIdleHashes[l], l, 0f);
        }
    }

    public void NotifyUnholsterFinished()
    {
        // Un evento de animación que llegue mientras todavía se está guardando el arma anterior
        // no debe dar por terminado el cambio (dejaba disparar y recargar a mitad del cambio).
        if (isSwitching && !unholsterPhase)
            return;

        unholsterPhase = false;

        WeaponSlot current = slots[currentIndex];

        if (current.weaponComponent != null)
            current.weaponComponent.enabled = true;

        isSwitching = false;
    }

    // =========================================================
    // DESBLOQUEO (WallBuy)
    // =========================================================

    /// <summary>
    /// Compra / desbloqueo de un arma. Reglas del inventario (2 casilleros):
    ///  - Con un casillero libre (solo tenés el arma inicial) el arma nueva ocupa el SECUNDARIO.
    ///  - Con los dos casilleros ocupados reemplaza el arma del casillero que tenés en la mano en ese momento
    ///    (con equipAfterUnlock = false reemplaza la del otro casillero, para no sacarte el arma de la mano).
    ///  - Si la compra llega mientras se cambia de arma, espera a que termine el cambio.
    /// El arma reemplazada deja de estar "desbloqueada": se puede volver a comprar.
    /// </summary>
    public void UnlockWeapon(string weaponId, bool equipAfterUnlock = true)
    {
        int index = FindSlotIndex(weaponId);

        if (index < 0)
            return;

        // Ya la tenés en el inventario: solo se habilita y, si se pide, se equipa.
        if (IsInInventory(index))
        {
            ReleaseInput(index);

            if (equipAfterUnlock)
                RequestEquip(index);

            return;
        }

        // En medio de un cambio de arma (o con otra compra esperando) esta compra va a la cola.
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

    // Mete el arma en el inventario: en el primer casillero libre o, si están los dos llenos, reemplazando uno.
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
            Debug.Log("[WeaponSwitcher] Compra de '" + slots[index].weaponId + "' -> casillero " + (target == 0 ? "primario" : "secundario") +
                      (replaced >= 0 ? " (reemplaza a '" + slots[replaced].weaponId + "')" : " (casillero libre)") +
                      " | primario=" + DescribeSlot(inventory[0]) + ", secundario=" + DescribeSlot(inventory[1]));
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

    /// <summary>true si el arma está en el inventario o su compra está esperando en la cola.</summary>
    public bool IsUnlocked(string weaponId)
    {
        int index = FindSlotIndex(weaponId);
        return index >= 0 && IsUnlocked(index);
    }
}