using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;

public class LogrosGlobalesOrdenados : MonoBehaviour
{
    [Header("Configuraci�n de API")]
    public string urlApi = "https://dead-quarentine-web.wasmer.app/api/logros";

    [System.Serializable]
    public class TarjetaLogroItem
    {

        [Tooltip("El ID exacto del logro en la base de datos (1, 2, 3...)")]
        public int idLogro;
        [Tooltip("El componente TextMeshPro que muestra el porcentaje (ej: GLOBAL 87.5%)")]
        public TextMeshProUGUI textoPorcentaje;
    }

    [Header("Lista de Logros en Orden")]
    [Tooltip("Define aqu� los logros en el orden que quieras")]
    public List<TarjetaLogroItem> listaTarjetas = new List<TarjetaLogroItem>();

    // DTOs para deserializar la respuesta de Next.js
    [Serializable]
    private class RespuestaApi
    {
        public int total_usuarios;
        public List<LogroDTO> logros;
    }

    [Serializable]
    private class LogroDTO
    {
        public int id;
        public string clave;
        public string titulo;
        public float porcentaje_global;
    }

    private void Start()
    {
        CargarPorcentajes();
    }

    public void CargarPorcentajes()
    {
        StartCoroutine(ObtenerDatosApi());
    }

    private DatosHistoria ObtenerDatosHistoria(
    ConfiguracionPartidaSeleccionada.ModoJuego modo,
    ConfiguracionPartidaSeleccionada.Escenario esc)
{
    // ------------------------- MODO HISTORIA -------------------------
    if (modo == ConfiguracionPartidaSeleccionada.ModoJuego.Historia)
    {
        if (esc == ConfiguracionPartidaSeleccionada.Escenario.Laboratorio)
        {
            return new DatosHistoria(
                "PUNTO DE PARTIDA",
                "HISTORIA: ¿Cómo empezó todo?",
                "Nos encontramos en el complejo donde ocurrió la brecha biológica: un patógeno experimental que muta violentamente a cualquiera que entre en contacto con él. Intentamos avanzar hacia la salida sin llamar la atención, pero el sistema de contención selló las instalaciones. Tu misión consiste en localizar la tarjeta de acceso principal para desbloquear la salida.\n\nOBJETIVO: Encontrar la tarjeta magnética y escapar del laboratorio."
            );
        }
        if (esc == ConfiguracionPartidaSeleccionada.Escenario.Ciudad)
        {
            return new DatosHistoria(
                "CHECKPOINT",
                "HISTORIA: La superficie",
                "Tras lograr salir del laboratorio, el escenario exterior es desolador: las calles están tomadas y el perímetro urbano colapsó por completo. La única ruta de escape factible es aérea. Debemos cruzar el distrito hasta la torre de comunicaciones para emitir una señal de auxilio militar y coordinar la extracción con otros supervivientes.\n\nOBJETIVO: Alcanzar la antena de radio y solicitar apoyo de evacuación."
            );
        }
        if (esc == ConfiguracionPartidaSeleccionada.Escenario.Cabana)
        {
            return new DatosHistoria(
                "ENCLAVE FINAL",
                "HISTORIA: Punto de reunión",
                "El convoy de evacuación se dispersó en los bosques periféricos. Mientras nos reagrupamos en una cabaña táctica, el mando informa de una amenaza crítica: un espécimen mutante alfa capaz de coordinar los ataques de la horda. Para asegurar el aterrizaje del rescate y frenar la propagación, el mutante debe ser neutralizado a toda costa.\n\nOBJETIVO: Proteger el perímetro y eliminar al infectado mutante."
            );
        }
    }

    // ------------------------- MODO RONDA 5 -------------------------
    if (modo == ConfiguracionPartidaSeleccionada.ModoJuego.Ronda5)
    {
        if (esc == ConfiguracionPartidaSeleccionada.Escenario.Laboratorio)
        {
            return new DatosHistoria(
                "",
                "OPERACIÓN: Falla de contención",
                "El laboratorio ha sido tomado por oleadas de sujetos infectados. Las lecturas confirman la presencia de un espécimen alfa mutado liderando las acometidas. Asegura la zona, gestiona tus municiones y resiste el embate de cinco asaltos antes de intentar cualquier repliegue.\n\nOBJETIVO: Sobrevivir a 5 oleadas y abatir al líder mutante."
            );
        }
        if (esc == ConfiguracionPartidaSeleccionada.Escenario.Ciudad)
        {
            return new DatosHistoria(
                "",
                "OPERACIÓN: Asalto urbano",
                "Las barricadas de la ciudad no resistirán por mucho tiempo. Múltiples grupos de infectados avanzan por las avenidas coordinados por una variante mutada de gran resistencia. Debemos mantener la posición y limpiar la zona durante las próximas oleadas hostiles.\n\nOBJETIVO: Sobrevivir a 5 oleadas y eliminar la amenaza mutante."
            );
        }
        if (esc == ConfiguracionPartidaSeleccionada.Escenario.Cabana)
        {
            return new DatosHistoria(
                "",
                "OPERACIÓN: Aislamiento",
                "El refugio en el bosque quedó rodeado por las hordas que emergieron de la espesura. Un enorme mutante está dirigiendo los ataques contra la estructura. Mantén la defensa del perímetro y contiene los cinco asaltos consecutivos para asegurar el sector.\n\nOBJETIVO: Sobrevivir a 5 oleadas y erradicar al mutante alfa."
            );
        }
    }

    // ------------------------- MODO SUPERVIVENCIA -------------------------
    if (modo == ConfiguracionPartidaSeleccionada.ModoJuego.Supervivencia)
    {
        if (esc == ConfiguracionPartidaSeleccionada.Escenario.Laboratorio)
        {
            return new DatosHistoria(
                "",
                "SUPERVIVENCIA: Zona cero",
                "No hay vías de escape ni refuerzos en camino dentro del laboratorio subterráneo. El aire está cargado y las hordas continúan llegando sin descanso. La única directiva es aguantar en pie todo el tiempo que sea posible ante oleadas incesantes y especímenes mutantes.\n\nOBJETIVO: Resistir tantas oleadas de infectados como puedas."
            );
        }
        if (esc == ConfiguracionPartidaSeleccionada.Escenario.Ciudad)
        {
            return new DatosHistoria(
                "",
                "SUPERVIVENCIA: Ruinas urbanas",
                "La metrópoli cayó por completo y las oleadas de infectados convergen hacia tu posición. En este punto la evacuación es imposible; solo queda resistir, administrar los recursos y contener los constantes ataques de la horda y sus mutaciones.\n\nOBJETIVO: Sobrevivir el mayor número de rondas posible."
            );
        }
        if (esc == ConfiguracionPartidaSeleccionada.Escenario.Cabana)
        {
            return new DatosHistoria(
                "",
                "SUPERVIVENCIA: Último bastión",
                "La cabaña en lo profundo del bosque está completamente asediada. La noche avanza y los infectados emergen de la oscuridad en números incontables, comandados por mutantes que no darán tregua. Lucha hasta el último cartucho.\n\nOBJETIVO: Aguantar y resistir oleadas continuas sin límite."
            );
        }
    }

    // ------------------------- VALOR POR DEFECTO -------------------------
    return new DatosHistoria(
        "",
        "COMBATE TÁCTICO",
        "El área de despliegue ha sido comprometida por fuerzas hostiles no identificadas. Mantén la posición, neutraliza las amenazas en el sector y procura no caer.\n\nOBJETIVO: Sobrevivir a las oleadas enemigas."
    );
}
    private IEnumerator ObtenerDatosApi()
    {
        using (UnityWebRequest request = UnityWebRequest.Get(urlApi))
        {
            request.timeout = 8;
            yield return request.SendWebRequest();

            // Si hay error de red o de servidor, mostramos error en todas las tarjetas
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[Logros] Error al contactar la API: {request.error}");
                PonerErrorEnTodas("GLOBAL --%");
                yield break;
            }

            string json = request.downloadHandler.text;
            RespuestaApi data = null;

            try
            {
                data = JsonUtility.FromJson<RespuestaApi>(json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Logros] Error parseando respuesta: {ex.Message}");
                PonerErrorEnTodas("GLOBAL --%");
                yield break;
            }

            if (data == null || data.logros == null || data.logros.Count == 0)
            {
                PonerErrorEnTodas("GLOBAL --%");
                yield break;
            }

            // Armamos un diccionario con los resultados de la API indexados por su ID de BD
            Dictionary<int, float> porcentajePorId = new Dictionary<int, float>();
            foreach (var l in data.logros)
            {
                porcentajePorId[l.id] = l.porcentaje_global;
            }

            // Asignamos a cada tarjeta de tu lista seg�n su ID asignado en el Inspector
            foreach (var item in listaTarjetas)
            {
                if (item.textoPorcentaje == null) continue;

                if (porcentajePorId.TryGetValue(item.idLogro, out float pct))
                {
                    item.textoPorcentaje.text = $"GLOBAL {pct:0.#}%";
                }
                else
                {
                    item.textoPorcentaje.text = "GLOBAL --%";
                }
            }
        }
    }

    private void PonerErrorEnTodas(string textoFallo)
    {
        foreach (var item in listaTarjetas)
        {
            if (item.textoPorcentaje != null)
            {
                item.textoPorcentaje.text = textoFallo;
            }
        }
    }
}