using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Cartel que ve el INVITADO cuando el host abandona la partida.
/// Va en un objeto vacío SIEMPRE ACTIVO de MainSceneMultiPlayer (no en el panel
/// ni dentro del prefab del Player, que se destruye al perder la conexión).
/// </summary>
public class HostLeftPopup : MonoBehaviour
{
    [SerializeField] private GameObject panel;

    [Tooltip("Cámara de respaldo: apagada por defecto, sin AudioListener. Se enciende cuando el Player (y su cámara) se destruyen.")]
    [SerializeField] private Camera camaraDeFondo;

    private NetworkBootstrap network;
    private bool mostrando;
    private NetworkManager nm;
    private bool esInvitado;

    private void Awake()
    {
        if (panel != null)
            panel.SetActive(false);

        if (camaraDeFondo != null)
            camaraDeFondo.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        network = NetworkBootstrap.Instance;

        if (network != null)
            network.OnHostLeft += Mostrar;
    }

    private void OnDisable()
    {
        if (network != null)
            network.OnHostLeft -= Mostrar;
    }
    private void Start()
{
    nm = NetworkManager.Singleton;

    Debug.Log("[HostLeftPopup] Start. NetworkManager: " + (nm != null) +
              " / IsClient: " + (nm != null && nm.IsClient) +
              " / IsServer: " + (nm != null && nm.IsServer));

    // Solo el invitado necesita escuchar si el host se va
    if (nm == null || nm.IsServer || !nm.IsClient)
        return;

    esInvitado = true;
    nm.OnClientDisconnectCallback += OnClientDisconnect;
    nm.OnClientStopped += OnClientStopped;
}

private void OnDestroy()
{
    if (nm == null)
        return;

    nm.OnClientDisconnectCallback -= OnClientDisconnect;
    nm.OnClientStopped -= OnClientStopped;
}

private void OnClientDisconnect(ulong clientId)
{
    Debug.Log("[HostLeftPopup] OnClientDisconnect. clientId: " + clientId);
    MostrarSiCorresponde();
}

private void OnClientStopped(bool wasHost)
{
    Debug.Log("[HostLeftPopup] OnClientStopped");
    MostrarSiCorresponde();
}

private void MostrarSiCorresponde()
{
    if (!esInvitado || mostrando)
        return;

    // Si el invitado salió a propósito (Pausa → Salir), no mostramos el cartel
    if (network != null && network.IsLeavingIntentionally)
        return;

    Mostrar();
}

    private void Mostrar()
    {
        //mostrando = true;
        if (mostrando)
            return;

        mostrando = true;
        Debug.Log("[HostLeftPopup] Mostrar. panel: " + (panel != null) +
                                " / camara: " + (camaraDeFondo != null));

        if (camaraDeFondo != null)
            camaraDeFondo.gameObject.SetActive(true);

        if (panel != null)
            panel.SetActive(true);

        Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    // LateUpdate corre después de los Update de otros scripts,
    // así que gana si alguno intenta volver a bloquear el cursor.
    private void LateUpdate()
    {
        if (!mostrando)
            return;

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    // Botón "Salir" del cartel
    public void OnClick_Salir()
    {
        if (network != null)
            _ = network.GoToMenuDueToDisconnect();
    }
}