using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

/// <summary>
/// Torreta automática colocada por un jugador (ver ColocadorObjetos). Va en la raíz del prefab, junto al NetworkObject.
///
/// El servidor elige el zombie vivo más cercano dentro del rango y a la vista (sin paredes en el medio), que ya terminó
/// de salir de la tierra, y le dispara sin
/// parar (munición infinita, sin recarga) mientras siga vivo, en rango y a la vista; después busca el siguiente.
/// El objetivo se sincroniza para que en todos los clientes
/// el cabezal gire hacia el mismo zombie y se vean los disparos. El daño lo hace solo el servidor, a nombre del
/// jugador que la colocó (las kills y los puntos son suyos).
///
/// Los zombies también la atacan (ZombieAI). Con la vida en 0 queda rota en el lugar: deja de disparar, baja el cañón y
/// echa humo negro. Su dueño la puede reparar en la tienda apenas recibe daño (PlayerRecursos.RepararTorretaServerRpc).
/// Cada jugador puede tener una sola.
/// </summary>
public class TorretaAutomatica : NetworkBehaviour
{
    private const ulong SinObjetivo = ulong.MaxValue;
    private const ulong SinDueno = ulong.MaxValue;

    [Header("Ataque")]
    [Tooltip("Rango de detección (m).")]
    [SerializeField] private float rango = 120f;
    [Tooltip("Daño por disparo.")]
    [SerializeField] private int dano = 50;
    [Tooltip("Segundos entre disparos.")]
    [SerializeField] private float cadencia = 0.1f;
    [Tooltip("Cada cuánto busca un zombie mientras no tiene objetivo (s).")]
    [SerializeField] private float intervaloBusqueda = 0.25f;
    [Tooltip("Capas que tapan los disparos (paredes, casas, árboles...). Los zombies, los jugadores y la propia torreta nunca tapan.")]
    [SerializeField] private LayerMask capasBloqueanDisparo = ~(1 << 2);

    [Header("Vida")]
    [SerializeField] private int vidaMaxima = 300;

    [Header("Tutorial")]
    [Tooltip("Vida en el tutorial: baja a propósito para que un zombie la rompa y el jugador aprenda a repararla.")]
    [SerializeField, Min(1)] private int vidaMaximaTutorial = 30;
    [Tooltip("Daño por disparo en el tutorial: más bajo, para que algún zombie llegue a pegarle.")]
    [SerializeField, Min(1)] private int danoTutorial = 25;

    [Header("Barra de vida flotante (como la de los zombies)")]
    [SerializeField] private Slider barraVida;
    [SerializeField] private Image rellenoBarra;
    [Tooltip("Canvas de la barra. Se oculta mientras la torreta está rota.")]
    [SerializeField] private GameObject canvasBarra;
    [SerializeField] private Color colorVidaAlta = Color.green;
    [SerializeField] private Color colorVidaMedia = Color.yellow;
    [SerializeField] private Color colorVidaBaja = Color.red;

    [Header("Rota")]
    [Tooltip("Humo que sale mientras está rota (ej: PolygonApocalypse/Prefabs/FX/Prefabbed/FX_Smoke_Black_01).")]
    [SerializeField] private GameObject efectoRota;
    [Tooltip("Opcional. De dónde sale el humo. Vacío = del pivote de giro del cañón.")]
    [SerializeField] private Transform puntoHumo;
    [Tooltip("Pinta todas las partículas del humo de este color (algunas del efecto original salen grises claras).")]
    [SerializeField] private bool forzarColorHumo = true;
    [SerializeField] private Color colorHumo = new Color(0.06f, 0.06f, 0.06f, 1f);
    [Tooltip("Qué tan rápido cae el cañón al romperse, comparado con la velocidad de giro normal.")]
    [SerializeField] private float velocidadCaidaCanon = 0.3f;

    [Header("Cabezal")]
    [Tooltip("Parte que gira a los costados (en el AA Gun: SM_Wep_AAGun_Swivel_01).")]
    [SerializeField] private Transform pivoteGiro;
    [Tooltip("Parte que sube y baja el cañón (en el AA Gun: SM_Wep_AAGun_Body_01). Vacío = el pivote de giro hace las dos cosas.")]
    [SerializeField] private Transform pivoteElevacion;
    [Tooltip("Punta del cañón, con el eje Z (azul) apuntando hacia afuera del caño. Es la dirección que se alinea con el zombie.")]
    [SerializeField] private Transform bocaCanon;
    [Tooltip("Grados por segundo.")]
    [SerializeField] private float velocidadGiro = 360f;
    [Tooltip("También es lo más abajo que queda el cañón cuando la torreta se rompe.")]
    [SerializeField] private float elevacionMinima = -80f;
    [SerializeField] private float elevacionMaxima = 85f;
    [Tooltip("Dispara cuando el cañón está a menos de estos grados del zombie.")]
    [SerializeField] private float toleranciaApuntado = 8f;
    [Tooltip("Altura a la que apunta si el zombie no tiene collider en la raíz (m).")]
    [SerializeField] private float alturaApuntado = 1.2f;

    [Header("Efectos (opcionales)")]
    [Tooltip("Muzzle con el mismo fogonazo y luz que las armas del jugador.")]
    [SerializeField] private Muzzle fogonazo;
    [Tooltip("Estela de la bala (el mismo prefab con BulletTrail que usan las armas).")]
    [SerializeField] private GameObject estelaBala;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip sonidoDisparo;

    // NetworkObjectId del zombie al que apunta (SinObjetivo = ninguno).
    private readonly NetworkVariable<ulong> objetivoNetwork =
        new NetworkVariable<ulong>(
            SinObjetivo,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    // Jugador que la colocó: la tienda y el cartel de reparar lo leen en cada cliente.
    private readonly NetworkVariable<ulong> duenoNetwork =
        new NetworkVariable<ulong>(
            SinDueno,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    // Arranca en 1 (sana) hasta que el servidor la pone en vidaMaxima al spawnear.
    private readonly NetworkVariable<int> vidaNetwork =
        new NetworkVariable<int>(
            1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    // Torretas spawneadas en la partida: las recorren los zombies (para atacarlas) y la tienda (la de cada jugador).
    private static readonly List<TorretaAutomatica> torretas = new List<TorretaAutomatica>();

    // Con "Enter Play Mode Options" (sin recargar dominio) los estaticos sobreviven entre partidas.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        torretas.Clear();
    }

    public static IReadOnlyList<TorretaAutomatica> Todas => torretas;

    /// <summary>La torreta de ese jugador (rota o no), o null si no colocó ninguna.</summary>
    public static TorretaAutomatica DelJugador(ulong clientId)
    {
        foreach (TorretaAutomatica torreta in torretas)
        {
            if (torreta != null && torreta.EsDe(clientId))
                return torreta;
        }

        return null;
    }

    // En el servidor también vale el dueño asignado antes de spawnear (por si la variable de red todavía no se escribió).
    private bool EsDe(ulong clientId)
    {
        if (IsServer && duenoAsignado && duenoClientId == clientId)
            return true;

        return IsSpawned && Dueno == clientId;
    }

    public ulong Dueno => duenoNetwork.Value;
    public bool EstaRota => vidaNetwork.Value <= 0;

    /// <summary>Recibió daño (o está rota): su dueño la puede reparar en la tienda.</summary>
    public bool NecesitaReparacion => vidaNetwork.Value < VidaMaxima;

    /// <summary>Vida máxima en esta partida (en el tutorial es menor, ver vidaMaximaTutorial).</summary>
    public int VidaMaxima => TutorialEscena.EsActiva ? vidaMaximaTutorial : vidaMaxima;

    private int Dano => TutorialEscena.EsActiva ? danoTutorial : dano;

    // Solo servidor.
    private ulong duenoClientId;
    private bool duenoAsignado;
    private float proximaBusqueda;

    private ZombieHealth objetivo;
    private Collider colliderObjetivo;
    private ulong idObjetivoCacheado = SinObjetivo;
    private float proximoDisparo;

    private readonly RaycastHit[] impactosVision = new RaycastHit[16];
    private Collider[] collidersPropios;
    private GameObject humoActivo;

    /// <summary>Solo servidor, antes de spawnearla: el jugador que la colocó.</summary>
    public void AsignarDueno(ulong clientId)
    {
        duenoClientId = clientId;
        duenoAsignado = true;

        // Cuenta desde ya para el límite de una por jugador, aunque todavía no terminó de spawnear.
        if (!torretas.Contains(this))
            torretas.Add(this);
    }

    private void Awake()
    {
        List<Collider> solidos = new List<Collider>();

        foreach (Collider propio in GetComponentsInChildren<Collider>(true))
        {
            if (!propio.isTrigger)
                solidos.Add(propio);
        }

        collidersPropios = solidos.ToArray();
    }

    public override void OnNetworkSpawn()
    {
        if (!torretas.Contains(this))
            torretas.Add(this);

        vidaNetwork.OnValueChanged += OnVidaCambiada;

        if (IsServer)
        {
            duenoNetwork.Value = duenoClientId;
            vidaNetwork.Value = VidaMaxima;
        }

        ActualizarBarraDeVida();
        ActualizarHumo();
    }

    public override void OnNetworkDespawn()
    {
        torretas.Remove(this);
        vidaNetwork.OnValueChanged -= OnVidaCambiada;
        ApagarHumo();
    }

    public override void OnDestroy()
    {
        torretas.Remove(this);
        base.OnDestroy();
    }

    private void Update()
    {
        if (!IsSpawned)
            return;

        // Rota: no busca ni dispara, solo deja caer el cañón.
        if (EstaRota)
        {
            if (IsServer && objetivoNetwork.Value != SinObjetivo)
                objetivoNetwork.Value = SinObjetivo;

            BajarCanon();
            return;
        }

        if (IsServer)
            ActualizarObjetivoServidor();

        ZombieHealth actual = ResolverObjetivo();

        if (actual == null)
            return;

        Vector3 punto = PuntoDeApuntado();
        bool apuntado = Apuntar(punto);

        if (!apuntado)
        {
            proximoDisparo = Mathf.Max(proximoDisparo, Time.time);
            return;
        }

        // Venía sin disparar: arranca ya, sin "recuperar" los disparos perdidos.
        if (proximoDisparo < Time.time - cadencia)
            proximoDisparo = Time.time;

        // Con frames de más de 0.1 s dispara varias veces en el mismo frame, para respetar la cadencia.
        int disparos = 0;

        while (Time.time >= proximoDisparo && disparos < 3)
        {
            proximoDisparo += Mathf.Max(0.01f, cadencia);
            disparos++;

            Disparar(actual, punto);

            if (actual.IsDead)
                break;
        }
    }

    // =========================================================
    // VIDA (servidor)
    // =========================================================

    /// <summary>Solo servidor. Golpe de un zombie (ZombieAI). Con la vida en 0 la torreta queda rota.</summary>
    public void RecibirDanoZombie(int cantidad)
    {
        if (!IsServer || EstaRota || cantidad <= 0)
            return;

        vidaNetwork.Value = Mathf.Max(0, vidaNetwork.Value - cantidad);
    }

    /// <summary>Solo servidor. La deja con la vida llena (dañada o rota) y vuelve a funcionar (la cobra PlayerRecursos).</summary>
    public void Reparar()
    {
        if (!IsServer || !NecesitaReparacion)
            return;

        vidaNetwork.Value = VidaMaxima;
    }

    /// <summary>Punto de la torreta más cercano a 'desde', para que los zombies midan si llegan a pegarle.</summary>
    public Vector3 PuntoMasCercano(Vector3 desde)
    {
        Vector3 masCercano = transform.position;
        float menorDistancia = float.PositiveInfinity;

        foreach (Collider propio in collidersPropios)
        {
            if (propio == null || !propio.enabled)
                continue;

            Vector3 punto = propio.bounds.ClosestPoint(desde);
            float distancia = (punto - desde).sqrMagnitude;

            if (distancia < menorDistancia)
            {
                menorDistancia = distancia;
                masCercano = punto;
            }
        }

        return masCercano;
    }

    private void OnVidaCambiada(int anterior, int actual)
    {
        ActualizarBarraDeVida();
        ActualizarHumo();
    }

    private void ActualizarBarraDeVida()
    {
        int vida = vidaNetwork.Value;
        float porcentaje = VidaMaxima > 0 ? (float)vida / VidaMaxima : 0f;

        if (canvasBarra != null && canvasBarra.activeSelf == EstaRota)
            canvasBarra.SetActive(!EstaRota);

        if (barraVida != null)
        {
            barraVida.maxValue = VidaMaxima;
            barraVida.value = vida;
        }

        if (rellenoBarra != null)
        {
            rellenoBarra.fillAmount = porcentaje;
            rellenoBarra.color = porcentaje > 0.66f ? colorVidaAlta : porcentaje > 0.33f ? colorVidaMedia : colorVidaBaja;
        }
    }

    // =========================================================
    // ROTA: HUMO Y CAÑÓN
    // =========================================================

    private void ActualizarHumo()
    {
        if (EstaRota)
            PrenderHumo();
        else
            ApagarHumo();
    }

    // Sale derecho hacia arriba aunque la torreta esté girada, y el humo no sigue al cañón (espacio del mundo).
    private void PrenderHumo()
    {
        if (humoActivo != null || efectoRota == null)
            return;

        Vector3 origen = puntoHumo != null
            ? puntoHumo.position
            : pivoteGiro != null ? pivoteGiro.position : transform.position + Vector3.up;

        humoActivo = Instantiate(efectoRota, origen, efectoRota.transform.rotation, transform);

        foreach (ParticleSystem particulas in humoActivo.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = particulas.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            if (forzarColorHumo)
                main.startColor = colorHumo;
        }
    }

    // Deja de echar humo; el que ya salió se disipa solo.
    private void ApagarHumo()
    {
        if (humoActivo == null)
            return;

        humoActivo.transform.SetParent(null, true);

        float duracion = 0f;

        foreach (ParticleSystem particulas in humoActivo.GetComponentsInChildren<ParticleSystem>(true))
        {
            particulas.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            duracion = Mathf.Max(duracion, particulas.main.startLifetime.constantMax);
        }

        Destroy(humoActivo, duracion + 0.5f);
        humoActivo = null;
    }

    // El cañón cae hacia el piso (hasta elevacionMinima) mirando hacia donde quedó.
    private void BajarCanon()
    {
        Vector3 frente = Vector3.ProjectOnPlane(DireccionCanon(), transform.up);

        if (frente.sqrMagnitude < 0.0001f)
            frente = transform.forward;

        Vector3 abajo = OrigenDisparo() + frente.normalized * 0.01f - transform.up * 10f;
        Apuntar(abajo, velocidadCaidaCanon);
    }

    // =========================================================
    // OBJETIVO (servidor)
    // =========================================================

    private void ActualizarObjetivoServidor()
    {
        ZombieHealth actual = ResolverObjetivo();

        if (actual != null && EsObjetivoValido(actual))
            return;

        // Si tenía objetivo (murió, salió del rango o quedó detrás de una pared) busca el siguiente en el mismo frame;
        // sin objetivo busca cada intervaloBusqueda.
        if (objetivoNetwork.Value == SinObjetivo && Time.time < proximaBusqueda)
            return;

        proximaBusqueda = Time.time + intervaloBusqueda;

        ZombieHealth nuevo = BuscarZombieMasCercano();
        objetivoNetwork.Value = nuevo != null ? nuevo.NetworkObjectId : SinObjetivo;
    }

    // El más cercano que esté a la vista: se revisan de más cerca a más lejos hasta encontrar uno sin pared en el medio.
    private ZombieHealth BuscarZombieMasCercano()
    {
        Vector3 origen = OrigenDisparo();
        List<(float distancia, ZombieHealth zombie, Vector3 punto)> candidatos = new List<(float, ZombieHealth, Vector3)>();

        foreach (ZombieHealth zombie in FindObjectsByType<ZombieHealth>(FindObjectsSortMode.None))
        {
            if (zombie == null || zombie.IsDead || !zombie.IsSpawned || EstaApareciendo(zombie))
                continue;

            Vector3 punto = PuntoDeApuntado(zombie, zombie.GetComponent<Collider>());
            float distancia = (punto - origen).sqrMagnitude;

            if (distancia <= rango * rango)
                candidatos.Add((distancia, zombie, punto));
        }

        candidatos.Sort((a, b) => a.distancia.CompareTo(b.distancia));

        foreach (var candidato in candidatos)
        {
            if (HayLineaDeVision(candidato.punto))
                return candidato.zombie;
        }

        return null;
    }

    // Mientras sale de la tierra no se le dispara: recién cuando termina la animación y empieza a caminar.
    private static bool EstaApareciendo(ZombieHealth zombie)
    {
        ZombieAI ia = zombie.GetComponent<ZombieAI>();
        return ia != null && ia.EstaApareciendo;
    }

    private bool EsObjetivoValido(ZombieHealth zombie)
    {
        if (zombie == null || !zombie.IsSpawned || zombie.IsDead || EstaApareciendo(zombie))
            return false;

        Vector3 punto = PuntoDeApuntado();

        return (punto - OrigenDisparo()).sqrMagnitude <= rango * rango &&
               HayLineaDeVision(punto);
    }

    // Nada de capasBloqueanDisparo entre la boca del cañón y el zombie.
    // No cuentan la propia torreta (al tirar hacia abajo pasa por la plataforma), los zombies ni los jugadores.
    private bool HayLineaDeVision(Vector3 punto)
    {
        Vector3 origen = OrigenDisparo();
        Vector3 haciaPunto = punto - origen;
        float distancia = haciaPunto.magnitude;

        if (distancia < 0.01f)
            return true;

        int cantidad = Physics.RaycastNonAlloc(
            origen,
            haciaPunto / distancia,
            impactosVision,
            distancia,
            capasBloqueanDisparo,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < cantidad; i++)
        {
            Collider impacto = impactosVision[i].collider;

            if (impacto == null || impacto.transform.IsChildOf(transform))
                continue;

            if (impacto.GetComponentInParent<ZombieHealth>() != null ||
                impacto.GetComponentInParent<PlayerHealth>() != null)
                continue;

            return false;
        }

        return true;
    }

    // Traduce el id sincronizado al zombie (en el servidor y en los clientes). Se cachea mientras no cambie.
    private ZombieHealth ResolverObjetivo()
    {
        ulong id = objetivoNetwork.Value;

        if (id != idObjetivoCacheado)
        {
            idObjetivoCacheado = id;
            objetivo = null;
            colliderObjetivo = null;

            if (id != SinObjetivo &&
                NetworkManager != null &&
                NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(id, out NetworkObject objeto))
            {
                objetivo = objeto.GetComponent<ZombieHealth>();
                colliderObjetivo = objeto.GetComponent<Collider>();
            }
        }

        if (objetivo == null || objetivo.IsDead)
            return null;

        return objetivo;
    }

    private Vector3 PuntoDeApuntado()
    {
        return PuntoDeApuntado(objetivo, colliderObjetivo);
    }

    // Centro del collider del zombie (sirve igual para el perro, que es más bajo).
    private Vector3 PuntoDeApuntado(ZombieHealth zombie, Collider colliderZombie)
    {
        if (colliderZombie != null && colliderZombie.enabled)
            return colliderZombie.bounds.center;

        return zombie.transform.position + Vector3.up * alturaApuntado;
    }

    // =========================================================
    // CABEZAL
    // =========================================================

    // Gira de a poco hacia el punto. Funciona con cualquier orientación del modelo:
    // usa la dirección real del cañón (bocaCanon) en vez de los ejes del FBX.
    private bool Apuntar(Vector3 punto, float multiplicadorVelocidad = 1f)
    {
        Transform giro = pivoteGiro != null ? pivoteGiro : pivoteElevacion;

        // Sin cabezal configurado: dispara igual.
        if (giro == null)
            return true;

        Transform elevacion = pivoteElevacion != null ? pivoteElevacion : giro;
        Vector3 arriba = transform.up;
        Vector3 haciaObjetivo = punto - OrigenDisparo();
        float paso = velocidadGiro * multiplicadorVelocidad * Time.deltaTime;

        // Giro a los costados.
        Vector3 frentePlano = Vector3.ProjectOnPlane(DireccionCanon(), arriba);
        Vector3 objetivoPlano = Vector3.ProjectOnPlane(haciaObjetivo, arriba);

        if (frentePlano.sqrMagnitude > 0.0001f && objetivoPlano.sqrMagnitude > 0.0001f)
        {
            float angulo = Vector3.SignedAngle(frentePlano, objetivoPlano, arriba);
            giro.rotation = Quaternion.AngleAxis(Mathf.Clamp(angulo, -paso, paso), arriba) * giro.rotation;
        }

        // Elevación, alrededor del eje "derecha" del cañón.
        Vector3 frente = DireccionCanon();
        Vector3 derecha = Vector3.Cross(arriba, Vector3.ProjectOnPlane(frente, arriba));

        if (derecha.sqrMagnitude > 0.0001f)
        {
            float elevacionActual = 90f - Vector3.Angle(arriba, frente);
            float elevacionDeseada = Mathf.Clamp(90f - Vector3.Angle(arriba, haciaObjetivo), elevacionMinima, elevacionMaxima);
            float delta = Mathf.Clamp(elevacionDeseada - elevacionActual, -paso, paso);

            // Rotar positivo alrededor de "derecha" baja el cañón: por eso el signo.
            elevacion.rotation = Quaternion.AngleAxis(-delta, derecha.normalized) * elevacion.rotation;
        }

        return Vector3.Angle(DireccionCanon(), haciaObjetivo) <= toleranciaApuntado;
    }

    private Vector3 DireccionCanon()
    {
        if (bocaCanon != null)
            return bocaCanon.forward;

        if (pivoteElevacion != null)
            return pivoteElevacion.forward;

        return pivoteGiro != null ? pivoteGiro.forward : transform.forward;
    }

    private Vector3 OrigenDisparo()
    {
        if (bocaCanon != null)
            return bocaCanon.position;

        if (pivoteElevacion != null)
            return pivoteElevacion.position;

        return pivoteGiro != null ? pivoteGiro.position : transform.position;
    }

    // =========================================================
    // DISPARO
    // =========================================================

    private void Disparar(ZombieHealth zombie, Vector3 punto)
    {
        if (IsServer)
        {
            Vector3 direccion = (punto - OrigenDisparo()).normalized;

            // Sin hit marker: el dueño puede estar en otro lado del mapa.
            zombie.RecibirDanoServidor(Dano, punto, -direccion, duenoClientId, true, false);
        }

        // Efectos en cada cliente (y en el host), sin RPC por disparo. Igual que las armas: fogonazo con luz y estela.
        if (IsClient)
        {
            if (fogonazo != null)
                fogonazo.PlayEffect();

            if (estelaBala != null)
            {
                GameObject estela = Instantiate(estelaBala, OrigenDisparo(), Quaternion.identity);
                BulletTrail trail = estela.GetComponent<BulletTrail>();

                if (trail != null)
                    trail.Init(punto);
                else
                    Destroy(estela, 1f);
            }

            if (audioSource != null && sonidoDisparo != null)
                audioSource.PlayOneShot(sonidoDisparo);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, rango);

        if (bocaCanon != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawRay(bocaCanon.position, bocaCanon.forward * 3f);
        }
    }
}
