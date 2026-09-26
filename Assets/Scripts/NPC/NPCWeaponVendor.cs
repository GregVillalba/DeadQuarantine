using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class NPCWeaponVendor : MonoBehaviour
{
    [Header("Ofertas")]
    [SerializeField] private List<WeaponOffer> offers = new List<WeaponOffer>();

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip purchaseSuccessSound;
    [SerializeField] private AudioClip purchaseDeniedSound;

    private WeaponSwitcher pendingSwitcher;
    private PlayerScore pendingScore;
    private string pendingWeaponId;

    public event Action<string, bool> OnPurchaseResult;

    public IReadOnlyList<WeaponOffer> Offers => offers;
    public string NombreMercader => gameObject.name;

    private void Awake()
    {
        if (audioSource != null)
            audioSource.ignoreListenerPause = true;
    }

    public void RequestPurchase(string weaponId)
    {
        WeaponOffer oferta = offers.Find(o => o.weaponId == weaponId);
        if (oferta == null)
        {
            Debug.LogWarning("[NPCWeaponVendor] No existe oferta con weaponId '" + weaponId + "' en " + gameObject.name + ".");
            return;
        }

        if (!oferta.disponible)
            return;

        Camera camara = BuscarCamaraJugadorLocal();
        if (camara == null)
        {
            Debug.LogWarning("[NPCWeaponVendor] No se encontró la cámara del jugador local.");
            return;
        }

        WeaponSwitcher switcher = camara.transform.root.GetComponentInChildren<WeaponSwitcher>();
        PlayerScore playerScore = camara.transform.root.GetComponentInChildren<PlayerScore>();

        if (switcher == null || playerScore == null)
        {
            Debug.LogWarning("[NPCWeaponVendor] No se encontraron WeaponSwitcher/PlayerScore en el jugador.");
            return;
        }

        if (switcher.IsUnlocked(weaponId))
        {
            OnPurchaseResult?.Invoke(weaponId, true);
            return;
        }

        pendingSwitcher = switcher;
        pendingScore = playerScore;
        pendingWeaponId = weaponId;

        playerScore.OnPurchaseResult += OnPurchaseResultInterno;
        playerScore.ComprarArmaServerRpc(weaponId, oferta.cost);
    }

    private void OnPurchaseResultInterno(string weaponId, bool exito)
    {
        if (weaponId != pendingWeaponId)
            return;

        if (pendingScore != null)
            pendingScore.OnPurchaseResult -= OnPurchaseResultInterno;

        PlaySound(exito ? purchaseSuccessSound : purchaseDeniedSound);

        if (exito && pendingSwitcher != null)
            pendingSwitcher.UnlockWeapon(weaponId, equipAfterUnlock: true);

        pendingSwitcher = null;
        pendingScore = null;
        pendingWeaponId = null;

        OnPurchaseResult?.Invoke(weaponId, exito);
    }

    private Camera BuscarCamaraJugadorLocal()
    {
        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);

        foreach (Camera cam in cameras)
        {
            if (!cam.isActiveAndEnabled)
                continue;

            NetworkObject networkObject = cam.GetComponentInParent<NetworkObject>();

            if (networkObject != null && networkObject.IsSpawned && networkObject.IsOwner)
                return cam;

            if (networkObject == null && cam == Camera.main)
                return cam;
        }

        return null;
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }
}