using UnityEngine;
using TMPro;

public class FiltroEstadoLogros : MonoBehaviour
{
    [Header("Referencias")]
    public Transform contenedorContent;
    public TMP_Dropdown dropdownFiltro;
    public GameObject textoNoItems; // Texto que dice "No items"

    private void Start()
    {
        if (dropdownFiltro != null)
        {
            dropdownFiltro.onValueChanged.RemoveAllListeners();
            dropdownFiltro.onValueChanged.AddListener(FiltrarPorEstado);
        }

        if (textoNoItems != null)
            textoNoItems.SetActive(false);
    }

    public void FiltrarPorEstado(int indice)
    {
        if (contenedorContent == null) return;

        int tarjetasVisibles = 0;

        foreach (Transform tarjeta in contenedorContent)
        {
            if (indice == 0) // Todos
            {
                tarjeta.gameObject.SetActive(true);
                tarjetasVisibles++;
                continue;
            }

            TextMeshProUGUI[] todosLosTextos = tarjeta.GetComponentsInChildren<TextMeshProUGUI>(true);
            bool esBloqueado = false;

            foreach (var txt in todosLosTextos)
            {
                string contenido = txt.text.Trim().ToLower();

                if (contenido.Contains("bloqueado"))
                {
                    esBloqueado = true;
                    break;
                }
                else if (contenido.Contains("desbloqueado") || contenido.Contains("completado"))
                {
                    esBloqueado = false;
                    break;
                }
            }

            bool mostrar = (indice == 1 && esBloqueado) || (indice == 2 && !esBloqueado);
            tarjeta.gameObject.SetActive(mostrar);

            if (mostrar)
            {
                tarjetasVisibles++;
            }
        }

        // Si no quedó ninguna tarjeta visible, prende el cartel
        if (textoNoItems != null)
        {
            textoNoItems.SetActive(tarjetasVisibles == 0);
        }
    }
}