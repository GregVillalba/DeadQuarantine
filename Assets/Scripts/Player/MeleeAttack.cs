using UnityEngine;
using UnityEngine.InputSystem;

public class MeleeAttack : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Animator weaponAnimator;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private WeaponSwitcher weaponSwitcher;
    [SerializeField] private GameObject knifeObject;

    [Header("Configuración")]
    [SerializeField] private float meleeRange = 2f;
    [SerializeField] private int meleeDamage = 40;
    [SerializeField] private float attackCooldown = 0.6f; // un poco menos que la duración del clip
    [SerializeField] private LayerMask hittableMask = ~0;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip[] swingSounds;
    [SerializeField] private AudioClip impactSound;

    private PlayerControls controls;
    private bool isAttacking;
    private int meleeLayerIndex = -1;
    private PlayerHealth saludJugador;

    // Se busca una sola vez en el jugador (PlayerHealth está en el mismo jugador que este script).
    private PlayerHealth Salud
    {
        get
        {
            if (saludJugador == null)
                saludJugador = transform.root.GetComponentInChildren<PlayerHealth>(true);

            return saludJugador;
        }
    }

    private void Awake()
    {
        controls = new PlayerControls();
        ConfiguracionesJuego.CargarRebinds(controls.asset);

        if (knifeObject != null)
            knifeObject.SetActive(false);

        if (weaponAnimator != null)
            meleeLayerIndex = weaponAnimator.GetLayerIndex("GranadeKnife"); // <- antes decía "Melee"
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
        if (PlayerInputGate.ActionsBlocked)
        return;

        if (PauseController.LocalPlayerPaused)
            return;

        // No se puede usar mientras se guarda o se saca un arma.
        if (weaponSwitcher != null && weaponSwitcher.IsSwitching)
            return;

        // Abatido solo puede disparar y recargar: no puede usar el cuchillo.
        if (Salud != null && Salud.IsDowned)
            return;

        if (isAttacking || weaponAnimator == null)
            return;

        if (meleeLayerIndex < 0)
        {
            Debug.LogWarning("[MeleeAttack] No se encontró la capa 'GranadeKnife' en el Animator.");
            return;
        }

        Weapon currentWeapon = weaponSwitcher != null ? weaponSwitcher.CurrentWeapon : null;

        // Acuchillar cancela la recarga en curso.
        if (currentWeapon != null && currentWeapon.IsReloading)
            currentWeapon.CancelReload();

        isAttacking = true;

        // Si estaba apuntando, forzar que deje de apuntar antes del golpe.
        if (currentWeapon != null && currentWeapon.IsAiming)
            currentWeapon.ForceStopAiming();

        if (knifeObject != null)
            knifeObject.SetActive(true);

        if (currentWeapon != null)
            currentWeapon.InputLocked = true;

        weaponAnimator.CrossFade("Knife_Attack", 0.1f, meleeLayerIndex);

        Invoke(nameof(FinishAttack), attackCooldown);
    }

    private void FinishAttack()
    {
        isAttacking = false;

        if (knifeObject != null)
            knifeObject.SetActive(false); // se esconde de nuevo al terminar

        if (weaponSwitcher != null && weaponSwitcher.CurrentWeapon != null)
            weaponSwitcher.CurrentWeapon.InputLocked = false;
    }

    public void OnMeleeSwoosh()
    {
        Debug.Log("[MeleeAttack] OnMeleeSwoosh llamado. AudioSource=" + (audioSource != null) +
            " | Cantidad de clips=" + (swingSounds != null ? swingSounds.Length : 0));

        if (audioSource == null || swingSounds == null || swingSounds.Length == 0)
            return;

        AudioClip clip = swingSounds[Random.Range(0, swingSounds.Length)];

        Debug.Log("[MeleeAttack] Reproduciendo clip: " + (clip != null ? clip.name : "NULL"));

        if (clip != null)
            audioSource.PlayOneShot(clip);
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
                zombie.TakeDamage(meleeDamage, hit.point, hit.normal);

                if (audioSource != null && impactSound != null)
                    audioSource.PlayOneShot(impactSound);

                Debug.Log("[MeleeAttack] Daño aplicado: " + meleeDamage);
            }
        }
    }
}