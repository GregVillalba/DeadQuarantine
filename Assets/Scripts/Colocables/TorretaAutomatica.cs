using System.Collections.Generic;
using UnityEngine;
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
/// </summary>
public class TorretaAutomatica : NetworkBehaviour
{
    private const ulong SinObjetivo = ulong.MaxValue;

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

    [Header("Cabezal")]
    [Tooltip("Parte que gira a los costados (en el AA Gun: SM_Wep_AAGun_Swivel_01).")]
    [SerializeField] private Transform pivoteGiro;
    [Tooltip("Parte que sube y baja el cañón (en el AA Gun: SM_Wep_AAGun_Body_01). Vacío = el pivote de giro hace las dos cosas.")]
    [SerializeField] private Transform pivoteElevacion;
    [Tooltip("Punta del cañón, con el eje Z (azul) apuntando hacia afuera del caño. Es la dirección que se alinea con el zombie.")]
    [SerializeField] private Transform bocaCanon;
    [Tooltip("Grados por segundo.")]
    [SerializeField] private float velocidadGiro = 360f;
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

    // Solo servidor.
    private ulong duenoClientId;
    private float proximaBusqueda;

    private ZombieHealth objetivo;
    private Collider colliderObjetivo;
    private ulong idObjetivoCacheado = SinObjetivo;
    private float proximoDisparo;

    private readonly RaycastHit[] impactosVision = new RaycastHit[16];

    /// <summary>Solo servidor, antes de spawnearla: el jugador que la colocó.</summary>
    public void AsignarDueno(ulong clientId)
    {
        duenoClientId = clientId;
    }

    private void Update()
    {
        if (!IsSpawned)
            return;

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
    private bool Apuntar(Vector3 punto)
    {
        Transform giro = pivoteGiro != null ? pivoteGiro : pivoteElevacion;

        // Sin cabezal configurado: dispara igual.
        if (giro == null)
            return true;

        Transform elevacion = pivoteElevacion != null ? pivoteElevacion : giro;
        Vector3 arriba = transform.up;
        Vector3 haciaObjetivo = punto - OrigenDisparo();
        float paso = velocidadGiro * Time.deltaTime;

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
            zombie.RecibirDanoServidor(dano, punto, -direccion, duenoClientId, true, false);
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
