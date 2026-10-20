using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Recurso que suelta un zombie al morir. No usa la tecla de interacción: el jugador vivo más cercano
/// que pase a menos de radioAtraccion lo atrae suavemente y se suma solo a su inventario al llegarle.
/// Si nadie lo junta en segundosEnElPiso, desaparece (parpadea un poco antes).
/// El servidor decide la recolección; cada cliente anima el movimiento por su cuenta.
/// </summary>
public class RecursoDrop : NetworkBehaviour
{
    [Header("Modelos (se muestra el del recurso que toca)")]
    [SerializeField] private GameObject modeloPilaDeLatas;
    [SerializeField] private GameObject modeloCantimplora;
    [SerializeField] private GameObject modeloBidon;

    [Tooltip("Hijo que gira, flota y parpadea. El objeto raíz es el que se mueve hacia el jugador.")]
    [SerializeField] private Transform visual;

    [Header("Atracción")]
    [Tooltip("Distancia (m) a la que un jugador vivo empieza a atraer el recurso.")]
    [SerializeField] private float radioAtraccion = 3f;
    [Tooltip("Distancia (m) al jugador a la que el recurso se suma al inventario.")]
    [SerializeField] private float radioRecoleccion = 0.6f;
    [Tooltip("Velocidad (m/s) con la que arranca a moverse en el borde del radio de atracción.")]
    [SerializeField] private float velocidadAtraccion = 3f;
    [Tooltip("Velocidad (m/s) cuando ya está pegado al jugador: acelera a medida que se acerca.")]
    [SerializeField] private float velocidadAtraccionMaxima = 12f;
    [Tooltip("Altura desde los pies del jugador a la que vuela el recurso.")]
    [SerializeField] private float alturaObjetivo = 0.9f;

    [Header("Tiempo en el piso")]
    [SerializeField] private float segundosEnElPiso = 60f;
    [Tooltip("Últimos segundos en los que parpadea antes de desaparecer.")]
    [SerializeField] private float segundosParpadeo = 5f;

    [Header("Animación")]
    [SerializeField] private float velocidadGiro = 90f;
    [SerializeField] private float alturaFlotacion = 0.08f;
    [SerializeField] private float velocidadFlotacion = 2f;

    public NetworkVariable<int> TipoNetwork = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<int> CantidadNetwork = new NetworkVariable<int>(
        1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public TipoRecurso Tipo => (TipoRecurso)TipoNetwork.Value;
    public int Cantidad => CantidadNetwork.Value;
    public bool Recolectado { get; private set; }

    // Lo carga el servidor antes de Spawn; se pasa a las NetworkVariables en OnNetworkSpawn
    // (así llega a los clientes con el primer sincronizado).
    private TipoRecurso tipoInicial;
    private int cantidadInicial = 1;

    private float tiempoEnElPiso;
    private Vector3 posicionVisualInicial;

    public float RadioAtraccion => radioAtraccion;
    public float SegundosEnElPiso => segundosEnElPiso;

    private void Awake()
    {
        if (visual != null)
            posicionVisualInicial = visual.localPosition;
    }

    /// <summary>Solo servidor, antes de NetworkObject.Spawn().</summary>
    public void Configurar(TipoRecurso tipo, int cantidad)
    {
        tipoInicial = tipo;
        cantidadInicial = Mathf.Max(1, cantidad);
        MostrarModelo(tipo);
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            TipoNetwork.Value = (int)tipoInicial;
            CantidadNetwork.Value = cantidadInicial;
        }

        tiempoEnElPiso = 0f;
        MostrarModelo(Tipo);
    }

    private void Update()
    {
        if (!IsSpawned || Recolectado)
            return;

        tiempoEnElPiso += Time.deltaTime;
        Animar();

        PlayerRecursos jugador = JugadorQueLoAtrae(out Vector3 objetivo);

        if (jugador != null)
        {
            float distancia = Vector3.Distance(transform.position, objetivo);
            float cercania = 1f - Mathf.Clamp01(distancia / Mathf.Max(0.01f, radioAtraccion));
            float velocidad = Mathf.Lerp(velocidadAtraccion, velocidadAtraccionMaxima, cercania);

            transform.position = Vector3.MoveTowards(transform.position, objetivo, velocidad * Time.deltaTime);

            if (IsServer && Vector3.Distance(transform.position, objetivo) <= radioRecoleccion)
            {
                Recolectar(jugador);
                return;
            }
        }

        if (IsServer && tiempoEnElPiso >= segundosEnElPiso)
            NetworkObject.Despawn(true);
    }

    /// <summary>
    /// El jugador vivo más cercano dentro del radio de atracción (o null).
    /// objetivo = el punto de su cuerpo hacia el que vuela el recurso.
    /// </summary>
    public PlayerRecursos JugadorQueLoAtrae(out Vector3 objetivo)
    {
        PlayerRecursos elegido = null;
        float mejorDistancia = radioAtraccion;
        objetivo = transform.position;

        foreach (PlayerRecursos jugador in PlayerRecursos.Jugadores)
        {
            if (jugador == null || !jugador.PuedeRecolectar)
                continue;

            Vector3 punto = jugador.transform.position + Vector3.up * alturaObjetivo;
            float distancia = Vector3.Distance(transform.position, punto);

            if (distancia <= mejorDistancia)
            {
                mejorDistancia = distancia;
                elegido = jugador;
                objetivo = punto;
            }
        }

        return elegido;
    }

    // Solo servidor.
    private void Recolectar(PlayerRecursos jugador)
    {
        Recolectado = true;
        jugador.SumarRecurso(Tipo, Cantidad);
        NetworkObject.Despawn(true);
    }

    private void Animar()
    {
        if (visual == null)
            return;

        visual.localRotation = Quaternion.Euler(0f, tiempoEnElPiso * velocidadGiro, 0f);
        visual.localPosition = posicionVisualInicial +
                               Vector3.up * (Mathf.Sin(tiempoEnElPiso * velocidadFlotacion) * alturaFlotacion);

        // Parpadeo de aviso antes de desaparecer (8 cambios por segundo).
        bool parpadeando = segundosEnElPiso - tiempoEnElPiso <= segundosParpadeo;
        bool visible = !parpadeando || Mathf.FloorToInt(tiempoEnElPiso * 8f) % 2 == 0;

        if (visual.gameObject.activeSelf != visible)
            visual.gameObject.SetActive(visible);
    }

    private void MostrarModelo(TipoRecurso tipo)
    {
        if (modeloPilaDeLatas != null)
            modeloPilaDeLatas.SetActive(tipo == TipoRecurso.PilaDeLatas);

        if (modeloCantimplora != null)
            modeloCantimplora.SetActive(tipo == TipoRecurso.Cantimplora);

        if (modeloBidon != null)
            modeloBidon.SetActive(tipo == TipoRecurso.Bidon);
    }
}
