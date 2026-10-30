using UnityEngine;

/// <summary>
/// Niebla independiente por escenario.
///
/// Agregá este componente al GameObject raíz del escenario.
/// Cuando el escenario se activa, aplica su configuración de niebla.
/// Cuando se desactiva, puede restaurar la configuración anterior.
///
/// No necesita cámara, BoxCollider, Volume, Renderer Feature,
/// shader personalizado ni otros scripts de niebla.
///
/// IMPORTANTE:
/// RenderSettings.fog es global. Este sistema está pensado para
/// escenarios que se activan/desactivan según cuál se esté jugando.
/// </summary>
[DisallowMultipleComponent]
public sealed class NieblaEscenario : MonoBehaviour
{
    [Header("Niebla del escenario")]
    [SerializeField] private bool activarNiebla = true;

    [SerializeField] private Color colorNiebla =
        new Color(0.12f, 0.14f, 0.17f, 1f);

    [SerializeField] private FogMode modoNiebla =
        FogMode.ExponentialSquared;

    [Tooltip("Se utiliza con Exponential y ExponentialSquared.")]
    [SerializeField, Range(0f, 0.1f)]
    private float densidadNiebla = 0.012f;

    [Tooltip("Se utiliza solamente con el modo Linear.")]
    [SerializeField, Min(0f)]
    private float inicioNiebla = 20f;

    [Tooltip("Se utiliza solamente con el modo Linear.")]
    [SerializeField, Min(0f)]
    private float finNiebla = 120f;

    [Header("Comportamiento")]
    [Tooltip("Si está activo, al desactivar este escenario se restaura la niebla que había antes.")]
    [SerializeField] private bool restaurarAlDesactivar = true;

    [Tooltip("Si está activo, vuelve a aplicar los valores durante Update. Útil si otro sistema cambia RenderSettings.")]
    [SerializeField] private bool mantenerConfiguracion = false;

    private bool estadoGuardado;
    private bool fogAnterior;
    private Color colorAnterior;
    private FogMode modoAnterior;
    private float densidadAnterior;
    private float inicioAnterior;
    private float finAnterior;

    private void OnEnable()
    {
        GuardarEstadoAnterior();
        AplicarNiebla();
    }

    private void Update()
    {
        if (mantenerConfiguracion)
            AplicarNiebla();
    }

    private void OnDisable()
    {
        if (restaurarAlDesactivar)
            RestaurarNieblaAnterior();
    }

    public void AplicarNiebla()
    {
        if (!activarNiebla)
        {
            RenderSettings.fog = false;
            return;
        }

        RenderSettings.fog = true;
        RenderSettings.fogColor = colorNiebla;
        RenderSettings.fogMode = modoNiebla;

        switch (modoNiebla)
        {
            case FogMode.Linear:
                RenderSettings.fogStartDistance = inicioNiebla;
                RenderSettings.fogEndDistance = finNiebla;
                break;

            case FogMode.Exponential:
            case FogMode.ExponentialSquared:
                RenderSettings.fogDensity = densidadNiebla;
                break;
        }
    }

    public void DesactivarNiebla()
    {
        RenderSettings.fog = false;
    }

    public void RestaurarNieblaAnterior()
    {
        if (!estadoGuardado)
            return;

        RenderSettings.fog = fogAnterior;
        RenderSettings.fogColor = colorAnterior;
        RenderSettings.fogMode = modoAnterior;
        RenderSettings.fogDensity = densidadAnterior;
        RenderSettings.fogStartDistance = inicioAnterior;
        RenderSettings.fogEndDistance = finAnterior;
    }

    private void GuardarEstadoAnterior()
    {
        if (estadoGuardado)
            return;

        estadoGuardado = true;

        fogAnterior = RenderSettings.fog;
        colorAnterior = RenderSettings.fogColor;
        modoAnterior = RenderSettings.fogMode;
        densidadAnterior = RenderSettings.fogDensity;
        inicioAnterior = RenderSettings.fogStartDistance;
        finAnterior = RenderSettings.fogEndDistance;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        densidadNiebla = Mathf.Max(0f, densidadNiebla);
        inicioNiebla = Mathf.Max(0f, inicioNiebla);
        finNiebla = Mathf.Max(inicioNiebla, finNiebla);

        if (Application.isPlaying && isActiveAndEnabled)
            AplicarNiebla();
    }
#endif
}
