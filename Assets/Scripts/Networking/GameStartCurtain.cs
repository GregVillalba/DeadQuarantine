using UnityEngine;
using Unity.Netcode;

public class GameStartCurtain : MonoBehaviour
{
    [Header("Cortina de carga")]
    [SerializeField] private GameObject curtainCanvas;

    [Header("Configuración")]
    [SerializeField] private int expectedPlayerCount = 2;

    private bool curtainHidden;

    private void Awake()
    {
        if (curtainCanvas != null)
            curtainCanvas.SetActive(true);

        BloquearJugadorLocalInmediatamente();
    }

    private void BloquearJugadorLocalInmediatamente()
    {
        // Cubre el caso del Host: su Player ya existía desde la escena
        // de Lobby y no vuelve a pasar por su propio Awake al cambiar
        // de escena, así que el autobloqueo de cada script puede llegar tarde.
        foreach (PlayerMovement movimiento in FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None))
        {
            if (movimiento.IsOwner)
                movimiento.MovementLocked = true;
        }

        foreach (PlayerLook look in FindObjectsByType<PlayerLook>(FindObjectsSortMode.None))
        {
            NetworkObject netObj = look.GetComponentInParent<NetworkObject>();

            if (netObj != null && netObj.IsOwner)
                look.enabled = false;
        }

        foreach (WeaponSwitcher switcher in FindObjectsByType<WeaponSwitcher>(FindObjectsSortMode.None))
        {
            NetworkObject netObj = switcher.GetComponentInParent<NetworkObject>();

            if (netObj != null && netObj.IsOwner && switcher.CurrentWeapon != null)
                switcher.CurrentWeapon.InputLocked = true;
        }
    }

    private void Update()
    {
        if (curtainHidden)
            return;

        if (!TodosListos())
            return;

        OcultarCortina();
    }

    private bool TodosListos()
    {
        // FindObjectsByType SÍ funciona igual en Host y en Cliente,
        // porque cada Player es un NetworkObject spawneado y visible
        // localmente en todas las máquinas — a diferencia de
        // ConnectedClientsList, que solo está completa en el servidor.
        MultiplayerPlayerSpawnAssigner[] assigners =
            FindObjectsByType<MultiplayerPlayerSpawnAssigner>(FindObjectsSortMode.None);

        if (assigners.Length < expectedPlayerCount)
            return false;

        foreach (MultiplayerPlayerSpawnAssigner assigner in assigners)
        {
            if (!assigner.IsAtSpawn.Value)
                return false;
        }

        return true;
    }

    private void OcultarCortina()
    {
        Debug.Log("[GameStartCurtain] OcultarCortina() ejecutado.");

        curtainHidden = true;

        if (curtainCanvas != null)
            curtainCanvas.SetActive(false);

        RevelarHUDParaJugadorLocal();
        DesbloquearJugadorLocal();
    }

    private void DesbloquearJugadorLocal()
    {
        PlayerMovement[] movimientos = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        Debug.Log("[GameStartCurtain] PlayerMovement encontrados: " + movimientos.Length);

        foreach (PlayerMovement movimiento in movimientos)
        {
            if (movimiento.IsOwner)
            {
                movimiento.MovementLocked = false;
                Debug.Log("[GameStartCurtain] Movimiento desbloqueado.");
                break;
            }
        }

        PlayerLook[] looks = FindObjectsByType<PlayerLook>(FindObjectsSortMode.None);
        Debug.Log("[GameStartCurtain] PlayerLook encontrados: " + looks.Length);

        foreach (PlayerLook look in looks)
        {
            NetworkObject netObj = look.GetComponentInParent<NetworkObject>();

            if (netObj != null && netObj.IsOwner)
            {
                look.enabled = true;
                Debug.Log("[GameStartCurtain] Cámara desbloqueada. enabled = " + look.enabled);
                break;
            }
        }

        WeaponSwitcher[] switchers = FindObjectsByType<WeaponSwitcher>(FindObjectsSortMode.None);
        Debug.Log("[GameStartCurtain] WeaponSwitcher encontrados: " + switchers.Length);

        foreach (WeaponSwitcher switcher in switchers)
        {
            NetworkObject netObj = switcher.GetComponentInParent<NetworkObject>();

            if (netObj != null && netObj.IsOwner && switcher.CurrentWeapon != null)
            {
                switcher.CurrentWeapon.InputLocked = false;
                Debug.Log("[GameStartCurtain] Arma desbloqueada. InputLocked = " + switcher.CurrentWeapon.InputLocked);
                break;
            }
        }
    }

    private void RevelarHUDParaJugadorLocal()
    {
        PauseController[] controllers =
            FindObjectsByType<PauseController>(FindObjectsSortMode.None);

        foreach (PauseController controller in controllers)
        {
            if (controller.IsOwner)
            {
                controller.MostrarHUD();
                break;
            }
        }
    }
}