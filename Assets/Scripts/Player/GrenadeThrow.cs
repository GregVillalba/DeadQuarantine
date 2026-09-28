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

    private PlayerControls controls;
    private int grenadeLeftLayerIndex = -1;
    private int grenadeRightLayerIndex = -1;
    private bool isThrowing;

    private void Awake()
    {
        controls = new PlayerControls();

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
