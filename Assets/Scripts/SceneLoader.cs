using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public enum PantallaApertura
    {
        Ninguna,
        Modalidad,
        ElegirPersonaje   // ya no se usa: el personaje se elige en la ventana PERSONAJE del lobby
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
                // Pedido viejo: ya no hay selección de personaje acá, se va al menú principal.
                MostrarMenuPrincipal();
                pantallaSolicitada = PantallaApertura.Ninguna;
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

    // Botón CREAR de NetworkLobby: va DIRECTO a elegir modalidad / mapa / dificultad (sin selección de personaje).
    public void IrACrearSalaModalidad()
    {
        MultiplayerLobbyController lobbyController =
            FindFirstObjectByType<MultiplayerLobbyController>();

        if (lobbyController == null)
        {
            Debug.LogError(
                "[SceneLoader] No se encontró MultiplayerLobbyController en NetworkLobby."
            );
            return;
        }

        if (!lobbyController.VerificarInternet())
            return;

        pantallaSolicitada = PantallaApertura.Modalidad;
        SceneManager.LoadScene("PantallasUI");
    }

    // Botón UNIRSE de NetworkLobby: va DIRECTO a poner el código (sin selección de personaje).
    // Se conserva el nombre del método para no perder el enlace del botón en el Inspector.
    public void IrAUnirseElegirPersonaje()
    {
        IrAUnirseIngresarCodigo();
    }

    public void IrAUnirseIngresarCodigo()
    {
        MultiplayerLobbyController lobbyController =
            FindFirstObjectByType<MultiplayerLobbyController>();

        if (lobbyController == null)
        {
            Debug.LogError(
                "[SceneLoader] No se encontró MultiplayerLobbyController en NetworkLobby."
            );
            return;
        }

        // Verifica internet y muestra el panel de ingresar código (en NetworkLobby no cambia de escena).
        lobbyController.OnClick_IrAIngresarCodigo();
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