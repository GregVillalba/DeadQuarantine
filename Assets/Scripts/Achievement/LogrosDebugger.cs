using System.Text;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class LogrosDebugger : MonoBehaviour
{
    private void Update()
    {
#if ENABLE_INPUT_SYSTEM
        // Compatible con el New Input System
        if (Keyboard.current != null && Keyboard.current.f8Key.wasPressedThisFrame)
        {
            ImprimirEstadoLogros();
        }
#else
        // Fallback al sistema tradicional si se cambia en Project Settings
        if (Input.GetKeyDown(KeyCode.F8))
        {
            ImprimirEstadoLogros();
        }
#endif
    }

    [ContextMenu("Imprimir Estado de Logros")]
    public void ImprimirEstadoLogros()
    {
        if (AchievementsManager.Instance == null)
        {
            Debug.LogError("[LOGROS DEBUG] No se encontró AchievementsManager.Instance en la escena.");
            return;
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("==================================================");
        sb.AppendLine("               REPORTE DE LOGROS Y SUBLOGROS       ");
        sb.AppendLine($" Jugador: {AchievementsManager.Instance.UsernameJugador} | ID Local: {AchievementsManager.Instance.IdLocalJugador}");
        sb.AppendLine("==================================================");

        System.Array valoresId = System.Enum.GetValues(typeof(AchievementId));

        int totalLogros = 0;
        int logrosCompletados = 0;
        int totalSublogrosDesbloqueados = 0;

        foreach (AchievementId id in valoresId)
        {
            totalLogros++;
            AchievementDefinition def = AchievementsManager.Instance.ObtenerDefinicionParaUI(id);
            var data = AchievementsManager.Instance.ObtenerLogro(id);

            string titulo = def != null ? def.titulo : id.ToString();
            int progreso = data != null ? data.progreso : 0;
            bool desbloqueado = data != null && data.desbloqueado;
            AchievementRank rango = data != null ? data.rangoActual : AchievementRank.Ninguno;

            if (desbloqueado)
                logrosCompletados++;

            string estadoLogro = desbloqueado ? "<color=green>[COMPLETO]</color>" : "<color=yellow>[EN PROGRESO]</color>";
            sb.AppendLine($"\n{estadoLogro} <b>{titulo}</b> (ID: {id})");
            sb.AppendLine($"   • Progreso acumulado: {progreso}");

            if (def != null && def.tieneRangos)
            {
                sb.AppendLine("   • Sublogros / Rangos:");

                bool tieneBronce = rango >= AchievementRank.Bronce;
                sb.AppendLine($"      - [Bronce]: {(tieneBronce ? "<color=green>DESBLOQUEADO</color>" : "<color=red>BLOQUEADO</color>")}");
                if (tieneBronce) totalSublogrosDesbloqueados++;

                bool tienePlata = rango >= AchievementRank.Plata;
                sb.AppendLine($"      - [Plata]:  {(tienePlata ? "<color=green>DESBLOQUEADO</color>" : "<color=red>BLOQUEADO</color>")}");
                if (tienePlata) totalSublogrosDesbloqueados++;

                bool tieneOro = rango >= AchievementRank.Oro;
                sb.AppendLine($"      - [Oro]:    {(tieneOro ? "<color=green>DESBLOQUEADO</color>" : "<color=red>BLOQUEADO</color>")}");
                if (tieneOro) totalSublogrosDesbloqueados++;
            }
            else
            {
                sb.AppendLine($"   • Sublogro único: {(desbloqueado ? "<color=green>DESBLOQUEADO</color>" : "<color=red>BLOQUEADO</color>")}");
                if (desbloqueado) totalSublogrosDesbloqueados++;
            }
        }

        sb.AppendLine("\n--------------------------------------------------");
        sb.AppendLine($" RESUMEN:");
        sb.AppendLine($" Logros completados: {logrosCompletados} / {totalLogros}");
        sb.AppendLine($" Total sublogros/rangos obtenidos: {totalSublogrosDesbloqueados}");
        sb.AppendLine("==================================================");

        Debug.Log(sb.ToString());
    }
}