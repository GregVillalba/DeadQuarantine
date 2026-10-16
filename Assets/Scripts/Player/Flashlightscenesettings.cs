using UnityEngine;

/// <summary>
/// Pone este componente en un objeto de la escena (por ejemplo "LightingSettings") para PERMITIR la linterna en ese escenario.
/// Las escenas que no lo tienen (por ejemplo la casa, que ya trae sus luces) no pueden usar la linterna.
/// </summary>
public class FlashlightSceneSettings : MonoBehaviour
{
    [Tooltip("Si está apagado, la linterna no se puede usar aunque el componente esté en la escena.")]
    [SerializeField] private bool flashlightAllowed = true;

    private static FlashlightSceneSettings active;

    /// <summary>True si la escena actual permite usar la linterna.</summary>
    public static bool Allowed => active != null && active.flashlightAllowed;

    private void OnEnable()
    {
        active = this;
    }

    private void OnDisable()
    {
        if (active == this)
            active = null;
    }
}