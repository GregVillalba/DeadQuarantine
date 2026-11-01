using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Diagnóstico del "mapa en blanco": a los 10, 20 y 30 s de cargar la escena multijugador escribe
// en la consola dónde está el jugador, qué cámaras hay y cuánto del escenario activo está realmente visible.
// Se crea solo al iniciar el juego: no hace falta ponerlo en ninguna escena. Filtrá la consola por "[Diag]".
public class EscenarioDiagnostico : MonoBehaviour
{
    private const string NombreEscena = "MainSceneMultiPlayer";

    private static EscenarioDiagnostico instancia;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Crear()
    {
        if (instancia != null)
            return;

        GameObject go = new GameObject("EscenarioDiagnostico");
        instancia = go.AddComponent<EscenarioDiagnostico>();
        DontDestroyOnLoad(go);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += AlCargarEscena;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= AlCargarEscena;
    }

    private void AlCargarEscena(Scene escena, LoadSceneMode modo)
    {
        if (escena.name == NombreEscena)
            StartCoroutine(Rutina());
    }

    private IEnumerator Rutina()
    {
        for (int i = 1; i <= 3; i++)
        {
            yield return new WaitForSecondsRealtime(10f);

            if (SceneManager.GetActiveScene().name != NombreEscena)
                yield break;

            Registrar(i * 10);
        }
    }

    private void Registrar(int segundos)
    {
        string p = "[Diag] +" + segundos + " s | ";

        GestorEscenariosPartida gestor = FindAnyObjectByType<GestorEscenariosPartida>();
        ConfiguracionEscenarioJugable esc = gestor != null ? gestor.ObtenerEscenarioActivo() : null;

        if (esc == null)
        {
            Debug.LogWarning(p + "No hay escenario activo en el Gestor.");
            return;
        }

        // ---- Cámaras ----
        Camera cam = null;

        foreach (Camera c in Camera.allCameras)
        {
            Debug.Log(p + "Cámara '" + c.name + "' pos=" + c.transform.position +
                      " cullingMask=" + c.cullingMask + " far=" + c.farClipPlane);

            if (c.targetTexture == null && (cam == null || c.depth > cam.depth))
                cam = c;
        }

        if (cam == null)
        {
            Debug.LogWarning(p + "No hay ninguna cámara renderizando.");
            return;
        }

        // ---- Jugador local ----
        foreach (MultiplayerPlayerSpawnAssigner a in
                 FindObjectsByType<MultiplayerPlayerSpawnAssigner>(FindObjectsSortMode.None))
        {
            if (!a.IsOwner)
                continue;

            Transform spawn = esc.ObtenerSpawnJugador(Mathf.Max(0, a.AssignedSpawnIndex.Value));
            string distancia = spawn != null
                ? Vector3.Distance(a.transform.position, spawn.position).ToString("F1") + " m"
                : "sin spawn";

            Debug.Log(p + "Jugador local pos=" + a.transform.position +
                      " | spawn esperado=" + (spawn != null ? spawn.position.ToString() : "null") +
                      " | distancia al spawn=" + distancia +
                      " | IsAtSpawn=" + a.IsAtSpawn.Value);
        }

        // ---- Escenario activo ----
        Transform raiz = esc.transform.parent != null ? esc.transform.parent : esc.transform;

        Renderer[] renderers = raiz.GetComponentsInChildren<Renderer>(true);
        Plane[] planos = GeometryUtility.CalculateFrustumPlanes(cam);

        int habilitados = 0, activos = 0, enVista = 0;
        Bounds area = new Bounds();
        bool primero = true;

        foreach (Renderer r in renderers)
        {
            if (r == null)
                continue;

            if (r.enabled)
                habilitados++;

            if (!r.enabled || !r.gameObject.activeInHierarchy)
                continue;

            activos++;

            if (primero)
            {
                area = r.bounds;
                primero = false;
            }
            else
            {
                area.Encapsulate(r.bounds);
            }

            if (GeometryUtility.TestPlanesAABB(planos, r.bounds))
                enVista++;
        }

        int hijosInactivos = 0;

        foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
        {
            if (!t.gameObject.activeSelf)
                hijosInactivos++;
        }

        Debug.Log(p + "Escenario '" + raiz.name + "': renderers=" + renderers.Length +
                  " | habilitados=" + habilitados +
                  " | habilitados Y activos en jerarquía=" + activos +
                  " | dentro del frustum de '" + cam.name + "'=" + enVista +
                  " | GameObjects inactivos=" + hijosInactivos);

        Debug.Log(p + "Área del mapa: centro=" + area.center + " tamaño=" + area.size +
                  " | cámara dentro del área=" + area.Contains(cam.transform.position));
    }
}