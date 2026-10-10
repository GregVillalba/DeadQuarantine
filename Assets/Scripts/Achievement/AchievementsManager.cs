
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using Unity.Netcode;

public class AchievementsManager : MonoBehaviour
{
    public static AchievementsManager Instance { get; private set; }

    [Header("Configuración Backend / Base de Datos")]
    [SerializeField]
    private string apiUrl = "http://localhost:3000/api/logros/desbloquear";

    [SerializeField]
    private string usernameJugador = "UserUnity";

    public string UsernameJugador
    {
        get => usernameJugador;
        set => usernameJugador = value;
    }

    [Header("Base de datos de logros")]
    [SerializeField]
    private AchievementDatabase achievementDatabase;

    [Header("Restricciones de logros")]
    [SerializeField]
    private string escenaSinLogros = "TutorialScene";

    private PlayerScore playerScore;
    private string rutaArchivo;
    private AchievementsData datos;

    // ============================================================
    // COMPROBAR SI LOS LOGROS ESTÁN HABILITADOS
    // ============================================================

    public bool LogrosHabilitados
    {
        get
        {
            return SceneManager.GetActiveScene().name != escenaSinLogros;
        }
    }

    private bool BloqueoLogrosActivo()
    {
        if (LogrosHabilitados)
            return false;

        return true;
    }

    // ============================================================
    // DATOS LOCALES Y SERIALIZACIÓN
    // ============================================================

    [System.Serializable]
    public class AchievementData
    {
        public string id;
        public int progreso;
        public AchievementRank rangoActual;
        public bool desbloqueado;
    }

    [System.Serializable]
    public class AchievementsData
    {
        public List<AchievementData> logros = new List<AchievementData>();
        public List<string> easterEggsEncontrados = new List<string>();
    }

    [System.Serializable]
    private class LogroApiPayload
    {
        public string username;
        public string sublogro_clave;
    }

    [System.Serializable]
    private class LogroApiResponse
    {
        public bool success;
        public string mensaje;
        public string logro_padre_desbloqueado;
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

        Debug.Log(
            "[ACHIEVEMENTS] Gestor inicializado. Logros habilitados: " +
            LogrosHabilitados
        );
    }

    // ============================================================
    // BASE DE DATOS LOCAL
    // ============================================================

    private void InicializarBaseDeDatos()
    {
        if (achievementDatabase == null)
        {
            Debug.LogError(
                "[ACHIEVEMENTS] AchievementsManager no tiene " +
                "una AchievementDatabase asignada."
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
            BuscarPlayerScore();
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

        // Evita importar las bajas acumuladas durante el tutorial
        // como progreso inicial de logros.
        if (!BloqueoLogrosActivo())
        {
            OnZombiesEliminadosChanged(
                0,
                playerScore.ZombiesEliminadosNetwork.Value
            );
        }

        Debug.Log("[ACHIEVEMENTS] PlayerScore encontrado.");
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

            CrearLogroSiNoExiste(definicion.id);
        }

        GuardarProgreso();
    }

    private void CrearLogroSiNoExiste(AchievementId id)
    {
        string idString = id.ToString();

        AchievementData logro = datos.logros.Find(
            x => x.id == idString
        );

        if (logro == null)
        {
            logro = new AchievementData
            {
                id = idString,
                progreso = 0,
                rangoActual = AchievementRank.Ninguno,
                desbloqueado = false
            };

            datos.logros.Add(logro);

            Debug.Log(
                "[ACHIEVEMENTS] Nuevo logro agregado: " + idString
            );
        }
    }

    // ============================================================
    // EASTER EGGS: CONSULTAR REGISTRO
    // ============================================================

    public bool EasterEggYaEncontrado(string easterEggId)
    {
        if (datos == null ||
            datos.easterEggsEncontrados == null ||
            string.IsNullOrWhiteSpace(easterEggId))
        {
            return false;
        }

        return datos.easterEggsEncontrados.Contains(easterEggId);
    }

    // ============================================================
    // EASTER EGGS: REGISTRAR UNA SOLA VEZ
    // ============================================================

    public bool RegistrarEasterEggEncontrado(string easterEggId)
    {
        if (BloqueoLogrosActivo())
            return false;

        if (datos == null)
        {
            Debug.LogError(
                "[ACHIEVEMENTS] No hay datos cargados para registrar " +
                "el Easter Egg."
            );
            return false;
        }

        if (string.IsNullOrWhiteSpace(easterEggId))
        {
            Debug.LogError(
                "[ACHIEVEMENTS] El Easter Egg no tiene un ID válido."
            );
            return false;
        }

        if (datos.easterEggsEncontrados == null)
        {
            datos.easterEggsEncontrados = new List<string>();
        }

        if (datos.easterEggsEncontrados.Contains(easterEggId))
        {
            Debug.Log(
                "[ACHIEVEMENTS] Easter Egg ya registrado: " + easterEggId
            );
            return false;
        }

        datos.easterEggsEncontrados.Add(easterEggId);

        GuardarProgreso();

        SumarProgreso(
            AchievementId.DondeEstaWally,
            1
        );

        Debug.Log(
            "[ACHIEVEMENTS] Nuevo Easter Egg registrado: " + easterEggId
        );

        return true;
    }

    // ============================================================
    // ZOMBIES
    // ============================================================

    private void OnZombiesEliminadosChanged(
        int valorAnterior,
        int valorNuevo
    )
    {
        if (BloqueoLogrosActivo())
            return;

        int zombiesNuevos = valorNuevo - valorAnterior;

        if (zombiesNuevos <= 0)
            return;

        SumarProgreso(
            AchievementId.DerrotadorSupremo,
            zombiesNuevos
        );
    }

    // ============================================================
    // SUMAR PROGRESO
    // ============================================================

    public void SumarProgreso(AchievementId id, int cantidad)
    {
        if (BloqueoLogrosActivo())
            return;

        if (cantidad <= 0)
            return;

        AchievementData logro = ObtenerLogro(id);
        if (logro == null)
            return;

        AchievementDefinition definicion = ObtenerDefinicion(id);
        if (definicion == null)
            return;

        if (!definicion.tieneRangos)
        {
            if (logro.desbloqueado)
                return;

            logro.progreso += cantidad;

            AchievementTierDefinition unico =
                ObtenerPrimerRango(definicion);

            if (unico == null)
                return;

            if (logro.progreso >= unico.objetivo)
            {
                logro.progreso = unico.objetivo;
                logro.desbloqueado = true;

                Debug.Log(
                    "[ACHIEVEMENTS] LOGRO COMPLETADO: " + id
                );

                MostrarLogroEnHUD(
                    id,
                    AchievementRank.Ninguno
                );

                ReportarProgresoServidor(
                    id,
                    AchievementRank.Ninguno
                );
            }

            GuardarProgreso();
            return;
        }

        logro.progreso += cantidad;

        ActualizarRangoDesdeProgreso(id);
        GuardarProgreso();
    }

    // ============================================================
    // ACTUALIZAR RANGO POR PROGRESO
    // ============================================================

    private void ActualizarRangoDesdeProgreso(AchievementId id)
    {
        if (BloqueoLogrosActivo())
            return;

        AchievementData logro = ObtenerLogro(id);
        AchievementDefinition definicion = ObtenerDefinicion(id);

        if (logro == null || definicion == null)
            return;

        AchievementRank nuevoRango = DeterminarRango(
            definicion,
            logro.progreso
        );

        if (nuevoRango <= logro.rangoActual)
        {
            logro.desbloqueado =
                nuevoRango == AchievementRank.Oro;
            return;
        }

        logro.rangoActual = nuevoRango;
        logro.desbloqueado = nuevoRango == AchievementRank.Oro;

        AchievementTierDefinition tier =
            definicion.ObtenerRango(nuevoRango);

        if (tier != null)
        {
            logro.progreso = Mathf.Max(
                logro.progreso,
                tier.objetivo
            );
        }

        Debug.Log(
            "[ACHIEVEMENTS] RANGO DESBLOQUEADO: " +
            id + " -> " + nuevoRango
        );

        if (nuevoRango == AchievementRank.Oro)
        {
            Debug.Log(
                "[ACHIEVEMENTS] LOGRO COMPLETADO: " + id
            );
        }

        MostrarLogroEnHUD(id, nuevoRango);
        ReportarProgresoServidor(id, nuevoRango);
    }

    // ============================================================
    // DETERMINAR RANGO
    // ============================================================

    private AchievementRank DeterminarRango(
        AchievementDefinition definicion,
        int progreso
    )
    {
        AchievementRank rangoActual = AchievementRank.Ninguno;

        AchievementRank[] rangos =
        {
            AchievementRank.Bronce,
            AchievementRank.Plata,
            AchievementRank.Oro
        };

        foreach (AchievementRank rango in rangos)
        {
            AchievementTierDefinition tier =
                definicion.ObtenerRango(rango);

            if (tier == null)
                continue;

            if (progreso >= tier.objetivo)
                rangoActual = rango;
        }

        return rangoActual;
    }

    // ============================================================
    // DESBLOQUEAR RANGO DIRECTAMENTE
    // ============================================================

    public void DesbloquearRango(
        AchievementId id,
        AchievementRank rango
    )
    {
        if (BloqueoLogrosActivo())
            return;

        if (rango == AchievementRank.Ninguno)
            return;

        AchievementData logro = ObtenerLogro(id);
        AchievementDefinition definicion = ObtenerDefinicion(id);

        if (logro == null || definicion == null)
            return;

        if (!definicion.tieneRangos)
        {
            Desbloquear(id);
            return;
        }

        if (rango <= logro.rangoActual)
            return;

        logro.rangoActual = rango;
        logro.desbloqueado = rango == AchievementRank.Oro;

        AchievementTierDefinition tier =
            definicion.ObtenerRango(rango);

        if (tier != null)
        {
            logro.progreso = Mathf.Max(
                logro.progreso,
                tier.objetivo
            );
        }

        Debug.Log(
            "[ACHIEVEMENTS] RANGO DESBLOQUEADO: " +
            id + " -> " + rango
        );

        if (rango == AchievementRank.Oro)
        {
            Debug.Log(
                "[ACHIEVEMENTS] LOGRO COMPLETADO: " + id
            );
        }

        MostrarLogroEnHUD(id, rango);
        GuardarProgreso();
        ReportarProgresoServidor(id, rango);
    }

    // ============================================================
    // DESBLOQUEAR LOGRO
    // ============================================================

    public void Desbloquear(AchievementId id)
    {
        if (BloqueoLogrosActivo())
            return;

        AchievementData logro = ObtenerLogro(id);
        AchievementDefinition definicion = ObtenerDefinicion(id);

        if (logro == null || definicion == null)
            return;

        if (!definicion.tieneRangos)
        {
            if (logro.desbloqueado)
                return;

            logro.desbloqueado = true;

            Debug.Log("[ACHIEVEMENTS] LOGRO COMPLETADO: " + id);

            MostrarLogroEnHUD(
                id,
                AchievementRank.Ninguno
            );

            GuardarProgreso();

            ReportarProgresoServidor(
                id,
                AchievementRank.Ninguno
            );

            return;
        }

        AchievementRank rango = DeterminarRango(
            definicion,
            logro.progreso
        );

        if (rango == AchievementRank.Ninguno)
            return;

        if (rango <= logro.rangoActual)
        {
            logro.desbloqueado = rango == AchievementRank.Oro;
            GuardarProgreso();
            return;
        }

        logro.rangoActual = rango;
        logro.desbloqueado = rango == AchievementRank.Oro;

        AchievementTierDefinition tier =
            definicion.ObtenerRango(rango);

        if (tier != null)
        {
            logro.progreso = Mathf.Max(
                logro.progreso,
                tier.objetivo
            );
        }

        Debug.Log(
            "[ACHIEVEMENTS] RANGO DESBLOQUEADO: " +
            id + " -> " + rango
        );

        if (rango == AchievementRank.Oro)
        {
            Debug.Log(
                "[ACHIEVEMENTS] LOGRO COMPLETADO: " + id
            );
        }

        MostrarLogroEnHUD(id, rango);
        GuardarProgreso();
        ReportarProgresoServidor(id, rango);
    }

    // ============================================================
    // COMUNICACIÓN CON MYSQL / SERVIDOR
    // ============================================================

    private void ReportarProgresoServidor(
        AchievementId id,
        AchievementRank rango
    )
    {
        if (BloqueoLogrosActivo())
            return;

        string sublogroClave = ObtenerClaveSublogro(id, rango);

        if (string.IsNullOrEmpty(sublogroClave))
        {
            Debug.LogWarning(
                "[ACHIEVEMENTS API] No se encontró clave asignada para: " +
                id + " - " + rango
            );
            return;
        }

        StartCoroutine(EnviarSublogroCoroutine(sublogroClave));
    }

    private string ObtenerClaveSublogro(
        AchievementId id,
        AchievementRank rango
    )
    {
        switch (id)
        {
            case AchievementId.DerrotadorSupremo:
                if (rango == AchievementRank.Bronce)
                    return "derrota_50_zombies";
                if (rango == AchievementRank.Plata)
                    return "derrota_100_zombies";
                if (rango == AchievementRank.Oro)
                    return "derrota_300_zombies";
                break;

            case AchievementId.SupervivienteDelInfierno:
                if (rango == AchievementRank.Bronce)
                    return "supervivencia_normal";
                if (rango == AchievementRank.Plata)
                    return "supervivencia_dificil";
                if (rango == AchievementRank.Oro)
                    return "supervivencia_pesadilla";
                break;

            case AchievementId.CompletadorDeRondas:
                if (rango == AchievementRank.Bronce)
                    return "rondas_normal";
                if (rango == AchievementRank.Plata)
                    return "rondas_dificil";
                if (rango == AchievementRank.Oro)
                    return "rondas_pesadilla";
                break;

            case AchievementId.CampeonDelPoligono:
                return "completar_poligono";

            case AchievementId.DondeEstaWally:
                if (rango == AchievementRank.Bronce)
                    return "easter_egg_1";
                if (rango == AchievementRank.Plata)
                    return "easter_egg_2";
                if (rango == AchievementRank.Oro)
                    return "easter_egg_3";
                break;
        }

        return null;
    }

    private IEnumerator EnviarSublogroCoroutine(string sublogroClave)
    {
        // No iniciar el envío si la escena activa es el tutorial.
        if (BloqueoLogrosActivo())
            yield break;

        LogroApiPayload payload = new LogroApiPayload
        {
            username = this.usernameJugador,
            sublogro_clave = sublogroClave
        };

        string jsonPayload = JsonUtility.ToJson(payload);

        using (UnityWebRequest request = new UnityWebRequest(apiUrl, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);

            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();

            request.SetRequestHeader(
                "Content-Type",
                "application/json"
            );

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                LogroApiResponse respuesta =
                    JsonUtility.FromJson<LogroApiResponse>(
                        request.downloadHandler.text
                    );

                Debug.Log(
                    "[ACHIEVEMENTS API] Servidor actualizó sublogro '" +
                    sublogroClave + "' para " + usernameJugador + "."
                );

                if (respuesta != null &&
                    !string.IsNullOrEmpty(
                        respuesta.logro_padre_desbloqueado
                    ))
                {
                    Debug.Log(
                        "[ACHIEVEMENTS API] ¡LOGRO GLOBAL COMPLETADO " +
                        "EN BASE DE DATOS: " +
                        respuesta.logro_padre_desbloqueado + "!"
                    );
                }
            }
            else
            {
                Debug.LogWarning(
                    "[ACHIEVEMENTS API] Falló al sincronizar en la nube " +
                    "(" + request.error +
                    "). Progreso guardado únicamente en local."
                );
            }
        }
    }

    // ============================================================
    // OBTENER DEFINICIONES Y ESTADOS
    // ============================================================

    private AchievementDefinition ObtenerDefinicion(AchievementId id)
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

    public AchievementDefinition ObtenerDefinicionParaUI(
        AchievementId id
    )
    {
        return ObtenerDefinicion(id);
    }

    // ============================================================
    // MOSTRAR HUD
    // ============================================================

    private void MostrarLogroEnHUD(
        AchievementId id,
        AchievementRank rango
    )
    {
        if (BloqueoLogrosActivo())
            return;

        if (GameplayPopupsController.Instance == null)
        {
            Debug.LogWarning(
                "[ACHIEVEMENTS] No existe GameplayPopupsController."
            );
            return;
        }

        GameplayPopupsController.Instance.MostrarLogro(id, rango);
    }

    // ============================================================
    // OBTENER LOGRO
    // ============================================================

    public AchievementData ObtenerLogro(AchievementId id)
    {
        if (datos == null || datos.logros == null)
            return null;

        return datos.logros.Find(
            x => x.id == id.ToString()
        );
    }

    public bool EstaDesbloqueado(AchievementId id)
    {
        AchievementData logro = ObtenerLogro(id);
        return logro != null && logro.desbloqueado;
    }

    public AchievementRank ObtenerRango(AchievementId id)
    {
        AchievementData logro = ObtenerLogro(id);

        return logro != null
            ? logro.rangoActual
            : AchievementRank.Ninguno;
    }

    public int ObtenerProgreso(AchievementId id)
    {
        AchievementData logro = ObtenerLogro(id);
        return logro != null ? logro.progreso : 0;
    }

    // ============================================================
    // OBTENER PRIMER RANGO
    // ============================================================

    private AchievementTierDefinition ObtenerPrimerRango(
        AchievementDefinition definicion
    )
    {
        if (definicion == null)
            return null;

        AchievementTierDefinition bronce =
            definicion.ObtenerRango(AchievementRank.Bronce);

        if (bronce != null)
            return bronce;

        AchievementTierDefinition plata =
            definicion.ObtenerRango(AchievementRank.Plata);

        if (plata != null)
            return plata;

        return definicion.ObtenerRango(AchievementRank.Oro);
    }

    // ============================================================
    // GUARDAR
    // ============================================================

    private void GuardarProgreso()
    {
        if (datos == null)
            return;

        string json = JsonUtility.ToJson(datos, true);

        File.WriteAllText(rutaArchivo, json);
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

        string json = File.ReadAllText(rutaArchivo);

        datos = JsonUtility.FromJson<AchievementsData>(json);

        if (datos == null)
        {
            datos = new AchievementsData();

            Debug.LogWarning(
                "[ACHIEVEMENTS] Archivo inválido."
            );
        }

        if (datos.logros == null)
            datos.logros = new List<AchievementData>();

        if (datos.easterEggsEncontrados == null)
        {
            datos.easterEggsEncontrados = new List<string>();
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

        if (Instance == this)
            Instance = null;
    }

    // ============================================================
    // TOTAL DE LOGROS
    // ============================================================

    public int ObtenerCantidadTotalLogros()
    {
        if (achievementDatabase == null)
            return 0;

        IReadOnlyList<AchievementDefinition> definiciones =
            achievementDatabase.ObtenerTodos();

        if (definiciones == null)
            return 0;

        return definiciones.Count;
    }

    // ============================================================
    // TOTAL DE RANGOS DESBLOQUEADOS
    // ============================================================

    public int ObtenerCantidadTotalRangosDesbloqueados()
    {
        if (datos == null || datos.logros == null)
            return 0;

        int total = 0;

        foreach (AchievementData logro in datos.logros)
        {
            if (logro == null)
                continue;

            switch (logro.rangoActual)
            {
                case AchievementRank.Bronce:
                    total += 1;
                    break;

                case AchievementRank.Plata:
                    total += 2;
                    break;

                case AchievementRank.Oro:
                    total += 3;
                    break;
            }
        }

        return total;
    }
}
