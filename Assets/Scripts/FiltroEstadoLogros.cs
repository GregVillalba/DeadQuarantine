using UnityEngine;
using TMPro;

public class FiltroEstadoLogros : MonoBehaviour
{
    [Header("Referencias")]
    public Transform contenedorContent;
    public TMP_Dropdown dropdownFiltro;

    private void Start()
    {
        if (dropdownFiltro != null)
        {
            dropdownFiltro.onValueChanged.AddListener(FiltrarPorEstado);
        }
    }

    public void FiltrarPorEstado(int indice)
    {
        if (contenedorContent == null) return;

        foreach (Transform tarjeta in contenedorContent)
        {
            if (indice == 0) // Todos
            {
                tarjeta.gameObject.SetActive(true);
                continue;
            }

            Transform estadoTransform = tarjeta.Find("estado");

            if (estadoTransform != null)
            {
                TextMeshProUGUI textoEstado = estadoTransform.GetComponent<TextMeshProUGUI>();

                if (textoEstado != null)
                {
                    string texto = textoEstado.text.Trim().ToLower();
                    bool estaBloqueado = texto.Contains("bloqueado");

                    if (indice == 1) // Bloqueados
                    {
                        tarjeta.gameObject.SetActive(estaBloqueado);
                    }
                    else if (indice == 2) // Desbloqueados
                    {
                        tarjeta.gameObject.SetActive(!estaBloqueado);
                    }
                }
            }
        }
    }
}