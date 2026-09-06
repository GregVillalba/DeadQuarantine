using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class WeaponSwitcher : MonoBehaviour
{
    [System.Serializable]
    public class WeaponSlot
    {
        public string weaponId;
        public GameObject weaponRoot;
        public Weapon weaponComponent;
        public AnimatorOverrideController overrideController;
        public bool unlockedByDefault;
        public Sprite weaponIcon;
    }

    [Header("Referencias")]
    [SerializeField] private Animator weaponAnimator;
    [SerializeField] private RuntimeAnimatorController baseController;
    [SerializeField] private HUDController hudController;
    [SerializeField] private WeaponAnimationEvents weaponAnimationEvents;

    [Header("Armas")]
    [SerializeField] private WeaponSlot[] slots;
    [SerializeField] private int startingSlotIndex = 0;

    private bool[] unlocked;
    private int currentIndex;
    private int pendingIndex = -1;
    private bool isSwitching;
    private int holsterLayerIndex = -1;
    public Weapon CurrentWeapon => slots[currentIndex].weaponComponent;

    private PlayerControls controls;

    private void Awake()
    {
        controls = new PlayerControls();

        unlocked = new bool[slots.Length];
        for (int i = 0; i < slots.Length; i++)
            unlocked[i] = slots[i].unlockedByDefault;

        if (weaponAnimator != null)
            holsterLayerIndex = weaponAnimator.GetLayerIndex("Holster");
    }

    private void OnEnable()
    {
        controls.Player.Enable();
        controls.Player.SwitchWeapon.performed += OnSwitchWeapon;
    }

    private void OnDisable()
    {
        controls.Player.SwitchWeapon.performed -= OnSwitchWeapon;
        controls.Player.Disable();
    }

    private void Start()
    {
        for (int i = 0; i < slots.Length; i++)
            slots[i].weaponRoot.SetActive(false);

        int index = Mathf.Clamp(startingSlotIndex, 0, slots.Length - 1);

        if (!unlocked[index])
        {
            Debug.LogWarning("[WeaponSwitcher] El slot inicial (" + index + ") no está desbloqueado. Revisá 'Unlocked By Default'.");
            index = 0;
        }

        EquipWeaponImmediate(index);

        // Arranca SIEMPRE bloqueado; se libera explícitamente después.
        if (CurrentWeapon != null)
            CurrentWeapon.InputLocked = true;
    }

    private void OnSwitchWeapon(InputAction.CallbackContext context)
    {
        RequestEquip(GetNextUnlockedIndex());
    }

    private int GetNextUnlockedIndex()
    {
        for (int offset = 1; offset <= slots.Length; offset++)
        {
            int candidate = (currentIndex + offset) % slots.Length;
            if (unlocked[candidate])
                return candidate;
        }
        return currentIndex;
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

    public void RequestEquip(int index)
    {
        if (index < 0 || index >= slots.Length || !unlocked[index])
            return;

        if (index == currentIndex || isSwitching)
            return;

        pendingIndex = index;
        StartCoroutine(SwitchWeaponRoutine());
    }

    private IEnumerator SwitchWeaponRoutine()
    {
        isSwitching = true;

        WeaponSlot current = slots[currentIndex];

        // Bloquea disparo/recarga sin ocultar el modelo todavía.
        if (current.weaponComponent != null)
            current.weaponComponent.enabled = false;

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

        current.weaponRoot.SetActive(false);

        currentIndex = pendingIndex;
        WeaponSlot next = slots[currentIndex];

        ApplyControllerAndVisuals(next);

        if (weaponAnimator != null)
            weaponAnimator.SetBool("Holstered", false);

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

        slot.weaponRoot.SetActive(true);

        if (hudController != null)
        hudController.SetWeapon(
            slot.weaponComponent,
            slot.weaponIcon
        );

        if (weaponAnimationEvents != null)
            weaponAnimationEvents.SetWeapon(slot.weaponComponent);
    }

    public void NotifyUnholsterFinished()
    {
        WeaponSlot current = slots[currentIndex];

        if (current.weaponComponent != null)
            current.weaponComponent.enabled = true;

        isSwitching = false;
    }

    // =========================================================
    // DESBLOQUEO (WallBuy)
    // =========================================================

    public void UnlockWeapon(string weaponId, bool equipAfterUnlock = true)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].weaponId == weaponId)
            {
                unlocked[i] = true;

                if (slots[i].weaponComponent != null)
                    slots[i].weaponComponent.InputLocked = false;

                if (equipAfterUnlock)
                    RequestEquip(i);
                return;
            }
        }
    }

    public bool IsUnlocked(string weaponId)
    {
        for (int i = 0; i < slots.Length; i++)
            if (slots[i].weaponId == weaponId)
                return unlocked[i];
        return false;
    }
}