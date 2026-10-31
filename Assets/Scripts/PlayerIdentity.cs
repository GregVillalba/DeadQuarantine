using System;
using System.Collections;
using UnityEngine;
using TMPro;

public static class PlayerIdentity
{
    private const string PREF_KEY_USER_ID = "DEVICE_LOCAL_USER_ID";
    private const string PREF_KEY_USERNAME = "PlayerUsername";

    public const int MaxUsernameLength = 10;

    private static Coroutine corrutinaFeedbackActiva;

    public static string GetUserId()
    {
        string currentId = PlayerPrefs.GetString(PREF_KEY_USER_ID, string.Empty);

        if (string.IsNullOrEmpty(currentId))
        {
            currentId = Guid.NewGuid().ToString();
            PlayerPrefs.SetString(PREF_KEY_USER_ID, currentId);
            PlayerPrefs.Save();

            Debug.Log($"[IDENTITY] Nuevo ID único generado: {currentId}");
        }

        return currentId;
    }

    /// <summary>
    /// Copia el ID al portapapeles y activa el mensaje "¡Copiado exitosamente!" en el label recibido.
    /// </summary>
    /// <param name="runner">Cualquier script activo (ej: this o CharacterRenameManager.Instance) para ejecutar la corrutina.</param>
    /// <param name="labelFeedback">TMP_Text donde se mostrará el mensaje.</param>
    /// <param name="duracionSegundos">Tiempo que permanecerá visible el mensaje (por defecto 2.5s).</param>
    public static void CopiarIdConFeedback(MonoBehaviour runner, TMP_Text labelFeedback, float duracionSegundos = 2.5f)
    {
        // 1. Copiar ID al portapapeles del sistema operativo
        string id = GetUserId();
        GUIUtility.systemCopyBuffer = id;
        Debug.Log($"[IDENTITY] ID copiado al portapapeles: {id}");

        // 2. Gestionar el feedback en el label
        if (labelFeedback == null) return;

        if (runner != null && runner.isActiveAndEnabled)
        {
            if (corrutinaFeedbackActiva != null)
                runner.StopCoroutine(corrutinaFeedbackActiva);

            corrutinaFeedbackActiva = runner.StartCoroutine(RutinaFeedbackCopia(labelFeedback, duracionSegundos));
        }
        else
        {
            labelFeedback.text = "¡Copiado exitosamente!";
            labelFeedback.gameObject.SetActive(true);
        }
    }

    private static IEnumerator RutinaFeedbackCopia(TMP_Text label, float segundos)
    {
        label.text = "¡Copiado exitosamente!";
        label.gameObject.SetActive(true);

        yield return new WaitForSeconds(segundos);

        label.text = string.Empty;
        corrutinaFeedbackActiva = null;
    }

    /// <summary>
    /// Copia únicamente el ID al portapapeles sin modificar UI.
    /// </summary>
    public static void CopiarIdAlPortapapeles()
    {
        GUIUtility.systemCopyBuffer = GetUserId();
    }

    /// <summary>
    /// Permite transferir o restaurar datos asignando el ID de otro dispositivo.
    /// </summary>
    public static bool TryImportarUserId(string nuevoId, out string error)
    {
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(nuevoId))
        {
            error = "El ID no puede estar vacío.";
            return false;
        }

        string idLimpio = nuevoId.Trim();

        if (idLimpio.Length < 8)
        {
            error = "El formato del ID es inválido.";
            return false;
        }

        PlayerPrefs.SetString(PREF_KEY_USER_ID, idLimpio);
        PlayerPrefs.Save();

        Debug.Log($"[IDENTITY] ID importado con éxito: {idLimpio}");
        return true;
    }

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

    public static bool TryUpdateUsername(string newUsername, out string error)
    {
        error = string.Empty;

        if (!IsValidUsername(newUsername, out error))
            return false;

        string cleanUsername = newUsername.Trim();

        PlayerPrefs.SetString(PREF_KEY_USERNAME, cleanUsername);
        PlayerPrefs.Save();

        GetUserId();

        Debug.Log($"[IDENTITY] Nombre de usuario actualizado: {cleanUsername}");
        return true;
    }

    public static void SetUsername(string newUsername)
    {
        TryUpdateUsername(newUsername, out string error);
    }

    public static bool TryCreateUsername(string newUsername, out string error)
    {
        return TryUpdateUsername(newUsername, out error);
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

        username = username.Trim();

        if (username.Length > MaxUsernameLength)
        {
            error = $"Máximo {MaxUsernameLength} caracteres.";
            return false;
        }

        for (int i = 0; i < username.Length; i++)
        {
            char c = username[i];

            if (!char.IsLetterOrDigit(c))
            {
                error = "Usá solamente letras y números, sin espacios.";
                return false;
            }
        }

        return true;
    }
}