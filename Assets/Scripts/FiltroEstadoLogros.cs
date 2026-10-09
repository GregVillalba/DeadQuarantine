using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class FiltroLogros : MonoBehaviour
{
    [Header("Referencias")]
    public Transform contenedorContent;  // El objeto "Content" del Scroll View
    public TMP_Dropdown dropdownModo;    // Todos / Singleplayer / Multiplayer
    public TMP_Dropdown dropdownTipo;    // Todos / Bajas / Supervivencia / Minijuego / Historia / Rondas / Easter Egg
    public TMP_Dropdown dropdownEstado;  // Todos / Bloqueado / Desbloqueado
    public GameObject textoNoItems;      // "sin_items"

    [Header("Etiquetas de modo (texto exacto, en minusculas)")]
    public string[] palabrasSingleplayer = { "singleplayer", "un jugador" };
    public string[] palabrasMultiplayer = { "multiplayer", "multijugador" };

    [Header("Depuracion")]
    public bool depurar = true;

    private void Start()
    {
        Conectar(dropdownModo);
        Conectar(dropdownTipo);
        Conectar(dropdownEstado);

        if (textoNoItems != null)
            textoNoItems.SetActive(false);
    }

    private void Conectar(TMP_Dropdown d)
    {
        if (d == null) return;
        d.onValueChanged.RemoveAllListeners();
        d.onValueChanged.AddListener(_ => AplicarFiltros());
    }

    // Llamalo tambien despues de generar/actualizar las tarjetas
    public void AplicarFiltros()
    {
        if (contenedorContent == null)
        {
            Debug.LogWarning("[FiltroLogros] contenedorContent no esta asignado.");
            return;
        }

        int modo = dropdownModo != null ? dropdownModo.value : 0;
        int estado = dropdownEstado != null ? dropdownEstado.value : 0;
        string tipo = ObtenerTipoSeleccionado();

        List<Transform> tarjetas = ObtenerTarjetas();
        int visibles = 0;

        foreach (Transform tarjeta in tarjetas)
        {
            TextMeshProUGUI[] textos = tarjeta.GetComponentsInChildren<TextMeshProUGUI>(true);

            bool okModo = CumpleModo(textos, modo);
            bool okTipo = CumpleTipo(textos, tipo);
            bool okEstado = CumpleEstado(textos, estado);
            bool mostrar = okModo && okTipo && okEstado;

            tarjeta.gameObject.SetActive(mostrar);
            if (mostrar) visibles++;

            if (depurar)
                Debug.Log($"[FiltroLogros] '{tarjeta.name}' modo={okModo} tipo={okTipo} estado={okEstado} -> {(mostrar ? "VISIBLE" : "oculta")}");
        }

        if (depurar)
            Debug.Log($"[FiltroLogros] tarjetas detectadas={tarjetas.Count} | filtro modo={modo} tipo='{tipo}' estado={estado} | visibles={visibles}");

        if (tarjetas.Count == 0)
            Debug.LogWarning("[FiltroLogros] No se detecto ninguna tarjeta. Revisa que las etiquetas de modo digan exactamente 'Singleplayer' o 'Multiplayer'.");

        if (textoNoItems != null)
            textoNoItems.SetActive(visibles == 0);
    }

    // Detecta las tarjetas sin depender de la jerarquia (filas, grids, etc.):
    // a partir de cada etiqueta de modo, sube hasta el contenedor mas grande que tenga UNA sola etiqueta.
    private List<Transform> ObtenerTarjetas()
    {
        var lista = new List<Transform>();
        var todos = contenedorContent.GetComponentsInChildren<TextMeshProUGUI>(true);

        foreach (var t in todos)
        {
            if (!EsEtiquetaModo(Normalizar(t.text))) continue;

            Transform tarjeta = t.transform;
            while (tarjeta.parent != null
                   && tarjeta.parent != contenedorContent
                   && ContarEtiquetasModo(tarjeta.parent) == 1)
            {
                tarjeta = tarjeta.parent;
            }

            if (!lista.Contains(tarjeta)) lista.Add(tarjeta);
        }

        return lista;
    }

    private int ContarEtiquetasModo(Transform raiz)
    {
        int n = 0;
        foreach (var t in raiz.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (EsEtiquetaModo(Normalizar(t.text))) n++;
        }
        return n;
    }

    private bool EsEtiquetaModo(string contenido)
    {
        foreach (string p in palabrasSingleplayer) if (contenido == p) return true;
        foreach (string p in palabrasMultiplayer) if (contenido == p) return true;
        return false;
    }

    private string ObtenerTipoSeleccionado()
    {
        if (dropdownTipo == null || dropdownTipo.value == 0) return ""; // Todos
        return Normalizar(dropdownTipo.options[dropdownTipo.value].text);
    }

    private bool CumpleModo(TextMeshProUGUI[] textos, int modo)
    {
        if (modo == 0) return true;
        string[] palabras = (modo == 1) ? palabrasSingleplayer : palabrasMultiplayer;

        foreach (var txt in textos)
        {
            string c = Normalizar(txt.text);
            foreach (string p in palabras) if (c == p) return true;
        }
        return false;
    }

    private bool CumpleTipo(TextMeshProUGUI[] textos, string tipo)
    {
        if (string.IsNullOrEmpty(tipo)) return true;

        foreach (var txt in textos)
        {
            if (Normalizar(txt.text) == tipo) return true;
        }
        return false;
    }

    private bool CumpleEstado(TextMeshProUGUI[] textos, int estado)
    {
        if (estado == 0) return true;

        bool esBloqueado = false;
        foreach (var txt in textos)
        {
            string c = Normalizar(txt.text);
            if (c == "desbloqueado" || c == "completado") { esBloqueado = false; break; }
            if (c == "bloqueado") { esBloqueado = true; break; }
        }

        return (estado == 1 && esBloqueado) || (estado == 2 && !esBloqueado);
    }

    private static string Normalizar(string s)
    {
        return s == null ? "" : s.Trim().ToLower();
    }
}