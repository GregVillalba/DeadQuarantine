using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;

public class StoryIntroController : NetworkBehaviour
{
    [Header("Historia Singleplayer")]
    [SerializeField] private GameObject popupHistoria;

    [Header("HUD del jugador")]
    [SerializeField] private GameObject hud;

    [Header("Controles del jugador")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerLook playerLook;

    [Header("Cámara del arma (FPS Arms)")]
    [SerializeField] private GameObject weaponCameraObject; // el objeto "WeaponCamera" de la Hierarchy

    private bool historiaActiva;
    public bool HistoriaActiva => historiaActiva;

    private void Awake()
    {
        if (popupHistoria != null)
            popupHistoria.SetActive(false);

        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();

        if (playerLook == null)
            playerLook = GetComponentInChildren<PlayerLook>(true);
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        if (EsSinglePlayer())
            MostrarHistoriaInicial();
    }

    private bool EsSinglePlayer()
    {
        return SceneManager.GetActiveScene().name == "MainSceneSinglePlayer";
    }

    private void MostrarHistoriaInicial()
    {
        historiaActiva = true;

        if (hud != null)
            hud.SetActive(false);

        BloquearJugador();

        if (popupHistoria != null)
            popupHistoria.SetActive(true);

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void ContinuarHistoria()
    {
        if (!IsOwner || !historiaActiva)
            return;

        historiaActiva = false;

        if (popupHistoria != null)
            popupHistoria.SetActive(false);

        if (hud != null)
            hud.SetActive(true);

        HabilitarJugador();

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        if (IsServer && RoundManager.Instance != null)
            RoundManager.Instance.StartRound(1);
    }

    private void BloquearJugador()
    {
        if (playerMovement != null)
            playerMovement.enabled = false;

        if (playerLook != null)
            playerLook.enabled = false;

        if (weaponCameraObject != null)
            weaponCameraObject.SetActive(false);
    }

    private void HabilitarJugador()
    {
        if (playerMovement != null)
            playerMovement.enabled = true;

        if (playerLook != null)
            playerLook.enabled = true;

        if (weaponCameraObject != null)
            weaponCameraObject.SetActive(true);
    }
}