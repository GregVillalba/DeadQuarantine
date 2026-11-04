using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class NPCWeaponVendor : MonoBehaviour
{
    [Header("Ofertas")]
    [SerializeField] private List<WeaponOffer> offers = new List<WeaponOffer>();

    [Header("Blindaje (chaleco / casco)")]
    [Tooltip("Precio de reparar una pieza, como fracción de su precio en recursos (0.25 = 25%, redondeado para arriba).")]
    [Range(0f, 1f)]
    [SerializeField] private float porcentajeReparacion = 0.25f;

    [Header("Torreta")]
    [Tooltip("Precio de reparar la torreta dañada o rota, como fracción de su precio en recursos (0.5 = la mitad, redondeado para arriba).")]
    [Range(0f, 1f)]
    [SerializeField] private float porcentajeReparacionTorreta = 0.5f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip purchaseSuccessSound;
    [SerializeField] private AudioClip purchaseDeniedSound;

    private WeaponSwitcher pendingSwitcher;
    private Weapon pendingWeapon;
    private PlayerRecursos pendingRecursos;
    private string pendingWeaponId;

    public event Action<string, bool> OnPurchaseResult;

    public IReadOnlyList<WeaponOffer> Offers => offers;
    public string NombreMercader => gameObject.name;

    // Cada recurso del precio por el porcentaje de reparación (torreta o blindaje), redondeado para arriba.
    public PrecioRecursos PrecioReparacion(WeaponOffer oferta)
    {
        float porcentaje = oferta.esTorreta ? porcentajeReparacionTorreta : porcentajeReparacion;
        PrecioRecursos precio = oferta.Precio;

        return new PrecioRecursos(
            Mathf.CeilToInt(precio.Cantidad(TipoRecurso.PilaDeLatas) * porcentaje),
            Mathf.CeilToInt(precio.Cantidad(TipoRecurso.Cantimplora) * porcentaje),
            Mathf.CeilToInt(precio.Cantidad(TipoRecurso.Bidon) * porcentaje));
    }

    /// <summary>La torreta del jugador local recibió daño (o está rota): la oferta de la torreta pasa a ser "reparar".</summary>
    private static bool TorretaLocalDanada(PlayerRecursos recursosJugador)
    {
        if (recursosJugador == null)
            return false;

        TorretaAutomatica torreta = TorretaAutomatica.DelJugador(recursosJugador.OwnerClientId);
        return torreta != null && torreta.NecesitaReparacion;
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

        // Torreta dañada o rota del jugador: se repara acá (el servidor revisa que siga dañada y cobra la mitad).
        PlayerRecursos recursosJugador = oferta.esTorreta ? BuscarRecursosJugadorLocal() : null;

        if (oferta.esTorreta && TorretaLocalDanada(recursosJugador))
        {
            pendingSwitcher = null;
            pendingWeapon = null;
            pendingRecursos = recursosJugador;
            pendingWeaponId = weaponId;

            recursosJugador.OnPurchaseResult += OnPurchaseResultInterno;
            recursosJugador.RepararTorretaServerRpc(weaponId, PrecioReparacion(oferta));
            return;
        }

        // Torretas y barriles no se cobran acá: NPCShopUI los manda a ColocadorObjetos, que los cobra al colocarlos.
        if (oferta.EsColocable)
            return;

        Camera camara = BuscarCamaraJugadorLocal();
        if (camara == null)
        {
            Debug.LogWarning("[NPCWeaponVendor] No se encontró la cámara del jugador local.");
            return;
        }

        WeaponSwitcher switcher = camara.transform.root.GetComponentInChildren<WeaponSwitcher>();
        PlayerRecursos playerRecursos = camara.transform.root.GetComponentInChildren<PlayerRecursos>();

        if (switcher == null || playerRecursos == null)
        {
            Debug.LogWarning("[NPCWeaponVendor] No se encontraron WeaponSwitcher/PlayerRecursos en el jugador.");
            return;
        }

        if (oferta.EsCuracion)
        {
            // Sin switcher pendiente: al terminar no se desbloquea ningún arma.
            pendingSwitcher = null;
            pendingWeapon = null;
            pendingRecursos = playerRecursos;
            pendingWeaponId = weaponId;

            playerRecursos.OnPurchaseResult += OnPurchaseResultInterno;
            playerRecursos.ComprarCuracionServerRpc(weaponId, oferta.Precio, oferta.puntosDeSalud);
            return;
        }

        if (oferta.esEspacioInventario)
        {
            // El servidor valida que ese slot (curas o granadas) no haya llegado al máximo de Slot extra.
            pendingSwitcher = null;
            pendingWeapon = null;
            pendingRecursos = playerRecursos;
            pendingWeaponId = weaponId;

            playerRecursos.OnPurchaseResult += OnPurchaseResultInterno;
            playerRecursos.ComprarEspacioExtraServerRpc(weaponId, oferta.Precio, oferta.slotExtra);
            return;
        }

        if (oferta.EsBlindaje)
        {
            // El servidor decide si es compra o reparación según el estado de la pieza.
            pendingSwitcher = null;
            pendingWeapon = null;
            pendingRecursos = playerRecursos;
            pendingWeaponId = weaponId;

            playerRecursos.OnPurchaseResult += OnPurchaseResultInterno;
            playerRecursos.ComprarBlindajeServerRpc(weaponId, oferta.piezaBlindaje, oferta.Precio, PrecioReparacion(oferta), oferta.armadura);
            return;
        }

        if (oferta.esMunicion)
        {
            // Restaura el arma que se tiene en la mano. Si ya está llena no se cobra.
            Weapon arma = switcher.CurrentWeapon;

            if (arma == null || !arma.PuedeComprarMunicion)
            {
                PlaySound(purchaseDeniedSound);
                OnPurchaseResult?.Invoke(weaponId, false);
                return;
            }

            pendingSwitcher = null;
            pendingWeapon = arma;
            pendingRecursos = playerRecursos;
            pendingWeaponId = weaponId;

            playerRecursos.OnPurchaseResult += OnPurchaseResultInterno;
            playerRecursos.ComprarArmaServerRpc(weaponId, oferta.Precio);
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
            pendingRecursos = playerRecursos;
            pendingWeaponId = weaponId;

            playerRecursos.OnPurchaseResult += OnPurchaseResultInterno;
            playerRecursos.ComprarGranadaServerRpc(weaponId, oferta.Precio);
            return;
        }

        if (switcher.IsUnlocked(weaponId))
        {
            OnPurchaseResult?.Invoke(weaponId, true);
            return;
        }

        pendingSwitcher = switcher;
        pendingWeapon = null;
        pendingRecursos = playerRecursos;
        pendingWeaponId = weaponId;

        playerRecursos.OnPurchaseResult += OnPurchaseResultInterno;
        playerRecursos.ComprarArmaServerRpc(weaponId, oferta.Precio);
    }

    private void OnPurchaseResultInterno(string weaponId, bool exito)
    {
        if (weaponId != pendingWeaponId)
            return;

        if (pendingRecursos != null)
            pendingRecursos.OnPurchaseResult -= OnPurchaseResultInterno;

        PlaySound(exito ? purchaseSuccessSound : purchaseDeniedSound);

        if (exito && pendingSwitcher != null)
            pendingSwitcher.UnlockWeapon(weaponId, equipAfterUnlock: true);

        if (exito && pendingWeapon != null)
            pendingWeapon.RestaurarMunicion();

        pendingSwitcher = null;
        pendingWeapon = null;
        pendingRecursos = null;
        pendingWeaponId = null;

        OnPurchaseResult?.Invoke(weaponId, exito);
    }

    private PlayerRecursos BuscarRecursosJugadorLocal()
    {
        Camera camara = BuscarCamaraJugadorLocal();
        return camara != null ? camara.transform.root.GetComponentInChildren<PlayerRecursos>() : null;
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