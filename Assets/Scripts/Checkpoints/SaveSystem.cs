
using System;
using System.IO;
using UnityEngine;

public static class SaveSystem
{
    private const string NombreArchivo = "SaveData.json";
    private const int VersionActual = 2;

#if UNITY_EDITOR
    private static string rutaDePrueba = null;
#endif

    private static string RutaArchivo =>
#if UNITY_EDITOR
        rutaDePrueba ??
#endif
        Path.Combine(
            Application.persistentDataPath,
            NombreArchivo
        );

    // =====================================================
    // GUARDAR HISTORIA
    // =====================================================

    public static bool GuardarHistoria(HistoriaSaveData datos)
    {
        if (datos == null)
        {
            Debug.LogError(
                "[SaveSystem] No se pueden guardar datos de Historia nulos."
            );

            return false;
        }

        SaveData archivo = CargarArchivoInterno();

        if (archivo == null)
            archivo = CrearArchivoNuevo();

        datos.fechaGuardado =
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        archivo.historia = datos;

        return EscribirArchivo(archivo);
    }

    // =====================================================
    // CARGAR HISTORIA
    // =====================================================

    public static HistoriaSaveData CargarHistoria()
    {
        SaveData archivo = CargarArchivoInterno();

        if (archivo == null || archivo.historia == null)
        {
            Debug.Log(
                "[SaveSystem] No existe una partida de Historia guardada."
            );

            return null;
        }

        Debug.Log(
            "[SaveSystem] Partida de Historia cargada. Último guardado: " +
            archivo.historia.fechaGuardado
        );

        return archivo.historia;
    }

    // =====================================================
    // COMPROBAR HISTORIA
    // =====================================================

    public static bool ExisteGuardadoHistoria()
    {
        HistoriaSaveData datos = CargarHistoria();

        return datos != null &&
               datos.checkpoint > 0;
    }

    // =====================================================
    // GUARDAR SUPERVIVENCIA
    // =====================================================

    public static bool GuardarSupervivencia(
        SupervivenciaSaveData datos)
    {
        if (datos == null)
        {
            Debug.LogError(
                "[SaveSystem] No se pueden guardar datos de Supervivencia nulos."
            );

            return false;
        }

        SaveData archivo = CargarArchivoInterno();

        if (archivo == null)
            archivo = CrearArchivoNuevo();

        datos.fechaGuardado =
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        archivo.supervivencia = datos;

        return EscribirArchivo(archivo);
    }

    // =====================================================
    // CARGAR SUPERVIVENCIA
    // =====================================================

    public static SupervivenciaSaveData CargarSupervivencia()
    {
        SaveData archivo = CargarArchivoInterno();

        if (archivo == null || archivo.supervivencia == null)
        {
            Debug.Log(
                "[SaveSystem] No existe una partida de Supervivencia guardada."
            );

            return null;
        }

        Debug.Log(
            "[SaveSystem] Partida de Supervivencia cargada. Último guardado: " +
            archivo.supervivencia.fechaGuardado
        );

        return archivo.supervivencia;
    }

    // =====================================================
    // COMPROBAR SUPERVIVENCIA
    // =====================================================

    public static bool ExisteGuardadoSupervivencia()
    {
        return CargarSupervivencia() != null;
    }

    // =====================================================
    // LEER ARCHIVO COMPLETO
    // =====================================================

    public static SaveData Cargar()
    {
        return CargarArchivoInterno();
    }

    private static SaveData CargarArchivoInterno()
    {
        if (!File.Exists(RutaArchivo))
            return null;

        try
        {
            string json = File.ReadAllText(RutaArchivo);

            SaveData datos =
                JsonUtility.FromJson<SaveData>(json);

            if (datos == null)
            {
                Debug.LogError(
                    "[SaveSystem] El archivo no contiene datos válidos."
                );

                return null;
            }

            if (datos.version != VersionActual)
            {
                Debug.LogWarning(
                    "[SaveSystem] Versión incompatible. " +
                    "Se esperaba " + VersionActual +
                    " y se encontró " + datos.version + "."
                );

                return null;
            }

            // JsonUtility puede convertir un modo nulo en un objeto con valores predeterminados.
            if (datos.historia is { checkpoint: 0 })
                datos.historia = null;
            if (datos.supervivencia is { rondaActual: 0 })
                datos.supervivencia = null;

            return datos;
        }
        catch (Exception excepcion)
        {
            Debug.LogError(
                "[SaveSystem] Error al leer el archivo: " +
                excepcion.Message
            );

            return null;
        }
    }

    // =====================================================
    // CREAR ARCHIVO NUEVO
    // =====================================================

    private static SaveData CrearArchivoNuevo()
    {
        return new SaveData
        {
            version = VersionActual,
            historia = null,
            supervivencia = null
        };
    }

    // =====================================================
    // ESCRIBIR ARCHIVO
    // =====================================================

    private static bool EscribirArchivo(SaveData datos)
    {
        string ruta = RutaArchivo;
        string temporal = ruta + ".tmp";
        try
        {
            datos.version = VersionActual;

            string json =
                JsonUtility.ToJson(datos, true);

            File.WriteAllText(temporal, json);
            if (File.Exists(ruta))
                File.Replace(temporal, ruta, null);
            else
                File.Move(temporal, ruta);

            Debug.Log(
                "[SaveSystem] Guardado actualizado correctamente. " +
                "Ruta: " + ruta
            );

            return true;
        }
        catch (Exception excepcion)
        {
            Debug.LogError(
                "[SaveSystem] Error al escribir el archivo: " +
                excepcion.Message
            );

            try { File.Delete(temporal); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }

            return false;
        }
    }

    // =====================================================
    // COMPROBAR SI EXISTE EL ARCHIVO
    // =====================================================

    public static bool ExisteGuardado()
    {
        return File.Exists(RutaArchivo);
    }

    // =====================================================
    // ELIMINAR TODO EL ARCHIVO
    // =====================================================

    public static bool EliminarGuardado()
    {
        try
        {
            if (!File.Exists(RutaArchivo))
            {
                Debug.Log(
                    "[SaveSystem] No había ningún guardado para eliminar."
                );

                return true;
            }

            File.Delete(RutaArchivo);

            Debug.Log(
                "[SaveSystem] Archivo de guardado eliminado correctamente."
            );

            return true;
        }
        catch (Exception excepcion)
        {
            Debug.LogError(
                "[SaveSystem] Error al eliminar el guardado: " +
                excepcion.Message
            );

            return false;
        }
    }

    // =====================================================
    // ELIMINAR SOLO HISTORIA
    // =====================================================

    public static bool EliminarGuardadoHistoria()
    {
        SaveData archivo = CargarArchivoInterno();

        if (archivo == null)
            return true;

        archivo.historia = null;

        if (archivo.supervivencia == null)
            return EliminarGuardado();

        return EscribirArchivo(archivo);
    }

    // =====================================================
    // ELIMINAR SOLO SUPERVIVENCIA
    // =====================================================

    public static bool EliminarGuardadoSupervivencia()
    {
        SaveData archivo = CargarArchivoInterno();

        if (archivo == null)
            return true;

        archivo.supervivencia = null;

        if (archivo.historia == null)
            return EliminarGuardado();

        return EscribirArchivo(archivo);
    }
}
