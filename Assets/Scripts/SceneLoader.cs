using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public enum PantallaApertura
    {
        Ninguna,
        Modalidad,
        ElegirPersonaje
    }

    // Esta variable estática sobrevive al cambio de escenas
    public static PantallaApertura pantallaSolicitada = PantallaApertura.Ninguna;

    [Header("Paneles / Sub-pantallas en PantallasUI")]
    [SerializeField] private GameObject menuPrincipal;
    [SerializeField] private GameObject pantallaModalidad;
    [SerializeField] private GameObject pantallaElegirPersonaje;

    private void Start()
    {
        // Al arrancar PantallasUI, verifica si venías de NetworkLobby pidiendo abrir una pantalla
        if (SceneManager.GetActiveScene().name == "PantallasUI")
        {
            if (pantallaSolicitada == PantallaApertura.Modalidad)
            {
                MostrarPantallaModalidad();
                pantallaSolicitada = PantallaApertura.Ninguna; // Se reinicia
            }
            else if (pantallaSolicitada == PantallaApertura.ElegirPersonaje)
            {
                MostrarPantallaElegirPersonaje();
                pantallaSolicitada = PantallaApertura.Ninguna; // Se reinicia
            }
            else
            {
                MostrarMenuPrincipal();
            }
        }
    }

    // --- CARGA DE ESCENAS ---

    public void LoadNetworkLobby()
    {
        SceneManager.LoadScene("NetworkLobby");
    }

    public void LoadPantallasUI()
    {
        SceneManager.LoadScene("PantallasUI");
    }

    // --- MÉTODOS PARA LLAMAR DESDE NETWORK LOBBY ---

    // Llamar al presionar CREAR en NetworkLobby
    public void IrACrearSalaModalidad()
    {
        pantallaSolicitada = PantallaApertura.Modalidad;
        SceneManager.LoadScene("PantallasUI");
    }

    // Llamar al presionar UNIRSE en NetworkLobby
    public void IrAUnirseElegirPersonaje()
    {
        pantallaSolicitada = PantallaApertura.ElegirPersonaje;
        SceneManager.LoadScene("PantallasUI");
    }

    // --- NAVEGACIÓN DENTRO DE PANTALLAS UI ---

    public void MostrarPantallaModalidad()
    {
        DesactivarTodosLosPaneles();
        if (pantallaModalidad != null)
        {
            pantallaModalidad.SetActive(true);
        }
    }

    public void MostrarPantallaElegirPersonaje()
    {
        DesactivarTodosLosPaneles();
        if (pantallaElegirPersonaje != null)
        {
            pantallaElegirPersonaje.SetActive(true);
        }
    }

    public void MostrarMenuPrincipal()
    {
        DesactivarTodosLosPaneles();
        if (menuPrincipal != null)
        {
            menuPrincipal.SetActive(true);
        }
    }

    private void DesactivarTodosLosPaneles()
    {
        if (menuPrincipal != null) menuPrincipal.SetActive(false);
        if (pantallaModalidad != null) pantallaModalidad.SetActive(false);
        if (pantallaElegirPersonaje != null) pantallaElegirPersonaje.SetActive(false);
    }
}