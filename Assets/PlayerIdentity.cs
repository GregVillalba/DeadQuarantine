using System;
using System.Collections;
using System.Text.RegularExpressions;
using UnityEngine;
using TMPro;

public static class PlayerIdentity
{
    // Claves estándar compatibles con los controladores de UI y Achievements
    private const string PREF_KEY_USER_ID = "DQ_USER_ID";
    private const string PREF_KEY_USERNAME = "DQ_USERNAME";

    // Claves persistentes para autenticación de Google
    private const string PREF_KEY_GOOGLE_LINKED = "DQ_GOOGLE_LINKED";
    private const string PREF_KEY_GOOGLE_ID = "DQ_GOOGLE_ID";
    private const string PREF_KEY_GOOGLE_EMAIL = "DQ_GOOGLE_EMAIL";

    public const int MaxUsernameLength = 10;
    private static readonly Regex patronAlfanumerico = new Regex("^[a-zA-Z0-9]{1,10}$");

    // ============================================================
    // GESTIÓN DE ID ÚNICO DEL JUGADOR
    // ============================================================

    /// <summary>
    /// Retorna el User ID persistente o genera un UUID nuevo si es la primera vez.
    /// </summary>
    public static string GetUserId()
    {
        string currentId = PlayerPrefs.GetString(PREF_KEY_USER_ID, string.Empty);

        if (string.IsNullOrEmpty(currentId))
        {
            currentId = Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(PREF_KEY_USER_ID, currentId);
            PlayerPrefs.Save();
            Debug.Log($"[IDENTITY] Nuevo ID único generado: {currentId}");
        }

        return currentId;
    }

    /// <summary>
    /// Pisa el ID local con el ID de una cuenta importada o transferida.
    /// </summary>
    public static bool TryImportarUserId(string idImportado, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(idImportado) || idImportado.Trim().Length < 8)
        {
            error = "El ID es demasiado corto o inválido.";
            return false;
        }

        string idLimpio = idImportado.Trim();
        PlayerPrefs.SetString(PREF_KEY_USER_ID, idLimpio);
        PlayerPrefs.Save();
        Debug.Log($"[IDENTITY] ID importado con éxito: {idLimpio}");
        return true;
    }

    // ============================================================
    // GESTIÓN DE USERNAME
    // ============================================================

    public static bool HasUsername()
    {
        if (!PlayerPrefs.HasKey(PREF_KEY_USERNAME))
            return false;

        return IsValidUsername(PlayerPrefs.GetString(PREF_KEY_USERNAME, string.Empty));
    }

    public static string GetUsername()
    {
        if (!HasUsername())
            return string.Empty;

        return PlayerPrefs.GetString(PREF_KEY_USERNAME, string.Empty).Trim();
    }

    public static void SetUsername(string newUsername)
    {
        if (!TryUpdateUsername(newUsername, out string error))
        {
            Debug.LogWarning("[IDENTITY] No se pudo guardar el username: " + error);
        }
    }

    public static bool TryCreateUsername(string newUsername, out string error)
    {
        if (!IsValidUsername(newUsername, out error))
            return false;

        string cleanUsername = newUsername.Trim();
        PlayerPrefs.SetString(PREF_KEY_USERNAME, cleanUsername);
        PlayerPrefs.Save();

        GetUserId(); // Asegura la existencia del ID

        Debug.Log($"[IDENTITY] Nombre de usuario creado: {cleanUsername}");
        return true;
    }

    public static bool TryUpdateUsername(string newUsername, out string error)
    {
        if (!IsValidUsername(newUsername, out error))
            return false;

        string cleanUsername = newUsername.Trim();
        PlayerPrefs.SetString(PREF_KEY_USERNAME, cleanUsername);
        PlayerPrefs.Save();

        Debug.Log($"[IDENTITY] Nombre de usuario actualizado: {cleanUsername}");
        return true;
    }

    public static bool IsValidUsername(string username)
    {
        return IsValidUsername(username, out _);
    }

    public static bool IsValidUsername(string username, out string error)
    {
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(username))
        {
            error = "El nombre no puede estar vacío.";
            return false;
        }

        string limpio = username.Trim();

        if (limpio.Length > MaxUsernameLength)
        {
            error = $"Máximo {MaxUsernameLength} caracteres.";
            return false;
        }

        if (!patronAlfanumerico.IsMatch(limpio))
        {
            error = "Usá solo letras y números, sin espacios.";
            return false;
        }

        return true;
    }

    // ============================================================
    // INTEGRACIÓN GOOGLE AUTH PERSISTENTE
    // ============================================================

    /// <summary>
    /// Devuelve true si la cuenta ya inició sesión con Google en este equipo.
    /// </summary>
    public static bool IsGoogleLinked()
    {
        return PlayerPrefs.GetInt(PREF_KEY_GOOGLE_LINKED, 0) == 1;
    }

    /// <summary>
    /// Guarda la vinculación con Google de manera permanente en disco.
    /// </summary>
    public static void SetGoogleSession(string googleId, string email)
    {
        PlayerPrefs.SetInt(PREF_KEY_GOOGLE_LINKED, 1);
        PlayerPrefs.SetString(PREF_KEY_GOOGLE_ID, googleId ?? string.Empty);
        PlayerPrefs.SetString(PREF_KEY_GOOGLE_EMAIL, email ?? string.Empty);

        // Si se vincula por Google, sincronizamos el User ID con su Google ID
        if (!string.IsNullOrEmpty(googleId))
        {
            PlayerPrefs.SetString(PREF_KEY_USER_ID, googleId);
        }

        PlayerPrefs.Save();
        Debug.Log($"[IDENTITY] Sesión de Google guardada en disco: {email}");
    }

    public static string GetGoogleEmail()
    {
        return PlayerPrefs.GetString(PREF_KEY_GOOGLE_EMAIL, string.Empty);
    }

    public static string GetGoogleId()
    {
        return PlayerPrefs.GetString(PREF_KEY_GOOGLE_ID, string.Empty);
    }

    // ============================================================
    // UTILIDADES DE UI (FEEDBACK Y PORTAPAPELES)
    // ============================================================

    public static void CopiarIdConFeedback(MonoBehaviour runner, TMP_Text txtFeedback, float duracionSegundos = 2.0f)
    {
        string id = GetUserId();
        GUIUtility.systemCopyBuffer = id;
        Debug.Log($"<color=cyan>[ID COPIADO]</color> {id}");

        if (runner != null && txtFeedback != null)
        {
            runner.StartCoroutine(RutinaFeedback(txtFeedback, "¡ID copiado exitosamente!", duracionSegundos));
        }
    }

    private static IEnumerator RutinaFeedback(TMP_Text label, string mensaje, float duracion)
    {
        label.text = mensaje;
        label.gameObject.SetActive(true);

        yield return new WaitForSeconds(duracion);

        label.text = string.Empty;
    }
}