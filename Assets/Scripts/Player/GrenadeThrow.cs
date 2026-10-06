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

    [Header("Tipos de granada (se cambian comprándolos en el mercader)")]
    [SerializeField] private TipoGranada[] tipos = new TipoGranada[0];
    [Tooltip("Tipo con el que arranca el jugador.")]
    [SerializeField] private string tipoInicialId = "Granadas";

    private PlayerControls controls;
    private int grenadeLeftLayerIndex = -1;
    private int grenadeRightLayerIndex = -1;
    private bool isThrowing;
    private int tipoActual = -1;

    /// <summary>Id del tipo de granada que se tira ahora (null si no hay tipos configurados).</summary>
    public string TipoActualId => tipoActual >= 0 ? tipos[tipoActual].id : null;

    public bool EsTipoDeGranada(string id) => IndiceDeTipo(id) >= 0;

    /// <summary>Todas las granadas que se tiren desde ahora son de este tipo. Sin inventario: reemplaza al anterior.</summary>
    public void EquiparTipo(string id)
    {
        int indice = IndiceDeTipo(id);

        if (indice >= 0)
            tipoActual = indice;
    }

    private int IndiceDeTipo(string id)
    {
        for (int i = 0; i < tipos.Length; i++)
            if (tipos[i] != null && tipos[i].id == id)
                return i;

        return -1;
    }

    private GameObject PrefabDeTipo(int indice)
    {
        if (indice >= 0 && indice < tipos.Length && tipos[indice].prefab != null)
            return tipos[indice].prefab;

        return grenadePrefab;
    }

    private void Awake()
    {
        controls = new PlayerControls();
        tipoActual = IndiceDeTipo(tipoInicialId);

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

        if (playerCamera == null || throwPoint == null)
            return;

        Vector3 direction = playerCamera.transform.forward +
                            Vector3.up * (throwUpwardArc / Mathf.Max(0.01f, throwForce));
        Vector3 velocity = direction.normalized * throwForce;

        RequestThrowServerRpc(throwPoint.position, velocity, tipoActual);
    }

    [ServerRpc]
    private void RequestThrowServerRpc(
        Vector3 position,
        Vector3 velocity,
        int tipo,
        ServerRpcParams rpcParams = default)
    {
        GameObject instance = Instantiate(PrefabDeTipo(tipo), position, Quaternion.identity);
        instance.SetActive(true);

        NetworkObject networkObject = instance.GetComponent<NetworkObject>();
        GrenadeProjectile projectile = instance.GetComponent<GrenadeProjectile>();

        if (networkObject == null || projectile == null)
        {
            Debug.LogError("[GrenadeThrow] grenadePrefab debe tener NetworkObject y GrenadeProjectile en el root.");
            Destroy(instance);
            return;
        }

        networkObject.Spawn(true);
        projectile.Launch(velocity);
    }

    private void FinishThrow()
    {
        isThrowing = false;

        Weapon currentWeapon = weaponSwitcher != null ? weaponSwitcher.CurrentWeapon : null;

        if (currentWeapon != null)
            currentWeapon.InputLocked = false;
    }
}