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
    private WeaponSwitcher weaponSwitcher;

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
        Debug.Log("[StoryIntro] ContinuarHistoria() INICIO. IsOwner=" + IsOwner + " | historiaActiva=" + historiaActiva);

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

        Debug.Log("[StoryIntro] ContinuarHistoria() FIN.");
    }

    private void BloquearJugador()
    {
        if (weaponSwitcher == null)
            weaponSwitcher = transform.root.GetComponentInChildren<WeaponSwitcher>(true);

        if (playerMovement != null)
        {
            playerMovement.enabled = false;
            playerMovement.MovementLocked = true;
        }

        if (playerLook != null)
            playerLook.enabled = false;

        foreach (Weapon arma in transform.root.GetComponentsInChildren<Weapon>(true))
        {
            arma.enabled = false;
            arma.InputLocked = true;
        }
    }

    private void HabilitarJugador()
    {
        if (weaponSwitcher == null)
            weaponSwitcher = transform.root.GetComponentInChildren<WeaponSwitcher>(true);

        Debug.Log("[StoryIntro] HabilitarJugador() INICIO. weaponSwitcher=" + (weaponSwitcher != null) +
            " | CurrentWeapon=" + (weaponSwitcher != null && weaponSwitcher.CurrentWeapon != null ? weaponSwitcher.CurrentWeapon.name : "NULL"));

        if (playerMovement != null)
        {
            playerMovement.enabled = true;
            playerMovement.MovementLocked = false;
        }

        if (playerLook != null)
            playerLook.enabled = true;

        if (weaponSwitcher != null && weaponSwitcher.CurrentWeapon != null)
        {
            weaponSwitcher.CurrentWeapon.enabled = true;
            weaponSwitcher.CurrentWeapon.InputLocked = false;
            Debug.Log("[StoryIntro] Arma desbloqueada: " + weaponSwitcher.CurrentWeapon.name);
        }
        else
        {
            Debug.LogWarning("[StoryIntro] Seguimos sin encontrar weaponSwitcher ni con el lookup manual.");
        }
    }
}