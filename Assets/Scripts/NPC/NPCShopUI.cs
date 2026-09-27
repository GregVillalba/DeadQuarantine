using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using UnityEngine.SceneManagement;
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

    [Header("Colores")]
    [SerializeField] private Color colorAlcanza = new Color(0f, 1f, 0f, 1f);
    [SerializeField] private Color colorNoAlcanza = new Color(1f, 0f, 0f, 1f);
    [SerializeField] private Color colorComprado = new Color(0.6f, 0.6f, 0.6f, 1f);
    [SerializeField] private Color colorNoDisponible = new Color(0.6f, 0.6f, 0.6f, 1f);
    [SerializeField] private Color colorFilaNormal = new Color(1f, 1f, 1f, 0.08f);
    [SerializeField] private Color colorFilaSeleccionada = new Color(1f, 0.85f, 0.2f, 0.35f);

    [Header("Panel de detalle (derecha)")]
    [SerializeField] private Image iconoDetalle;
    [SerializeField] private TextMeshProUGUI nombreDetalleText;
    [SerializeField] private TextMeshProUGUI descripcionDetalleText;
    [SerializeField] private TextMeshProUGUI statsDetalleText;
    [SerializeField] private string textoSinSeleccion = "Seleccioná un arma de la lista para ver su descripción y estadísticas.";

    [Header("Comprar manteniendo ESPACIO")]
    [SerializeField] private Image progresoCompraFill;
    [SerializeField] private float tiempoMantenerParaComprar = 2f;

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

    [Header("Fila de oferta (nombres de los hijos del prefab)")]
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

    private void Awake()
    {
        controls = new PlayerControls();

        if (shopPanel != null)
            shopPanel.SetActive(false);

        if (closeButton != null)
            closeButton.onClick.AddListener(CerrarTienda);

        if (audioSource != null)
            audioSource.ignoreListenerPause = true;

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
    }

    private void Update()
    {
        if (estaAbierta)
        {
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
                MostrarPrompt(vendor);

                if (controls.Player.Interact.triggered)
                    AbrirTienda(vendor);

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
            interactText.text = "E para hablar con " + vendor.NombreMercader;

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
        ofertaSeleccionada = null;
        tiempoMantenido = 0f;

        OcultarPrompt();
        CrearFilas();
        ActualizarFilas();
        ActualizarPanelDetalle();

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

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (EsSinglePlayer())
        {
            Time.timeScale = 0f;
            AudioListener.pause = true;
        }
    }

    public void CerrarTienda()
    {
        if (!estaAbierta)
            return;

        estaAbierta = false;
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

        if (EsSinglePlayer())
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        currentVendor = null;
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

    private void ActualizarPanelDetalle()
    {
        bool haySeleccion = ofertaSeleccionada != null;

        if (iconoDetalle != null)
        {
            iconoDetalle.sprite = haySeleccion ? ofertaSeleccionada.weaponIcon : null;
            iconoDetalle.enabled = haySeleccion && ofertaSeleccionada.weaponIcon != null;
        }

        if (nombreDetalleText != null)
            nombreDetalleText.text = haySeleccion ? ofertaSeleccionada.weaponName : string.Empty;

        if (descripcionDetalleText != null)
            descripcionDetalleText.text = haySeleccion ? ofertaSeleccionada.descripcion : textoSinSeleccion;

        if (statsDetalleText != null)
        {
            statsDetalleText.text = haySeleccion
                ? "Daño: " + ofertaSeleccionada.dano +
                  "\nAlcance: " + ofertaSeleccionada.alcance + " m" +
                  "\nCargador: " + ofertaSeleccionada.capacidadCargador + " balas" +
                  "\nCadencia: " + ofertaSeleccionada.cadencia
                : string.Empty;
        }

        if (progresoCompraFill != null)
            progresoCompraFill.fillAmount = 0f;
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
            scoreText.text = "Tus puntos: " + playerScore.ScoreNetwork.Value;

        foreach (OfertaFila fila in filas)
        {
            bool disponible = fila.oferta.disponible;

            bool comprada = disponible && weaponSwitcher != null &&
                            weaponSwitcher.IsUnlocked(fila.oferta.weaponId);

            bool alcanza = playerScore != null &&
                           playerScore.ScoreNetwork.Value >= fila.oferta.cost;

            if (fila.icono != null)
            {
                fila.icono.sprite = fila.oferta.weaponIcon;
                fila.icono.enabled = fila.oferta.weaponIcon != null;
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
                fila.etiquetaNombre.color = colorFila;

            if (fila.etiquetaCosto != null)
                fila.etiquetaCosto.color = colorFila;

            if (fila.fondo != null)
                fila.fondo.color = fila.oferta == ofertaSeleccionada ? colorFilaSeleccionada : colorFilaNormal;
        }
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

    private bool EsSinglePlayer()
    {
        return SceneManager.GetActiveScene().name == "MainSceneSinglePlayer";
    }

    private void OnDestroy()
    {
        if (EsSinglePlayer())
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }
    }
}