using UnityEngine;

public class AchievementUIItem : MonoBehaviour
{
    [Header("Identificación")]
    [SerializeField]
    private AchievementId achievementId;


    private GameObject estadoLogrado;
    private GameObject estadoNoLogrado;


    // ============================================================
    // AWAKE
    // ============================================================

    private void Awake()
    {
        Transform estado =
            transform.Find("estado");


        if (estado == null)
        {
            Debug.LogWarning(
                $"[ACHIEVEMENTS UI] No se encontró 'estado' en {gameObject.name}"
            );

            return;
        }


        Transform logrado =
            estado.Find("logrado");


        Transform noLogrado =
            estado.Find("no_logrado");


        if (logrado != null)
        {
            estadoLogrado =
                logrado.gameObject;
        }


        if (noLogrado != null)
        {
            estadoNoLogrado =
                noLogrado.gameObject;
        }
    }


    // ============================================================
    // ACTUALIZAR ESTADO
    // ============================================================

    public void ActualizarEstado()
    {
        if (AchievementsManager.Instance == null)
            return;


        bool desbloqueado =
            EstaDesbloqueado();


        if (estadoLogrado != null)
        {
            estadoLogrado.SetActive(
                desbloqueado
            );
        }


        if (estadoNoLogrado != null)
        {
            estadoNoLogrado.SetActive(
                !desbloqueado
            );
        }
    }


    // ============================================================
    // ESTADO
    // ============================================================

    public bool EstaDesbloqueado()
    {
        if (AchievementsManager.Instance == null)
            return false;


        return AchievementsManager.Instance.EstaDesbloqueado(
            achievementId
        );
    }
}