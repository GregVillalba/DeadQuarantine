using System;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Guía del tutorial por secciones, con el panel de objetivos del HUD (el mismo que usa Historia:
/// HistoriaHUD / Panel_Objetivo, textos "Objetivo" y "Descripcion").
///
/// 1. Obstáculos: llegar a la última plataforma (zonaFinSeccion1).
/// 2. Recursos y torreta:
///    - Juntar los recursos del cubo (que se pone verde y parpadea).
///    - Comprar la torreta en la tienda (Utilidades) y colocarla en la zona marcada del piso.
///    - Al colocarla aparecen los zombies de la sección y van a atacarla (en el tutorial tiene poca vida).
///    - Repararla en la tienda con lo que sueltan los zombies y terminar con los que quedan.
/// 3. Barriles y granadas:
///    - Comprar un barril explosivo y colocarlo en la zona marcada (donde están los jefes).
///    - Al colocarlo aparecen los jefes; dispararle al barril para que explote.
///    - Comprar granadas y tirar al menos una: tutorial completado.
///
/// El tutorial es de un jugador (host): la lógica corre en el servidor y el panel se muestra en su jugador.
/// Como la escena no tiene NavMesh horneado (los zombies de práctica no caminan), al empezar se arma uno solo
/// dentro de zonaNavMesh, para que los zombies de la sección 2 puedan llegar a la torreta.
/// </summary>
public class TutorialSecciones : MonoBehaviour
{
    public enum Paso
    {
        Obstaculos,
        JuntarRecursos,
        ComprarTorreta,
        UbicarTorreta,
        DefenderTorreta,
        RepararTorreta,
        EliminarZombies,
        Seccion2Completada,
        ComprarBarril,
        UbicarBarril,
        ExplotarBarril,
        ComprarGranadas,
        LanzarGranada,
        Completado
    }

    private enum Sonido
    {
        Ninguno,
        Objetivo,
        Seccion
    }

    [Serializable]
    public class ZombieSeccion
    {
        [Tooltip("Dónde aparece (en el piso). Los de la sección 2, dentro de la Zona NavMesh.")]
        public Transform punto;
        [Tooltip("Opcional: otro prefab (ej: un jefe). Vacío = Prefab Zombie.")]
        public NetworkObject prefab;
        [Tooltip("Corre en vez de caminar.")]
        public bool corriendo;
        [Tooltip("Lo que suelta al morir (para pagar la reparación de la torreta).")]
        public TipoRecurso recursoQueSuelta = TipoRecurso.PilaDeLatas;
        [Min(1)] public int cantidad = 1;
    }

    [Header("Sección 1: obstáculos")]
    [Tooltip("Zona sobre la última plataforma: al llegar el jugador se desbloquea la sección 2.")]
    [SerializeField] private ZonaTutorial zonaFinSeccion1;
    [Tooltip("Renderers que se ponen verdes y parpadean mientras hay que llegar al final de la sección 1 (la última plataforma).")]
    [SerializeField] private Renderer[] resaltarFinSeccion1;

    [Header("Sección 2: recursos")]
    [SerializeField] private TutorialRecursosMapa recursos;
    [Tooltip("Cuántos recursos distintos del cubo hay que juntar. 0 = todos los puntos.")]
    [SerializeField, Min(0)] private int recursosNecesarios = 4;
    [Tooltip("Renderers que se ponen verdes y parpadean mientras hay que juntar los recursos (el cubo donde están).")]
    [SerializeField] private Renderer[] resaltarConRecursos;

    [Header("Resaltado (última plataforma y cubo de recursos)")]
    [SerializeField] private Color colorResaltado = new Color(0.1f, 0.85f, 0.2f, 1f);
    [SerializeField] private Color colorResaltadoClaro = new Color(0.55f, 1f, 0.55f, 1f);
    [SerializeField, Min(0f)] private float parpadeosPorSegundo = 1.5f;

    [Header("Sección 2: torreta")]
    [Tooltip("Weapon Id de la torreta en el mercader.")]
    [SerializeField] private string weaponIdTorreta = "torreta_aa";
    [Tooltip("Zona marcada en el piso donde hay que colocar la torreta (solo se puede colocar ahí mientras tanto).")]
    [SerializeField] private ZonaTutorial zonaTorreta;
    [Tooltip("Segundos de gracia entre que se confirma la colocación y aparece la torreta (la spawnea el servidor).")]
    [SerializeField, Min(0f)] private float esperaConfirmacion = 1.5f;

    [Header("Sección 2: zombies")]
    [Tooltip("Prefab del zombie (con NetworkObject, en DefaultNetworkPrefabs).")]
    [SerializeField] private NetworkObject prefabZombie;
    [SerializeField] private ZombieSeccion[] zombies = new ZombieSeccion[0];
    [Tooltip("Si los zombies mueren sin romper la torreta, vuelven a aparecer hasta esta cantidad de oleadas.")]
    [SerializeField, Min(1)] private int maximoOleadas = 3;

    [Header("Sección 3: barriles y granadas")]
    [Tooltip("Segundos que se muestra 'Sección completada' antes de pasar a la sección 3.")]
    [SerializeField, Min(0f)] private float segundosSeccionCompletada = 4f;
    [Tooltip("Zona marcada en el piso donde hay que colocar el barril (donde aparecen los jefes).")]
    [SerializeField] private ZonaTutorial zonaBarril;
    [Tooltip("Jefes que aparecen al colocar el barril (usar el Prefab de cada uno).")]
    [SerializeField] private ZombieSeccion[] jefes = new ZombieSeccion[0];
    [Tooltip("Al empezar la sección 3 se saca la torreta de la sección 2: si no, mata a los jefes apenas salen " +
             "del suelo y el jugador no llega a usar el barril.")]
    [SerializeField] private bool retirarTorretaEnSeccion3 = true;

    [Header("NavMesh de la sección 2")]
    [Tooltip("Caja donde se arma el NavMesh al empezar (solo el pasillo de la sección 2: afuera los zombies de " +
             "práctica siguen quietos).")]
    [SerializeField] private ZonaTutorial zonaNavMesh;
    [Tooltip("Capas de los colliders que forman el piso y las paredes del NavMesh.")]
    [SerializeField] private LayerMask capasNavMesh = 1;

    [Header("Sonidos")]
    [Tooltip("Suena cada vez que se cumple un objetivo.")]
    [SerializeField] private AudioClip sonidoObjetivo;
    [Tooltip("Suena al completar una sección y al terminar el tutorial.")]
    [SerializeField] private AudioClip sonidoSeccionCompletada;
    [SerializeField, Range(0f, 1f)] private float volumenSonidos = 0.6f;

    [Header("Textos del panel de objetivos")]
    [SerializeField] private string tituloObjetivo = "OBJETIVO";
    [SerializeField] private string tituloCompletado = "SECCIÓN COMPLETADA";
    [SerializeField, TextArea] private string textoObstaculos = "Atraviesa la zona de obstáculos y llega a la última plataforma";
    [Tooltip("{0} = juntados, {1} = necesarios.")]
    [SerializeField, TextArea] private string textoJuntarRecursos = "Recoge los recursos para intercambiar por armamento ({0}/{1})";
    [SerializeField, TextArea] private string textoComprarTorreta = "Compra la torreta automática en la tienda en Utilidades";
    [SerializeField, TextArea] private string textoUbicarTorreta = "Ubica la torreta en la zona marcada del piso";
    [SerializeField, TextArea] private string textoDefender = "¡Defiende la torreta! Los zombies van a intentar romperla";
    [SerializeField, TextArea] private string textoOtraOleada = "¡Vienen más zombies! Defiende la torreta";
    [SerializeField, TextArea] private string textoReparar = "La torreta se rompió: junta lo que sueltan los zombies y repárala en la tienda (Utilidades)";
    [SerializeField, TextArea] private string textoEliminar = "Elimina a los zombies que quedan";
    [SerializeField, TextArea] private string textoCompletado = "¡Bien hecho! Ya sabes juntar recursos, comprar una torreta, defenderla y repararla";
    [SerializeField, TextArea] private string textoComprarBarril = "Compra un barril explosivo en la tienda en Utilidades y ubícalo en la zona marcada (si te faltan recursos, vuelve a juntar los del cubo)";
    [SerializeField, TextArea] private string textoUbicarBarril = "Ubica el barril explosivo en la zona marcada del piso, donde están los jefes";
    [SerializeField, TextArea] private string textoExplotarBarril = "¡Llegaron los jefes! Dispárale al barril para que explote";
    [SerializeField, TextArea] private string textoComprarGranadas = "Compra granadas en la tienda en Utilidades";
    [Tooltip("{0} = tecla de la granada.")]
    [SerializeField, TextArea] private string textoLanzarGranada = "Lanza una granada con la tecla {0}";
    [SerializeField] private string tituloTutorialCompletado = "¡TUTORIAL COMPLETADO!";
    [SerializeField, TextArea] private string textoTutorialCompletado = "Ya conoces todas las mecánicas del juego. ¡Suerte sobreviviendo!";

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private Paso paso = Paso.Obstaculos;
    private int oleadas;
    private float momentoSinColocar = -1f;
    private float momentoPaso;
    private int explosionesAlInicio;
    private int granadasAlInicio;
    private bool navMeshArmado;
    private NavMeshDataInstance navMesh;
    private readonly List<ZombieHealth> zombiesVivos = new List<ZombieHealth>();
    private MaterialPropertyBlock bloque;
    private AudioSource fuenteSonido;

    // HUD del jugador local.
    private NetworkObject jugadorConHud;
    private TMP_Text textoTitulo;
    private TMP_Text textoDescripcion;
    private string ultimoTitulo;
    private string ultimaDescripcion;
    private Color ultimoColor;
    private string textoActual;

    public Paso PasoActual => paso;

    private void Start()
    {
        textoActual = textoObstaculos;
    }

    private void OnDestroy()
    {
        if (navMesh.valid)
            NavMesh.RemoveNavMeshData(navMesh);

        if (ZonaTutorial.ColocacionActiva != null &&
            (ZonaTutorial.ColocacionActiva == zonaTorreta || ZonaTutorial.ColocacionActiva == zonaBarril))
            ZonaTutorial.ColocacionActiva = null;
    }

    private void Update()
    {
        NetworkManager red = NetworkManager.Singleton;

        if (red == null || !red.IsListening || !red.IsServer)
            return;

        if (!navMeshArmado)
            ArmarNavMesh();

        NetworkObject jugador = red.LocalClient != null ? red.LocalClient.PlayerObject : null;

        if (jugador == null)
            return;

        AvanzarPaso(jugador, red.LocalClientId);
        ActualizarResaltado();
        MostrarObjetivo(jugador);
    }

    // =========================================================
    // PASOS
    // =========================================================

    private void AvanzarPaso(NetworkObject jugador, ulong clienteLocal)
    {
        TorretaAutomatica torreta = TorretaAutomatica.DelJugador(clienteLocal);
        LimpiarZombiesMuertos();

        switch (paso)
        {
            case Paso.Obstaculos:
                if (zonaFinSeccion1 == null || zonaFinSeccion1.Contiene(jugador.transform.position))
                    EmpezarRecursos();
                break;

            case Paso.JuntarRecursos:
                textoActual = string.Format(textoJuntarRecursos, Mathf.Min(RecursosJuntados(), RecursosNecesarios()), RecursosNecesarios());

                if (RecursosJuntados() >= RecursosNecesarios())
                    CambiarPaso(Paso.ComprarTorreta, textoComprarTorreta, Sonido.Objetivo);
                break;

            case Paso.ComprarTorreta:
                // Si ya la tenía colocada (de antes), se pasa directo a defenderla.
                if (torreta != null)
                    EmpezarDefensa(textoDefender, Sonido.Objetivo);
                else if (EstaColocando(jugador))
                    CambiarPaso(Paso.UbicarTorreta, textoUbicarTorreta, Sonido.Objetivo);
                break;

            case Paso.UbicarTorreta:
                if (torreta != null)
                {
                    EmpezarDefensa(textoDefender, Sonido.Objetivo);
                }
                else if (EstaColocando(jugador))
                {
                    momentoSinColocar = -1f;
                }
                else
                {
                    // Canceló (o todavía no apareció la que confirmó): un rato de gracia y vuelve a comprarla.
                    if (momentoSinColocar < 0f)
                        momentoSinColocar = Time.time;
                    else if (Time.time - momentoSinColocar >= esperaConfirmacion)
                        CambiarPaso(Paso.ComprarTorreta, textoComprarTorreta);
                }
                break;

            case Paso.DefenderTorreta:
                if (torreta == null)
                    Completar();
                else if (torreta.EstaRota)
                    CambiarPaso(Paso.RepararTorreta, textoReparar);
                else if (zombiesVivos.Count == 0)
                {
                    if (torreta.NecesitaReparacion)
                        CambiarPaso(Paso.RepararTorreta, textoReparar, Sonido.Objetivo);
                    else if (oleadas < maximoOleadas)
                        EmpezarDefensa(textoOtraOleada, Sonido.Ninguno);
                    else
                        Completar();
                }
                break;

            case Paso.RepararTorreta:
                if (torreta == null || !torreta.NecesitaReparacion)
                {
                    if (zombiesVivos.Count > 0)
                        CambiarPaso(Paso.EliminarZombies, textoEliminar, Sonido.Objetivo);
                    else
                        Completar();
                }
                break;

            case Paso.EliminarZombies:
                if (zombiesVivos.Count == 0)
                    Completar();
                break;

            // ---------------- Sección 3 ----------------

            case Paso.Seccion2Completada:
                if (Time.time - momentoPaso >= segundosSeccionCompletada)
                {
                    // La torreta de la sección 2 mataría a los jefes antes de que el jugador use el barril.
                    if (retirarTorretaEnSeccion3)
                        RetirarTorreta(torreta);

                    CambiarPaso(Paso.ComprarBarril, textoComprarBarril);
                }
                break;

            case Paso.ComprarBarril:
                if (BarrilEnZona())
                    EmpezarJefes();
                else if (EstaColocando(jugador))
                    CambiarPaso(Paso.UbicarBarril, textoUbicarBarril, Sonido.Objetivo);
                break;

            case Paso.UbicarBarril:
                if (BarrilEnZona())
                {
                    EmpezarJefes();
                }
                else if (EstaColocando(jugador))
                {
                    momentoSinColocar = -1f;
                }
                else
                {
                    // Canceló (o todavía no apareció el que confirmó): un rato de gracia y vuelve a comprarlo.
                    if (momentoSinColocar < 0f)
                        momentoSinColocar = Time.time;
                    else if (Time.time - momentoSinColocar >= esperaConfirmacion)
                        CambiarPaso(Paso.ComprarBarril, textoComprarBarril);
                }
                break;

            case Paso.ExplotarBarril:
                if (BarrilExplosivo.ExplosionesServidor > explosionesAlInicio)
                {
                    granadasAlInicio = GrenadeThrow.LanzadasServidor;
                    CambiarPaso(Paso.ComprarGranadas, textoComprarGranadas, Sonido.Objetivo);
                }
                else if (!BarrilEnZona())
                {
                    // El barril ya no está y no explotó (no debería pasar): se vuelve a comprar.
                    CambiarPaso(Paso.ComprarBarril, textoComprarBarril);
                }
                break;

            case Paso.ComprarGranadas:
                if (GrenadeThrow.LanzadasServidor > granadasAlInicio)
                    CambiarPaso(Paso.Completado, textoTutorialCompletado, Sonido.Seccion);
                else if (TieneGranadas(jugador))
                    CambiarPaso(Paso.LanzarGranada, string.Format(textoLanzarGranada, TeclaGranada(jugador)), Sonido.Objetivo);
                break;

            case Paso.LanzarGranada:
                if (GrenadeThrow.LanzadasServidor > granadasAlInicio)
                    CambiarPaso(Paso.Completado, textoTutorialCompletado, Sonido.Seccion);
                else if (!TieneGranadas(jugador))
                    CambiarPaso(Paso.ComprarGranadas, textoComprarGranadas);
                break;
        }
    }

    // sonido: qué suena al pasar al paso nuevo. Ninguno cuando se vuelve atrás (canceló la colocación,
    // se le acabaron las granadas...) o cuando no es un logro del jugador (la torreta se rompió, otra oleada).
    private void CambiarPaso(Paso nuevo, string texto, Sonido sonido = Sonido.Ninguno)
    {
        paso = nuevo;
        textoActual = texto;
        momentoSinColocar = -1f;
        momentoPaso = Time.time;

        ReproducirSonido(sonido);

        // Mientras hay que comprar y colocar algo, solo se puede colocar en su zona; el marcador se ve al colocar.
        ZonaTutorial zonaColocacion = null;

        if (nuevo == Paso.ComprarTorreta || nuevo == Paso.UbicarTorreta)
            zonaColocacion = zonaTorreta;
        else if (nuevo == Paso.ComprarBarril || nuevo == Paso.UbicarBarril)
            zonaColocacion = zonaBarril;

        bool colocando = nuevo == Paso.UbicarTorreta || nuevo == Paso.UbicarBarril;

        if (zonaTorreta != null)
            zonaTorreta.MostrarMarcador(colocando && zonaColocacion == zonaTorreta);

        if (zonaBarril != null)
            zonaBarril.MostrarMarcador(colocando && zonaColocacion == zonaBarril);

        ZonaTutorial.ColocacionActiva = zonaColocacion;

        Debug.Log("[TutorialSecciones] Paso: " + nuevo);
    }

    private void EmpezarRecursos()
    {
        if (recursos != null)
            recursos.Activar();

        // Fin de la sección 1.
        CambiarPaso(Paso.JuntarRecursos, string.Format(textoJuntarRecursos, 0, RecursosNecesarios()), Sonido.Seccion);
    }

    private void EmpezarDefensa(string texto, Sonido sonido)
    {
        CambiarPaso(Paso.DefenderTorreta, texto, sonido);
        oleadas++;
        AparecerZombies(zombies, true);
    }

    // Fin de la sección 2: se muestra "Sección completada" un rato y arranca la sección 3.
    private void Completar()
    {
        CambiarPaso(Paso.Seccion2Completada, textoCompletado, Sonido.Seccion);
    }

    private void EmpezarJefes()
    {
        explosionesAlInicio = BarrilExplosivo.ExplosionesServidor;
        CambiarPaso(Paso.ExplotarBarril, textoExplotarBarril, Sonido.Objetivo);
        AparecerZombies(jefes, false);
    }

    // =========================================================
    // SONIDOS (los escucha el jugador local: el tutorial es de un jugador, en el host)
    // =========================================================

    private void ReproducirSonido(Sonido sonido)
    {
        AudioClip clip = sonido == Sonido.Objetivo ? sonidoObjetivo
                       : sonido == Sonido.Seccion ? sonidoSeccionCompletada
                       : null;

        if (clip == null)
            return;

        if (fuenteSonido == null)
        {
            fuenteSonido = gameObject.AddComponent<AudioSource>();
            fuenteSonido.playOnAwake = false;
            fuenteSonido.spatialBlend = 0f; // 2D: se escucha igual esté donde esté el jugador.
        }

        // Si justo estaba sonando otro (dos objetivos seguidos), se corta para que no se encimen.
        fuenteSonido.Stop();
        fuenteSonido.clip = clip;
        fuenteSonido.volume = volumenSonidos;
        fuenteSonido.Play();
    }

    // Solo servidor.
    private static void RetirarTorreta(TorretaAutomatica torreta)
    {
        if (torreta != null && torreta.NetworkObject != null && torreta.NetworkObject.IsSpawned)
        {
            torreta.NetworkObject.Despawn(true);
            Debug.Log("[TutorialSecciones] Torreta de la sección 2 retirada para la sección 3.");
        }
    }

    private bool BarrilEnZona()
    {
        if (zonaBarril == null)
            return BarrilExplosivo.Todos.Count > 0;

        foreach (BarrilExplosivo barril in BarrilExplosivo.Todos)
        {
            if (barril != null && barril.IsSpawned && zonaBarril.Contiene(barril.transform.position))
                return true;
        }

        return false;
    }

    private static bool TieneGranadas(NetworkObject jugador)
    {
        GrenadeThrow granadas = jugador.GetComponentInChildren<GrenadeThrow>();
        return granadas != null && granadas.TieneGranadas;
    }

    private static string TeclaGranada(NetworkObject jugador)
    {
        GrenadeThrow granadas = jugador.GetComponentInChildren<GrenadeThrow>();
        string tecla = granadas != null ? granadas.TeclaGranada : null;
        return string.IsNullOrEmpty(tecla) ? "G" : tecla;
    }

    private int RecursosNecesarios()
    {
        int puntos = recursos != null ? recursos.CantidadPuntos : 0;

        if (recursosNecesarios <= 0)
            return Mathf.Max(1, puntos);

        return puntos > 0 ? Mathf.Min(recursosNecesarios, puntos) : recursosNecesarios;
    }

    private int RecursosJuntados()
    {
        return recursos != null ? recursos.PuntosJuntados : 0;
    }

    private bool EstaColocando(NetworkObject jugador)
    {
        ColocadorObjetos colocador = jugador.GetComponentInChildren<ColocadorObjetos>();
        return colocador != null && colocador.Colocando;
    }

    // =========================================================
    // ZOMBIES (servidor)
    // =========================================================

    // contarComoVivos: los de la sección 2 (la defensa de la torreta espera a que mueran); los jefes no.
    private void AparecerZombies(ZombieSeccion[] lista, bool contarComoVivos)
    {
        if (lista == null)
            return;

        foreach (ZombieSeccion datos in lista)
        {
            if (datos == null || datos.punto == null)
                continue;

            NetworkObject prefab = datos.prefab != null ? datos.prefab : prefabZombie;

            if (prefab == null)
            {
                Debug.LogWarning("[TutorialSecciones] Falta el prefab del zombie '" + datos.punto.name + "'.", this);
                continue;
            }

            NetworkObject zombie = Instantiate(prefab, datos.punto.position, datos.punto.rotation);

            // Fuera del NavMesh (ej: los jefes) el agente no puede engancharse y, al terminar de salir del suelo,
            // puede mover al zombie a cualquier lado: se apaga y el zombie se queda quieto donde aparece.
            NavMeshAgent agente = zombie.GetComponent<NavMeshAgent>();
            if (agente != null && !NavMesh.SamplePosition(datos.punto.position, out _, 2f, NavMesh.AllAreas))
                agente.enabled = false;

            DropRecursosZombie drop = zombie.GetComponent<DropRecursosZombie>();
            if (drop != null)
                drop.ConfigurarDropTutorial(datos.recursoQueSuelta, datos.cantidad);

            zombie.Spawn(true);

            ZombieAI ai = zombie.GetComponent<ZombieAI>();
            if (ai != null)
                ai.SetRunning(datos.corriendo);

            ZombieHealth salud = zombie.GetComponent<ZombieHealth>();
            if (salud != null && contarComoVivos)
                zombiesVivos.Add(salud);
        }
    }

    private void LimpiarZombiesMuertos()
    {
        zombiesVivos.RemoveAll(z => z == null || !z.IsSpawned || z.IsDead);
    }

    // =========================================================
    // NAVMESH (servidor: los zombies solo se mueven ahí)
    // =========================================================

    private void ArmarNavMesh()
    {
        navMeshArmado = true;

        if (zonaNavMesh == null)
            return;

        Transform zona = zonaNavMesh.transform;
        Bounds limites = new Bounds(zona.position, Vector3.Scale(zonaNavMesh.Tamano, zona.lossyScale));

        List<NavMeshBuildSource> fuentes = new List<NavMeshBuildSource>();
        NavMeshBuilder.CollectSources(limites, capasNavMesh, NavMeshCollectGeometry.PhysicsColliders, 0,
            new List<NavMeshBuildMarkup>(), fuentes);

        // Solo el escenario: sin triggers (zonas, obstáculos), jugadores, zombies ni objetos colocados.
        fuentes.RemoveAll(fuente =>
        {
            Collider collider = fuente.component as Collider;
            return collider != null &&
                   (collider.isTrigger ||
                    collider.GetComponentInParent<NetworkObject>() != null);
        });

        NavMeshBuildSettings ajustes = NavMesh.GetSettingsByID(0);
        NavMeshData datos = NavMeshBuilder.BuildNavMeshData(ajustes, fuentes, limites, Vector3.zero, Quaternion.identity);

        if (datos == null)
        {
            Debug.LogWarning("[TutorialSecciones] No se pudo armar el NavMesh de la sección 2.", this);
            return;
        }

        navMesh = NavMesh.AddNavMeshData(datos);
        Debug.Log("[TutorialSecciones] NavMesh de la sección 2 armado con " + fuentes.Count + " colliders.");
    }

    // =========================================================
    // RESALTADO: ÚLTIMA PLATAFORMA (sección 1) Y CUBO DE RECURSOS (sección 2)
    // =========================================================

    private void ActualizarResaltado()
    {
        if (bloque == null)
            bloque = new MaterialPropertyBlock();

        Color color = Color.Lerp(colorResaltado, colorResaltadoClaro,
            parpadeosPorSegundo > 0f ? Mathf.PingPong(Time.time * parpadeosPorSegundo * 2f, 1f) : 0f);

        Pintar(resaltarFinSeccion1, paso == Paso.Obstaculos, color);
        Pintar(resaltarConRecursos, paso == Paso.JuntarRecursos, color);
    }

    // Verde parpadeante mientras toca; si no, vuelve a su material.
    private void Pintar(Renderer[] renders, bool resaltar, Color color)
    {
        if (renders == null)
            return;

        foreach (Renderer render in renders)
        {
            if (render == null)
                continue;

            if (!resaltar)
            {
                if (render.HasPropertyBlock())
                    render.SetPropertyBlock(null);

                continue;
            }

            render.GetPropertyBlock(bloque);
            bloque.SetColor(BaseColorId, color);
            bloque.SetColor(ColorId, color);
            render.SetPropertyBlock(bloque);
        }
    }

    // =========================================================
    // PANEL DE OBJETIVOS (HUD del jugador local)
    // =========================================================

    private void MostrarObjetivo(NetworkObject jugador)
    {
        if (jugador != jugadorConHud || textoTitulo == null || textoDescripcion == null)
        {
            if (!BuscarHud(jugador))
                return;
        }

        string titulo = tituloObjetivo;
        Color color = Color.white;

        if (paso == Paso.Seccion2Completada)
        {
            titulo = tituloCompletado;
            color = Color.green;
        }
        else if (paso == Paso.Completado)
        {
            titulo = tituloTutorialCompletado;
            color = Color.green;
        }

        if (titulo != ultimoTitulo || color != ultimoColor)
        {
            textoTitulo.text = titulo;
            textoTitulo.color = color;
            ultimoTitulo = titulo;
            ultimoColor = color;
        }

        if (textoActual != ultimaDescripcion)
        {
            textoDescripcion.text = textoActual;
            ultimaDescripcion = textoActual;
        }
    }

    // HistoriaHUD / Panel_Objetivo / FILA1 / Objetivo y FILA2 / Descripcion, como en GestorMansionHistoria.
    private bool BuscarHud(NetworkObject jugador)
    {
        jugadorConHud = jugador;
        textoTitulo = null;
        textoDescripcion = null;
        ultimoTitulo = null;
        ultimaDescripcion = null;

        Transform hud = BuscarHijo(jugador.transform, "HistoriaHUD");
        Transform panel = hud != null ? BuscarHijo(hud, "Panel_Objetivo") : null;

        if (panel == null)
            return false;

        foreach (TMP_Text texto in panel.GetComponentsInChildren<TMP_Text>(true))
        {
            if (texto.name == "Objetivo")
                textoTitulo = texto;
            else if (texto.name == "Descripcion")
                textoDescripcion = texto;
            else if (texto.name == "Temporizador")
                texto.gameObject.SetActive(false);
        }

        // Los iconos de tarjeta y radio son solo de Historia.
        Transform objetos = BuscarHijo(hud, "Panel_ObjetosHistoria");
        if (objetos != null)
            objetos.gameObject.SetActive(false);

        hud.gameObject.SetActive(true);
        panel.gameObject.SetActive(true);

        return textoTitulo != null && textoDescripcion != null;
    }

    private static Transform BuscarHijo(Transform padre, string nombre)
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
}
