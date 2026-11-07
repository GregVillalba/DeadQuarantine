using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Zona en forma de caja para el tutorial, SIN collider (así no frena balas, rayos de la colocación ni al jugador).
/// La usa TutorialSecciones para saber si el jugador llegó a un lugar (ej: la última plataforma de los obstáculos)
/// y para marcar en el piso dónde se coloca la torreta: mientras es la ColocacionActiva, ColocadorObjetos solo deja
/// colocar adentro. El marcador del piso se crea al empezar y parpadea mientras está visible.
/// En el editor se ve como una caja verde (Gizmo): moverla y cambiarle el Tamaño desde el Inspector.
/// </summary>
public class ZonaTutorial : MonoBehaviour
{
    [Tooltip("Tamaño de la caja (m), centrada en este objeto. Usa la rotación del objeto.")]
    [SerializeField] private Vector3 tamano = new Vector3(2f, 2f, 2f);

    [Header("Marcador en el piso (opcional)")]
    [Tooltip("Crea un rectángulo translúcido en la base de la caja. Se muestra con MostrarMarcador.")]
    [SerializeField] private bool usarMarcadorPiso;
    [SerializeField] private Color colorMarcador = new Color(0.15f, 1f, 0.3f, 0.45f);
    [Tooltip("Parpadeos por segundo del marcador. 0 = fijo.")]
    [SerializeField, Min(0f)] private float parpadeosPorSegundo = 1.5f;
    [Tooltip("Alto del marcador sobre la base de la caja (m), para que no se mezcle con el piso.")]
    [SerializeField] private float alturaMarcador = 0.03f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private GameObject marcador;
    private Material materialMarcador;

    /// <summary>Zona en la que ColocadorObjetos deja colocar (null = sin límite, como en las partidas).</summary>
    public static ZonaTutorial ColocacionActiva { get; set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        ColocacionActiva = null;
    }

    public Vector3 Tamano => tamano;

    /// <summary>true si el punto (en el mundo) está dentro de la caja.</summary>
    public bool Contiene(Vector3 punto)
    {
        return ContieneLocal(transform.InverseTransformPoint(punto), tamano);
    }

    public static bool ContieneLocal(Vector3 local, Vector3 tamano)
    {
        Vector3 mitad = tamano * 0.5f;

        return Mathf.Abs(local.x) <= mitad.x &&
               Mathf.Abs(local.y) <= mitad.y &&
               Mathf.Abs(local.z) <= mitad.z;
    }

    /// <summary>Solo para armar zonas en código (tests).</summary>
    public void Configurar(Vector3 nuevoTamano)
    {
        tamano = nuevoTamano;
    }

    public void MostrarMarcador(bool mostrar)
    {
        if (!usarMarcadorPiso)
            return;

        if (mostrar && marcador == null)
            CrearMarcador();

        if (marcador != null)
            marcador.SetActive(mostrar);
    }

    private void OnDisable()
    {
        if (ColocacionActiva == this)
            ColocacionActiva = null;
    }

    private void OnDestroy()
    {
        if (ColocacionActiva == this)
            ColocacionActiva = null;

        if (materialMarcador != null)
            Destroy(materialMarcador);
    }

    private void Update()
    {
        if (marcador == null || !marcador.activeSelf || materialMarcador == null)
            return;

        Color color = colorMarcador;

        if (parpadeosPorSegundo > 0f)
            color.a *= Mathf.Lerp(0.25f, 1f, Mathf.PingPong(Time.time * parpadeosPorSegundo * 2f, 1f));

        PintarMarcador(color);
    }

    private void CrearMarcador()
    {
        marcador = GameObject.CreatePrimitive(PrimitiveType.Cube);
        marcador.name = "MarcadorPiso";

        // Solo visual: sin collider (no tapa disparos ni la colocación).
        Collider collider = marcador.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        marcador.transform.SetParent(transform, false);
        marcador.transform.localPosition = new Vector3(0f, -tamano.y * 0.5f + alturaMarcador, 0f);
        marcador.transform.localRotation = Quaternion.identity;
        marcador.transform.localScale = new Vector3(tamano.x, 0.02f, tamano.z);

        Renderer render = marcador.GetComponent<Renderer>();
        materialMarcador = CrearMaterialTransparente();
        render.sharedMaterial = materialMarcador;
        render.shadowCastingMode = ShadowCastingMode.Off;
        render.receiveShadows = false;

        PintarMarcador(colorMarcador);
    }

    private void PintarMarcador(Color color)
    {
        if (materialMarcador.HasProperty(BaseColorId))
            materialMarcador.SetColor(BaseColorId, color);

        if (materialMarcador.HasProperty(ColorId))
            materialMarcador.SetColor(ColorId, color);
    }

    // Igual que el fantasma de ColocadorObjetos: URP/Unlit transparente, solo color.
    private static Material CrearMaterialTransparente()
    {
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

    private void OnDrawGizmos()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(colorMarcador.r, colorMarcador.g, colorMarcador.b, 0.15f);
        Gizmos.DrawCube(Vector3.zero, tamano);
        Gizmos.color = new Color(colorMarcador.r, colorMarcador.g, colorMarcador.b, 0.8f);
        Gizmos.DrawWireCube(Vector3.zero, tamano);
    }
}
