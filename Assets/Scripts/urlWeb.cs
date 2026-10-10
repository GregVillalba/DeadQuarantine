using UnityEngine;

public class OpenWebLink : MonoBehaviour
{
    [Header("Configuración del Enlace")]
    [Tooltip("URL a la que redirigirá el botón")]
    [SerializeField] private string targetUrl = "https://dead-quarentine-web.wasmer.app/";

    public void AbrirSitioWeb()
    {
        if (!string.IsNullOrEmpty(targetUrl))
        {
            Application.OpenURL(targetUrl);
        }
        else
        {
            Debug.LogWarning("[OpenWebLink] La URL está vacía.");
        }
    }
}