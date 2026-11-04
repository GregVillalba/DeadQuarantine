using UnityEngine;

/// <summary>
/// Asegura que lluvia y niebla sigan reproduciéndose aun si el menú pone Time.timeScale = 0.
/// </summary>
public class MainMenuAtmosphere : MonoBehaviour
{
    private void OnEnable()
    {
        ParticleSystem[] systems =
            GetComponentsInChildren<ParticleSystem>(true);

        foreach (ParticleSystem ps in systems)
        {
            if (ps == null)
                continue;

            ParticleSystem.MainModule main = ps.main;
            main.useUnscaledTime = true;

            if (!ps.gameObject.activeInHierarchy)
                continue;

            ps.Play(true);
        }
    }
}
