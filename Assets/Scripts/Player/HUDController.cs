using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Unity.Netcode;

public class HUDController : MonoBehaviour
{
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private Weapon weapon;
    [SerializeField] private Image weaponIcon;
    [SerializeField] private TextMeshProUGUI playerLabelText;

    private RoundManager roundManager;

    [Header("Vida")]
    [SerializeField] private Image healthFill;
    [SerializeField] private TextMeshProUGUI healthPercentText;
    [SerializeField] private Color healthColorFull = Color.white;
    [SerializeField] private Color healthColorHalf = new Color(1f, 0.65f, 0.3f);
    [SerializeField] private Color healthColorLow = Color.red;
    [SerializeField] private GameObject vidas1;
    [SerializeField] private GameObject vidas0;

    [Header("Estamina")]
    [SerializeField] private Image staminaFill;
    [Tooltip("Número que baja junto con la barra (0-100).")]
    [SerializeField] private TextMeshProUGUI staminaValueText;

    [Header("Escudo (chaleco + casco)")]
    [SerializeField] private Image armorFill;
    [Tooltip("Número que acompaña a la barra de escudo (0 a Max Armor de PlayerHealth).")]
    [SerializeField] private TextMeshProUGUI armorValueText;
    [Tooltip("Color del relleno de la barra de escudo.")]
    [SerializeField] private Color armorColor = new Color(0.35f, 0.8f, 1f, 1f);

    [Header("Inventario (slots al lado de la munición)")]
    [Tooltip("Slot 1: curas. Si queda vacío se busca el objeto llamado Slot01.")]
    [SerializeField] private Image slotCuras;
    [Tooltip("Slot 2: granada equipada. Si queda vacío se busca el objeto llamado Slot02.")]
    [SerializeField] private Image slotGranada;
    [Tooltip("Margen entre el marco del slot y el icono.")]
    [SerializeField] private float margenIconoSlot = 14f;
    [SerializeField] private float tamanoTextoSlot = 22f;
    [Tooltip("Opacidad del texto de la tecla cuando el slot está vacío.")]
    [Range(0f, 1f)] [SerializeField] private float alfaTeclaSlotVacio = 0.35f;

    [Header("Blindaje en el HUD (slots 3 y 4)")]
    [Tooltip("Slot 3: chaleco. Si queda vacío se busca el objeto llamado Slot03; si tampoco existe se duplica el marco de Slot02.")]
    [SerializeField] private Image slotChaleco;
    [Tooltip("Slot 4: casco. Si queda vacío se busca el objeto llamado Slot04; si tampoco existe se duplica el marco de Slot02.")]
    [SerializeField] private Image slotCasco;
    [Tooltip("Separación entre marcos cuando se duplican (solo si el contenedor no tiene un Layout Group que los acomode).")]
    [SerializeField] private float separacionSlotsBlindaje = 10f;
    [Tooltip("Tinte del icono de una pieza rota cuando la oferta no tiene Icono Hud Roto.")]
    [SerializeField] private Color colorBlindajeRotoSinIcono = new Color(1f, 0.3f, 0.3f, 0.6f);

    [Header("Munición")]
    [SerializeField] private TextMeshProUGUI ammoText;
    [SerializeField] private TextMeshProUGUI ammoMaxText;
    [SerializeField] private TextMeshProUGUI weaponNameText;

    [Header("Rondas")]
    [SerializeField] private TextMeshProUGUI roundsText;
    [SerializeField] private TextMeshProUGUI zombiesText;

    [Header("Crosshair")]
    [SerializeField] private GameObject crosshairRoot;
    [SerializeField] private RectTransform dashTop;
    [SerializeField] private RectTransform dashBottom;
    [SerializeField] private RectTransform dashLeft;
    [SerializeField] private RectTransform dashRight;

    [SerializeField] private float crosshairMinGap = 40f;
    [SerializeField] private float crosshairMaxGap = 140f;

    [Header("Hit Marker")]
    [SerializeField] private GameObject hitMarker;
    [SerializeField] private float hitMarkerDuration = 0.10f;

    private Image[] hitMarkerImages;

    private Coroutine hitMarkerCoroutine;

    private bool etiquetaActualizada;

    private void Start()
    {
        BuscarRoundManager();

       /* NetworkObject networkObject =
            GetComponentInParent<NetworkObject>();

        if (
            networkObject != null &&
            networkObject.IsOwner &&
            NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsClient
        )
        {
            if (NetworkManager.Singleton.LocalClientId == 1)
            {
                if (playerLabelText != null)
                    playerLabelText.text = "Jugador 2 (TÚ)";
            }
        }*/

        if (hitMarker != null)
        {
            // Obtiene las Images de:
            //
            // HitMarker
            // ├── Dash_Top
            // ├── Dash_Bottom
            // ├── Dash_Left
            // └── Dash_Right
            //
            hitMarkerImages =
                hitMarker.GetComponentsInChildren<Image>(
                    true
                );

            hitMarker.SetActive(false);
        }

        ActualizarTodo();
    }

    private void ActualizarEtiquetaJugador()
    {
        if (etiquetaActualizada || playerLabelText == null)
            return;

        // El jugador al que pertenece este HUD.
        NetworkObject networkObject =
            playerHealth != null
                ? playerHealth.GetComponentInParent<NetworkObject>()
                : GetComponentInParent<NetworkObject>();

        // Todavia no esta listo: se reintenta en el proximo cuadro.
        if (networkObject == null || !networkObject.IsSpawned)
            return;

        // Este HUD no es del jugador local.
        if (!networkObject.IsOwner)
            return;

        MultiplayerPlayerSpawnAssigner assigner =
            networkObject.GetComponent<MultiplayerPlayerSpawnAssigner>();

        // Singleplayer: no hay asignador, es siempre el jugador 1.
        if (assigner == null)
        {
            playerLabelText.text = "Jugador 1 (TÚ)";
            etiquetaActualizada = true;
            return;
        }

        // Multijugador: espera a que el servidor asigne el numero de jugador.
        if (assigner.AssignedSpawnIndex.Value < 0)
            return;

        playerLabelText.text =
            "Jugador " + (assigner.AssignedSpawnIndex.Value + 1) + " (TÚ)";

        etiquetaActualizada = true;
    }

    private void Update()
    {
        ActualizarEtiquetaJugador();
        
        if (roundManager == null)
        {
            BuscarRoundManager();
        }

        ActualizarTodo();
    }

    private void UpdateLivesUI()
    {
        if (playerHealth == null)
            return;

        bool mostrarVidaLlena =
            playerHealth.Lives.Value > 0 &&
            !playerHealth.IsDowned;

        if (vidas1 != null)
            vidas1.SetActive(mostrarVidaLlena);

        if (vidas0 != null)
            vidas0.SetActive(!mostrarVidaLlena);
    }

    public void SetWeapon(Weapon newWeapon, Sprite newIcon)
    {
        weapon = newWeapon;

        // Si la imagen del icono pertenece a un objeto con ImageWeapon (arma + accesorios), ese script ya la
        // maneja solo: no se le pisa el sprite (antes se le ponía null y el icono desaparecía).
        // Solo se usa el sprite suelto cuando es una imagen común y hay un sprite para poner.
        if (weaponIcon != null && newIcon != null && weaponIcon.GetComponentInParent<ImageWeapon>(true) == null)
            weaponIcon.sprite = newIcon;
    }
    private void BuscarRoundManager()
    {
        roundManager =
            FindFirstObjectByType<RoundManager>();

        if (roundManager == null)
            return;

        Debug.Log(
            "[HUDController] RoundManager encontrado en " +
            gameObject.name +
            " | Round=" +
            roundManager.CurrentRound +
            " | Zombies=" +
            roundManager.AliveZombies +
            "/" +
            roundManager.ZombiesThisRound
        );
    }

    private void ActualizarTodo()
    {
        UpdateHealthBar();
        UpdateStaminaBar();
        UpdateArmorBar();
        UpdateAmmoText();
        UpdateRounds();
        UpdateCrosshair();
        UpdateLivesUI();
        UpdateInventarioCuras();
    }

    // =========================================================
    // INVENTARIO (slot 1: curas, slot 2: granadas, slots 3 y 4: chaleco y casco)
    // =========================================================

    private class SlotHud
    {
        public Image icono;
        public TextMeshProUGUI cantidad;
        public TextMeshProUGUI tecla;
    }

    private SlotHud hudCura;
    private SlotHud hudGranada;
    private SlotHud hudChaleco;
    private SlotHud hudCasco;
    private bool slotsCreados;
    private GrenadeThrow grenadeThrow;

    private readonly System.Collections.Generic.Dictionary<string, Sprite> iconosOfertas =
        new System.Collections.Generic.Dictionary<string, Sprite>();

    private readonly System.Collections.Generic.Dictionary<PiezaBlindaje, WeaponOffer> ofertasBlindaje =
        new System.Collections.Generic.Dictionary<PiezaBlindaje, WeaponOffer>();

    private void UpdateInventarioCuras()
    {
        if (!slotsCreados)
            CrearSlotsHud();

        UpdateSlotCura();
        UpdateSlotGranada();
        UpdateSlotBlindaje(PiezaBlindaje.Chaleco, slotChaleco, hudChaleco);
        UpdateSlotBlindaje(PiezaBlindaje.Casco, slotCasco, hudCasco);
    }

    private void UpdateSlotCura()
    {
        if (hudCura == null || playerHealth == null || playerHealth.SlotsCuras == null)
            return;

        SlotCura slot = playerHealth.GetSlotCura(0);
        bool lleno = !slot.Vacio;

        MostrarIcono(hudCura, lleno ? IconoDeOferta(slot.itemId.ToString()) : null);

        // La cantidad solo aporta algo cuando en el slot entra más de una cura (Slot extra comprado).
        hudCura.cantidad.text = lleno && playerHealth.CapacidadCuras > 1
            ? slot.cantidad + "/" + playerHealth.CapacidadCuras
            : string.Empty;

        hudCura.tecla.text = playerHealth.TeclaSlotCura;
        hudCura.tecla.alpha = lleno ? 1f : alfaTeclaSlotVacio;
    }

    // Slot G: muestra la granada que lleva el jugador y cuántas. Vacío = no puede tirar granadas.
    private void UpdateSlotGranada()
    {
        if (hudGranada == null || playerHealth == null)
            return;

        SlotCura slot = playerHealth.SlotGranada.Value;
        bool lleno = !slot.Vacio;

        MostrarIcono(hudGranada, lleno ? IconoDeOferta(slot.itemId.ToString()) : null);

        // La cantidad solo aporta algo cuando en el slot entra más de una granada (Slot extra comprado).
        hudGranada.cantidad.text = lleno && playerHealth.CapacidadGranadas > 1
            ? slot.cantidad + "/" + playerHealth.CapacidadGranadas
            : string.Empty;

        hudGranada.tecla.text = grenadeThrow != null ? grenadeThrow.TeclaGranada : string.Empty;
        hudGranada.tecla.alpha = lleno ? 1f : alfaTeclaSlotVacio;
    }

    // Chaleco / casco: el slot solo se activa cuando se compró la pieza. Si pierde todo el escudo
    // se muestra el icono "roto" hasta que se repare en la tienda.
    private void UpdateSlotBlindaje(PiezaBlindaje pieza, Image marco, SlotHud hud)
    {
        if (hud == null || marco == null || playerHealth == null)
            return;

        bool tiene = playerHealth.TieneBlindaje(pieza);

        if (marco.gameObject.activeSelf != tiene)
            marco.gameObject.SetActive(tiene);

        if (!tiene)
            return;

        bool rota = playerHealth.EscudoDe(pieza) <= 0;
        WeaponOffer oferta = OfertaDeBlindaje(pieza);

        Sprite icono = oferta != null ? IconoHudDeOferta(oferta) : null;
        Color color = Color.white;

        if (rota)
        {
            if (oferta != null && oferta.iconoHudRoto != null)
                icono = oferta.iconoHudRoto;
            else
                color = colorBlindajeRotoSinIcono;
        }

        MostrarIcono(hud, icono);
        hud.icono.color = color;
        hud.cantidad.text = string.Empty;
        hud.tecla.text = string.Empty;
    }

    // Busca en los mercaderes la oferta de esa pieza (de ahí salen los iconos del HUD).
    private WeaponOffer OfertaDeBlindaje(PiezaBlindaje pieza)
    {
        if (ofertasBlindaje.TryGetValue(pieza, out WeaponOffer cacheada))
            return cacheada;

        foreach (NPCWeaponVendor vendor in FindObjectsByType<NPCWeaponVendor>(FindObjectsSortMode.None))
        {
            foreach (WeaponOffer oferta in vendor.Offers)
            {
                if (oferta.EsBlindaje && oferta.piezaBlindaje == pieza)
                {
                    // Solo se cachea si se encontró: el mercader puede aparecer más tarde.
                    ofertasBlindaje[pieza] = oferta;
                    return oferta;
                }
            }
        }

        return null;
    }

    // Icono del HUD: el propio de la oferta; si no tiene, el de la tienda (imagen de tarjeta o icono).
    private static Sprite IconoHudDeOferta(WeaponOffer oferta)
    {
        if (oferta.iconoHud != null)
            return oferta.iconoHud;

        return oferta.imagenTarjeta != null ? oferta.imagenTarjeta : oferta.weaponIcon;
    }

    private static void MostrarIcono(SlotHud hud, Sprite icono)
    {
        hud.icono.sprite = icono;
        hud.icono.enabled = icono != null;
    }

    private void CrearSlotsHud()
    {
        slotsCreados = true;

        if (slotCuras == null)
            slotCuras = BuscarImagenPorNombre("Slot01");

        if (slotGranada == null)
            slotGranada = BuscarImagenPorNombre("Slot02");

        // Chaleco y casco: Slot03 / Slot04 del HUD. Si no existen se duplica el marco del slot de granadas
        // (antes de crearle los hijos, para que la copia salga limpia).
        if (slotChaleco == null)
            slotChaleco = BuscarImagenPorNombre("Slot03");

        if (slotChaleco == null)
            slotChaleco = ClonarMarco(slotGranada, "Slot03", 1);

        if (slotCasco == null)
            slotCasco = BuscarImagenPorNombre("Slot04");

        if (slotCasco == null)
            slotCasco = ClonarMarco(slotGranada, "Slot04", 2);

        grenadeThrow = transform.root.GetComponentInChildren<GrenadeThrow>(true);

        hudCura = CrearSlotHud(slotCuras);
        hudGranada = CrearSlotHud(slotGranada);
        hudChaleco = CrearSlotHud(slotChaleco);
        hudCasco = CrearSlotHud(slotCasco);
    }

    // Duplica un marco de slot y lo corre hacia la derecha (si su contenedor no lo acomoda solo).
    private Image ClonarMarco(Image origen, string nombre, int posicion)
    {
        if (origen == null)
            return null;

        GameObject copia = Instantiate(origen.gameObject, origen.transform.parent, false);
        copia.name = nombre;

        Transform padre = origen.transform.parent;
        bool padreAcomoda = padre != null && padre.GetComponent<UnityEngine.UI.LayoutGroup>() != null;

        if (!padreAcomoda)
        {
            RectTransform rt = (RectTransform)copia.transform;
            rt.anchoredPosition += new Vector2((rt.rect.width + separacionSlotsBlindaje) * posicion, 0f);
        }

        return copia.GetComponent<Image>();
    }

    private SlotHud CrearSlotHud(Image marco)
    {
        // Slot sin marco en el HUD: se ignora.
        if (marco == null)
            return null;

        return new SlotHud
        {
            icono = CrearIcono(marco.transform),
            cantidad = CrearTexto(marco.transform, "Cantidad", TextAlignmentOptions.BottomRight),
            tecla = CrearTexto(marco.transform, "Tecla", TextAlignmentOptions.TopLeft)
        };
    }

    private Image BuscarImagenPorNombre(string nombre)
    {
        foreach (Image imagen in transform.root.GetComponentsInChildren<Image>(true))
        {
            if (imagen.name == nombre)
                return imagen;
        }

        return null;
    }

    private Image CrearIcono(Transform marco)
    {
        GameObject go = new GameObject("Icono", typeof(RectTransform), typeof(Image));
        go.layer = marco.gameObject.layer;
        go.transform.SetParent(marco, false);

        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(margenIconoSlot, margenIconoSlot);
        rt.offsetMax = new Vector2(-margenIconoSlot, -margenIconoSlot);

        Image imagen = go.GetComponent<Image>();
        imagen.preserveAspect = true;
        imagen.raycastTarget = false;
        imagen.enabled = false;
        return imagen;
    }

    private TextMeshProUGUI CrearTexto(Transform marco, string nombre, TextAlignmentOptions alineacion)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.layer = marco.gameObject.layer;
        go.transform.SetParent(marco, false);

        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(6f, 4f);
        rt.offsetMax = new Vector2(-6f, -4f);

        TextMeshProUGUI texto = go.GetComponent<TextMeshProUGUI>();
        texto.alignment = alineacion;
        texto.fontSize = tamanoTextoSlot;
        texto.fontStyle = FontStyles.Bold;
        texto.raycastTarget = false;
        texto.text = string.Empty;

        // Misma fuente que el texto de munición, para que combine con el HUD.
        if (ammoText != null)
            texto.font = ammoText.font;

        return texto;
    }

    // El icono sale de la oferta del mercader con ese id (Icono Hud; si no tiene, imagen de tarjeta o icono).
    private Sprite IconoDeOferta(string itemId)
    {
        if (iconosOfertas.TryGetValue(itemId, out Sprite cacheado))
            return cacheado;

        Sprite icono = null;

        foreach (NPCWeaponVendor vendor in FindObjectsByType<NPCWeaponVendor>(FindObjectsSortMode.None))
        {
            foreach (WeaponOffer oferta in vendor.Offers)
            {
                if (oferta.weaponId == itemId)
                {
                    icono = IconoHudDeOferta(oferta);
                    break;
                }
            }

            if (icono != null)
                break;
        }

        // Solo se cachea si se encontró: el mercader puede aparecer más tarde.
        if (icono != null)
            iconosOfertas[itemId] = icono;

        return icono;
    }

    // =========================================================
    // VIDA
    // =========================================================

    private void UpdateHealthBar()
    {
        if (playerHealth == null)
            return;

        float percentage =
            (float)playerHealth.CurrentHealth.Value /
            playerHealth.MaxHealth;

        if (healthFill != null)
        {
            healthFill.fillAmount =
                percentage;
        }

        if (healthPercentText != null)
        {
            healthPercentText.text =
                Mathf.RoundToInt(
                    percentage * 100f
                ) + "%";
        }

        if (healthFill != null)
        {
            healthFill.color =
                percentage > 0.5f
                    ? Color.Lerp(
                        healthColorHalf,
                        healthColorFull,
                        (percentage - 0.5f) / 0.5f
                    )
                    : Color.Lerp(
                        healthColorLow,
                        healthColorHalf,
                        percentage / 0.5f
                    );
        }
    }

    // =========================================================
    // ESTAMINA
    // =========================================================

    private void UpdateStaminaBar()
    {
        if (playerMovement == null)
            return;

        float fraction =
            playerMovement.MaxStamina > 0f
                ? Mathf.Clamp01(playerMovement.CurrentStamina / playerMovement.MaxStamina)
                : 0f;

        if (staminaFill != null)
            staminaFill.fillAmount = fraction;

        // El número baja a medida que baja la barra.
        if (staminaValueText != null)
            staminaValueText.text = Mathf.CeilToInt(fraction * 100f).ToString();
    }

    // =========================================================
    // CHALECO
    // =========================================================

    private void UpdateArmorBar()
    {
        if (playerHealth == null)
            return;

        int current = playerHealth.Armor.Value;
        int max = Mathf.Max(playerHealth.MaxArmor, 1);

        if (armorFill != null)
        {
            armorFill.fillAmount = Mathf.Clamp01((float)current / max);
            armorFill.color = armorColor;
        }

        if (armorValueText != null)
            armorValueText.text = current.ToString();
    }

    // =========================================================
    // MUNICIÓN
    // =========================================================

    private void UpdateAmmoText()
    {
        if (weapon == null)
            return;

        if (ammoText != null)
        {
            ammoText.text =
                weapon.CurrentAmmo.ToString();
        }

        if (ammoMaxText != null)
        {
            // Reserva limitada (Difícil): "12  / 108" = cargador / reserva.
            // Con recargas ilimitadas queda como antes.
            ammoMaxText.text = weapon.UsesLimitedReserve
                ? "/ " + weapon.ReserveAmmo
                : "/ " + weapon.EffectiveMaxAmmo;
        }

        if (weaponNameText != null)
        {
            weaponNameText.text =
                weapon.WeaponName.ToUpper();
        }
    }

    // =========================================================
    // RONDAS
    // =========================================================

    private void UpdateRounds()
    {
        if (roundManager == null)
            return;

       /* if (roundsText != null)
        {
            roundsText.text =
                "RONDAS   " +
                roundManager.CurrentRound +
                "/" +
                roundManager.MaxRounds;
        }*/

        if (roundsText != null)
        {
            // MaxRounds = 0 significa rondas infinitas (Supervivencia).
           /* roundsText.text =
                roundManager.IsInfiniteMode
                    ? "RONDA   " + roundManager.CurrentRound
                    : "RONDAS   " +
                      roundManager.CurrentRound +
                      "/" +
                      roundManager.MaxRounds;*/

            roundsText.text =
                roundManager.IsInfiniteMode
                    ? roundManager.CurrentRound.ToString()
                    : roundManager.CurrentRound + "/" + roundManager.MaxRounds;
        }

        if (zombiesText != null)
        {
            zombiesText.text =
                roundManager.AliveZombies +
                "/" +
                roundManager.ZombiesThisRound;
        }
    }

    // =========================================================
    // CROSSHAIR
    // =========================================================

    private void UpdateCrosshair()
    {
        if (weapon == null ||
            crosshairRoot == null)
            return;

        if (weapon.IsAiming)
        {
            crosshairRoot.SetActive(false);
            return;
        }

        crosshairRoot.SetActive(true);

        float gap =
            Mathf.Lerp(
                crosshairMinGap,
                crosshairMaxGap,
                weapon.CurrentSpreadNormalized
            );

        if (dashTop != null)
        {
            dashTop.anchoredPosition =
                new Vector2(
                    0f,
                    gap
                );
        }

        if (dashBottom != null)
        {
            dashBottom.anchoredPosition =
                new Vector2(
                    0f,
                    -gap
                );
        }

        if (dashLeft != null)
        {
            dashLeft.anchoredPosition =
                new Vector2(
                    -gap,
                    0f
                );
        }

        if (dashRight != null)
        {
            dashRight.anchoredPosition =
                new Vector2(
                    gap,
                    0f
                );
        }
    }

    // =========================================================
    // HIT MARKER
    // =========================================================

    public void ShowHitMarker(bool killedZombie)
    {
        if (hitMarker == null)
            return;

        // Por si todavía no se obtuvieron las imágenes.
        if (hitMarkerImages == null ||
            hitMarkerImages.Length == 0)
        {
            hitMarkerImages =
                hitMarker.GetComponentsInChildren<Image>(
                    true
                );
        }

        Color markerColor =
            killedZombie
                ? Color.red
                : Color.white;

        // Cambiar el color de las 4 partes
        // del HitMarker.
        foreach (Image image in hitMarkerImages)
        {
            if (image != null)
            {
                image.color =
                    markerColor;
            }
        }

        if (hitMarkerCoroutine != null)
        {
            StopCoroutine(
                hitMarkerCoroutine
            );
        }

        hitMarkerCoroutine =
            StartCoroutine(
                HitMarkerRoutine()
            );
    }

    private IEnumerator HitMarkerRoutine()
    {
        hitMarker.SetActive(true);

        yield return new WaitForSecondsRealtime(
            hitMarkerDuration
        );

        hitMarker.SetActive(false);

        hitMarkerCoroutine = null;
    }
}