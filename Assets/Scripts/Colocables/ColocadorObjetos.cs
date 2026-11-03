using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using Unity.Netcode;
using TMPro;

/// <summary>
/// Colocación de los objetos comprados en la tienda que se ponen en el mapa: torretas, barriles explosivos...
/// (estilo Fortnite). Va en el Player, en el mismo objeto que el NetworkObject o en un hijo.
///
/// Al comprar una oferta colocable (WeaponOffer.EsColocable), NPCShopUI cierra la tienda y llama a
/// IniciarColocacion: aparece un fantasma translúcido del objeto frente al jugador, azul si el lugar está libre
/// y rojo si choca con algo. Fire (clic izquierdo) lo coloca solo en azul; Aim (clic derecho) cancela.
/// Se cobra recién al colocarlo: el servidor descuenta los recursos y lo spawnea.
/// </summary>
public class ColocadorObjetos : NetworkBehaviour
{
    [Serializable]
    private class ObjetoColocable
    {
        [Tooltip("El mismo Weapon Id de la oferta en el mercader.")]
        public string weaponId;
        [Tooltip("Prefab con NetworkObject. Tiene que estar en la lista de Network Prefabs del NetworkManager.")]
        public NetworkObject prefab;
        [Tooltip("Distancia horizontal (m) entre la cámara y el objeto proyectado.")]
        public float distancia = 6f;
    }

    [Header("Objetos")]
    [Tooltip("Un elemento por oferta colocable de la tienda (torreta, barril explosivo...).")]
    [SerializeField] private List<ObjetoColocable> objetos = new List<ObjetoColocable>();

    [Header("Fantasma")]
    [Tooltip("Opcional. Material translúcido sin texturas (URP/Unlit, Surface Type: Transparent). Vacío = se crea uno igual al iniciar.")]
    [SerializeField] private Material materialFantasma;
    [SerializeField] private Color colorValido = new Color(0.2f, 0.55f, 1f, 0.45f);
    [SerializeField] private Color colorInvalido = new Color(1f, 0.15f, 0.15f, 0.45f);

    [Header("Validación del espacio")]
    [Tooltip("Capas sobre las que se puede apoyar el objeto (terreno, calles, veredas, charcos...). Se usan solo para medir la altura del piso.")]
    [SerializeField] private LayerMask capasSuelo = 1;
    [Tooltip("Capas que impiden colocarla: árboles, paredes, casas, techos, props, jugadores, zombies. " +
             "Pueden incluir las del suelo: el piso no choca porque se revisa desde 'Altura Ignorada' para arriba.")]
    [SerializeField] private LayerMask capasObstaculos = ~0;
    [Tooltip("Desde cuánto más arriba de la cámara se busca el piso (m). Bajo, para que no se apoye sobre techos.")]
    [SerializeField] private float alturaRayoSuelo = 1f;
    [Tooltip("Hasta cuánto más abajo de la cámara se busca el piso (m). Sin piso en ese rango no se puede colocar.")]
    [SerializeField] private float caidaMaximaSuelo = 6f;
    [Tooltip("Diferencia máxima de altura del piso entre el centro y las esquinas del objeto (m). Más que eso = pendiente, escalones, autos, troncos...")]
    [SerializeField] private float desnivelMaximo = 0.6f;
    [Tooltip("Lo que no pasa de esta altura sobre el piso no impide colocarla: pasto, charcos de barro, cordones, piedras (m).")]
    [SerializeField] private float alturaIgnorada = 0.5f;
    [Tooltip("Objetos que nunca impiden colocarla aunque sean altos o tengan collider, porque son parte del piso. " +
             "Se compara el comienzo del nombre del objeto con el collider (o de su padre).")]
    [SerializeField] private string[] nombresIgnorados =
    {
        "SM_Env_Grass", "SM_Env_Dirt", "SM_Env_Leaves", "SM_Env_Pebbles", "SM_Env_SteppingStone",
        "SM_Env_Puddle", "SM_Env_Mud", "SM_Env_Sidewalk", "SM_Env_Road", "SM_Env_Ground"
    };
    [Tooltip("Achica la caja de choque por cada costado (m), para que rozar algo no la ponga en rojo.")]
    [SerializeField] private float margenLateral = 0.05f;
    [Tooltip("Escribe en la consola por qué el fantasma queda en rojo (qué objeto y en qué capa). Para ajustar las capas.")]
    [SerializeField] private bool depurarChoques;

    [Header("Ayuda en pantalla (opcional)")]
    [Tooltip("Se muestra solo mientras se coloca un objeto.")]
    [SerializeField] private GameObject panelAyuda;
    [SerializeField] private TextMeshProUGUI textoAyuda;
    [Tooltip("{0} = tecla de colocar, {1} = tecla de cancelar, {2} = nombre del objeto (el de la oferta).")]
    [SerializeField] private string formatoAyuda = "{0} Colocar {2}    {1} Cancelar";
    [SerializeField] private string textoEspacioOcupado = "Espacio ocupado";

    [Header("Audio (opcional)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip sonidoColocar;
    [SerializeField] private AudioClip sonidoDenegado;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private PlayerControls controls;
    private Camera playerCamera;
    private PlayerHealth playerHealth;

    private GameObject fantasma;
    private Material materialInstancia;
    private Bounds limitesLocales;
    private readonly Collider[] choques = new Collider[16];
    private readonly RaycastHit[] impactosVision = new RaycastHit[16];
    private readonly RaycastHit[] impactosPiso = new RaycastHit[16];
    private string ultimoMotivo;

    private bool colocando;
    private bool posicionValida;
    private bool esperandoSoltarFire;
    private PrecioRecursos precioPendiente;
    private ObjetoColocable objetoActual;
    private string nombreActual;

    // ---------------------------------------------------------
    // Estado del jugador LOCAL, mismo patrón que NPCShopUI.LocalShopOpen.
    // Sigue en true hasta soltar el clic con el que se colocó, así el arma no dispara con ese mismo clic.
    // ---------------------------------------------------------

    /// <summary>true mientras el jugador local está colocando un objeto.</summary>
    public static bool ColocandoLocal { get; private set; }

    // Con "Enter Play Mode Options" (sin recargar dominio) los estaticos sobreviven entre partidas.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        ColocandoLocal = false;
    }

    public bool Colocando => colocando;

    private void Awake()
    {
        controls = new PlayerControls();
        ConfiguracionesJuego.CargarRebinds(controls.asset);

        playerHealth = transform.root.GetComponentInChildren<PlayerHealth>(true);

        if (panelAyuda != null)
            panelAyuda.SetActive(false);

        // En una sola línea, centrada, aunque la caja del texto sea angosta.
        if (textoAyuda != null)
        {
            textoAyuda.textWrappingMode = TextWrappingModes.NoWrap;
            textoAyuda.overflowMode = TextOverflowModes.Overflow;
            textoAyuda.alignment = TextAlignmentOptions.Center;
        }
    }

    private void OnEnable()
    {
        controls.Player.Enable();
    }

    private void OnDisable()
    {
        controls.Player.Disable();

        if (colocando)
            TerminarColocacion();

        esperandoSoltarFire = false;

        if (IsOwner)
            ColocandoLocal = false;
    }

    // =========================================================
    // INICIO / FIN
    // =========================================================

    /// <summary>true si hay un prefab configurado para esa oferta.</summary>
    public bool PuedeColocar(string weaponId)
    {
        ObjetoColocable objeto = BuscarObjeto(weaponId);
        return objeto != null && objeto.prefab != null;
    }

    /// <summary>
    /// Lo llama NPCShopUI al comprar una oferta colocable (con la tienda ya cerrada).
    /// Devuelve false si no se puede colocar (otro modo de juego, sin prefab, ya colocando...).
    /// </summary>
    public bool IniciarColocacion(WeaponOffer oferta)
    {
        if (!IsOwner || colocando || oferta == null)
            return false;

        ObjetoColocable objeto = BuscarObjeto(oferta.weaponId);

        if (objeto == null || objeto.prefab == null)
        {
            Debug.LogError("[ColocadorObjetos] No hay prefab para la oferta '" + oferta.weaponId + "'. Agregalo en la lista Objetos.", this);
            return false;
        }

        if (!ModoPermiteColocables())
            return false;

        if (playerCamera == null)
            BuscarCamara();

        if (playerCamera == null)
            return false;

        objetoActual = objeto;
        nombreActual = string.IsNullOrEmpty(oferta.weaponName) ? oferta.weaponId : oferta.weaponName.ToLower();
        precioPendiente = oferta.Precio;
        fantasma = CrearFantasma(objeto.prefab);
        colocando = true;
        posicionValida = false;

        if (panelAyuda != null)
            panelAyuda.SetActive(true);

        ActualizarFantasma();
        ActualizarEstadoLocal();
        return true;
    }

    public void CancelarColocacion()
    {
        if (!colocando)
            return;

        TerminarColocacion();
        ActualizarEstadoLocal();
    }

    private void TerminarColocacion()
    {
        colocando = false;
        posicionValida = false;

        if (fantasma != null)
            Destroy(fantasma);

        if (materialInstancia != null)
            Destroy(materialInstancia);

        fantasma = null;
        materialInstancia = null;

        if (panelAyuda != null)
            panelAyuda.SetActive(false);
    }

    private void ActualizarEstadoLocal()
    {
        if (IsOwner)
            ColocandoLocal = colocando || esperandoSoltarFire;
    }

    private ObjetoColocable BuscarObjeto(string weaponId)
    {
        if (string.IsNullOrEmpty(weaponId))
            return null;

        return objetos.Find(o => o != null && o.weaponId == weaponId);
    }

    // Supervivencia. Sin modo elegido (escena abierta directo desde el editor) también, igual que la tienda.
    public static bool ModoPermiteColocables()
    {
        ConfiguracionPartidaSeleccionada.ModoJuego modo = DisponibilidadTienda.ModoActual();

        return modo == ConfiguracionPartidaSeleccionada.ModoJuego.Supervivencia ||
               modo == ConfiguracionPartidaSeleccionada.ModoJuego.Ninguno;
    }

    // =========================================================
    // INPUT
    // =========================================================

    private void Update()
    {
        if (!IsOwner)
            return;

        if (esperandoSoltarFire && !controls.Player.Fire.IsPressed())
        {
            esperandoSoltarFire = false;
            ActualizarEstadoLocal();
        }

        if (!colocando)
            return;

        // Abatido o muerto: se cancela (todavía no se cobró nada).
        if (playerHealth != null && !playerHealth.IsAlive)
        {
            CancelarColocacion();
            return;
        }

        if (PauseController.LocalPlayerPaused)
            return;

        if (controls.Player.Aim.triggered)
        {
            CancelarColocacion();
            return;
        }

        if (controls.Player.Fire.triggered)
        {
            if (posicionValida)
                ConfirmarColocacion();
            else
                PlaySound(sonidoDenegado);
        }
    }

    // Después de que la cámara se movió, así el fantasma no tiembla.
    private void LateUpdate()
    {
        if (IsOwner && colocando)
            ActualizarFantasma();
    }

    private void ConfirmarColocacion()
    {
        Vector3 posicion = fantasma.transform.position;
        Quaternion rotacion = fantasma.transform.rotation;
        PrecioRecursos precio = precioPendiente;
        string weaponId = objetoActual.weaponId;

        TerminarColocacion();
        esperandoSoltarFire = true;
        ActualizarEstadoLocal();

        ColocarObjetoServerRpc(weaponId, posicion, rotacion, precio);
    }

    // =========================================================
    // FANTASMA
    // =========================================================

    private void ActualizarFantasma()
    {
        if (fantasma == null || playerCamera == null)
            return;

        Transform camara = playerCamera.transform;

        // Siempre parada, a distancia fija sobre el plano, girando con la vista del jugador.
        Vector3 frente = Vector3.ProjectOnPlane(camara.forward, Vector3.up);

        if (frente.sqrMagnitude < 0.0001f)
            frente = Vector3.ProjectOnPlane(camara.up, Vector3.up);

        frente.Normalize();

        Vector3 punto = camara.position + frente * objetoActual.distancia;
        Quaternion rotacion = Quaternion.LookRotation(frente, Vector3.up);
        float alturaRayo = camara.position.y + alturaRayoSuelo;

        bool haySuelo = BuscarPiso(punto, alturaRayo, out RaycastHit suelo);

        if (haySuelo)
            punto = suelo.point;

        // La parte más baja del modelo queda apoyada en el piso, aunque el pivote del prefab esté en el medio
        // (ej: el barril). Se coloca exactamente donde queda el fantasma.
        fantasma.transform.SetPositionAndRotation(punto + Vector3.up * -limitesLocales.min.y, rotacion);

        string motivo = null;

        // La pendiente se mide con el desnivel entre el centro y las esquinas (no con la normal del punto central,
        // que en el borde de una vereda o un cordón da casi vertical aunque el piso sea plano).
        if (!haySuelo)
            motivo = "no hay piso";
        else if (!MedirPisoEsquinas(alturaRayo, punto.y, out float pisoMaximo, out string motivoEsquinas))
            motivo = motivoEsquinas;
        else
            motivo = BuscarObstaculo(pisoMaximo) ?? BuscarParedEnElMedio(camara.position, punto);

        posicionValida = motivo == null;

        if (depurarChoques && motivo != ultimoMotivo && motivo != null)
            Debug.Log("[ColocadorObjetos] En rojo: " + motivo, this);

        ultimoMotivo = motivo;

        PintarFantasma(posicionValida);
        ActualizarAyuda();
    }

    // La primera superficie desde arriba (la que pisa el jugador): calle, vereda, pasto, barro o lo que sea.
    // No se saltean los nombresIgnorados: en la ciudad hay suelo más bajo debajo de algunas calles y no de otras,
    // y medir a veces uno y a veces otro daba desnivel falso entre el centro y las esquinas.
    private bool BuscarPiso(Vector3 punto, float alturaRayo, out RaycastHit suelo)
    {
        Vector3 origen = new Vector3(punto.x, alturaRayo, punto.z);

        int cantidad = Physics.RaycastNonAlloc(
            origen,
            Vector3.down,
            impactosPiso,
            alturaRayoSuelo + caidaMaximaSuelo,
            capasSuelo,
            QueryTriggerInteraction.Ignore);

        suelo = default;
        bool hayPiso = false;

        for (int i = 0; i < cantidad; i++)
        {
            RaycastHit impacto = impactosPiso[i];

            if (impacto.collider.transform.IsChildOf(transform.root))
                continue;

            if (!hayPiso || impacto.distance < suelo.distance)
                suelo = impacto;

            hayPiso = true;
        }

        return hayPiso;
    }

    private bool EsIgnorado(Collider collider)
    {
        if (nombresIgnorados == null)
            return false;

        string nombre = collider.name;
        Transform padre = collider.transform.parent;
        string nombrePadre = padre != null ? padre.name : string.Empty;

        foreach (string prefijo in nombresIgnorados)
        {
            if (string.IsNullOrEmpty(prefijo))
                continue;

            if (nombre.StartsWith(prefijo, System.StringComparison.OrdinalIgnoreCase) ||
                nombrePadre.StartsWith(prefijo, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    // Altura del piso en las 4 esquinas de la base. Todas tienen que tener piso y estar casi a la misma altura.
    private bool MedirPisoEsquinas(float alturaRayo, float pisoCentro, out float pisoMaximo, out string motivo)
    {
        Transform t = fantasma.transform;
        float pisoMinimo = pisoCentro;
        pisoMaximo = pisoCentro;
        motivo = null;

        Vector3 centro = limitesLocales.center;
        Vector3 mitad = limitesLocales.extents;

        for (int i = 0; i < 4; i++)
        {
            Vector3 esquinaLocal = new Vector3(
                centro.x + ((i & 1) == 0 ? -1f : 1f) * Mathf.Max(0f, mitad.x - margenLateral),
                0f,
                centro.z + ((i & 2) == 0 ? -1f : 1f) * Mathf.Max(0f, mitad.z - margenLateral));

            if (!BuscarPiso(t.TransformPoint(esquinaLocal), alturaRayo, out RaycastHit piso))
            {
                motivo = "una esquina queda sin piso";
                return false;
            }

            pisoMinimo = Mathf.Min(pisoMinimo, piso.point.y);
            pisoMaximo = Mathf.Max(pisoMaximo, piso.point.y);

            if (pisoMaximo - pisoMinimo > desnivelMaximo)
            {
                motivo = "desnivel en la base (" + piso.collider.name + ", capa " + LayerMask.LayerToName(piso.collider.gameObject.layer) + ")";
                return false;
            }
        }

        return true;
    }

    // Caja que envuelve el modelo, desde 'alturaIgnorada' sobre el punto más alto del piso, y un poco más chica a los costados.
    // Así el piso, el pasto y los charcos no la tocan, pero un árbol, una pared o un techo sí.
    private string BuscarObstaculo(float pisoMaximo)
    {
        Transform t = fantasma.transform;

        float abajo = pisoMaximo + alturaIgnorada - t.position.y;
        float arriba = limitesLocales.max.y;

        if (arriba - abajo < 0.02f)
            return null;

        Vector3 centro = new Vector3(limitesLocales.center.x, (abajo + arriba) * 0.5f, limitesLocales.center.z);
        Vector3 mitad = new Vector3(
            Mathf.Max(0.01f, limitesLocales.extents.x - margenLateral),
            (arriba - abajo) * 0.5f,
            Mathf.Max(0.01f, limitesLocales.extents.z - margenLateral));

        int cantidad = Physics.OverlapBoxNonAlloc(
            t.TransformPoint(centro),
            mitad,
            choques,
            t.rotation,
            capasObstaculos,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < cantidad; i++)
        {
            if (!EsIgnorado(choques[i]))
                return "choca con " + DescribirCollider(choques[i]);
        }

        return null;
    }

    // Que no se pueda poner del otro lado de una pared: de la cámara al centro de la base no tiene que haber nada.
    private string BuscarParedEnElMedio(Vector3 desde, Vector3 piso)
    {
        Vector3 hasta = piso + Vector3.up * (alturaIgnorada + 0.5f);
        Vector3 direccion = hasta - desde;
        float distancia = direccion.magnitude;

        if (distancia < 0.01f)
            return null;

        int cantidad = Physics.RaycastNonAlloc(
            desde,
            direccion / distancia,
            impactosVision,
            distancia,
            capasObstaculos,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < cantidad; i++)
        {
            Collider impacto = impactosVision[i].collider;

            // El propio jugador (cápsula, hitboxes, cuerpo), el pasto y el barro no cuentan.
            if (impacto != null && !impacto.transform.IsChildOf(transform.root) && !EsIgnorado(impacto))
                return "hay algo entre el jugador y el objeto: " + DescribirCollider(impacto);
        }

        return null;
    }

    private static string DescribirCollider(Collider collider)
    {
        return collider.name + " (capa " + LayerMask.LayerToName(collider.gameObject.layer) + ")";
    }

    private void PintarFantasma(bool valido)
    {
        if (materialInstancia == null)
            return;

        Color color = valido ? colorValido : colorInvalido;

        if (materialInstancia.HasProperty(BaseColorId))
            materialInstancia.SetColor(BaseColorId, color);

        if (materialInstancia.HasProperty(ColorId))
            materialInstancia.SetColor(ColorId, color);
    }

    private void ActualizarAyuda()
    {
        if (textoAyuda == null)
            return;

        string ayuda = string.Format(
            formatoAyuda,
            controls.Player.Fire.GetBindingDisplayString(),
            controls.Player.Aim.GetBindingDisplayString(),
            nombreActual);

        textoAyuda.text = posicionValida ? ayuda : textoEspacioOcupado + "    " + ayuda;
    }

    // Copia solo visual del prefab: sin scripts, red, física ni efectos, y con el material translúcido.
    private GameObject CrearFantasma(NetworkObject prefab)
    {
        // Se arma desactivado para que no corra el Awake de ningún componente del prefab.
        GameObject raiz = new GameObject("FantasmaColocable");
        raiz.SetActive(false);

        GameObject modelo = Instantiate(prefab.gameObject, raiz.transform);
        modelo.transform.localPosition = Vector3.zero;
        modelo.transform.localRotation = Quaternion.identity;

        DejarSoloModelo(modelo);

        limitesLocales = CalcularLimitesLocales(raiz.transform);
        materialInstancia = CrearMaterialFantasma();

        foreach (Renderer renderer in modelo.GetComponentsInChildren<Renderer>(true))
        {
            if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer))
            {
                renderer.enabled = false;
                continue;
            }

            Material[] materiales = new Material[renderer.sharedMaterials.Length];

            for (int i = 0; i < materiales.Length; i++)
                materiales[i] = materialInstancia;

            renderer.sharedMaterials = materiales;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        raiz.SetActive(true);
        return raiz;
    }

    // En orden, para no borrar un componente del que depende otro.
    private static void DejarSoloModelo(GameObject modelo)
    {
        foreach (Behaviour componente in modelo.GetComponentsInChildren<Behaviour>(true))
        {
            if (!(componente is NetworkObject))
                DestroyImmediate(componente);
        }

        foreach (ParticleSystem particulas in modelo.GetComponentsInChildren<ParticleSystem>(true))
            DestroyImmediate(particulas);

        foreach (ParticleSystemRenderer particulas in modelo.GetComponentsInChildren<ParticleSystemRenderer>(true))
            DestroyImmediate(particulas);

        foreach (Joint joint in modelo.GetComponentsInChildren<Joint>(true))
            DestroyImmediate(joint);

        foreach (Collider collider in modelo.GetComponentsInChildren<Collider>(true))
            DestroyImmediate(collider);

        foreach (Rigidbody body in modelo.GetComponentsInChildren<Rigidbody>(true))
            DestroyImmediate(body);

        foreach (NetworkObject networkObject in modelo.GetComponentsInChildren<NetworkObject>(true))
            DestroyImmediate(networkObject);
    }

    // Caja que envuelve todas las mallas, en el espacio de la raíz del fantasma.
    private static Bounds CalcularLimitesLocales(Transform raiz)
    {
        Matrix4x4 aRaiz = raiz.worldToLocalMatrix;
        Bounds limites = new Bounds();
        bool hayMalla = false;

        foreach (MeshFilter filtro in raiz.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filtro.sharedMesh != null)
                Encapsular(ref limites, ref hayMalla, aRaiz * filtro.transform.localToWorldMatrix, filtro.sharedMesh.bounds);
        }

        foreach (SkinnedMeshRenderer skinned in raiz.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            Encapsular(ref limites, ref hayMalla, aRaiz * skinned.transform.localToWorldMatrix, skinned.localBounds);

        return hayMalla ? limites : new Bounds(Vector3.up, Vector3.one * 2f);
    }

    private static void Encapsular(ref Bounds limites, ref bool hayMalla, Matrix4x4 matriz, Bounds local)
    {
        Vector3 min = local.min;
        Vector3 max = local.max;

        for (int i = 0; i < 8; i++)
        {
            Vector3 esquina = new Vector3(
                (i & 1) == 0 ? min.x : max.x,
                (i & 2) == 0 ? min.y : max.y,
                (i & 4) == 0 ? min.z : max.z);

            Vector3 punto = matriz.MultiplyPoint3x4(esquina);

            if (!hayMalla)
            {
                limites = new Bounds(punto, Vector3.zero);
                hayMalla = true;
            }
            else
            {
                limites.Encapsulate(punto);
            }
        }
    }

    private Material CrearMaterialFantasma()
    {
        if (materialFantasma != null)
            return new Material(materialFantasma);

        // URP/Unlit transparente: sin texturas, solo el color.
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");

        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        Material material = new Material(shader);
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
        return material;
    }

    // =========================================================
    // RED
    // =========================================================

    // El servidor cobra y spawnea. Si ya no alcanzan los recursos (o no es Supervivencia) no se coloca nada.
    [ServerRpc]
    private void ColocarObjetoServerRpc(string weaponId, Vector3 posicion, Quaternion rotacion, PrecioRecursos precio)
    {
        PlayerRecursos recursos = transform.root.GetComponentInChildren<PlayerRecursos>();
        ObjetoColocable objeto = BuscarObjeto(weaponId);

        bool exito =
            objeto != null &&
            objeto.prefab != null &&
            ModoPermiteColocables() &&
            (playerHealth == null || playerHealth.IsAlive) &&
            recursos != null &&
            recursos.GastarRecursos(precio);

        if (exito)
        {
            NetworkObject colocado = Instantiate(objeto.prefab, posicion, rotacion);

            TorretaAutomatica torreta = colocado.GetComponent<TorretaAutomatica>();
            if (torreta != null)
                torreta.AsignarDueno(OwnerClientId);

            colocado.Spawn(true);
        }

        ResultadoColocacionClientRpc(exito, ParametrosParaDueno());
    }

    private ClientRpcParams ParametrosParaDueno()
    {
        return new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { OwnerClientId }
            }
        };
    }

    [ClientRpc]
    private void ResultadoColocacionClientRpc(bool exito, ClientRpcParams clientRpcParams = default)
    {
        PlaySound(exito ? sonidoColocar : sonidoDenegado);
    }

    // =========================================================
    // UTILIDADES
    // =========================================================

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

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }
}
