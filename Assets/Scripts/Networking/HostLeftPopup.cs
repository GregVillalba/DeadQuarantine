using UnityEngine;

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

    private void Mostrar()
    {
        mostrando = true;

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