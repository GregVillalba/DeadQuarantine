using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class NPCWeaponVendor : MonoBehaviour
{
    [Header("Ofertas")]
    [SerializeField] private List<WeaponOffer> offers = new List<WeaponOffer>();

    [Header("Blindaje (chaleco / casco)")]
    [Tooltip("Costo de reparar una pieza, como fracción de su costo de compra (0.25 = 25%).")]
    [Range(0f, 1f)]
    [SerializeField] private float porcentajeReparacion = 0.25f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip purchaseSuccessSound;
    [SerializeField] private AudioClip purchaseDeniedSound;

    private WeaponSwitcher pendingSwitcher;
    private Weapon pendingWeapon;
    private PlayerScore pendingScore;
    private string pendingWeaponId;

    public event Action<string, bool> OnPurchaseResult;

    public IReadOnlyList<WeaponOffer> Offers => offers;
    public string NombreMercader => gameObject.name;

    public int CostoReparacion(WeaponOffer oferta)
    {
        return Mathf.CeilToInt(oferta.cost * porcentajeReparacion);
    }

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

        if (!oferta.disponible || !DisponibilidadTienda.SeMuestraEnPartidaActual(oferta))
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

        if (oferta.EsCuracion)
        {
            // Sin switcher pendiente: al terminar no se desbloquea ningún arma.
            pendingSwitcher = null;
            pendingWeapon = null;
            pendingScore = playerScore;
            pendingWeaponId = weaponId;

            playerScore.OnPurchaseResult += OnPurchaseResultInterno;
            playerScore.ComprarCuracionServerRpc(weaponId, oferta.cost, oferta.puntosDeSalud);
            return;
        }

        if (oferta.esEspacioInventario)
        {
            // El servidor valida que ese slot (curas o granadas) no haya llegado al máximo de Slot extra.
            pendingSwitcher = null;
            pendingWeapon = null;
            pendingScore = playerScore;
            pendingWeaponId = weaponId;

            playerScore.OnPurchaseResult += OnPurchaseResultInterno;
            playerScore.ComprarEspacioExtraServerRpc(weaponId, oferta.cost, oferta.slotExtra);
            return;
        }

        if (oferta.EsBlindaje)
        {
            // El servidor decide si es compra o reparación según el estado de la pieza.
            pendingSwitcher = null;
            pendingWeapon = null;
            pendingScore = playerScore;
            pendingWeaponId = weaponId;

            playerScore.OnPurchaseResult += OnPurchaseResultInterno;
            playerScore.ComprarBlindajeServerRpc(weaponId, oferta.piezaBlindaje, oferta.cost, CostoReparacion(oferta), oferta.armadura);
            return;
        }

        if (oferta.esMunicion)
        {
            // Restaura el arma que se tiene en la mano. Si ya está llena no se cobra.
            Weapon arma = switcher.CurrentWeapon;

            if (arma == null || arma.MunicionCompleta)
            {
                PlaySound(purchaseDeniedSound);
                OnPurchaseResult?.Invoke(weaponId, false);
                return;
            }

            pendingSwitcher = null;
            pendingWeapon = arma;
            pendingScore = playerScore;
            pendingWeaponId = weaponId;

            playerScore.OnPurchaseResult += OnPurchaseResultInterno;
            playerScore.ComprarArmaServerRpc(weaponId, oferta.cost);
            return;
        }

        GrenadeThrow grenadeThrow = camara.transform.root.GetComponentInChildren<GrenadeThrow>();

        if (grenadeThrow != null && grenadeThrow.EsTipoDeGranada(weaponId))
        {
            // Las granadas van al slot G: sin lugar para ese tipo no se cobra.
            PlayerHealth salud = camara.transform.root.GetComponentInChildren<PlayerHealth>();

            if (salud == null || !salud.PuedeGuardarGranada(weaponId))
            {
                PlaySound(purchaseDeniedSound);
                OnPurchaseResult?.Invoke(weaponId, false);
                return;
            }

            // El servidor guarda la granada en el slot: acá no queda nada pendiente por aplicar.
            pendingSwitcher = null;
            pendingWeapon = null;
            pendingScore = playerScore;
            pendingWeaponId = weaponId;

            playerScore.OnPurchaseResult += OnPurchaseResultInterno;
            playerScore.ComprarGranadaServerRpc(weaponId, oferta.cost);
            return;
        }

        if (switcher.IsUnlocked(weaponId))
        {
            OnPurchaseResult?.Invoke(weaponId, true);
            return;
        }

        pendingSwitcher = switcher;
        pendingWeapon = null;
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

        if (exito && pendingWeapon != null)
            pendingWeapon.RestaurarMunicion();

        pendingSwitcher = null;
        pendingWeapon = null;
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