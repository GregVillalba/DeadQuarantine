using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

public class GrenadeThrow : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Animator weaponAnimator;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private WeaponSwitcher weaponSwitcher;

    [Header("Granada")]
    [SerializeField] private GameObject grenadeHandModel;
    [SerializeField] private Transform throwPoint;
    [SerializeField] private GameObject grenadePrefab;
    [SerializeField] private float throwForce = 12f;
    [SerializeField] private float throwUpwardArc = 3f;
    [SerializeField] private float throwCooldown = 1.2f;

    [System.Serializable]
    public class TipoGranada
    {
        [Tooltip("Tiene que coincidir con el weaponId de la oferta del mercader.")]
        public string id;
        [Tooltip("Opcional. Si queda vacío se usa Grenade Prefab. Tiene que estar registrado como Network Prefab.")]
        public GameObject prefab;
    }

    [Header("Tipos de granada (el jugador empieza sin ninguna: se compran en el mercader)")]
    [SerializeField] private TipoGranada[] tipos = new TipoGranada[0];

    private PlayerControls controls;
    private PlayerHealth playerHealth;
    private int grenadeLeftLayerIndex = -1;
    private int grenadeRightLayerIndex = -1;
    private bool isThrowing;

    // Las granadas (tipo y cantidad) viven en el slot G de PlayerHealth, que es el que se sincroniza por red.
    private PlayerHealth Salud
    {
        get
        {
            if (playerHealth == null)
                playerHealth = transform.root.GetComponentInChildren<PlayerHealth>(true);

            return playerHealth;
        }
    }

    /// <summary>true si el jugador tiene al menos una granada en el slot G.</summary>
    public bool TieneGranadas => Salud != null && !Salud.SlotGranada.Value.Vacio;

    public bool EsTipoDeGranada(string id) => IndiceDeTipo(id) >= 0;

    /// <summary>Tecla para tirar la granada (respeta la reasignación de teclas). Lo muestra el HUD.</summary>
    public string TeclaGranada => controls != null ? controls.Player.Grenade.GetBindingDisplayString() : string.Empty;

    private int IndiceDeTipo(string id)
    {
        if (string.IsNullOrEmpty(id))
            return -1;

        for (int i = 0; i < tipos.Length; i++)
            if (tipos[i] != null && tipos[i].id == id)
                return i;

        return -1;
    }

    private GameObject PrefabDeTipo(int indice)
    {
        if (indice >= 0 && indice < tipos.Length && tipos[indice] != null && tipos[indice].prefab != null)
            return tipos[indice].prefab;

        return grenadePrefab;
    }

    private void Awake()
    {
        controls = new PlayerControls();
        ConfiguracionesJuego.CargarRebinds(controls.asset);

        if (grenadeHandModel != null)
            grenadeHandModel.SetActive(false);

        if (weaponAnimator != null)
        {
            grenadeLeftLayerIndex = weaponAnimator.GetLayerIndex("GranadeKnife");
            grenadeRightLayerIndex = weaponAnimator.GetLayerIndex("GranadeKnifeRight");
        }
    }

    private void OnEnable()
    {
        controls.Player.Enable();
        controls.Player.Grenade.performed += OnGrenadeInput;
    }

    private void OnDisable()
    {
        controls.Player.Grenade.performed -= OnGrenadeInput;
        controls.Player.Disable();
    }

    private void OnGrenadeInput(InputAction.CallbackContext context)
    {
        if (PlayerInputGate.ActionsBlocked)
            return;

        if (PauseController.LocalPlayerPaused)
            return;

        if (isThrowing || weaponAnimator == null)
            return;

        // Solo el dueño del jugador tira granadas, y solo si tiene alguna en el slot G.
        if (Salud == null || !Salud.IsOwner)
            return;

        if (!TieneGranadas)
            return;

        if (grenadeLeftLayerIndex < 0 && grenadeRightLayerIndex < 0)
        {
            Debug.LogWarning("[GrenadeThrow] No se encontraron las capas GranadeKnife / GranadeKnifeRight.");
            return;
        }

        Weapon currentWeapon = weaponSwitcher != null ? weaponSwitcher.CurrentWeapon : null;

        if (currentWeapon != null && currentWeapon.IsReloading)
            return;

        isThrowing = true;

        if (currentWeapon != null && currentWeapon.IsAiming)
            currentWeapon.ForceStopAiming();

        if (grenadeHandModel != null)
            grenadeHandModel.SetActive(true);

        if (currentWeapon != null)
            currentWeapon.InputLocked = true;

        // Brazo izquierdo: Override.
        if (grenadeLeftLayerIndex >= 0)
            weaponAnimator.CrossFade("Granade", 0.1f, grenadeLeftLayerIndex, 0f);

        // Brazo derecho: Additive.
        if (grenadeRightLayerIndex >= 0)
            weaponAnimator.CrossFade("Granade", 0.1f, grenadeRightLayerIndex, 0f);

        Invoke(nameof(FinishThrow), throwCooldown);
    }

    // Animation Event en el frame exacto de soltar la granada.
    public void OnGrenadeRelease()
    {
        if (grenadeHandModel != null)
            grenadeHandModel.SetActive(false);

        if (playerCamera == null || throwPoint == null || Salud == null)
            return;

        Vector3 direction = playerCamera.transform.forward +
                            Vector3.up * (throwUpwardArc / Mathf.Max(0.01f, throwForce));
        Vector3 velocity = direction.normalized * throwForce;

        // El servidor descuenta la granada del slot G y crea el proyectil.
        Salud.SolicitarLanzamientoGranadaServerRpc(throwPoint.position, velocity);
    }

    /// <summary>
    /// Solo servidor (lo llama PlayerHealth después de validar que hay granada en el slot).
    /// Crea y lanza el proyectil del tipo indicado. false si no se pudo crear.
    /// </summary>
    public bool CrearProyectilServidor(string tipoId, Vector3 position, Vector3 velocity)
    {
        GameObject prefab = PrefabDeTipo(IndiceDeTipo(tipoId));

        if (prefab == null)
        {
            Debug.LogError("[GrenadeThrow] No hay prefab para la granada '" + tipoId + "' ni Grenade Prefab asignado.");
            return false;
        }

        GameObject instance = Instantiate(prefab, position, Quaternion.identity);
        instance.SetActive(true);

        NetworkObject networkObject = instance.GetComponent<NetworkObject>();
        GrenadeProjectile projectile = instance.GetComponent<GrenadeProjectile>();

        if (networkObject == null || projectile == null)
        {
            Debug.LogError("[GrenadeThrow] El prefab de la granada debe tener NetworkObject y GrenadeProjectile en el root.");
            Destroy(instance);
            return false;
        }

        networkObject.Spawn(true);
        projectile.Launch(velocity);
        return true;
    }

    private void FinishThrow()
    {
        isThrowing = false;

        Weapon currentWeapon = weaponSwitcher != null ? weaponSwitcher.CurrentWeapon : null;

        if (currentWeapon != null)
            currentWeapon.InputLocked = false;
    }
}