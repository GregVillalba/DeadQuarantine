using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GrupoBotonesFooterSimple : MonoBehaviour
{
    [System.Serializable]
    public class BotonSimple
    {
        public Button boton;
        [HideInInspector] public Image imagenFondo;
        [HideInInspector] public TMP_Text texto;
    }

    [Header("Botones")]
    [SerializeField] private BotonSimple btnRestablecer;
    [SerializeField] private BotonSimple btnAplicar;
    [SerializeField] private BotonSimple btnVolver;

    [Header("Colores Seleccionado (Tema APLICAR)")]
    [SerializeField] private Color colorFondoActivo = new Color(0.12f, 0.40f, 0.28f, 1f); // Verde oscuro de fondo
    [SerializeField] private Color colorTextoActivo = new Color(0.35f, 1f, 0.65f, 1f);   // Verde claro/neón

    [Header("Colores Inactivos (Normal / Blanco)")]
    [SerializeField] private Color colorFondoInactivo = new Color(0f, 0f, 0f, 0f);        // Fondo transparente
    [SerializeField] private Color colorTextoInactivo = Color.white;                      // Texto en blanco

    private BotonSimple[] listaBotones;
    private bool inicializado = false;

    private void Awake()
    {
        Inicializar();
    }

    private void Inicializar()
    {
        if (inicializado) return;
        inicializado = true;

        listaBotones = new BotonSimple[] { btnRestablecer, btnAplicar, btnVolver };

        foreach (var item in listaBotones)
        {
            if (item != null && item.boton != null)
            {
                // Obtiene automáticamente el Image del botón y el Text de sus hijos
                item.imagenFondo = item.boton.GetComponent<Image>();
                item.texto = item.boton.GetComponentInChildren<TMP_Text>();

                // Desactiva la transición nativa de Unity para que no sobreescriba los colores
                item.boton.transition = Selectable.Transition.None;

                BotonSimple presionado = item;
                item.boton.onClick.AddListener(() => Seleccionar(presionado));
            }
        }
    }

    private void OnEnable()
    {
        Inicializar();
        // Por defecto arranca seleccionado "APLICAR"
        Seleccionar(btnAplicar);
    }

    public void Seleccionar(BotonSimple seleccionado)
    {
        if (listaBotones == null) return;

        foreach (var item in listaBotones)
        {
            if (item == null || item.boton == null) continue;

            bool esActivo = (item == seleccionado);

            if (item.imagenFondo != null)
            {
                item.imagenFondo.color = esActivo ? colorFondoActivo : colorFondoInactivo;
            }

            if (item.texto != null)
            {
                item.texto.color = esActivo ? colorTextoActivo : colorTextoInactivo;
            }
        }
    }
}