using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;

// Fila de círculos (uno por compañero) que se ubica justo a la derecha de un texto
// durante la fase de compras en multijugador:
// verde = ya marcó "Listo", rojo = sigue comprando, gris = no conectado
// (o muerto: no puede marcar "Listo" y no hace falta esperarlo).
// Se crea por código con Crear(texto), no hace falta armarla en el prefab.
public class IndicadorCompanerosListos : MonoBehaviour
{
    // El multijugador acepta hasta 4 jugadores: el local + 3 compañeros.
    public const int MaxCompaneros = 3;

    private static readonly Color ColorListo = new Color(0.25f, 0.85f, 0.3f, 1f);
    private static readonly Color ColorComprando = new Color(0.9f, 0.2f, 0.2f, 1f);
    private static readonly Color ColorDesconectado = new Color(0.45f, 0.45f, 0.45f, 1f);

    private static Sprite spriteCirculo;

    private TextMeshProUGUI texto;
    private float separacion;
    private RectTransform rect;

    private readonly Image[] circulos = new Image[MaxCompaneros];
    private readonly RoundManager.EstadoCompaneroCompras[] estados =
        new RoundManager.EstadoCompaneroCompras[MaxCompaneros];

    public static IndicadorCompanerosListos Crear(TextMeshProUGUI texto, float separacion = 12f)
    {
        GameObject objeto = new GameObject("CompanerosListos", typeof(RectTransform));
        objeto.layer = texto.gameObject.layer;

        RectTransform rect = (RectTransform)objeto.transform;
        rect.SetParent(texto.rectTransform, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);

        IndicadorCompanerosListos indicador = objeto.AddComponent<IndicadorCompanerosListos>();
        indicador.texto = texto;
        indicador.separacion = separacion;
        indicador.rect = rect;
        indicador.CrearCirculos();

        objeto.SetActive(false);
        return indicador;
    }

    // Los clientes siempre juegan acompañados; el host está solo si es el único conectado.
    public static bool HayCompaneros()
    {
        NetworkManager networkManager = NetworkManager.Singleton;

        if (networkManager == null)
            return false;

        if (!networkManager.IsServer)
            return true;

        return networkManager.ConnectedClientsIds.Count > 1;
    }

    public void Mostrar(bool visible)
    {
        if (gameObject.activeSelf != visible)
            gameObject.SetActive(visible);
    }

    private void LateUpdate()
    {
        if (RoundManager.Instance != null)
            RoundManager.Instance.ObtenerEstadosCompanerosFaseCompras(estados);

        for (int i = 0; i < circulos.Length; i++)
            circulos[i].color = ColorDe(estados[i]);

        Ubicar();
    }

    private static Color ColorDe(RoundManager.EstadoCompaneroCompras estado)
    {
        switch (estado)
        {
            case RoundManager.EstadoCompaneroCompras.Listo:
                return ColorListo;
            case RoundManager.EstadoCompaneroCompras.Comprando:
                return ColorComprando;
            default:
                return ColorDesconectado;
        }
    }

    // Tamaño según la fuente del texto y posición justo después de donde termina lo escrito
    // (textBounds sirve igual si el texto está alineado a la izquierda o centrado).
    private void Ubicar()
    {
        float diametro = texto.fontSize * 0.65f;
        float espacio = diametro * 0.45f;

        rect.sizeDelta = new Vector2(MaxCompaneros * diametro + (MaxCompaneros - 1) * espacio, diametro);

        for (int i = 0; i < circulos.Length; i++)
        {
            RectTransform circulo = circulos[i].rectTransform;
            circulo.sizeDelta = new Vector2(diametro, diametro);
            circulo.anchoredPosition = new Vector2(i * (diametro + espacio) + diametro * 0.5f, 0f);
        }

        Bounds bounds = texto.textBounds;
        rect.localPosition = new Vector3(bounds.max.x + separacion, bounds.center.y, 0f);
    }

    private void CrearCirculos()
    {
        for (int i = 0; i < circulos.Length; i++)
        {
            GameObject objeto = new GameObject("Companero" + (i + 1), typeof(RectTransform));
            objeto.layer = gameObject.layer;

            RectTransform circulo = (RectTransform)objeto.transform;
            circulo.SetParent(rect, false);
            circulo.anchorMin = new Vector2(0f, 0.5f);
            circulo.anchorMax = new Vector2(0f, 0.5f);
            circulo.pivot = new Vector2(0.5f, 0.5f);

            Image imagen = objeto.AddComponent<Image>();
            imagen.sprite = SpriteCirculo();
            imagen.raycastTarget = false;
            imagen.color = ColorDesconectado;

            circulos[i] = imagen;
        }
    }

    // Círculo blanco con borde oscuro, generado una sola vez. El color de la Image tiñe
    // el relleno y el borde queda oscuro para que se lea sobre cualquier fondo.
    private static Sprite SpriteCirculo()
    {
        if (spriteCirculo != null)
            return spriteCirculo;

        const int tamano = 64;
        const float borde = 6f;
        float radio = tamano * 0.5f - 1f;
        Vector2 centro = new Vector2(tamano * 0.5f, tamano * 0.5f);
        Color colorBorde = new Color(0.08f, 0.08f, 0.08f, 1f);

        Texture2D textura = new Texture2D(tamano, tamano, TextureFormat.RGBA32, false);
        textura.wrapMode = TextureWrapMode.Clamp;
        textura.filterMode = FilterMode.Bilinear;

        Color[] pixeles = new Color[tamano * tamano];

        for (int y = 0; y < tamano; y++)
        {
            for (int x = 0; x < tamano; x++)
            {
                float distancia = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), centro);
                float alfa = Mathf.Clamp01(radio - distancia + 0.5f);
                float relleno = Mathf.Clamp01(radio - borde - distancia + 0.5f);

                Color color = Color.Lerp(colorBorde, Color.white, relleno);
                color.a = alfa;
                pixeles[y * tamano + x] = color;
            }
        }

        textura.SetPixels(pixeles);
        textura.Apply();

        spriteCirculo = Sprite.Create(textura, new Rect(0f, 0f, tamano, tamano), new Vector2(0.5f, 0.5f), 100f);
        spriteCirculo.name = "CirculoCompanero";
        return spriteCirculo;
    }
}
