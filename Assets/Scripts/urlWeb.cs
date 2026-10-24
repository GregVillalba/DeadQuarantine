using UnityEngine;

public class CreditosLinks : MonoBehaviour
{
    private const string URL_LUCIANA = "https://www.linkedin.com/in/lucianaoviedowanuffelen/";
    private const string URL_MARTINA = "https://www.linkedin.com/in/marsoria/";
    private const string URL_GREGORIO = "https://www.linkedin.com/in/gregorio-villalba/";
    private const string URL_ALAN = "https://www.linkedin.com/in/alan-riggio-78ab22225/";
    private const string URL_FRANCO = "https://www.linkedin.com/in/franco-lencina-b20397282/";
    private const string URL_web = "https://dead-quarentine-web.wasmer.app/";

   

    public void AbrirWEB()
    {
        AbrirURL(URL_web);
    }
    // --- MÉTODOS PARA CADA PERFIL ---
    public void AbrirLinkedInLuciana()
    {
        AbrirURL(URL_LUCIANA);
    }

    public void AbrirLinkedInMartina()
    {
        AbrirURL(URL_MARTINA);
    }

    public void AbrirLinkedInGregorio()
    {
        AbrirURL(URL_GREGORIO);
    }

    public void AbrirLinkedInAlan()
    {
        AbrirURL(URL_ALAN);
    }

    public void AbrirLinkedInFranco()
    {
        AbrirURL(URL_FRANCO);
    }

    // --- MÉTODO AUXILIAR ---

    private void AbrirURL(string url)
    {
        if (!string.IsNullOrEmpty(url))
        {
            Application.OpenURL(url);
        }
        else
        {
            Debug.LogWarning("[CreditosLinks] La URL especificada no es válida.");
        }
    }
}