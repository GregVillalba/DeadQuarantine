using UnityEngine;

public class AchievementUIItem : MonoBehaviour
{
    [Header("Identificación")]
    [SerializeField]
    private AchievementId achievementId;


    private GameObject estadoLogrado;
    private GameObject estadoNoLogrado;


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


    private void OnEnable()
    {
        ActualizarEstado();
    }


    public void ActualizarEstado()
    {
        if (AchievementsManager.Instance == null)
            return;


        bool desbloqueado =
            AchievementsManager.Instance.EstaDesbloqueado(
                achievementId
            );


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
}