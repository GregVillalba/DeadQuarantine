using UnityEngine;
using UnityEngine.InputSystem;

public class MeleeAttack : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Animator weaponAnimator;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private WeaponSwitcher weaponSwitcher;

    [Header("Configuración")]
    [SerializeField] private float meleeRange = 2f;
    [SerializeField] private int meleeDamage = 75;
    [SerializeField] private float attackCooldown = 0.6f; // un poco menos que la duración del clip
    [SerializeField] private LayerMask hittableMask = ~0;

    private PlayerControls controls;
    private bool isAttacking;
    private int meleeLayerIndex = -1;

    private void Awake()
    {
        controls = new PlayerControls();

        if (weaponAnimator != null)
            meleeLayerIndex = weaponAnimator.GetLayerIndex("GranadeKnife");
    }

    private void OnEnable()
    {
        controls.Player.Enable();
        controls.Player.Melee.performed += OnMeleeInput;
    }

    private void OnDisable()
    {
        controls.Player.Melee.performed -= OnMeleeInput;
        controls.Player.Disable();
    }

    private void OnMeleeInput(InputAction.CallbackContext context)
    {
        if (isAttacking || weaponAnimator == null)
            return;

        if (meleeLayerIndex < 0)
        {
            Debug.LogWarning("[MeleeAttack] No se encontró la capa 'Melee' en el Animator. Revisá que el nombre sea EXACTO (mayúsculas, sin espacios de más).");
            return;
        }

        isAttacking = true;

        if (weaponSwitcher != null && weaponSwitcher.CurrentWeapon != null)
            weaponSwitcher.CurrentWeapon.InputLocked = true;

        weaponAnimator.CrossFade("Knife_Attack", 0.1f, meleeLayerIndex);

        Invoke(nameof(FinishAttack), attackCooldown);
    }

    private void FinishAttack()
    {
        isAttacking = false;

        if (weaponSwitcher != null && weaponSwitcher.CurrentWeapon != null)
            weaponSwitcher.CurrentWeapon.InputLocked = false;
    }

    // Llamado por el Animation Event, en el frame exacto donde el cuchillo conecta.
    public void OnMeleeHit()
    {
        if (playerCamera == null)
            return;

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, meleeRange, hittableMask))
        {
            ZombieHealth zombie = hit.collider.GetComponentInParent<ZombieHealth>();

            if (zombie != null)
            {
                zombie.TakeDamage(
                    meleeDamage,
                    hit.point,
                    hit.normal
                );
            }
        }
    }
}