using UnityEngine;
using TMPro;

public class ItemLogroUI : MonoBehaviour
{
    [Header("Referencias Visuales")]
    [SerializeField] private TMP_Text txtTitulo;
    [SerializeField] private TMP_Text txtPorcentajeGlobal;

    public int LogroId { get; private set; }

    public void ConfigurarTarjeta(int id, string titulo, float porcentajeGlobal)
    {
        LogroId = id;

        if (txtTitulo != null)
            txtTitulo.text = titulo;

        if (txtPorcentajeGlobal != null)
            txtPorcentajeGlobal.text = $"GLOBAL {porcentajeGlobal:0.#}%";
    }

    public void MostrarErrorCarga()
    {
        if (txtPorcentajeGlobal != null)
        {
            txtPorcentajeGlobal.text = "GLOBAL --%";
        }
    }
}