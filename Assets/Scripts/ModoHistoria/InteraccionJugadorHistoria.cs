
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;
using TMPro;

public class InteraccionJugadorHistoria : MonoBehaviour
{
    [Header("Interacción")]
    [SerializeField] private float radioInteraccion = 3f;
    [SerializeField] private LayerMask capasInteraccion = ~0;

    [Header("HUD local del jugador")]
    [SerializeField] private TMP_Text textoTituloObjetivo;
    [SerializeField] private TMP_Text textoDescripcionObjetivo;

    [Header("Cartel de interacción")]
    [SerializeField] private GameObject interactPrompt;
    [SerializeField] private TMP_Text interactText;

    private NetworkObject jugadorNetworkObject;
    private bool hudRegistrado;

    private PlayerControls controls;
    private bool tieneObjetoEnfocado;
    private TarjetaAccesoHistoria tarjetaEnfocada;
    private LectorTarjetaHistoria lectorEnfocado;

    private void Awake()
    {
        jugadorNetworkObject = GetComponentInParent<NetworkObject>();

        controls = new PlayerControls();
        ConfiguracionesJuego.CargarRebinds(controls.asset);

        if (jugadorNetworkObject == null)
        {
            Debug.LogError(
                "[Historia] No se encontró un NetworkObject en el jugador."
            );
        }

        OcultarCartel();
    }

    private void OnEnable()
    {
        controls?.Player.Enable();
    }

    private void OnDisable()
    {
        controls?.Player.Disable();
        OcultarCartel();
    }

    private void OnDestroy()
    {
        controls?.Dispose();
    }

    private void Update()
    {
        if (jugadorNetworkObject == null ||
            !jugadorNetworkObject.IsSpawned ||
            !jugadorNetworkObject.IsOwner)
        {
            OcultarCartel();
            return;
        }

        ConfigurarHUDPorModo();

        if (ConfiguracionPartidaSeleccionada.Instancia == null ||
            ConfiguracionPartidaSeleccionada.Instancia.modoSeleccionado !=
            ConfiguracionPartidaSeleccionada.ModoJuego.Historia)
        {
            OcultarCartel();
            return;
        }

        RegistrarHUD();

        // Actualiza el cartel continuamente, independientemente
        // de si el jugador pulsa la tecla de interacción.
        ActualizarObjetoEnfocado();

        if (controls != null &&
            controls.Player.Interact.WasPressedThisFrame())
        {
            InteractuarConObjetoEnfocado();
        }
    }

    private void ActualizarObjetoEnfocado()
    {
        tarjetaEnfocada = null;
        lectorEnfocado = null;
        tieneObjetoEnfocado = false;

        if (interactPrompt == null)
            return;

        Collider[] colliders = Physics.OverlapSphere(
            transform.position,
            radioInteraccion,
            capasInteraccion,
            QueryTriggerInteraction.Collide
        );

        TarjetaAccesoHistoria tarjetaCercana = null;
        LectorTarjetaHistoria lectorCercano = null;

        float distanciaTarjeta = float.MaxValue;
        float distanciaLector = float.MaxValue;

        foreach (Collider col in colliders)
        {
            TarjetaAccesoHistoria tarjeta =
                col.GetComponentInParent<TarjetaAccesoHistoria>();

            if (tarjeta != null &&
                tarjeta.IsSpawned &&
                tarjeta.gameObject.activeInHierarchy)
            {
                float distancia = Vector3.Distance(
                    transform.position,
                    tarjeta.transform.position
                );

                if (distancia < distanciaTarjeta)
                {
                    distanciaTarjeta = distancia;
                    tarjetaCercana = tarjeta;
                }

                continue;
            }

            LectorTarjetaHistoria lector =
                col.GetComponentInParent<LectorTarjetaHistoria>();

            if (lector != null)
            {
                float distancia = Vector3.Distance(
                    transform.position,
                    lector.transform.position
                );

                if (distancia < distanciaLector)
                {
                    distanciaLector = distancia;
                    lectorCercano = lector;
                }
            }
        }

        // Si hay una tarjeta cerca y la misión todavía permite recogerla,
        // priorizamos la tarjeta sobre el lector.
        GestorHistoriaPartida gestor = GestorHistoriaPartida.Instance;

        if (gestor == null || !gestor.IsSpawned)
        {
            OcultarCartel();
            return;
        }

        if (tarjetaCercana != null &&
            gestor.EstadoMisionNetwork.Value == 1)
        {
            tarjetaEnfocada = tarjetaCercana;
            tieneObjetoEnfocado = true;

            MostrarCartel(ObtenerTecla() + " para agarrar");
            return;
        }

        if (lectorCercano != null &&
    (gestor.EstadoMisionNetwork.Value == 1 ||
     gestor.EstadoMisionNetwork.Value == 2))
{
    lectorEnfocado = lectorCercano;
    tieneObjetoEnfocado = true;

    bool tieneTarjeta =
        NetworkManager.Singleton != null &&
        gestor.ClienteConTarjetaNetwork.Value ==
        NetworkManager.Singleton.LocalClientId;

    if (tieneTarjeta &&
        gestor.EstadoMisionNetwork.Value == 2)
    {
        MostrarCartel(ObtenerTecla() + " para usar lector");
    }
    else
    {
        MostrarCartel("Requiere una tarjeta magnética");
    }

    return;
}

        OcultarCartel();
    }

    private void InteractuarConObjetoEnfocado()
    {
        if (!tieneObjetoEnfocado)
            return;

        if (tarjetaEnfocada != null &&
            tarjetaEnfocada.IsSpawned)
        {
            tarjetaEnfocada.IntentarRecoger();
            return;
        }

        if (lectorEnfocado != null)
        {
            GestorHistoriaPartida gestor = GestorHistoriaPartida.Instance;

            if (gestor == null)
                return;

            // Sin tarjeta, no se intenta abrir la puerta.
            if (gestor.ClienteConTarjetaNetwork.Value !=
                NetworkManager.Singleton.LocalClientId)
            {
                return;
            }

            lectorEnfocado.IntentarInteractuar();
        }
    }

    private string ObtenerTecla()
    {
        if (controls == null)
            return "E";

        string tecla =
            controls.Player.Interact.GetBindingDisplayString();

        return string.IsNullOrEmpty(tecla) ? "E" : tecla;
    }

    private void MostrarCartel(string mensaje)
    {
        if (interactPrompt == null)
            return;

        if (interactText != null)
            interactText.text = mensaje;

        if (!interactPrompt.activeSelf)
            interactPrompt.SetActive(true);
    }

    private void OcultarCartel()
    {
        tieneObjetoEnfocado = false;
        tarjetaEnfocada = null;
        lectorEnfocado = null;

        if (interactPrompt != null &&
            interactPrompt.activeSelf)
        {
            interactPrompt.SetActive(false);
        }
    }

    private void RegistrarHUD()
    {
        if (hudRegistrado)
            return;

        GestorHistoriaPartida gestor = GestorHistoriaPartida.Instance;

        if (gestor == null || !gestor.IsSpawned)
            return;

        Transform hud = BuscarHijo(transform, "HistoriaHUD");

        if (hud == null)
        {
            Debug.LogError(
                "[Historia] No se encontró HistoriaHUD en el jugador."
            );
            return;
        }

        TMP_Text[] textos = hud.GetComponentsInChildren<TMP_Text>(true);

        TMP_Text titulo = null;
        TMP_Text descripcion = null;

        foreach (TMP_Text texto in textos)
        {
            if (texto.name == "Objetivo")
                titulo = texto;
            else if (texto.name == "Descripcion")
                descripcion = texto;
        }

        if (titulo == null || descripcion == null)
        {
            Debug.LogError(
                "[Historia] Faltan los textos Objetivo o Descripcion."
            );
            return;
        }

        hud.gameObject.SetActive(true);

        gestor.RegistrarHUDLocal(titulo, descripcion);

        hudRegistrado = true;
    }

    private Transform BuscarHijo(Transform padre, string nombre)
    {
        foreach (Transform hijo in padre)
        {
            if (hijo.name == nombre)
                return hijo;

            Transform encontrado = BuscarHijo(hijo, nombre);

            if (encontrado != null)
                return encontrado;
        }

        return null;
    }

    private void ConfigurarHUDPorModo()
    {
        Transform hud = BuscarHijo(transform, "HistoriaHUD");

        if (hud == null)
            return;

        bool esHistoria =
            ConfiguracionPartidaSeleccionada.Instancia != null &&
            ConfiguracionPartidaSeleccionada.Instancia.modoSeleccionado ==
            ConfiguracionPartidaSeleccionada.ModoJuego.Historia;

        hud.gameObject.SetActive(esHistoria);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, radioInteraccion);
    }
}
