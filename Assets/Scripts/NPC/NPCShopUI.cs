using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using UnityEngine.InputSystem;

public class NPCShopUI : MonoBehaviour
{
    [Header("Prompt de interacción")]
    [SerializeField] private GameObject interactPrompt;
    [SerializeField] private TextMeshProUGUI interactText;
    [SerializeField] private float interactRange = 3f;

    [Header("Panel de la tienda")]
    [SerializeField] private GameObject shopPanel;
    [SerializeField] private Transform rowsContainer;
    [SerializeField] private GameObject rowPrefab;
    [SerializeField] private Button closeButton;
    [SerializeField] private TextMeshProUGUI scoreText;
    [Tooltip("{0} se reemplaza por los puntos del jugador.")]
    [SerializeField] private string formatoPuntos = "Tus puntos: {0}";

    [Header("Secciones (pestañas de la izquierda)")]
    [Tooltip("Un elemento por pestaña: su categoría y el botón que la abre. Las ofertas se filtran por WeaponOffer.categoria.")]
    [SerializeField] private List<SeccionTienda> secciones = new List<SeccionTienda>();
    [SerializeField] private CategoriaTienda seccionInicial = CategoriaTienda.Armas;
    [SerializeField] private Color colorSeccionNormal = new Color(1f, 1f, 1f, 0.08f);
    [SerializeField] private Color colorSeccionSeleccionada = new Color(0.4f, 0.9f, 1f, 0.35f);
    [SerializeField] private Color colorMarcoNormal = new Color(1f, 1f, 1f, 0.35f);
    [SerializeField] private Color colorMarcoSeleccionado = new Color(0.45f, 0.95f, 1f, 1f);
    [Tooltip("Opcional. Se activa cuando la sección elegida no tiene ofertas (ej: un texto \"Próximamente\").")]
    [SerializeField] private GameObject avisoSeccionVacia;

    [Serializable]
    private class SeccionTienda
    {
        public CategoriaTienda categoria;
        public Button boton;
        [Tooltip("Opcional. Imagen que se tiñe al seleccionar la sección. Si queda vacío se usa la imagen del botón.")]
        public Image fondo;
        [Tooltip("Opcional. Borde de la pestaña: se tiñe con Color Marco Normal / Seleccionado.")]
        public Image marco;
    }

    [Header("Colores")]
    [SerializeField] private Color colorAlcanza = new Color(0f, 1f, 0f, 1f);
    [SerializeField] private Color colorNoAlcanza = new Color(1f, 0f, 0f, 1f);
    [SerializeField] private Color colorComprado = new Color(0.6f, 0.6f, 0.6f, 1f);
    [SerializeField] private Color colorNoDisponible = new Color(0.6f, 0.6f, 0.6f, 1f);
    [SerializeField] private Color colorFilaNormal = new Color(1f, 1f, 1f, 0.08f);
    [SerializeField] private Color colorFilaSeleccionada = new Color(1f, 0.85f, 0.2f, 0.35f);
    [Tooltip("Color fijo del nombre en la tarjeta. El estado (alcanza / no alcanza / comprada) se indica solo con el color del precio.")]
    [SerializeField] private Color colorNombreTarjeta = Color.white;

    [Header("Panel de detalle (derecha)")]
    [Tooltip("Opcional. Se muestra solo mientras hay un objeto seleccionado.")]
    [SerializeField] private GameObject panelDetalle;
    [SerializeField] private Image iconoDetalle;
    [SerializeField] private TextMeshProUGUI nombreDetalleText;
    [SerializeField] private TextMeshProUGUI descripcionDetalleText;
    [Tooltip("Opcional. Todas las estadísticas en un solo texto (formato viejo). Si usás las filas de abajo podés dejarlo vacío.")]
    [SerializeField] private TextMeshProUGUI statsDetalleText;
    [SerializeField] private string textoSinSeleccion = "Seleccioná un objeto de la lista para ver su descripción y estadísticas.";

    [Header("Estadísticas (filas con barra)")]
    [SerializeField] private FilaEstadistica filaDano;
    [SerializeField] private FilaEstadistica filaCadencia;
    [SerializeField] private FilaEstadistica filaAlcance;
    [SerializeField] private FilaEstadistica filaCargador;
    [Tooltip("Daño que llena la barra entera.")]
    [SerializeField] private float danoMaximo = 200f;
    [Tooltip("Disparos por minuto que llenan la barra entera (cadencia = segundos entre disparos).")]
    [SerializeField] private float disparosPorMinutoMaximo = 600f;
    [Tooltip("Alcance (m) que llena la barra entera.")]
    [SerializeField] private float alcanceMaximo = 150f;
    [Tooltip("Balas de cargador que llenan la barra entera.")]
    [SerializeField] private float cargadorMaximo = 30f;

    [Serializable]
    private class FilaEstadistica
    {
        [Tooltip("La fila entera: se oculta cuando la estadística no aplica (ej: cargador en granadas).")]
        public GameObject fila;
        public TextMeshProUGUI valor;
        [Tooltip("Opcional. Image en modo Filled.")]
        public Image barra;
    }

    [Header("Comprar manteniendo ESPACIO")]
    [Tooltip("Opcional. Bloque de compra (abajo a la derecha). Se muestra solo mientras hay un objeto seleccionado.")]
    [SerializeField] private GameObject panelCompra;
    [SerializeField] private TextMeshProUGUI textoCompra;
    [Tooltip("{0} se reemplaza por el costo.")]
    [SerializeField] private string formatoComprar = "COMPRAR - {0} pts";
    [SerializeField] private string textoYaComprado = "COMPRADO";
    [SerializeField] private string textoNoDisponibleCompra = "PRÓXIMAMENTE";
    [SerializeField] private Image progresoCompraFill;
    [SerializeField] private float tiempoMantenerParaComprar = 2f;

    [Header("Fase de compras")]
    [SerializeField] private Button listoButton;
    [SerializeField] private TextMeshProUGUI listoButtonText;
    [SerializeField] private TextMeshProUGUI tiempoTiendaText;
    [Tooltip("Texto debajo del botón Listo. Solo se ve cuando el otro jugador ya marcó Listo.")]
    [SerializeField] private TextMeshProUGUI otroJugadorListoText;
    [SerializeField] private string textoListo = "Listo";
    [SerializeField] private string textoEsperandoListo = "Esperando...";
    [SerializeField] private string formatoMensajeBloqueado = "Se habilitará un tiempo para comerciar luego de las rondas {0}";
    [SerializeField] private float duracionMensajeBloqueado = 3f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip openSound;
    [SerializeField] private AudioClip closeSound;
    [SerializeField] private AudioClip purchaseSound;

    private PlayerControls controls;
    private Camera playerCamera;
    private NPCWeaponVendor currentVendor;
    private PlayerScore playerScore;
    private WeaponSwitcher weaponSwitcher;
    private PlayerMovement playerMovement;
    private PlayerLook playerLook;
    private PlayerHealth playerHealth;

    private bool estaAbierta;
    private bool movementWasLocked;
    private bool lookWasEnabled;
    private bool weaponWasLocked;

    public bool EstaAbierta => estaAbierta;

    // ---------------------------------------------------------
    // Estado de la tienda del jugador LOCAL, legible desde cualquier script de arma / movimiento.
    // Mismo patron que PauseController.LocalPlayerPaused / MouseInputBlocked.
    // Solo lo escribe la tienda del dueño (la de los jugadores remotos nunca se abre).
    // ---------------------------------------------------------

    /// <summary>true mientras la tienda del jugador local esta abierta.</summary>
    public static bool LocalShopOpen { get; private set; }

    private static int closeFrame = -100;

    /// <summary>
    /// true con la tienda abierta y durante los 2 frames siguientes a cerrarla. Hay que ignorar el delta
    /// del mouse en ese lapso: al volver a bloquear el cursor el primer delta puede ser enorme.
    /// </summary>
    public static bool MouseInputBlocked =>
        LocalShopOpen || Time.frameCount - closeFrame <= 2;

    // Con "Enter Play Mode Options" (sin recargar dominio) los estaticos sobreviven entre partidas.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        LocalShopOpen = false;
        closeFrame = -100;
    }

    [Header("Tarjeta de oferta (nombres de los hijos del prefab)")]
    [SerializeField] private string nombreIcono = "Icon";
    [SerializeField] private string nombreTextoNombre = "NameText";
    [SerializeField] private string nombreTextoCosto = "CostText";

    private class OfertaFila
    {
        public WeaponOffer oferta;
        public Button boton;
        public Image icono;
        public Image fondo;
        public TextMeshProUGUI etiquetaNombre;
        public TextMeshProUGUI etiquetaCosto;
    }

    private readonly List<OfertaFila> filas = new List<OfertaFila>();

    private WeaponOffer ofertaSeleccionada;
    private float tiempoMantenido;
    private CategoriaTienda seccionActual;

    private NetworkObject networkObject;
    private bool faseComprasAnterior;
    private bool listoMarcado;
    private float mensajeBloqueadoHasta;

    private void Awake()
    {
        controls = new PlayerControls();
        ConfiguracionesJuego.CargarRebinds(controls.asset);

        if (shopPanel != null)
            shopPanel.SetActive(false);

        if (closeButton != null)
            closeButton.onClick.AddListener(CerrarTienda);

        if (listoButton != null)
            listoButton.onClick.AddListener(MarcarListo);

        foreach (SeccionTienda seccion in secciones)
        {
            if (seccion == null || seccion.boton == null)
                continue;

            CategoriaTienda captura = seccion.categoria;
            seccion.boton.onClick.AddListener(() => SeleccionarSeccion(captura));
        }

        if (listoButtonText == null && listoButton != null)
            listoButtonText = listoButton.GetComponentInChildren<TextMeshProUGUI>(true);

        if (audioSource != null)
            audioSource.ignoreListenerPause = true;

        networkObject = GetComponentInParent<NetworkObject>();

        BuscarCamara();
        CachearReferenciasJugador();

        OcultarPrompt();
    }

    private void OnEnable()
    {
        controls.Player.Enable();
    }

    private void OnDisable()
    {
        controls.Player.Disable();

        // Si este jugador se desactiva / destruye con la tienda abierta, no dejar el bloqueo pegado.
        // (Solo si estaba abierta: asi un jugador remoto no pisa el estado del local.)
        if (estaAbierta)
            LocalShopOpen = false;
    }

    private void Update()
    {
        // En multiplayer, la copia del jugador remoto no maneja UI.
        if (networkObject != null && networkObject.IsSpawned && !networkObject.IsOwner)
            return;

        ActualizarFaseCompras();

        if (estaAbierta)
        {
            // Al llegar el temporizador a 0:00 (o si todos marcaron Listo) la tienda se cierra.
            if (!TiendaHabilitada())
            {
                CerrarTienda();
                return;
            }

            ActualizarCompraMantenida();
            return;
        }

        if (playerCamera == null)
        {
            BuscarCamara();

            if (playerCamera == null)
            {
                OcultarPrompt();
                return;
            }
        }

        DetectarMercader();
    }

    private void DetectarMercader()
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, interactRange))
        {
            // GetComponent (no GetComponentInParent): así el raycast solo detecta
            // el propio collider del mercader, no los assets del puesto (mesa,
            // estantería, etc.) que cuelgan de él como hijos.
            NPCWeaponVendor vendor = hit.collider.GetComponent<NPCWeaponVendor>();

            if (vendor != null)
            {
                if (controls.Player.Interact.triggered)
                {
                    if (TiendaHabilitada())
                    {
                        AbrirTienda(vendor);
                        return;
                    }

                    // Fuera de la fase de compras el mercader no comercia.
                    mensajeBloqueadoHasta = Time.unscaledTime + duracionMensajeBloqueado;
                }

                MostrarPrompt(vendor);
                return;
            }
        }

        OcultarPrompt();
    }

    private void MostrarPrompt(NPCWeaponVendor vendor)
    {
        if (interactPrompt == null)
            return;

        if (interactText != null)
        {
            interactText.text = Time.unscaledTime < mensajeBloqueadoHasta
                ? ConstruirMensajeBloqueado()
                : controls.Player.Interact.GetBindingDisplayString() + " para hablar con " + vendor.NombreMercader;
        }

        interactPrompt.SetActive(true);
    }

    private void OcultarPrompt()
    {
        if (interactPrompt != null)
            interactPrompt.SetActive(false);
    }

    private void AbrirTienda(NPCWeaponVendor vendor)
    {
        if (estaAbierta)
            return;

        currentVendor = vendor;
        estaAbierta = true;
        LocalShopOpen = true;
        ofertaSeleccionada = null;
        tiempoMantenido = 0f;
        seccionActual = seccionInicial;

        OcultarPrompt();
        CrearFilas();
        ActualizarFilas();
        ActualizarPanelDetalle();
        ActualizarSecciones();

        if (shopPanel != null)
            shopPanel.SetActive(true);

        if (audioSource != null && openSound != null)
            audioSource.PlayOneShot(openSound);

        if (currentVendor != null)
            currentVendor.OnPurchaseResult += OnCompraTerminada;

        if (playerScore != null)
            playerScore.ScoreNetwork.OnValueChanged += OnScoreChanged;

        movementWasLocked = playerMovement != null && playerMovement.MovementLocked;
        lookWasEnabled = playerLook != null && playerLook.enabled;
        weaponWasLocked = weaponSwitcher != null &&
                          weaponSwitcher.CurrentWeapon != null &&
                          weaponSwitcher.CurrentWeapon.InputLocked;

        BloquearJugador();

        // El juego NO se pausa: el temporizador de la fase de compras sigue corriendo.
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        ActualizarUIFaseCompras();
    }

    public void CerrarTienda()
    {
        if (!estaAbierta)
            return;

        estaAbierta = false;
        LocalShopOpen = false;
        closeFrame = Time.frameCount;
        ofertaSeleccionada = null;
        tiempoMantenido = 0f;

        if (currentVendor != null)
            currentVendor.OnPurchaseResult -= OnCompraTerminada;

        if (playerScore != null)
            playerScore.ScoreNetwork.OnValueChanged -= OnScoreChanged;

        DestruirFilas();

        if (shopPanel != null)
            shopPanel.SetActive(false);

        if (audioSource != null && closeSound != null)
            audioSource.PlayOneShot(closeSound);

        RestaurarJugador();

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        currentVendor = null;

        ActualizarUIFaseCompras();
    }

    private void CrearFilas()
    {
        if (rowPrefab == null || rowsContainer == null || currentVendor == null)
        {
            Debug.LogError("[NPCShopUI] Falta rowPrefab, rowsContainer o mercader.", this);
            return;
        }

        foreach (WeaponOffer oferta in currentVendor.Offers)
        {
            if (oferta.categoria != seccionActual)
                continue;

            GameObject fila = Instantiate(rowPrefab, rowsContainer);
            fila.name = "Oferta_" + oferta.weaponId;
            fila.SetActive(true);

            Button boton = fila.GetComponent<Button>();
            Image fondo = fila.GetComponent<Image>();

            Transform iconoTransform = fila.transform.Find(nombreIcono);
            Transform nombreTransform = fila.transform.Find(nombreTextoNombre);
            Transform costoTransform = fila.transform.Find(nombreTextoCosto);

            Image icono = iconoTransform != null ? iconoTransform.GetComponent<Image>() : null;
            TextMeshProUGUI etiquetaNombre = nombreTransform != null ? nombreTransform.GetComponent<TextMeshProUGUI>() : null;
            TextMeshProUGUI etiquetaCosto = costoTransform != null ? costoTransform.GetComponent<TextMeshProUGUI>() : null;

            if (boton != null)
            {
                WeaponOffer captura = oferta;
                boton.onClick.AddListener(() => SeleccionarOferta(captura));
            }

            filas.Add(new OfertaFila
            {
                oferta = oferta,
                boton = boton,
                icono = icono,
                fondo = fondo,
                etiquetaNombre = etiquetaNombre,
                etiquetaCosto = etiquetaCosto
            });
        }
    }

    private void DestruirFilas()
    {
        foreach (OfertaFila fila in filas)
        {
            if (fila.boton != null)
                Destroy(fila.boton.gameObject);
        }

        filas.Clear();
    }

    private void ComprarOferta(WeaponOffer oferta)
    {
        if (currentVendor != null)
            currentVendor.RequestPurchase(oferta.weaponId);
    }

    private void SeleccionarOferta(WeaponOffer oferta)
    {
        ofertaSeleccionada = oferta;
        tiempoMantenido = 0f;

        ActualizarFilas();
        ActualizarPanelDetalle();
    }

    private void SeleccionarSeccion(CategoriaTienda categoria)
    {
        if (!estaAbierta || categoria == seccionActual)
            return;

        seccionActual = categoria;
        ofertaSeleccionada = null;
        tiempoMantenido = 0f;

        DestruirFilas();
        CrearFilas();
        ActualizarFilas();
        ActualizarPanelDetalle();
        ActualizarSecciones();
    }

    private void ActualizarSecciones()
    {
        foreach (SeccionTienda seccion in secciones)
        {
            if (seccion == null)
                continue;

            Image fondo = seccion.fondo != null
                ? seccion.fondo
                : seccion.boton != null ? seccion.boton.image : null;

            bool seleccionada = seccion.categoria == seccionActual;

            if (fondo != null)
                fondo.color = seleccionada ? colorSeccionSeleccionada : colorSeccionNormal;

            if (seccion.marco != null)
                seccion.marco.color = seleccionada ? colorMarcoSeleccionado : colorMarcoNormal;
        }

        if (avisoSeccionVacia != null)
            avisoSeccionVacia.SetActive(filas.Count == 0);
    }

    private void ActualizarPanelDetalle()
    {
        bool haySeleccion = ofertaSeleccionada != null;

        if (panelDetalle != null && panelDetalle.activeSelf != haySeleccion)
            panelDetalle.SetActive(haySeleccion);

        if (panelCompra != null && panelCompra.activeSelf != haySeleccion)
            panelCompra.SetActive(haySeleccion);

        if (iconoDetalle != null)
        {
            Sprite imagen = haySeleccion
                ? (ofertaSeleccionada.imagenTarjeta != null ? ofertaSeleccionada.imagenTarjeta : ofertaSeleccionada.weaponIcon)
                : null;

            iconoDetalle.sprite = imagen;
            iconoDetalle.enabled = imagen != null;
        }

        if (nombreDetalleText != null)
            nombreDetalleText.text = haySeleccion ? ofertaSeleccionada.weaponName : string.Empty;

        if (descripcionDetalleText != null)
            descripcionDetalleText.text = haySeleccion ? ofertaSeleccionada.descripcion : textoSinSeleccion;

        if (statsDetalleText != null)
            statsDetalleText.text = haySeleccion ? ConstruirTextoStats(ofertaSeleccionada) : string.Empty;

        ActualizarFilasEstadisticas();
        ActualizarTextoCompra();

        if (progresoCompraFill != null)
            progresoCompraFill.fillAmount = 0f;
    }

    private void ActualizarFilasEstadisticas()
    {
        WeaponOffer oferta = ofertaSeleccionada;
        bool conStats = oferta != null &&
                        oferta.categoria != CategoriaTienda.Consumibles &&
                        oferta.categoria != CategoriaTienda.Equipamiento;
        bool conCargador = conStats && !oferta.esArrojadiza;

        if (!conStats)
        {
            MostrarEstadistica(filaDano, false, string.Empty, 0f);
            MostrarEstadistica(filaCadencia, false, string.Empty, 0f);
            MostrarEstadistica(filaAlcance, false, string.Empty, 0f);
            MostrarEstadistica(filaCargador, false, string.Empty, 0f);
            return;
        }

        // cadencia = segundos entre disparos -> disparos por minuto.
        int disparosPorMinuto = oferta.cadencia > 0f ? Mathf.RoundToInt(60f / oferta.cadencia) : 0;

        MostrarEstadistica(filaDano, true, oferta.dano.ToString(), oferta.dano / danoMaximo);
        MostrarEstadistica(filaAlcance, true, oferta.alcance + " m", oferta.alcance / alcanceMaximo);
        MostrarEstadistica(filaCadencia, conCargador, disparosPorMinuto + " disp/min", disparosPorMinuto / disparosPorMinutoMaximo);
        MostrarEstadistica(filaCargador, conCargador, oferta.capacidadCargador + " balas", oferta.capacidadCargador / cargadorMaximo);
    }

    private static void MostrarEstadistica(FilaEstadistica fila, bool visible, string valor, float proporcion)
    {
        if (fila == null)
            return;

        if (fila.fila != null && fila.fila.activeSelf != visible)
            fila.fila.SetActive(visible);

        if (fila.valor != null)
            fila.valor.text = valor;

        if (fila.barra != null)
            fila.barra.fillAmount = Mathf.Clamp01(proporcion);
    }

    private void ActualizarTextoCompra()
    {
        if (textoCompra == null || ofertaSeleccionada == null)
            return;

        if (!ofertaSeleccionada.disponible)
        {
            textoCompra.text = textoNoDisponibleCompra;
            textoCompra.color = colorNoDisponible;
        }
        else if (weaponSwitcher != null && weaponSwitcher.IsUnlocked(ofertaSeleccionada.weaponId))
        {
            textoCompra.text = textoYaComprado;
            textoCompra.color = colorComprado;
        }
        else
        {
            bool alcanza = playerScore != null && playerScore.ScoreNetwork.Value >= ofertaSeleccionada.cost;
            textoCompra.text = string.Format(formatoComprar, ofertaSeleccionada.cost);
            textoCompra.color = alcanza ? colorAlcanza : colorNoAlcanza;
        }
    }

    private string ConstruirTextoStats(WeaponOffer oferta)
    {
        // Daño/alcance/cargador solo tienen sentido en armas y utilidades arrojadizas;
        // consumibles y equipamiento se describen con el texto de descripción.
        if (oferta.categoria == CategoriaTienda.Consumibles || oferta.categoria == CategoriaTienda.Equipamiento)
            return string.Empty;

        string texto = "Daño: " + oferta.dano + "\nAlcance: " + oferta.alcance + " m";

        if (!oferta.esArrojadiza)
        {
            texto += "\nCargador: " + oferta.capacidadCargador + " balas" +
                     "\nCadencia: " + oferta.cadencia;
        }

        return texto;
    }

    private void ActualizarCompraMantenida()
    {
        bool puedeComprar =
            ofertaSeleccionada != null &&
            ofertaSeleccionada.disponible &&
            weaponSwitcher != null &&
            !weaponSwitcher.IsUnlocked(ofertaSeleccionada.weaponId) &&
            playerScore != null &&
            playerScore.ScoreNetwork.Value >= ofertaSeleccionada.cost;

        bool teclaPresionada = Keyboard.current != null && Keyboard.current.spaceKey.isPressed;

        if (!puedeComprar || !teclaPresionada)
        {
            tiempoMantenido = 0f;

            if (progresoCompraFill != null)
                progresoCompraFill.fillAmount = 0f;

            return;
        }

        tiempoMantenido += Time.unscaledDeltaTime;

        if (progresoCompraFill != null)
            progresoCompraFill.fillAmount = Mathf.Clamp01(tiempoMantenido / tiempoMantenerParaComprar);

        if (tiempoMantenido >= tiempoMantenerParaComprar)
        {
            tiempoMantenido = 0f;

            if (progresoCompraFill != null)
                progresoCompraFill.fillAmount = 0f;

            ComprarOferta(ofertaSeleccionada);
        }
    }

    private void OnCompraTerminada(string weaponId, bool exito)
    {
        if (!estaAbierta)
            return;

        if (exito && audioSource != null && purchaseSound != null)
            audioSource.PlayOneShot(purchaseSound);

        ActualizarFilas();
    }

    private void OnScoreChanged(int previousValue, int currentValue)
    {
        if (estaAbierta)
            ActualizarFilas();
    }

    private void ActualizarFilas()
    {
        if (playerScore != null && scoreText != null)
            scoreText.text = string.Format(formatoPuntos, playerScore.ScoreNetwork.Value);

        foreach (OfertaFila fila in filas)
        {
            bool disponible = fila.oferta.disponible;

            bool comprada = disponible && weaponSwitcher != null &&
                            weaponSwitcher.IsUnlocked(fila.oferta.weaponId);

            bool alcanza = playerScore != null &&
                           playerScore.ScoreNetwork.Value >= fila.oferta.cost;

            if (fila.icono != null)
            {
                Sprite imagen = fila.oferta.imagenTarjeta != null ? fila.oferta.imagenTarjeta : fila.oferta.weaponIcon;
                fila.icono.sprite = imagen;
                fila.icono.enabled = imagen != null;
            }

            if (fila.etiquetaNombre != null)
                fila.etiquetaNombre.text = fila.oferta.weaponName;

            Color colorFila;

            if (!disponible)
            {
                if (fila.etiquetaCosto != null)
                    fila.etiquetaCosto.text = "Próximamente";

                colorFila = colorNoDisponible;
            }
            else if (comprada)
            {
                if (fila.etiquetaCosto != null)
                    fila.etiquetaCosto.text = "Comprada";

                colorFila = colorComprado;
            }
            else
            {
                if (fila.etiquetaCosto != null)
                    fila.etiquetaCosto.text = fila.oferta.cost + " pts";

                colorFila = alcanza ? colorAlcanza : colorNoAlcanza;
            }

            if (fila.etiquetaNombre != null)
                fila.etiquetaNombre.color = colorNombreTarjeta;

            if (fila.etiquetaCosto != null)
                fila.etiquetaCosto.color = colorFila;

            if (fila.fondo != null)
                fila.fondo.color = fila.oferta == ofertaSeleccionada ? colorFilaSeleccionada : colorFilaNormal;
        }

        // Los puntos o la compra cambiaron: el bloque de compra también.
        ActualizarTextoCompra();
    }

    private void BloquearJugador()
    {
        if (playerMovement != null)
            playerMovement.MovementLocked = true;

        if (playerLook != null)
            playerLook.enabled = false;

        if (weaponSwitcher != null && weaponSwitcher.CurrentWeapon != null)
            weaponSwitcher.CurrentWeapon.InputLocked = true;
    }

    private void RestaurarJugador()
    {
        if (playerHealth != null && !playerHealth.IsAlive)
            return;

        if (playerMovement != null)
            playerMovement.MovementLocked = movementWasLocked;

        if (playerLook != null)
            playerLook.enabled = lookWasEnabled;

        if (weaponSwitcher != null && weaponSwitcher.CurrentWeapon != null)
            weaponSwitcher.CurrentWeapon.InputLocked = weaponWasLocked;
    }

    private void BuscarCamara()
    {
        Camera[] cameras = transform.root.GetComponentsInChildren<Camera>(true);

        foreach (Camera cam in cameras)
        {
            NetworkObject networkObject = cam.GetComponentInParent<NetworkObject>();

            if (networkObject != null)
            {
                if (networkObject.IsOwner)
                {
                    playerCamera = cam;
                    return;
                }
            }
            else if (cam == Camera.main)
            {
                playerCamera = cam;
                return;
            }
        }
    }

    private void CachearReferenciasJugador()
    {
        Transform root = transform.root;

        playerScore = root.GetComponentInChildren<PlayerScore>(true);
        weaponSwitcher = root.GetComponentInChildren<WeaponSwitcher>(true);
        playerMovement = root.GetComponentInChildren<PlayerMovement>(true);
        playerLook = root.GetComponentInChildren<PlayerLook>(true);
        playerHealth = root.GetComponentInChildren<PlayerHealth>(true);
    }

    // =========================================================
    // FASE DE COMPRAS
    // =========================================================

    // Sin RoundManager (ej. escenas de prueba) el mercader queda siempre habilitado.
    private bool TiendaHabilitada()
    {
        return RoundManager.Instance == null || RoundManager.Instance.IsShopPhaseActive;
    }

    private string ConstruirMensajeBloqueado()
    {
        string rondas = RoundManager.Instance != null
            ? RoundManager.Instance.GetShopRoundsDescription()
            : string.Empty;

        return string.Format(formatoMensajeBloqueado, rondas);
    }

    private void ActualizarFaseCompras()
    {
        bool faseActiva = RoundManager.Instance != null && RoundManager.Instance.IsShopPhaseActive;

        // Cada fase de compras nueva arranca sin "Listo" marcado.
        if (faseActiva && !faseComprasAnterior)
        {
            listoMarcado = false;
            mensajeBloqueadoHasta = 0f;
        }

        faseComprasAnterior = faseActiva;

        ActualizarUIFaseCompras();
    }

    private void ActualizarUIFaseCompras()
    {
        bool faseActiva = RoundManager.Instance != null && RoundManager.Instance.IsShopPhaseActive;
        string tiempo = faseActiva ? FormatearTiempo(RoundManager.Instance.ShopPhaseRemaining) : string.Empty;

        if (tiempoTiendaText != null)
        {
            if (tiempoTiendaText.gameObject.activeSelf != faseActiva)
                tiempoTiendaText.gameObject.SetActive(faseActiva);

            if (faseActiva)
                tiempoTiendaText.text = "Tiempo restante: " + tiempo;
        }

        if (listoButton != null)
        {
            if (listoButton.gameObject.activeSelf != faseActiva)
                listoButton.gameObject.SetActive(faseActiva);

            listoButton.interactable = !listoMarcado;
        }

        if (listoButtonText != null)
            listoButtonText.text = listoMarcado ? textoEsperandoListo : textoListo;

        if (otroJugadorListoText != null)
        {
            bool otroListo = faseActiva && RoundManager.Instance.OtroJugadorListoFaseCompras();

            if (otroJugadorListoText.gameObject.activeSelf != otroListo)
                otroJugadorListoText.gameObject.SetActive(otroListo);
        }
    }

    private void MarcarListo()
    {
        if (listoMarcado || RoundManager.Instance == null || !RoundManager.Instance.IsShopPhaseActive)
            return;

        listoMarcado = true;
        RoundManager.Instance.MarcarListoFaseComprasServerRpc();

        ActualizarUIFaseCompras();
    }

    private static string FormatearTiempo(int segundos)
    {
        segundos = Mathf.Max(0, segundos);
        return (segundos / 60).ToString("00") + ":" + (segundos % 60).ToString("00");
    }
}