using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;

public class RankingTop10Manager : MonoBehaviour
{
    [System.Serializable]
    public class JugadorRanking
    {
        public string username;
        public int puntos;
        public string modo;
        public string dificultad;
    }

    // Estructura interna para retener el puesto real obtenido de la base de datos
    private class JugadorConPuesto
    {
        public int puestoOriginal;
        public JugadorRanking datos;
    }

    public static class JsonHelper
    {
        public static T[] FromJson<T>(string json)
        {
            string newJson = "{\"array\":" + json + "}";
            Wrapper<T> wrapper = JsonUtility.FromJson<Wrapper<T>>(newJson);
            return wrapper.array;
        }

        [Serializable]
        private class Wrapper<T>
        {
            public T[] array;
        }
    }

    [Header("Conexión URL")]
    [SerializeField] private string rankingUrl = "http://localhost:3000/api/ranking";

    [Header("Referencias de UI - Tabla")]
    [SerializeField] private Transform contenedorFilas;
    [SerializeField] private GameObject prefabFila;
    [SerializeField] private TMP_Text txtEstado;

    [Header("Dropdowns de Filtro")]
    // Opciones: 0: Todos/Predeterminado (1° al 10°), 1: Ascendente (1° al 10°), 2: Descendente (10° al 1°)
    [SerializeField] private TMP_Dropdown dropdownOrden;

    // Opciones: 0: Todos, 1: Singleplayer, 2: Multiplayer
    [SerializeField] private TMP_Dropdown dropdownModo;

    // Opciones: 0: Todos, 1: Normal, 2: Difícil, 3: Pesadilla
    [SerializeField] private TMP_Dropdown dropdownDificultad;

    [Header("Colores Podio")]
    [SerializeField] private Color colorOro = new Color(1f, 0.84f, 0f);      // 1°
    [SerializeField] private Color colorPlata = new Color(0.85f, 0.85f, 0.85f); // 2°
    [SerializeField] private Color colorBronce = new Color(0.8f, 0.5f, 0.2f);   // 3°
    [SerializeField] private Color colorResto = Color.white;

    private List<JugadorRanking> listaOriginal = new List<JugadorRanking>();

    private void Awake()
    {
        if (dropdownOrden != null)
            dropdownOrden.onValueChanged.AddListener(delegate { AplicarFiltrosYRenderizar(); });

        if (dropdownModo != null)
            dropdownModo.onValueChanged.AddListener(delegate { AplicarFiltrosYRenderizar(); });

        if (dropdownDificultad != null)
            dropdownDificultad.onValueChanged.AddListener(delegate { AplicarFiltrosYRenderizar(); });
    }

    private void OnEnable()
    {
        ActualizarRanking();
    }

    public void ActualizarRanking()
    {
        StartCoroutine(CargarRankingDesdeURL());
    }

    private IEnumerator CargarRankingDesdeURL()
    {
        if (txtEstado != null)
        {
            txtEstado.gameObject.SetActive(true);
            txtEstado.text = "Cargando Top 10...";
        }

        using (UnityWebRequest req = UnityWebRequest.Get(rankingUrl))
        {
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                if (txtEstado != null) txtEstado.gameObject.SetActive(false);

                string json = req.downloadHandler.text;
                JugadorRanking[] datos = JsonHelper.FromJson<JugadorRanking>(json);

                listaOriginal = (datos != null) ? new List<JugadorRanking>(datos) : new List<JugadorRanking>();

                AplicarFiltrosYRenderizar();
            }
            else
            {
                if (txtEstado != null)
                {
                    txtEstado.gameObject.SetActive(true);
                    txtEstado.text = "Error al conectar con el servidor.";
                }
                Debug.LogWarning($"[RANKING ERROR]: {req.error}");
            }
        }
    }

    public void AplicarFiltrosYRenderizar()
    {
        // Limpiar filas previas
        foreach (Transform child in contenedorFilas)
        {
            Destroy(child.gameObject);
        }

        if (listaOriginal == null || listaOriginal.Count == 0)
        {
            if (txtEstado != null)
            {
                txtEstado.gameObject.SetActive(true);
                txtEstado.text = "No hay registros disponibles.";
            }
            return;
        }

        // 1. Asignar el puesto oficial (1° a 10°) según el orden natural por puntos
        List<JugadorConPuesto> rankingBase = listaOriginal
            .OrderByDescending(j => j.puntos)
            .Select((j, index) => new JugadorConPuesto { puestoOriginal = index + 1, datos = j })
            .ToList();

        IEnumerable<JugadorConPuesto> filtrados = rankingBase;

        // 2. FILTRAR POR MODO
        if (dropdownModo != null && dropdownModo.value > 0)
        {
            string opcionModo = dropdownModo.options[dropdownModo.value].text.Trim();
            filtrados = filtrados.Where(item => string.Equals(item.datos.modo, opcionModo, StringComparison.OrdinalIgnoreCase));
        }

        // 3. FILTRAR POR DIFICULTAD
        if (dropdownDificultad != null && dropdownDificultad.value > 0)
        {
            string opcionDificultad = dropdownDificultad.options[dropdownDificultad.value].text.Trim();
            filtrados = filtrados.Where(item => string.Equals(item.datos.dificultad, opcionDificultad, StringComparison.OrdinalIgnoreCase));
        }

        // 4. ORDENAR RESPECTO AL VALOR DEL PUESTO
        // Ascendente: 1°, 2°, 3°... (del menor valor de puesto al mayor)
        // Descendente: 10°, 9°, 8°... (del mayor valor de puesto al menor)
        if (dropdownOrden != null)
        {
            string textoOrden = dropdownOrden.options[dropdownOrden.value].text.ToLower();

            if (textoOrden.Contains("descendente"))
            {
                // De 10° a 1°
                filtrados = filtrados.OrderByDescending(item => item.puestoOriginal);
            }
            else
            {
                // "Ascendente" o "Todos": de 1° a 10°
                filtrados = filtrados.OrderBy(item => item.puestoOriginal);
            }
        }
        else
        {
            filtrados = filtrados.OrderBy(item => item.puestoOriginal);
        }

        List<JugadorConPuesto> listaFinal = filtrados.Take(10).ToList();

        if (listaFinal.Count == 0)
        {
            if (txtEstado != null)
            {
                txtEstado.gameObject.SetActive(true);
                txtEstado.text = "No hay resultados con estos filtros.";
            }
            return;
        }

        if (txtEstado != null) txtEstado.gameObject.SetActive(false);

        // 5. Renderizar filas
        for (int i = 0; i < listaFinal.Count; i++)
        {
            JugadorConPuesto item = listaFinal[i];
            GameObject filaObj = Instantiate(prefabFila, contenedorFilas);

            TMP_Text[] textosFila = filaObj.GetComponentsInChildren<TMP_Text>();

            int puesto = item.puestoOriginal;

            // Columna 1: Puesto numérico real
            if (textosFila.Length > 0)
            {
                textosFila[0].text = $"{puesto}°";

                if (puesto == 1) textosFila[0].color = colorOro;
                else if (puesto == 2) textosFila[0].color = colorPlata;
                else if (puesto == 3) textosFila[0].color = colorBronce;
                else textosFila[0].color = colorResto;
            }

            // Columna 2: Usuario
            if (textosFila.Length > 1)
            {
                textosFila[1].text = item.datos.username;
            }

            // Columna 3: Puntos
            if (textosFila.Length > 2)
            {
                textosFila[2].text = item.datos.puntos.ToString("N0") + " PTS";
            }

            // Columna 4: Modo
            if (textosFila.Length > 3)
            {
                textosFila[3].text = item.datos.modo;
            }

            // Columna 5: Dificultad
            if (textosFila.Length > 4)
            {
                textosFila[4].text = item.datos.dificultad;
            }
        }
    }
}