using UnityEngine;

/// <summary>
/// Lugar del escenario donde aparece un recurso. Siempre da el mismo tipo (US 19.1).
/// Va como hijo de una OpcionRecursosEscenario; el recurso aparece exactamente en esta posición y rotación.
/// </summary>
public class PuntoRecursoEscenario : MonoBehaviour
{
    [SerializeField] private TipoRecurso tipo;

    public TipoRecurso Tipo => tipo;

    /// <summary>Solo para armar escenarios en código (tests).</summary>
    public void Configurar(TipoRecurso nuevoTipo)
    {
        tipo = nuevoTipo;
    }

    public static Color ColorGizmo(TipoRecurso tipo)
    {
        switch (tipo)
        {
            case TipoRecurso.Cantimplora: return new Color(0.2f, 0.8f, 0.3f);
            case TipoRecurso.Bidon: return new Color(0.9f, 0.25f, 0.2f);
            default: return new Color(0.3f, 0.6f, 1f);
        }
    }

    // Solo en el editor, para ubicar los puntos: no existe en el build.
    private void OnDrawGizmos()
    {
        Gizmos.color = ColorGizmo(tipo);
        Gizmos.DrawSphere(transform.position, 0.2f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 0.6f);

#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.7f, tipo.Nombre());
#endif
    }
}
