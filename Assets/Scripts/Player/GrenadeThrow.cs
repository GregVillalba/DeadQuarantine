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
    [SerializeField] private GameObject grenadeHandModel; // mesh en la mano, solo visible durante la animación
    [SerializeField] private Transform throwPoint;
    [SerializeField] private GameObject grenadePrefab; // debe tener NetworkObject + GrenadeProjectile
    [SerializeField] private float throwForce = 12f;
    [SerializeField] private float throwUpwardArc = 3f;
    [SerializeField] private float throwCooldown = 1.2f;

    private PlayerControls controls;
    private int grenadeLayerIndex = -1;
    private bool isThrowing;

    private void Awake()
    {
        controls = new PlayerControls();

        if (grenadeHandModel != null)
            grenadeHandModel.SetActive(false);

        if (weaponAnimator != null)
            grenadeLayerIndex = weaponAnimator.GetLayerIndex("GranadeKnife");
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
        if (isThrowing || weaponAnimator == null)
            return;

        if (grenadeLayerIndex < 0)
        {
            Debug.LogWarning("[GrenadeThrow] No se encontró la capa 'GranadeKnife' en el Animator.");
            return;
        }

        Weapon currentWeapon = weaponSwitcher != null ? weaponSwitcher.CurrentWeapon : null;

        if (currentWeapon != null && currentWeapon.IsReloading)
        {
            Debug.Log("[GrenadeThrow] Bloqueado: el arma está recargando.");
            return;
        }

        isThrowing = true;

        if (currentWeapon != null && currentWeapon.IsAiming)
            currentWeapon.ForceStopAiming();

        if (grenadeHandModel != null)
            grenadeHandModel.SetActive(true);

        if (currentWeapon != null)
            currentWeapon.InputLocked = true;

        weaponAnimator.CrossFade("Granade", 0.1f, grenadeLayerIndex);

        Invoke(nameof(FinishThrow), throwCooldown);
    }

    // Llamado por Animation Event, en el frame exacto donde se suelta la granada.
    public void OnGrenadeRelease()
    {
        if (grenadeHandModel != null)
            grenadeHandModel.SetActive(false);

        if (playerCamera == null || throwPoint == null)
            return;

        Vector3 direction = playerCamera.transform.forward + Vector3.up * (throwUpwardArc / throwForce);
        Vector3 velocity = direction.normalized * throwForce;

        RequestThrowServerRpc(throwPoint.position, velocity);
    }

    [ServerRpc]
    private void RequestThrowServerRpc(
        Vector3 position,
        Vector3 velocity,
        ServerRpcParams rpcParams = default)
    {
        GameObject instance = Instantiate(grenadePrefab, position, Quaternion.identity);

        instance.SetActive(true);

        instance.GetComponent<NetworkObject>().Spawn(true);
        instance.GetComponent<GrenadeProjectile>().Launch(velocity);
    }

    private void FinishThrow()
    {
        isThrowing = false;

        Weapon currentWeapon = weaponSwitcher != null ? weaponSwitcher.CurrentWeapon : null;

        if (currentWeapon != null)
            currentWeapon.InputLocked = false;
    }
}