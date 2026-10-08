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

        public AchievementRank rangoActual;

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


    // ============================================================
    // BASE DE DATOS
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
            logro.rangoActual = AchievementRank.Ninguno;
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
            AchievementId.DerrotadorSupremo,
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
        if (cantidad <= 0)
            return;

        AchievementData logro =
            ObtenerLogro(id);

        if (logro == null)
            return;

        AchievementDefinition definicion =
            ObtenerDefinicion(id);

        if (definicion == null)
            return;


        // ========================================================
        // LOGRO SIN RANGOS
        // ========================================================

        if (!definicion.tieneRangos)
        {
            // Si ya está completado, no seguimos acumulando.
            if (logro.desbloqueado)
                return;

            logro.progreso += cantidad;

            AchievementTierDefinition unico =
                ObtenerPrimerRango(definicion);

            if (unico == null)
                return;

            if (logro.progreso >= unico.objetivo)
            {
                logro.progreso =
                    unico.objetivo;

                logro.desbloqueado =
                    true;

                Debug.Log(
                    "[ACHIEVEMENTS] LOGRO COMPLETADO: " +
                    id
                );

                MostrarLogroEnHUD(
                    id,
                    AchievementRank.Ninguno
                );
            }

            GuardarProgreso();

            return;
        }


        // ========================================================
        // LOGRO CON RANGOS
        // ========================================================

        logro.progreso += cantidad;

        ActualizarRangoDesdeProgreso(
            id
        );

        GuardarProgreso();
    }


    // ============================================================
    // ACTUALIZAR RANGO POR PROGRESO
    // ============================================================

    private void ActualizarRangoDesdeProgreso(
        AchievementId id
    )
    {
        AchievementData logro =
            ObtenerLogro(id);

        AchievementDefinition definicion =
            ObtenerDefinicion(id);

        if (logro == null || definicion == null)
            return;


        AchievementRank nuevoRango =
            DeterminarRango(
                definicion,
                logro.progreso
            );


        // ========================================================
        // CORREGIR ESTADO DE COMPLETADO
        // ========================================================
        //
        // Un logro con rangos solamente está COMPLETADO
        // cuando llega a ORO.
        //

        if (nuevoRango <= logro.rangoActual)
        {
            logro.desbloqueado =
                nuevoRango == AchievementRank.Oro;

            return;
        }


        // ========================================================
        // ACTUALIZAR RANGO
        // ========================================================

        logro.rangoActual =
            nuevoRango;


        // ========================================================
        // COMPLETADO SOLO EN ORO
        // ========================================================

        logro.desbloqueado =
            nuevoRango == AchievementRank.Oro;


        // ========================================================
        // ASEGURAR PROGRESO MÍNIMO DEL RANGO
        // ========================================================

        AchievementTierDefinition tier =
            definicion.ObtenerRango(
                nuevoRango
            );

        if (tier != null)
        {
            logro.progreso =
                Mathf.Max(
                    logro.progreso,
                    tier.objetivo
                );
        }


        // ========================================================
        // DEBUG
        // ========================================================

        Debug.Log(
            "[ACHIEVEMENTS] RANGO DESBLOQUEADO: " +
            id +
            " -> " +
            nuevoRango
        );


        if (nuevoRango == AchievementRank.Oro)
        {
            Debug.Log(
                "[ACHIEVEMENTS] LOGRO COMPLETADO: " +
                id
            );
        }


        // ========================================================
        // HUD
        // ========================================================

        MostrarLogroEnHUD(
            id,
            nuevoRango
        );
    }


    // ============================================================
    // DETERMINAR RANGO
    // ============================================================

    private AchievementRank DeterminarRango(
        AchievementDefinition definicion,
        int progreso
    )
    {
        AchievementRank rangoActual =
            AchievementRank.Ninguno;

        AchievementRank[] rangos =
        {
            AchievementRank.Bronce,
            AchievementRank.Plata,
            AchievementRank.Oro
        };

        foreach (AchievementRank rango in rangos)
        {
            AchievementTierDefinition tier =
                definicion.ObtenerRango(
                    rango
                );

            if (tier == null)
                continue;

            if (progreso >= tier.objetivo)
            {
                rangoActual =
                    rango;
            }
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
        if (rango == AchievementRank.Ninguno)
            return;


        AchievementData logro =
            ObtenerLogro(id);

        AchievementDefinition definicion =
            ObtenerDefinicion(id);

        if (logro == null || definicion == null)
            return;


        // ========================================================
        // LOGRO SIN RANGOS
        // ========================================================

        if (!definicion.tieneRangos)
        {
            Desbloquear(id);
            return;
        }


        // ========================================================
        // SI YA TENEMOS ESE RANGO O UNO SUPERIOR
        // ========================================================

        if (rango <= logro.rangoActual)
            return;


        // ========================================================
        // ACTUALIZAR RANGO
        // ========================================================

        logro.rangoActual =
            rango;


        // ========================================================
        // COMPLETADO SOLO EN ORO
        // ========================================================

        logro.desbloqueado =
            rango == AchievementRank.Oro;


        // ========================================================
        // ACTUALIZAR PROGRESO
        // ========================================================

        AchievementTierDefinition tier =
            definicion.ObtenerRango(
                rango
            );

        if (tier != null)
        {
            logro.progreso =
                Mathf.Max(
                    logro.progreso,
                    tier.objetivo
                );
        }


        // ========================================================
        // DEBUG
        // ========================================================

        Debug.Log(
            "[ACHIEVEMENTS] RANGO DESBLOQUEADO: " +
            id +
            " -> " +
            rango
        );


        if (rango == AchievementRank.Oro)
        {
            Debug.Log(
                "[ACHIEVEMENTS] LOGRO COMPLETADO: " +
                id
            );
        }


        // ========================================================
        // HUD
        // ========================================================

        MostrarLogroEnHUD(
            id,
            rango
        );


        GuardarProgreso();
    }


    // ============================================================
    // DESBLOQUEAR LOGRO
    // ============================================================

    public void Desbloquear(
        AchievementId id
    )
    {
        AchievementData logro =
            ObtenerLogro(id);

        AchievementDefinition definicion =
            ObtenerDefinicion(id);

        if (logro == null || definicion == null)
            return;


        // ========================================================
        // LOGRO SIN RANGOS
        // ========================================================

        if (!definicion.tieneRangos)
        {
            if (logro.desbloqueado)
                return;

            logro.desbloqueado =
                true;

            Debug.Log(
                "[ACHIEVEMENTS] LOGRO COMPLETADO: " +
                id
            );

            MostrarLogroEnHUD(
                id,
                AchievementRank.Ninguno
            );

            GuardarProgreso();

            return;
        }


        // ========================================================
        // LOGRO CON RANGOS
        // ========================================================

        AchievementRank rango =
            DeterminarRango(
                definicion,
                logro.progreso
            );


        if (rango == AchievementRank.Ninguno)
            return;


        if (rango <= logro.rangoActual)
        {
            // Aseguramos que el estado coincida
            // con el rango actual.
            logro.desbloqueado =
                rango == AchievementRank.Oro;

            GuardarProgreso();

            return;
        }


        // ========================================================
        // ACTUALIZAR RANGO
        // ========================================================

        logro.rangoActual =
            rango;


        // ========================================================
        // COMPLETADO SOLO EN ORO
        // ========================================================

        logro.desbloqueado =
            rango == AchievementRank.Oro;


        // ========================================================
        // ACTUALIZAR PROGRESO
        // ========================================================

        AchievementTierDefinition tier =
            definicion.ObtenerRango(
                rango
            );

        if (tier != null)
        {
            logro.progreso =
                Mathf.Max(
                    logro.progreso,
                    tier.objetivo
                );
        }


        // ========================================================
        // DEBUG
        // ========================================================

        Debug.Log(
            "[ACHIEVEMENTS] RANGO DESBLOQUEADO: " +
            id +
            " -> " +
            rango
        );


        if (rango == AchievementRank.Oro)
        {
            Debug.Log(
                "[ACHIEVEMENTS] LOGRO COMPLETADO: " +
                id
            );
        }


        // ========================================================
        // HUD
        // ========================================================

        MostrarLogroEnHUD(
            id,
            rango
        );


        GuardarProgreso();
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
    // OBTENER DEFINICIÓN PARA LA UI
    // ============================================================

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
        if (GameplayPopupsController.Instance == null)
        {
            Debug.LogWarning(
                "[ACHIEVEMENTS] No existe GameplayPopupsController."
            );

            return;
        }

        GameplayPopupsController.Instance
            .MostrarLogro(
                id,
                rango
            );
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
    // OBTENER RANGO
    // ============================================================

    public AchievementRank ObtenerRango(
        AchievementId id
    )
    {
        AchievementData logro =
            ObtenerLogro(id);

        if (logro == null)
            return AchievementRank.Ninguno;

        return logro.rangoActual;
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
    // OBTENER PRIMER RANGO
    // ============================================================

    private AchievementTierDefinition ObtenerPrimerRango(
        AchievementDefinition definicion
    )
    {
        if (definicion == null)
            return null;


        AchievementTierDefinition bronce =
            definicion.ObtenerRango(
                AchievementRank.Bronce
            );

        if (bronce != null)
            return bronce;


        AchievementTierDefinition plata =
            definicion.ObtenerRango(
                AchievementRank.Plata
            );

        if (plata != null)
            return plata;


        return definicion.ObtenerRango(
            AchievementRank.Oro
        );
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
            datos =
                new AchievementsData();

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
            datos =
                new AchievementsData();

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