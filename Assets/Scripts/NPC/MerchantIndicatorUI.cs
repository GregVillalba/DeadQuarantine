using UnityEngine;
using TMPro;
using Unity.Netcode;
using UnityEngine.InputSystem;

// HUD (2D, no afecta al modelo 3D del jugador) que muestra a cuántos metros
// está el mercader, una flecha que apunta hacia dónde caminar para encontrarlo
// y el tiempo restante de la fase de compras.
// Solo se muestra durante la fase de compras entre rondas.
// También maneja la tecla para saltar la fase de compras sin ir al mercader
// (marca "Listo" igual que el botón de la tienda) y el texto que la indica.
public class MerchantIndicatorUI : MonoBehaviour
{
    [Header("Referencias UI")]
    [SerializeField] private GameObject indicadorRoot;
    [SerializeField] private RectTransform flecha;
    [SerializeField] private TextMeshProUGUI distanciaText;

    [Header("Tiempo de la fase de compras")]
    [Tooltip("Texto debajo de la distancia. Se ve toda la fase, aunque estés al lado del mercader.")]
    [SerializeField] private TextMeshProUGUI tiempoText;
    [SerializeField] private string prefijoTiempo = "Tiempo para comprar: ";

    [Header("Saltar fase de compras")]
    [Tooltip("Texto al lado del tiempo. Si queda vacío se crea solo, copiando el estilo del texto del tiempo.")]
    [SerializeField] private TextMeshProUGUI saltarComprasText;
    [Tooltip("{0} se reemplaza por la tecla asignada a saltar compras.")]
    [SerializeField] private string formatoSaltarCompras = "{0} para saltar compras";
    [Tooltip("Multijugador: se muestra después de apretar la tecla, hasta que los demás también estén listos.")]
    [SerializeField] private string textoEsperandoCompaneros = "Esperando compañeros";
    [Tooltip("Espacio entre el final del tiempo y el texto (solo si el texto se crea solo).")]
    [SerializeField] private float separacionSaltarCompras = 20f;

    [Header("Comportamiento")]
    [SerializeField] private float distanciaParaOcultar = 3f;

    private Transform jugador;
    private Transform mercader;
    private NPCShopUI shopUI;
    private NetworkObject networkObject;
    private bool faseComprasAnterior;

    private PlayerControls controls;
    private string teclaSaltarCompras = string.Empty;
    private bool saltarComprasEnviado;
    private bool saltarComprasTextoCreado;

    private void Awake()
    {
        controls = new PlayerControls();
        ConfiguracionesJuego.CargarRebinds(controls.asset);
        ActualizarTeclaSaltarCompras();

        // ConfiguracionesJuego ya recarga las teclas de este asset; solo falta refrescar el texto.
        ConfiguracionesJuego.CambiosAplicados += ActualizarTeclaSaltarCompras;
    }

    private void OnEnable()
    {
        controls.Player.Enable();
        controls.Player.SkipShop.performed += OnSaltarComprasInput;
    }

    private void OnDisable()
    {
        controls.Player.SkipShop.performed -= OnSaltarComprasInput;
        controls.Player.Disable();
    }

    private void OnDestroy()
    {
        ConfiguracionesJuego.CambiosAplicados -= ActualizarTeclaSaltarCompras;

        if (controls != null)
        {
            controls.Dispose();
            controls = null;
        }
    }

    private void Start()
    {
        networkObject = GetComponentInParent<NetworkObject>();
        jugador = networkObject != null ? networkObject.transform : transform;

        shopUI = transform.root.GetComponentInChildren<NPCShopUI>(true);

        CrearTextoSaltarCompras();
        BuscarMercader();
    }

    private void Update()
    {
        // La flecha y el tiempo solo se muestran durante la fase de compras.
        if (RoundManager.Instance == null || !RoundManager.Instance.IsShopPhaseActive)
        {
            faseComprasAnterior = false;
            saltarComprasEnviado = false;
            MostrarIndicador(false, false);
            return;
        }

        // Al empezar cada fase de compras se vuelve a elegir el mercader (el jugador ya está en su mapa).
        if (!faseComprasAnterior)
        {
            faseComprasAnterior = true;
            saltarComprasEnviado = false;
            BuscarMercader();
        }

        // Con la tienda abierta el tiempo ya se ve dentro del panel (y la fase se termina con "Listo").
        if (shopUI != null && shopUI.EstaAbierta)
        {
            MostrarIndicador(false, false);
            return;
        }

        if (tiempoText != null)
            tiempoText.text = prefijoTiempo + FormatearTiempo(RoundManager.Instance.ShopPhaseRemaining);

        ActualizarTextoSaltarCompras();

        if (mercader == null)
            BuscarMercader();

        if (mercader == null || jugador == null)
        {
            MostrarIndicador(false, true);
            return;
        }

        Vector3 haciaMercader = mercader.position - jugador.position;
        float distancia = haciaMercader.magnitude;

        if (distancia <= distanciaParaOcultar)
        {
            MostrarIndicador(false, true);
            return;
        }

        MostrarIndicador(true, true);

        if (distanciaText != null)
            distanciaText.text = "Mercader a " + Mathf.RoundToInt(distancia) + " m";

        if (flecha != null)
        {
            Vector3 direccionPlana = haciaMercader;
            direccionPlana.y = 0f;

            Vector3 adelanteJugador = jugador.forward;
            adelanteJugador.y = 0f;

            if (direccionPlana.sqrMagnitude > 0.0001f && adelanteJugador.sqrMagnitude > 0.0001f)
            {
                float angulo = Vector3.SignedAngle(adelanteJugador, direccionPlana, Vector3.up);
                flecha.localRotation = Quaternion.Euler(0f, 0f, -angulo);
            }
        }
    }

    // indicadorVisible: flecha + distancia. tiempoVisible: texto del tiempo restante.
    private void MostrarIndicador(bool indicadorVisible, bool tiempoVisible)
    {
        if (tiempoText != null)
            SetActivo(tiempoText.gameObject, tiempoVisible);

        // Con el tiempo visible, ActualizarTextoSaltarCompras decide si se ve.
        if (!tiempoVisible && saltarComprasText != null)
            SetActivo(saltarComprasText.gameObject, false);

        if (indicadorRoot == null)
            return;

        // Si el root es el mismo objeto que tiene este script, desactivarlo
        // apagaría también el Update y no se volvería a mostrar nunca:
        // en ese caso se prenden/apagan sus hijos (menos el del tiempo).
        if (indicadorRoot == gameObject)
        {
            foreach (Transform hijo in transform)
            {
                if (tiempoText != null && hijo == tiempoText.transform)
                    continue;

                if (saltarComprasText != null && hijo == saltarComprasText.transform)
                    continue;

                SetActivo(hijo.gameObject, indicadorVisible);
            }

            return;
        }

        SetActivo(indicadorRoot, indicadorVisible);
    }

    // =========================================================
    // SALTAR FASE DE COMPRAS
    // =========================================================

    private void OnSaltarComprasInput(InputAction.CallbackContext context)
    {
        // Solo el jugador local, y no con la pausa o la tienda abiertas (ahí está el botón "Listo").
        if (networkObject != null && !networkObject.IsOwner)
            return;

        if (PlayerInputGate.ActionsBlocked)
            return;

        RoundManager roundManager = RoundManager.Instance;

        // Sin fase de compras la tecla no hace nada.
        if (roundManager == null || !roundManager.IsShopPhaseActive)
            return;

        if (saltarComprasEnviado || roundManager.JugadorLocalListoFaseCompras())
            return;

        saltarComprasEnviado = true;
        roundManager.MarcarListoFaseComprasServerRpc();

        ActualizarTextoSaltarCompras();
    }

    private void ActualizarTeclaSaltarCompras()
    {
        if (controls != null)
            teclaSaltarCompras = controls.Player.SkipShop.GetBindingDisplayString();
    }

    private void ActualizarTextoSaltarCompras()
    {
        if (saltarComprasText == null || RoundManager.Instance == null)
            return;

        string texto;

        if (saltarComprasEnviado || RoundManager.Instance.JugadorLocalListoFaseCompras())
        {
            // Solo: la fase termina enseguida, no hay a quién esperar.
            texto = HayOtrosJugadores() ? textoEsperandoCompaneros : string.Empty;
        }
        else
        {
            texto = string.Format(formatoSaltarCompras, teclaSaltarCompras);
        }

        bool visible = !string.IsNullOrEmpty(texto);
        SetActivo(saltarComprasText.gameObject, visible);

        if (!visible)
            return;

        saltarComprasText.text = texto;

        if (tiempoText != null)
            saltarComprasText.fontSize = tiempoText.fontSize;

        if (saltarComprasTextoCreado)
            UbicarTextoSaltarCompras();
    }

    // Los clientes siempre juegan acompañados; el host está solo si es el único conectado.
    private static bool HayOtrosJugadores()
    {
        NetworkManager networkManager = NetworkManager.Singleton;

        if (networkManager == null)
            return false;

        if (!networkManager.IsServer)
            return true;

        return networkManager.ConnectedClientsIds.Count > 1;
    }

    // Si no se asignó un texto en el Inspector, se clona el del tiempo (misma fuente, color y tamaño).
    private void CrearTextoSaltarCompras()
    {
        if (saltarComprasText != null || tiempoText == null)
            return;

        GameObject copia = Instantiate(tiempoText.gameObject, tiempoText.transform, false);
        copia.name = "SaltarComprasText";

        // Por si el texto del tiempo tenía hijos: la copia queda solo con el texto.
        foreach (Transform hijo in copia.transform)
            Destroy(hijo.gameObject);

        saltarComprasText = copia.GetComponent<TextMeshProUGUI>();
        saltarComprasText.enableAutoSizing = false;
        saltarComprasText.textWrappingMode = TextWrappingModes.NoWrap;
        saltarComprasText.overflowMode = TextOverflowModes.Overflow;
        saltarComprasText.horizontalAlignment = HorizontalAlignmentOptions.Left;

        RectTransform rect = saltarComprasText.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
        rect.sizeDelta = new Vector2(400f, tiempoText.rectTransform.rect.height);

        saltarComprasTextoCreado = true;
        copia.SetActive(false);
    }

    // Lo pone justo a la derecha de donde termina el texto del tiempo (que puede estar centrado).
    private void UbicarTextoSaltarCompras()
    {
        Rect rectTiempo = tiempoText.rectTransform.rect;
        float anchoTexto = Mathf.Min(tiempoText.preferredWidth, rectTiempo.width);
        float bordeDerecho;

        switch (tiempoText.horizontalAlignment)
        {
            case HorizontalAlignmentOptions.Left:
                bordeDerecho = rectTiempo.xMin + anchoTexto;
                break;
            case HorizontalAlignmentOptions.Right:
                bordeDerecho = rectTiempo.xMax;
                break;
            case HorizontalAlignmentOptions.Center:
            case HorizontalAlignmentOptions.Geometry:
                bordeDerecho = rectTiempo.center.x + anchoTexto * 0.5f;
                break;
            default:
                bordeDerecho = rectTiempo.xMax;
                break;
        }

        saltarComprasText.rectTransform.localPosition =
            new Vector3(bordeDerecho + separacionSaltarCompras, rectTiempo.center.y, 0f);
    }

    private static void SetActivo(GameObject objeto, bool activo)
    {
        if (objeto.activeSelf != activo)
            objeto.SetActive(activo);
    }

    private static string FormatearTiempo(int segundos)
    {
        segundos = Mathf.Max(0, segundos);
        return (segundos / 60).ToString("00") + ":" + (segundos % 60).ToString("00");
    }

    // Puede haber más de un mercader activo en la escena (uno por mapa). Se elige el que está dentro
    // del escenario de la partida; si ninguno lo está, el más cercano al jugador.
    private void BuscarMercader()
    {
        Transform raizEscenario = RaizEscenarioActivo();
        NPCWeaponVendor elegido = null;
        bool elegidoEnEscenario = false;
        float mejorDistancia = float.MaxValue;

        foreach (NPCWeaponVendor vendedor in FindObjectsByType<NPCWeaponVendor>(FindObjectsSortMode.None))
        {
            bool enEscenario = raizEscenario != null && vendedor.transform.IsChildOf(raizEscenario);
            float distancia = jugador != null
                ? (vendedor.transform.position - jugador.position).sqrMagnitude
                : 0f;

            bool mejor = elegido == null ||
                         (enEscenario && !elegidoEnEscenario) ||
                         (enEscenario == elegidoEnEscenario && distancia < mejorDistancia);

            if (!mejor)
                continue;

            elegido = vendedor;
            elegidoEnEscenario = enEscenario;
            mejorDistancia = distancia;
        }

        mercader = elegido != null ? elegido.transform : null;
    }

    // GestorEscenariosPartida activa el padre del escenario elegido: ese es el que contiene a su mercader.
    private static Transform RaizEscenarioActivo()
    {
        GestorEscenariosPartida gestor = FindFirstObjectByType<GestorEscenariosPartida>();
        ConfiguracionEscenarioJugable escenario = gestor != null ? gestor.ObtenerEscenarioActivo() : null;

        if (escenario == null)
            return null;

        return escenario.transform.parent != null ? escenario.transform.parent : escenario.transform;
    }
}
