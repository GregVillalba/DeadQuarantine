
using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class AchievementsMenuUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text valorLogrosCompletado;
    [SerializeField] private TMP_Dropdown dropdownFiltro;
    [SerializeField] private Slider barraProgresoLogros;

    private AchievementUIItem[] elementos;


    // ============================================================
    // AWAKE
    // ============================================================

    private void Awake()
    {
        BuscarElementos();
    }


    // ============================================================
    // ENABLE
    // ============================================================

    private void OnEnable()
    {
        if (dropdownFiltro != null)
        {
            dropdownFiltro.onValueChanged.RemoveListener(CambiarFiltro);
            dropdownFiltro.onValueChanged.AddListener(CambiarFiltro);
        }

        StartCoroutine(ActualizarAlAbrir());
    }


    // ============================================================
    // ACTUALIZAR AL ABRIR
    // ============================================================

    private IEnumerator ActualizarAlAbrir()
    {
        // Esperamos un frame para asegurarnos de que
        // el estado de los logros ya esté actualizado.
        yield return null;

        ActualizarTodos();
    }


    // ============================================================
    // BUSCAR ELEMENTOS
    // ============================================================

    private void BuscarElementos()
    {
        elementos = FindObjectsByType<AchievementUIItem>(
            FindObjectsInactive.Include
        );

        Debug.Log(
            $"[ACHIEVEMENTS UI] Tarjetas encontradas: {elementos.Length}"
        );
    }


    // ============================================================
    // ACTUALIZAR TODOS
    // ============================================================

    public void ActualizarTodos()
    {
        if (AchievementsManager.Instance == null)
        {
            Debug.LogWarning(
                "[ACHIEVEMENTS UI] AchievementsManager no existe."
            );

            return;
        }


        BuscarElementos();


        int logrosCompletados = 0;


        // ========================================================
        // ACTUALIZAR TARJETAS
        // ========================================================

        foreach (AchievementUIItem elemento in elementos)
        {
            if (elemento == null)
                continue;


            if (elemento.EstaDesbloqueado())
            {
                logrosCompletados++;
            }


            elemento.ActualizarEstado();
        }


        // ========================================================
        // OBTENER TOTAL
        // ========================================================

        int logrosTotales =
            AchievementsManager.Instance
                .ObtenerCantidadTotalLogros();


        // ========================================================
        // TEXTO
        // ========================================================

        if (valorLogrosCompletado != null)
        {
            valorLogrosCompletado.text =
                $"{logrosCompletados} / {logrosTotales} logros completados";
        }


        // ========================================================
        // BARRA DE PROGRESO
        // ========================================================

        if (barraProgresoLogros != null)
        {
            float porcentaje = 0f;


            if (logrosTotales > 0)
            {
                porcentaje =
                    (float)logrosCompletados / logrosTotales;
            }


            barraProgresoLogros.minValue = 0f;
            barraProgresoLogros.maxValue = 1f;
            barraProgresoLogros.value = porcentaje;
        }


        // ========================================================
        // DEBUG
        // ========================================================

        Debug.Log(
            $"[ACHIEVEMENTS UI] " +
            $"{logrosCompletados}/{logrosTotales} logros completados."
        );


        // ========================================================
        // APLICAR FILTRO ACTUAL
        // ========================================================

        if (dropdownFiltro != null)
        {
            CambiarFiltro(
                dropdownFiltro.value
            );
        }
    }


    // ============================================================
    // CAMBIAR FILTRO
    // ============================================================

    private void CambiarFiltro(int indice)
    {
        Debug.Log(
            $"[ACHIEVEMENTS UI] CAMBIANDO FILTRO -> {indice}"
        );


        if (indice == 0)
        {
            MostrarTodos();
        }
        else if (indice == 1)
        {
            MostrarSoloBloqueados();
        }
        else if (indice == 2)
        {
            MostrarSoloDesbloqueados();
        }
    }


    // ============================================================
    // MOSTRAR TODOS
    // ============================================================

    private void MostrarTodos()
    {
        foreach (AchievementUIItem elemento in elementos)
        {
            if (elemento != null)
            {
                elemento.gameObject.SetActive(true);
            }
        }


        Debug.Log(
            "[ACHIEVEMENTS UI] Mostrando TODOS"
        );
    }


    // ============================================================
    // MOSTRAR BLOQUEADOS
    // ============================================================

    private void MostrarSoloBloqueados()
    {
        foreach (AchievementUIItem elemento in elementos)
        {
            if (elemento == null)
                continue;


            bool desbloqueado =
                elemento.EstaDesbloqueado();


            elemento.gameObject.SetActive(
                !desbloqueado
            );
        }


        Debug.Log(
            "[ACHIEVEMENTS UI] Mostrando BLOQUEADOS"
        );
    }


    // ============================================================
    // MOSTRAR DESBLOQUEADOS
    // ============================================================

    private void MostrarSoloDesbloqueados()
    {
        foreach (AchievementUIItem elemento in elementos)
        {
            if (elemento == null)
                continue;


            bool desbloqueado =
                elemento.EstaDesbloqueado();


            elemento.gameObject.SetActive(
                desbloqueado
            );
        }


        Debug.Log(
            "[ACHIEVEMENTS UI] Mostrando DESBLOQUEADOS"
        );
    }


    // ============================================================
    // DISABLE
    // ============================================================

    private void OnDisable()
    {
        if (dropdownFiltro != null)
        {
            dropdownFiltro.onValueChanged.RemoveListener(
                CambiarFiltro
            );
        }
    }
}
