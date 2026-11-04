using UnityEngine;
using Unity.Netcode;

public class GameStartCurtain : MonoBehaviour
{
    [Header("Cortina de carga")]
    [SerializeField] private GameObject curtainCanvas;

    [Header("Configuración")]
    [SerializeField] private int expectedPlayerCount = 2;

    private bool curtainHidden;

    private float proximoLog;

    private float inicioEspera;

    private void Awake()
    {
        Debug.Log("[GameStartCurtain] Awake");

        OcultarRestosDelLobby();

        if (curtainCanvas != null)
            curtainCanvas.SetActive(true);

        BloquearJugadorLocalInmediatamente();

        inicioEspera = Time.unscaledTime;
    }

    private void OcultarRestosDelLobby()
    {
        // LoadSceneMode.Single debería descargar NetworkLobby, pero durante la
        // transición de Netcode puede coexistir unos cuadros. Se oculta cualquier
        // resto visual antes de mostrar la escena jugable.
        MultiplayerLobbyController[] lobbies =
            FindObjectsByType<MultiplayerLobbyController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        foreach (MultiplayerLobbyController lobby in lobbies)
        {
            if (lobby == null)
                continue;

            Canvas canvas = lobby.GetComponentInParent<Canvas>(true);

            if (canvas != null)
                canvas.gameObject.SetActive(false);

            lobby.gameObject.SetActive(false);
        }

        Transform[] all =
            FindObjectsByType<Transform>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        foreach (Transform t in all)
        {
            if (t == null)
                continue;

            if (t.name == "salaEsperaPanel" ||
                t.name.StartsWith("LobbyCinematic"))
            {
                t.gameObject.SetActive(false);
            }
        }
    }

    private void BloquearJugadorLocalInmediatamente()
    {
        // Bloquea cualquier Player que ya haya sido creado por el spawner de esta
        // escena mientras terminan de sincronizarse host e invitados.
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

   /* private void Update()
    {
        if (curtainHidden)
            return;

        if (!TodosListos())
           // return;
           if (Time.unscaledTime - inicioEspera < 20f)
            return;

            Debug.LogWarning("[GameStartCurtain] Tiempo agotado: se abre la cortina aunque no todos esten ubicados.");

        OcultarCortina();
    }*/

    private void Update()
    {
        if (curtainHidden)
            return;


        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            Debug.LogError("[GameStartCurtain] La red no está activa: vuelvo al menú.");
            curtainHidden = true;

            if (NetworkBootstrap.Instance != null)
                _ = NetworkBootstrap.Instance.GoToMenuDueToDisconnect();

            return;
        }
        if (!TodosListos())
        {
            if (Time.unscaledTime - inicioEspera < 20f)
                return;

            Debug.LogWarning("[GameStartCurtain] Tiempo agotado: se abre la cortina aunque no todos estén ubicados.");
            LogEstadoJugadores();
        }

        OcultarCortina();
    }

    private void LogEstadoJugadores()
    {
        foreach (var a in ObtenerAssignersDePlayersReales())
        {
            Debug.LogWarning(
                $"[GameStartCurtain] clientId={a.OwnerClientId} | " +
                $"spawnIndex={a.AssignedSpawnIndex.Value} | " +
                $"IsAtSpawn={a.IsAtSpawn.Value} | pos={a.transform.position}"
            );
        }
    }

    private MultiplayerPlayerSpawnAssigner[] ObtenerAssignersDePlayersReales()
    {
        MultiplayerPlayerSpawnAssigner[] todos =
            FindObjectsByType<MultiplayerPlayerSpawnAssigner>(
                FindObjectsSortMode.None);

        System.Collections.Generic.List<MultiplayerPlayerSpawnAssigner> reales =
            new System.Collections.Generic.List<MultiplayerPlayerSpawnAssigner>();

        foreach (MultiplayerPlayerSpawnAssigner a in todos)
        {
            if (a == null)
                continue;

            NetworkObject no = a.GetComponent<NetworkObject>();

            // MainSceneMultiPlayer tiene un NetworkObject de escena llamado
            // "multiplayer" con este componente, pero NO es un PlayerObject.
            if (no == null || !no.IsPlayerObject)
                continue;

            reales.Add(a);
        }

        return reales.ToArray();
    }

    private bool TodosListos()
    {
        // FindObjectsByType SÍ funciona igual en Host y en Cliente,
        // porque cada Player es un NetworkObject spawneado y visible
        // localmente en todas las máquinas — a diferencia de
        // ConnectedClientsList, que solo está completa en el servidor.
       /* MultiplayerPlayerSpawnAssigner[] assigners =
            FindObjectsByType<MultiplayerPlayerSpawnAssigner>(FindObjectsSortMode.None);

        if (assigners.Length < jugadoresEsperados)
            return false;

        foreach (MultiplayerPlayerSpawnAssigner assigner in assigners)
        {
            if (!assigner.IsAtSpawn.Value)
                return false;
        }

        if (Time.time >= proximoLog)
        {
            proximoLog = Time.time + 2f;

            string estados = "";

            foreach (MultiplayerPlayerSpawnAssigner a in assigners)
                estados += a.IsAtSpawn.Value + " ";

            Debug.Log(
                "[Curtain] Jugadores: " + assigners.Length + "/" + jugadoresEsperados +
                " | IsAtSpawn: " + estados
            );

        }*/

        MultiplayerPlayerSpawnAssigner[] assigners =
            ObtenerAssignersDePlayersReales();

        int jugadoresEsperados = expectedPlayerCount;

        if (NetworkBootstrap.Instance != null &&
            NetworkBootstrap.Instance.PlayerCount > 0)
        {
            jugadoresEsperados = NetworkBootstrap.Instance.PlayerCount;
        }

        // Log de diagnóstico: va ANTES de los return para verse siempre.
        if (Time.unscaledTime >= proximoLog)
        {
            proximoLog = Time.unscaledTime + 2f;

            string estados = "";

            foreach (MultiplayerPlayerSpawnAssigner a in assigners)
                estados += a.IsAtSpawn.Value + " ";

            Debug.Log(
                "[Curtain] Jugadores: " + assigners.Length + "/" + jugadoresEsperados +
                " | IsAtSpawn: " + estados
            );
        }

        if (assigners.Length < jugadoresEsperados)
            return false;

        foreach (MultiplayerPlayerSpawnAssigner assigner in assigners)
        {
            if (!assigner.IsAtSpawn.Value)
                return false;
        }

        return true;

       // return true;
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