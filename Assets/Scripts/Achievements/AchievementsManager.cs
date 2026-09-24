using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Unity.Netcode;

public class AchievementsManager : MonoBehaviour
{
    public static AchievementsManager Instance { get; private set; }

    private PlayerScore playerScore;

    private string rutaArchivo;
    private AchievementsData datos;


    [System.Serializable]
    public class AchievementData
    {
        public string id;
        public int progreso;
        public bool desbloqueado;
    }


    [System.Serializable]
    public class AchievementsData
    {
        public List<AchievementData> logros =
            new List<AchievementData>();
    }


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        DontDestroyOnLoad(gameObject);

        rutaArchivo = Path.Combine(
            Application.persistentDataPath,
            "achievements.json"
        );

        CargarProgreso();
        InicializarLogros();
    }


    private void Update()
    {
        if (playerScore == null)
        {
            BuscarPlayerScore();
        }
    }


    private void BuscarPlayerScore()
    {
        if (NetworkManager.Singleton == null)
            return;

        if (NetworkManager.Singleton.LocalClient == null)
            return;

        if (NetworkManager.Singleton.LocalClient.PlayerObject == null)
            return;


        playerScore =
            NetworkManager.Singleton.LocalClient.PlayerObject
                .GetComponentInChildren<PlayerScore>();


        if (playerScore == null)
            return;


        playerScore.ZombiesEliminadosNetwork.OnValueChanged +=
            OnZombiesEliminadosChanged;


        OnZombiesEliminadosChanged(
            0,
            playerScore.ZombiesEliminadosNetwork.Value
        );


        Debug.Log(
            "[ACHIEVEMENTS] PlayerScore encontrado."
        );
    }


    // ============================================================
    // INICIALIZAR LOGROS
    // ============================================================

    private void InicializarLogros()
    {
        CrearLogroSiNoExiste(
            AchievementId.Zombies50,
            50
        );

        CrearLogroSiNoExiste(
            AchievementId.Zombies100,
            100
        );

        CrearLogroSiNoExiste(
            AchievementId.Zombies300,
            300
        );

        CrearLogroSiNoExiste(
            AchievementId.BossFinal,
            1
        );

        CrearLogroSiNoExiste(
            AchievementId.Headshots10,
            10
        );

        CrearLogroSiNoExiste(
            AchievementId.Headshots50,
            50
        );

        CrearLogroSiNoExiste(
            AchievementId.Headshots100,
            100
        );

        CrearLogroSiNoExiste(
            AchievementId.Headshots300,
            300
        );

        CrearLogroSiNoExiste(
            AchievementId.RondasNormal,
            1
        );

        CrearLogroSiNoExiste(
            AchievementId.RondasDificil,
            1
        );

        CrearLogroSiNoExiste(
            AchievementId.RondasPesadilla,
            1
        );

        CrearLogroSiNoExiste(
            AchievementId.SupervivenciaNormal,
            1
        );

        CrearLogroSiNoExiste(
            AchievementId.SupervivenciaDificil,
            1
        );

        CrearLogroSiNoExiste(
            AchievementId.SupervivenciaPesadilla,
            1
        );

        CrearLogroSiNoExiste(
            AchievementId.HistoriaNormal,
            1
        );

        CrearLogroSiNoExiste(
            AchievementId.HistoriaDificil,
            1
        );

        CrearLogroSiNoExiste(
            AchievementId.HistoriaPesadilla,
            1
        );

        CrearLogroSiNoExiste(
            AchievementId.EasterEgg1,
            1
        );

        CrearLogroSiNoExiste(
            AchievementId.EasterEgg3,
            3
        );

        CrearLogroSiNoExiste(
            AchievementId.MinijuegoZonaTiro,
            1
        );


        GuardarProgreso();
    }


    private void CrearLogroSiNoExiste(
        AchievementId id,
        int objetivo
    )
    {
        string idString = id.ToString();


        AchievementData logro =
            datos.logros.Find(
                x => x.id == idString
            );


        if (logro == null)
        {
            logro = new AchievementData();

            logro.id = idString;
            logro.progreso = 0;
            logro.desbloqueado = false;

            datos.logros.Add(logro);


            Debug.Log(
                "[ACHIEVEMENTS] Nuevo logro agregado: " +
                idString
            );
        }
    }


    // ============================================================
    // ZOMBIES
    // ============================================================

    private void OnZombiesEliminadosChanged(
        int valorAnterior,
        int valorNuevo
    )
    {
        int zombiesNuevos =
            valorNuevo - valorAnterior;


        if (zombiesNuevos <= 0)
            return;


        SumarProgreso(
            AchievementId.Zombies50,
            zombiesNuevos
        );

        SumarProgreso(
            AchievementId.Zombies100,
            zombiesNuevos
        );

        SumarProgreso(
            AchievementId.Zombies300,
            zombiesNuevos
        );
    }


    // ============================================================
    // SUMAR PROGRESO
    // ============================================================

    public void SumarProgreso(
        AchievementId id,
        int cantidad
    )
    {
        AchievementData logro =
            ObtenerLogro(id);


        if (logro == null)
            return;


        if (logro.desbloqueado)
            return;


        logro.progreso += cantidad;


        Debug.Log(
            $"[ACHIEVEMENTS] {id}: " +
            $"{logro.progreso}"
        );


        ComprobarDesbloqueo(id);


        GuardarProgreso();
    }


    // ============================================================
    // DESBLOQUEAR
    // ============================================================

    public void Desbloquear(
        AchievementId id
    )
    {
        AchievementData logro =
            ObtenerLogro(id);


        if (logro == null)
            return;


        if (logro.desbloqueado)
            return;


        logro.desbloqueado = true;


        Debug.Log(
            "[ACHIEVEMENTS] LOGRO DESBLOQUEADO: " +
            id
        );


        MostrarLogroEnHUD(id);


        GuardarProgreso();
    }


    // ============================================================
    // COMPROBAR DESBLOQUEO
    // ============================================================

    private void ComprobarDesbloqueo(
        AchievementId id
    )
    {
        AchievementData logro =
            ObtenerLogro(id);


        if (logro == null)
            return;


        int objetivo =
            ObtenerObjetivo(id);


        if (logro.progreso >= objetivo)
        {
            logro.progreso = objetivo;

            logro.desbloqueado = true;


            Debug.Log(
                "[ACHIEVEMENTS] LOGRO DESBLOQUEADO: " +
                id
            );


            MostrarLogroEnHUD(id);
        }
    }


    // ============================================================
    // MOSTRAR LOGRO EN HUD
    // ============================================================

    private void MostrarLogroEnHUD(
        AchievementId id
    )
    {
        if (GameplayPopupsController.Instance == null)
        {
            Debug.LogWarning(
                "[ACHIEVEMENTS] No existe GameplayPopupsController " +
                "en la escena actual."
            );

            return;
        }


        GameplayPopupsController.Instance
            .MostrarLogro(id);
    }


    // ============================================================
    // OBJETIVOS
    // ============================================================

    private int ObtenerObjetivo(
        AchievementId id
    )
    {
        switch (id)
        {
            case AchievementId.Zombies50:
                return 50;

            case AchievementId.Zombies100:
                return 100;

            case AchievementId.Zombies300:
                return 300;


            case AchievementId.Headshots10:
                return 10;

            case AchievementId.Headshots50:
                return 50;

            case AchievementId.Headshots100:
                return 100;

            case AchievementId.Headshots300:
                return 300;


            case AchievementId.EasterEgg1:
                return 1;

            case AchievementId.EasterEgg3:
                return 3;


            default:
                return 1;
        }
    }


    // ============================================================
    // OBTENER LOGRO
    // ============================================================

    public AchievementData ObtenerLogro(
        AchievementId id
    )
    {
        if (datos == null)
            return null;


        if (datos.logros == null)
            return null;


        return datos.logros.Find(
            x => x.id == id.ToString()
        );
    }


    // ============================================================
    // ¿ESTÁ DESBLOQUEADO?
    // ============================================================

    public bool EstaDesbloqueado(
        AchievementId id
    )
    {
        AchievementData logro =
            ObtenerLogro(id);


        if (logro == null)
            return false;


        return logro.desbloqueado;
    }


    // ============================================================
    // OBTENER PROGRESO
    // ============================================================

    public int ObtenerProgreso(
        AchievementId id
    )
    {
        AchievementData logro =
            ObtenerLogro(id);


        if (logro == null)
            return 0;


        return logro.progreso;
    }


    // ============================================================
    // GUARDAR
    // ============================================================

    private void GuardarProgreso()
    {
        if (datos == null)
            return;


        string json =
            JsonUtility.ToJson(
                datos,
                true
            );


        File.WriteAllText(
            rutaArchivo,
            json
        );
    }


    // ============================================================
    // CARGAR
    // ============================================================

    private void CargarProgreso()
    {
        if (!File.Exists(rutaArchivo))
        {
            datos = new AchievementsData();


            Debug.Log(
                "[ACHIEVEMENTS] No existe archivo. " +
                "Creando progreso nuevo."
            );


            return;
        }


        string json =
            File.ReadAllText(
                rutaArchivo
            );


        datos =
            JsonUtility.FromJson<AchievementsData>(
                json
            );


        if (datos == null)
        {
            datos = new AchievementsData();


            Debug.LogWarning(
                "[ACHIEVEMENTS] Archivo inválido."
            );
        }
    }


    // ============================================================
    // DESTRUIR
    // ============================================================

    private void OnDestroy()
    {
        if (playerScore != null)
        {
            playerScore.ZombiesEliminadosNetwork.OnValueChanged -=
                OnZombiesEliminadosChanged;
        }
    }
}