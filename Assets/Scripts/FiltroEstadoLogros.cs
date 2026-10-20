using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using TMPro;

public class FiltroLogros : MonoBehaviour
{
    [Header("Referencias")]
    public Transform contenedorContent;  
    public TMP_Dropdown dropdownModo;    // Todos / Singleplayer / Multiplayer
    public TMP_Dropdown dropdownTipo;    // Todos / Bajas / Supervivencia / Minijuego / Historia / Rondas / Easter Eggs
    public TMP_Dropdown dropdownEstado;  // Todos / Bloqueados / En progreso / Desbloqueado
    public GameObject textoNoItems;      

    [Header("Etiquetas de Modo (en minusculas)")]
    public string[] palabrasSingleplayer = { "singleplayer", "un jugador" };
    public string[] palabrasMultiplayer = { "multiplayer", "multijugador" };

    [Header("Depuracion")]
    public bool depurar = true;

    private bool iniciado;

    private void Start()
    {
        Conectar(dropdownModo);
        Conectar(dropdownTipo);
        Conectar(dropdownEstado);

        if (textoNoItems != null)
            textoNoItems.SetActive(false);

        iniciado = true;
        AplicarFiltros();
    }

    private void OnEnable()
    {
        // Al volver a abrir la pantalla se reaplican los filtros
        if (iniciado) AplicarFiltros();
    }

    private void Conectar(TMP_Dropdown d)
    {
        if (d == null) return;
        d.onValueChanged.RemoveAllListeners();
        d.onValueChanged.AddListener(_ => AplicarFiltros());
    }

    // Llamalo tambien despues de crear/actualizar tarjetas por codigo
    public void AplicarFiltros()
    {
        if (contenedorContent == null)
        {
            Debug.LogWarning("[FiltroLogros] contenedorContent no esta asignado.");
            return;
        }

        int grupoModo = ObtenerGrupoModo();                                   // 0 = todos, 1 = single, 2 = multi
        string tipo = Singular(SeleccionCanonica(dropdownTipo));              // "" = todos
        string estado = EstadoDesdeDropdown(SeleccionCanonica(dropdownEstado)); // "" = todos

        List<Transform> tarjetas = ObtenerTarjetas();
        int visibles = 0;

        foreach (Transform tarjeta in tarjetas)
        {
            TextMeshProUGUI[] textos = tarjeta.GetComponentsInChildren<TextMeshProUGUI>(true);

            bool okModo = CumpleModo(textos, grupoModo);
            bool okTipo = CumpleTipo(textos, tipo);
            bool okEstado = CumpleEstado(tarjeta, textos, estado);

            bool mostrar = okModo && okTipo && okEstado;
            tarjeta.gameObject.SetActive(mostrar);
            if (mostrar) visibles++;

            if (depurar)
                Debug.Log($"[FiltroLogros] '{tarjeta.name}' -> Estado detectado: {EstadoDeTarjeta(tarjeta, textos)} | Modo:{okModo} | Tipo:{okTipo} | Estado:{okEstado} ==> {(mostrar ? "VISIBLE" : "OCULTO")}");
        }

        if (depurar)
            Debug.Log($"[FiltroLogros] Tarjetas: {tarjetas.Count} | Visibles: {visibles} | Modo: {grupoModo} | Tipo: '{tipo}' | Estado: '{estado}'");

        if (textoNoItems != null)
        {
            textoNoItems.SetActive(visibles == 0);
            if (visibles == 0)
                textoNoItems.transform.SetAsLastSibling();
        }
    }

    // Interpreta la opcion elegida en el dropdown (acepta plural, "En progreso", etc.)
    private static string EstadoDesdeDropdown(string c)
    {
        if (c == "") return "";
        if (c.StartsWith("desbloq")) return "desbloqueado";
        if (c.StartsWith("bloq")) return "bloqueado";
        if (c.StartsWith("en pro")) return "en progreso";
        return c;
    }

    // ---------------------------------------------------------
    // MODO
    // ---------------------------------------------------------
    private int ObtenerGrupoModo()
    {
        string sel = SeleccionCanonica(dropdownModo);
        if (sel == "") return 0;

        if (ContienePalabra(palabrasSingleplayer, sel)) return 1;
        if (ContienePalabra(palabrasMultiplayer, sel)) return 2;

        // Si el texto de la opcion no coincide con las etiquetas, se usa la posicion
        return dropdownModo.value;
    }

    private bool CumpleModo(TextMeshProUGUI[] textos, int grupoModo)
    {
        if (grupoModo == 0) return true;
        string[] palabras = (grupoModo == 1) ? palabrasSingleplayer : palabrasMultiplayer;

        foreach (var txt in textos)
        {
            if (txt == null) continue;
            if (ContienePalabra(palabras, Canon(txt.text))) return true;
        }
        return false;
    }

    // ---------------------------------------------------------
    // TIPO
    // ---------------------------------------------------------
    private bool CumpleTipo(TextMeshProUGUI[] textos, string tipo)
    {
        if (string.IsNullOrEmpty(tipo)) return true;

        foreach (var txt in textos)
        {
            if (txt == null) continue;
            if (Singular(Canon(txt.text)) == tipo) return true;
        }
        return false;
    }

    // ---------------------------------------------------------
    // ESTADO
    // ---------------------------------------------------------
    private bool CumpleEstado(Transform tarjeta, TextMeshProUGUI[] textos, string estadoFiltro)
    {
        if (string.IsNullOrEmpty(estadoFiltro)) return true;
        return EstadoDeTarjeta(tarjeta, textos) == estadoFiltro;
    }

    // Lee el estado solo de los textos que realmente se ven en la tarjeta
    private string EstadoDeTarjeta(Transform tarjeta, TextMeshProUGUI[] textos)
    {
        foreach (var txt in textos)
        {
            if (txt == null || string.IsNullOrWhiteSpace(txt.text)) continue;
            if (!EsVisibleDentroDeTarjeta(txt, tarjeta)) continue;

            string c = Canon(txt.text);

            if (c == "desbloqueado" || c == "desbloqueados") return "desbloqueado";
            if (c == "bloqueado" || c == "bloqueados") return "bloqueado";
            if (c == "en progreso" || c.StartsWith("en progreso ")) return "en progreso";
        }
        return "desconocido";
    }

    // El texto cuenta solo si su componente y todos sus padres hasta la tarjeta están activos.
    // No se mira la propia tarjeta, porque el filtro la activa y desactiva.
    private static bool EsVisibleDentroDeTarjeta(TextMeshProUGUI txt, Transform tarjeta)
    {
        if (!txt.enabled) return false;

        Transform t = txt.transform;
        while (t != null && t != tarjeta)
        {
            if (!t.gameObject.activeSelf) return false;
            t = t.parent;
        }
        return true;
    }
    // ---------------------------------------------------------
    // TARJETAS
    // ---------------------------------------------------------
    private List<Transform> ObtenerTarjetas()
    {
        var lista = new List<Transform>();
        if (contenedorContent == null) return lista;

        var todos = contenedorContent.GetComponentsInChildren<TextMeshProUGUI>(true);

        foreach (var t in todos)
        {
            if (!EsEtiquetaModo(Canon(t.text))) continue;

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
            if (EsEtiquetaModo(Canon(t.text))) n++;
        }
        return n;
    }

    private bool EsEtiquetaModo(string contenido)
    {
        return ContienePalabra(palabrasSingleplayer, contenido)
            || ContienePalabra(palabrasMultiplayer, contenido);
    }

    // ---------------------------------------------------------
    // UTILIDADES
    // ---------------------------------------------------------
    private static bool ContienePalabra(string[] palabras, string canonico)
    {
        if (palabras == null || string.IsNullOrEmpty(canonico)) return false;
        foreach (string p in palabras)
        {
            if (Canon(p) == canonico) return true;
        }
        return false;
    }

    // Devuelve "" si el dropdown esta en "Todos"
    private static string SeleccionCanonica(TMP_Dropdown d)
    {
        if (d == null || d.options.Count == 0 || d.value <= 0) return "";

        string c = Canon(d.options[d.value].text);
        return (c == "todos" || c == "todo" || c == "all") ? "" : c;
    }

    private static string Singular(string s)
    {
        return (!string.IsNullOrEmpty(s) && s.Length > 3 && s.EndsWith("s"))
            ? s.Substring(0, s.Length - 1)
            : s;
    }

    // Minusculas, sin acentos, sin etiquetas TMP y con espacios normalizados
    private static string Canon(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";

        s = Regex.Replace(s, "<.*?>", string.Empty);
        s = s.Replace("\u00A0", " ").Trim().ToLowerInvariant();

        string descompuesto = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(descompuesto.Length);
        foreach (char ch in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }

        return Regex.Replace(sb.ToString().Normalize(NormalizationForm.FormC), @"\s+", " ");
    }
}