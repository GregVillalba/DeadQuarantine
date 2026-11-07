using UnityEngine.SceneManagement;

/// <summary>Si la escena activa es la del tutorial (ahí cambian algunos valores: tienda, torreta, zombies...).</summary>
public static class TutorialEscena
{
    public const string Nombre = "TutorialScene";

    public static bool EsActiva => SceneManager.GetActiveScene().name == Nombre;
}
