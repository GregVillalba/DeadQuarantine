using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class AchievementHUD : MonoBehaviour
{
    [Header("Textos")]
    [SerializeField]
    private TextMeshProUGUI achievementTitleText;

    [SerializeField]
    private TextMeshProUGUI achievementDescriptionText;

    [Header("Tiempo visible")]
    [SerializeField]
    private float maxVisibleTime = 5f;

    private Queue<AchievementId> colaLogros =
        new Queue<AchievementId>();

    private Coroutine mostrarColasCoroutine;

    private bool mostrandoLogro = false;


    private void Awake()
    {
        gameObject.SetActive(false);
    }


    public void Show(AchievementId id)
    {
        // Agregar el logro a la cola.
        colaLogros.Enqueue(id);

        // Si el HUD está apagado, lo activamos
        // antes de intentar iniciar la coroutine.
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        // Si ya existe una coroutine procesando la cola,
        // no iniciamos otra.
        if (mostrarColasCoroutine == null)
        {
            mostrarColasCoroutine =
                StartCoroutine(MostrarColaDeLogros());
        }
    }


    private IEnumerator MostrarColaDeLogros()
    {
        while (colaLogros.Count > 0)
        {
            AchievementId id = colaLogros.Dequeue();

            mostrandoLogro = true;

            // Nos aseguramos de que esté visible.
            gameObject.SetActive(true);

            if (achievementTitleText != null)
            {
                achievementTitleText.text =
                    ObtenerTitulo(id);
            }

            if (achievementDescriptionText != null)
            {
                achievementDescriptionText.text =
                    ObtenerDescripcion(id);
            }

            // Esperar el tiempo configurado.
            yield return new WaitForSeconds(
                maxVisibleTime
            );

            mostrandoLogro = false;

            // Si todavía hay logros pendientes,
            // NO apagamos el HUD.
            if (colaLogros.Count > 0)
            {
                continue;
            }

            // No quedan logros.
            gameObject.SetActive(false);
        }

        mostrarColasCoroutine = null;
    }


    public void Hide()
    {
        if (mostrarColasCoroutine != null)
        {
            StopCoroutine(mostrarColasCoroutine);
            mostrarColasCoroutine = null;
        }

        colaLogros.Clear();

        mostrandoLogro = false;

        gameObject.SetActive(false);
    }


    private string ObtenerTitulo(AchievementId id)
    {
        switch (id)
        {
            case AchievementId.Zombies50:
                return "Cazador";

            case AchievementId.Zombies100:
                return "Exterminador";

            case AchievementId.Zombies300:
                return "Apocalipsis";

            case AchievementId.BossFinal:
                return "Jefe final";

            case AchievementId.Headshots10:
                return "Buena puntería";

            case AchievementId.Headshots50:
                return "Francotirador";

            case AchievementId.Headshots100:
                return "Tirador experto";

            case AchievementId.Headshots300:
                return "Maestro de la puntería";

            case AchievementId.RondasNormal:
                return "Superviviente";

            case AchievementId.RondasDificil:
                return "Superviviente difícil";

            case AchievementId.RondasPesadilla:
                return "Superviviente de pesadilla";

            case AchievementId.SupervivenciaNormal:
                return "Sobreviviente";

            case AchievementId.SupervivenciaDificil:
                return "Superviviente nato";

            case AchievementId.SupervivenciaPesadilla:
                return "Contra todo pronóstico";

            case AchievementId.HistoriaNormal:
                return "Historia completada";

            case AchievementId.HistoriaDificil:
                return "Historia difícil";

            case AchievementId.HistoriaPesadilla:
                return "Historia de pesadilla";

            case AchievementId.EasterEgg1:
                return "¿Qué fue eso?";

            case AchievementId.EasterEgg3:
                return "Cazador de secretos";

            case AchievementId.MinijuegoZonaTiro:
                return "Buena puntería";

            default:
                return "Logro desbloqueado";
        }
    }


    private string ObtenerDescripcion(AchievementId id)
    {
        switch (id)
        {
            case AchievementId.Zombies50:
                return "Eliminaste 50 zombies.";

            case AchievementId.Zombies100:
                return "Eliminaste 100 zombies.";

            case AchievementId.Zombies300:
                return "Eliminaste 300 zombies.";

            case AchievementId.BossFinal:
                return "Derrotaste al jefe final.";

            case AchievementId.Headshots10:
                return "Conseguiste 10 disparos a la cabeza.";

            case AchievementId.Headshots50:
                return "Conseguiste 50 disparos a la cabeza.";

            case AchievementId.Headshots100:
                return "Conseguiste 100 disparos a la cabeza.";

            case AchievementId.Headshots300:
                return "Conseguiste 300 disparos a la cabeza.";

            case AchievementId.RondasNormal:
                return "Completaste una ronda en dificultad normal.";

            case AchievementId.RondasDificil:
                return "Completaste una ronda en dificultad difícil.";

            case AchievementId.RondasPesadilla:
                return "Completaste una ronda en dificultad pesadilla.";

            case AchievementId.SupervivenciaNormal:
                return "Completaste una partida de supervivencia normal.";

            case AchievementId.SupervivenciaDificil:
                return "Completaste una partida de supervivencia difícil.";

            case AchievementId.SupervivenciaPesadilla:
                return "Completaste una partida de supervivencia pesadilla.";

            case AchievementId.HistoriaNormal:
                return "Completaste la historia en dificultad normal.";

            case AchievementId.HistoriaDificil:
                return "Completaste la historia en dificultad difícil.";

            case AchievementId.HistoriaPesadilla:
                return "Completaste la historia en dificultad pesadilla.";

            case AchievementId.EasterEgg1:
                return "Encontraste un secreto.";

            case AchievementId.EasterEgg3:
                return "Encontraste tres secretos.";

            case AchievementId.MinijuegoZonaTiro:
                return "Completaste el minijuego de la zona de tiro.";

            default:
                return "Desbloqueaste un nuevo logro.";
        }
    }
}