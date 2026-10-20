
using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;
using TMPro;

public class InteraccionJugadorHistoria : MonoBehaviour
{
    [Header("Interacción")]
    [SerializeField] private float radioInteraccion = 3f;
    [SerializeField] private LayerMask capasInteraccion = ~0;

    [Header("HUD local del jugador")]
    [SerializeField] private TMP_Text textoTituloObjetivo;
    [SerializeField] private TMP_Text textoDescripcionObjetivo;

    private NetworkObject jugadorNetworkObject;
    private bool hudRegistrado;

    private void Awake()
{
    jugadorNetworkObject = GetComponentInParent<NetworkObject>();

    if (jugadorNetworkObject == null)
    {
        Debug.LogError(
            "[Historia] No se encontró un NetworkObject en el objeto " +
            "ni en sus padres. Revisá el prefab del jugador."
        );
    }
}

    private void Update()
    {
        // Solo el jugador local puede usar la interacción.
        if (jugadorNetworkObject == null ||
            !jugadorNetworkObject.IsSpawned ||
            !jugadorNetworkObject.IsOwner)
        {
            return;
        }

        RegistrarHUD();

        if (Keyboard.current == null ||
            !Keyboard.current.eKey.wasPressedThisFrame)
        {
            return;
        }

        InteractuarConObjetoCercano();
    }
private void RegistrarHUD()
{
    if (hudRegistrado)
        return;

    GestorHistoriaPartida gestor = GestorHistoriaPartida.Instance;

    if (gestor == null || !gestor.IsSpawned)
        return;

    if (ConfiguracionPartidaSeleccionada.Instancia == null ||
        ConfiguracionPartidaSeleccionada.Instancia.modoSeleccionado !=
        ConfiguracionPartidaSeleccionada.ModoJuego.Historia)
    {
        return;
    }

    Transform hud = BuscarHijo(transform, "HistoriaHUD");

    if (hud == null)
    {
        Debug.LogError("[Historia] No se encontró HistoriaHUD en el jugador.");
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
            "[Historia] Faltan los textos Objetivo o Descripcion en HistoriaHUD."
        );
        return;
    }

    hud.gameObject.SetActive(true);

    gestor.RegistrarHUDLocal(titulo, descripcion);

    hudRegistrado = true;

    Debug.Log("[Historia] HUD registrado correctamente.");
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
private string ObtenerRuta(Transform elemento)
{
    string ruta = elemento.name;

    while (elemento.parent != null)
    {
        elemento = elemento.parent;

        if (elemento == transform)
            break;

        ruta = elemento.name + "/" + ruta;
    }

    return ruta;
}

    private void InteractuarConObjetoCercano()
    {
        Collider[] colliders = Physics.OverlapSphere(
            transform.position,
            radioInteraccion,
            capasInteraccion,
            QueryTriggerInteraction.Collide
        );

        // 1. Buscar una tarjeta cercana.
        foreach (Collider col in colliders)
        {
            TarjetaAccesoHistoria tarjeta =
                col.GetComponentInParent<TarjetaAccesoHistoria>();

            if (tarjeta == null)
                continue;

            if (!tarjeta.IsSpawned)
                continue;

            Debug.Log("[Historia] Solicitando recoger tarjeta.");

            tarjeta.IntentarRecoger();
            return;
        }

        // 2. Si no hay tarjeta, buscar un lector cercano.
        foreach (Collider col in colliders)
        {
            LectorTarjetaHistoria lector =
                col.GetComponentInParent<LectorTarjetaHistoria>();

            if (lector == null)
                continue;

            Debug.Log("[Historia] Solicitando usar lector de tarjeta.");

            lector.IntentarInteractuar();
            return;
        }

        Debug.Log("[Historia] No hay objetos interactuables cerca.");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, radioInteraccion);
    }
}
