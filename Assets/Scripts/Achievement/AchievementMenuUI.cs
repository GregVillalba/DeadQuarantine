using UnityEngine;

public class AchievementsMenuUI : MonoBehaviour
{
    private AchievementUIItem[] elementos;


    private void Awake()
    {
        elementos =
            GetComponentsInChildren<AchievementUIItem>(
                true
            );
    }


    private void OnEnable()
    {
        ActualizarTodos();
    }


    public void ActualizarTodos()
    {
        if (elementos == null)
            return;


        foreach (AchievementUIItem elemento in elementos)
        {
            if (elemento == null)
                continue;


            elemento.ActualizarEstado();
        }
    }
}