using UnityEditor;
using UnityEngine;
using Dificultad = ConfiguracionPartidaSeleccionada.Dificultad;
using Modo = ConfiguracionPartidaSeleccionada.ModoJuego;

/// <summary>
/// Inspector de DifficultyManager: agrega, debajo de los campos de siempre, los checkboxes para
/// desbloquear o bloquear dificultades mientras se prueba (ver DesbloqueoDificultades).
/// Los checkboxes leen y escriben el progreso guardado de ESTA máquina; no se guardan en la escena.
/// Funcionan con el juego detenido o en Play.
/// </summary>
[CustomEditor(typeof(DifficultyManager))]
public class DifficultyManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(12f);
        EditorGUILayout.LabelField("Desbloqueo de dificultades (pruebas)", EditorStyles.boldLabel);

        // ---------- Atajo: todo desbloqueado sin tocar el progreso ----------
        bool forzar = DesbloqueoDificultades.DesbloquearTodoEnEditor;

        bool nuevoForzar = EditorGUILayout.ToggleLeft(
            new GUIContent(
                "Desbloquear todas las dificultades (solo Editor)",
                "Mientras está tildado, en el Editor se puede elegir cualquier dificultad en cualquier modo. " +
                "No modifica el progreso guardado y no afecta a las builds."),
            forzar);

        if (nuevoForzar != forzar)
            DesbloqueoDificultades.DesbloquearTodoEnEditor = nuevoForzar;

        // ---------- Progreso guardado ----------
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Progreso guardado en esta máquina", EditorStyles.miniBoldLabel);

        using (new EditorGUI.DisabledScope(nuevoForzar))
        {
            DibujarModo("Historia", Modo.Historia);
            DibujarModo("5 Rondas", Modo.Ronda5);
            DibujarModo("Supervivencia", Modo.Supervivencia);
        }

        EditorGUILayout.Space(4f);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Bloquear todo (primera vez)"))
                DesbloqueoDificultades.ReiniciarProgreso();

            if (GUILayout.Button("Desbloquear todo"))
            {
                DesbloqueoDificultades.EstablecerMaximaDesbloqueada(Modo.Historia, Dificultad.Pesadilla);
                DesbloqueoDificultades.EstablecerMaximaDesbloqueada(Modo.Ronda5, Dificultad.Pesadilla);
                DesbloqueoDificultades.EstablecerMaximaDesbloqueada(Modo.Supervivencia, Dificultad.Pesadilla);
            }
        }

        EditorGUILayout.HelpBox(
            nuevoForzar
                ? "Todo desbloqueado por el checkbox de arriba. Destildalo para probar el progreso guardado."
                : "Normal siempre está desbloqueada. Pesadilla necesita Dificil: al tildar Pesadilla se tilda " +
                  "Dificil, y al destildar Dificil se destilda Pesadilla.",
            MessageType.Info);
    }

    private static void DibujarModo(string etiqueta, Modo modo)
    {
        Dificultad maxima = DesbloqueoDificultades.MaximaDesbloqueada(modo);

        bool dificil = maxima >= Dificultad.Dificil;
        bool pesadilla = maxima >= Dificultad.Pesadilla;

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.PrefixLabel(etiqueta);

            bool nuevoDificil = EditorGUILayout.ToggleLeft("Dificil", dificil, GUILayout.Width(80f));
            bool nuevoPesadilla = EditorGUILayout.ToggleLeft("Pesadilla", pesadilla, GUILayout.Width(90f));

            if (nuevoDificil == dificil && nuevoPesadilla == pesadilla)
                return;

            Dificultad nueva;

            if (nuevoPesadilla != pesadilla)
                nueva = nuevoPesadilla ? Dificultad.Pesadilla : Dificultad.Dificil;
            else
                nueva = nuevoDificil ? Dificultad.Dificil : Dificultad.Normal;

            DesbloqueoDificultades.EstablecerMaximaDesbloqueada(modo, nueva);
        }
    }

    // En Play el progreso puede cambiar solo (al desbloquear jugando): mantener los checkboxes al día.
    public override bool RequiresConstantRepaint()
    {
        return Application.isPlaying;
    }
}
