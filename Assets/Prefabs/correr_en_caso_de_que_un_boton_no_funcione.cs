using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class UIClickDebug : MonoBehaviour
{
    private readonly List<RaycastResult> resultados = new List<RaycastResult>();
    private float proximoLog;

    private void Update()
    {
        if (Time.unscaledTime < proximoLog) return;
        proximoLog = Time.unscaledTime + 0.5f;

        var es = EventSystem.current;
        if (es == null) { Debug.LogWarning("[UIDebug] NO hay EventSystem activo."); return; }

        Vector2 pos;
#if ENABLE_INPUT_SYSTEM
        pos = UnityEngine.InputSystem.Mouse.current != null
            ? UnityEngine.InputSystem.Mouse.current.position.ReadValue()
            : Vector2.zero;
#else
    pos = Input.mousePosition;
#endif

        bool dentro = pos.x >= 0 && pos.y >= 0 && pos.x <= Screen.width && pos.y <= Screen.height;

        var datos = new PointerEventData(es) { position = pos };
        resultados.Clear();
        es.RaycastAll(datos, resultados);

        string golpes = resultados.Count > 0
    ? string.Join(" > ", resultados.ConvertAll(resultado => resultado.gameObject.name))
    : "(nada)";

        Debug.Log($"[UIDebug] pos={pos} pantalla={Screen.width}x{Screen.height} dentro={dentro} | golpes: {golpes}");
    }
}