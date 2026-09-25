using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Unity.Netcode;

public class AchievementsManager : MonoBehaviour
{
    public static AchievementsManager Instance { get; private set; }

    [Header("Base de datos de logros")]
    [SerializeField]
    private AchievementDatabase achievementDatabase;

    private PlayerScore playerScore;

    private string rutaArchivo;
    private AchievementsData datos;


    // ============================================================
    // DATOS
    // ============================================================

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


    // ============================================================
    // AWAKE
    // ============================================================

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

        InicializarBaseDeDatos();

        InicializarLogros();
    }


    private void InicializarBaseDeDatos()
    {
        if (achievementDatabase == null)
        {
            Debug.LogError(
                "[ACHIEVEMENTS] AchievementsManager no tiene " +
                "una AchievementDatabase asignada en el Inspector."
            );

            return;
        }

        achievementDatabase.Inicializar();
    }


    // ============================================================
    // UPDATE
    // ============================================================

    private void Update()
    {
        if (playerScore == null)
        {
            BuscarPlayerScore();
        }
    }


    // ============================================================
    // BUSCAR PLAYER SCORE
    // ============================================================

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
        if (achievementDatabase == null)
        {
            Debug.LogError(
                "[ACHIEVEMENTS] No se puede inicializar la lista " +
                "porque no existe AchievementDatabase."
            );

            return;
        }


        IReadOnlyList<AchievementDefinition> definiciones =
            achievementDatabase.ObtenerTodos();


        foreach (AchievementDefinition definicion in definiciones)
        {
            if (definicion == null)
                continue;


            CrearLogroSiNoExiste(
                definicion.id
            );
        }


        GuardarProgreso();
    }


    private void CrearLogroSiNoExiste(
        AchievementId id
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


        AchievementDefinition definicion =
            ObtenerDefinicion(id);


        if (definicion == null)
            return;


        if (logro.progreso >= definicion.objetivo)
        {
            logro.progreso =
                definicion.objetivo;

            logro.desbloqueado = true;


            Debug.Log(
                "[ACHIEVEMENTS] LOGRO DESBLOQUEADO: " +
                id
            );


            MostrarLogroEnHUD(id);
        }
        else
        {
            Debug.Log(
                $"[ACHIEVEMENTS] {id}: " +
                $"{logro.progreso}/{definicion.objetivo}"
            );
        }


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


        AchievementDefinition definicion =
            ObtenerDefinicion(id);


        if (definicion != null)
        {
            logro.progreso =
                definicion.objetivo;
        }


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


        if (logro.desbloqueado)
            return;


        AchievementDefinition definicion =
            ObtenerDefinicion(id);


        if (definicion == null)
            return;


        if (logro.progreso >= definicion.objetivo)
        {
            logro.progreso =
                definicion.objetivo;

            logro.desbloqueado = true;


            Debug.Log(
                "[ACHIEVEMENTS] LOGRO DESBLOQUEADO: " +
                id
            );


            MostrarLogroEnHUD(id);
        }
    }


    // ============================================================
    // OBTENER DEFINICIÓN
    // ============================================================

    private AchievementDefinition ObtenerDefinicion(
        AchievementId id
    )
    {
        if (achievementDatabase == null)
        {
            Debug.LogError(
                "[ACHIEVEMENTS] No existe AchievementDatabase."
            );

            return null;
        }


        return achievementDatabase.Obtener(id);
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


        if (datos.logros == null)
        {
            datos.logros =
                new List<AchievementData>();
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