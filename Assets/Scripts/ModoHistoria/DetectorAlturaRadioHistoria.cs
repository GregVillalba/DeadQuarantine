
using UnityEngine;
using UnityEngine.Events;
using Unity.Netcode;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class DetectorAlturaRadioHistoria : MonoBehaviour
{
    [Header("Altura requerida")]
    [SerializeField] private float alturaMinima = 3f;
    [SerializeField] private float tiempoEnAltura = 1.5f;

   [Header("Detección del suelo")]
    [SerializeField] private string tagSuelo = "SueloCiudad";
    [SerializeField] private float distanciaMaximaSuelo = 50f;
    [SerializeField] private float origenRaycastExtra = 0.2f;

    [Header("Evento")]
    [SerializeField] private UnityEvent alEncontrarSenal;

    private CharacterController characterController;
    private NetworkObject jugadorNetworkObject;

    private bool busquedaActiva;
    private bool senalEncontrada;
    private float tiempoEnAlturaActual;

    public bool SenalEncontrada => senalEncontrada;
    public float AlturaActual { get; private set; }

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        jugadorNetworkObject = GetComponentInParent<NetworkObject>();
    }

    private void Update()
    {
        if (!EsJugadorLocal() || !busquedaActiva || senalEncontrada)
            return;

        if (!CalcularAltura(out float altura))
        {
            AlturaActual = 0f;
            tiempoEnAlturaActual = 0f;
            return;
        }

        AlturaActual = altura;

        if (altura >= alturaMinima)
        {
            tiempoEnAlturaActual += Time.deltaTime;

            if (tiempoEnAlturaActual >= tiempoEnAltura)
            {
                senalEncontrada = true;
                busquedaActiva = false;

                Debug.Log(
                    $"[Radio] Señal encontrada. Altura: {AlturaActual:F2} m."
                );

                alEncontrarSenal?.Invoke();
            }
        }
        else
        {
            tiempoEnAlturaActual = 0f;
        }

        Debug.Log($"[Radio] Altura actual: {altura:F2} m | " +
          $"Altura requerida: {alturaMinima:F2} m");
    }

    private bool EsJugadorLocal()
    {
        // Permite usar el script también en pruebas sin Netcode.
        if (jugadorNetworkObject == null)
            return true;

        return jugadorNetworkObject.IsSpawned &&
               jugadorNetworkObject.IsOwner;
    }

    private bool CalcularAltura(out float altura)
{
    altura = 0f;

    if (characterController == null)
        return false;

    float alturaPies = characterController.bounds.min.y;

    Vector3 origen = new Vector3(
        transform.position.x,
        alturaPies + origenRaycastExtra,
        transform.position.z
    );

    RaycastHit[] impactos = Physics.RaycastAll(
        origen,
        Vector3.down,
        distanciaMaximaSuelo,
        ~0,
        QueryTriggerInteraction.Ignore
    );

    System.Array.Sort(
        impactos,
        (a, b) => a.distance.CompareTo(b.distance)
    );

    foreach (RaycastHit impacto in impactos)
    {
        if (!impacto.collider.CompareTag(tagSuelo))
            continue;

        altura = Mathf.Max(
            0f,
            impacto.distance - origenRaycastExtra
        );

        return true;
    }

    return false;
}

    public void ActivarBusquedaDeSenal()
    {
        busquedaActiva = true;
        senalEncontrada = false;
        tiempoEnAlturaActual = 0f;

        Debug.Log("[Radio] Búsqueda de señal activada.");
    }

    public void DesactivarBusquedaDeSenal()
    {
        busquedaActiva = false;
        tiempoEnAlturaActual = 0f;
    }

    public void ReiniciarBusquedaDeSenal()
    {
        busquedaActiva = false;
        senalEncontrada = false;
        tiempoEnAlturaActual = 0f;
        AlturaActual = 0f;
    }

    private void OnDrawGizmosSelected()
    {
        CharacterController cc = GetComponent<CharacterController>();

        if (cc == null)
            return;

        Vector3 origen = new Vector3(
            transform.position.x,
            cc.bounds.min.y + origenRaycastExtra,
            transform.position.z
        );

        Gizmos.color = senalEncontrada ? Color.green : Color.yellow;

        Gizmos.DrawLine(
            origen,
            origen + Vector3.down * distanciaMaximaSuelo
        );
    }
    private void LateUpdate()
{
    if (!EsJugadorLocal() || Keyboard.current == null)
        return;

    if (Keyboard.current.f8Key.wasPressedThisFrame)
        ActivarBusquedaDeSenal();
}
}
