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
    private bool hitDelivered,swooshPlayed;
    private Weapon attackWeapon;
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
        CancelInvoke(nameof(FinishAttack));
        FinishAttack();
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

        if (Salud == null || !Salud.IsOwner || !Salud.IsAlive) return;

        // Abatido solo puede disparar y recargar: no puede usar el cuchillo.
        if (Salud != null && Salud.IsDowned)
            return;

        if (isAttacking || weaponAnimator == null)
            return;

        // Each equipped controller has its own layer indices, just like the existing grenade action.
        meleeLayerIndex = weaponAnimator.GetLayerIndex("GranadeKnife");
        if (meleeLayerIndex < 0)
        {
            Debug.LogWarning("[MeleeAttack] No se encontró la capa 'GranadeKnife' en el Animator.");
            return;
        }

        Weapon currentWeapon = weaponSwitcher != null ? weaponSwitcher.CurrentWeapon : null;

        // Acuchillar cancela la recarga en curso.
        if (currentWeapon != null && currentWeapon.IsReloading)
            currentWeapon.CancelReload();

        DeadQuarantine.PvPvE.Networking.PvPvETeamDeployment.Active?.OffensiveActionServerRpc();
        isAttacking = true;hitDelivered=false;swooshPlayed=false;attackWeapon=currentWeapon;

        // Si estaba apuntando, forzar que deje de apuntar antes del golpe.
        if (currentWeapon != null && currentWeapon.IsAiming)
            currentWeapon.ForceStopAiming();

        if (knifeObject != null)
            knifeObject.SetActive(true);

        if (currentWeapon != null)
            currentWeapon.InputLocked = true;

        weaponAnimator.SetLayerWeight(meleeLayerIndex, 1f);
        weaponAnimator.CrossFade("Knife_Attack", 0.1f, meleeLayerIndex, 0f);
        int right=weaponAnimator.GetLayerIndex("GranadeKnifeRight");
        if(right>=0&&weaponAnimator.HasState(right,Animator.StringToHash("Knife_Attack"))){weaponAnimator.SetLayerWeight(right,1);weaponAnimator.CrossFade("Knife_Attack",.1f,right,0f);}

        Invoke(nameof(FinishAttack), attackCooldown);
    }

    private void FinishAttack()
    {
        isAttacking = false;

        if (knifeObject != null)
            knifeObject.SetActive(false); // se esconde de nuevo al terminar

        if (attackWeapon != null) attackWeapon.InputLocked = false;
        attackWeapon=null;
    }

    public void OnMeleeSwoosh()
    {
        if(!isAttacking||swooshPlayed)return;swooshPlayed=true;
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
    public float PvPvERange=>meleeRange; public int PvPvEDamage=>meleeDamage; public float PvPvECooldown=>attackCooldown;
    public void OnMeleeHit()
    {
        var view=attackWeapon&&attackWeapon.AimCamera?attackWeapon.AimCamera:playerCamera;
        if(!isAttacking||hitDelivered||!Salud||!Salud.IsOwner||!Salud.IsAlive||!view)return;
        hitDelivered=true;
        var combat=DeadQuarantine.PvPvE.Networking.PvPvECombat.Active;
        if(combat){combat.MeleeServerRpc(Salud.NetworkObjectId,view.transform.position,view.transform.forward);return;}
        if (playerCamera == null)
            return;

        Ray ray = new Ray(view.transform.position, view.transform.forward);

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