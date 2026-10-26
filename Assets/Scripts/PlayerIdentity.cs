using System;
using UnityEngine;

public static class PlayerIdentity
{
    private const string PREF_KEY_USER_ID = "DEVICE_LOCAL_USER_ID";
    private const string PREF_KEY_USERNAME = "PlayerUsername";

    public const int MaxUsernameLength = 10;

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
    /// Devuelve true únicamente si existe un username real guardado.
    /// Ya no se inventa un nombre por defecto.
    /// </summary>
    public static bool HasUsername()
    {
        if (!PlayerPrefs.HasKey(PREF_KEY_USERNAME))
            return false;

        return IsValidUsername(PlayerPrefs.GetString(PREF_KEY_USERNAME, string.Empty));
    }

    /// <summary>
    /// Username permanente del jugador.
    /// Devuelve string.Empty si todavía no fue elegido.
    /// </summary>
    public static string GetUsername()
    {
        if (!HasUsername())
            return string.Empty;

        return PlayerPrefs.GetString(PREF_KEY_USERNAME, string.Empty).Trim();
    }

    /// <summary>
    /// Se mantiene el nombre del método por compatibilidad.
    /// Por ahora el username es de creación única: si ya existe, NO se modifica.
    /// Cuando se implemente la US de cambio de username conviene agregar un método separado.
    /// </summary>
    public static void SetUsername(string newUsername)
    {
        if (HasUsername())
        {
            Debug.LogWarning(
                "[IDENTITY] Ya existe un nombre de usuario. " +
                "El cambio de username está bloqueado hasta implementar esa funcionalidad."
            );
            return;
        }

        if (!TryCreateUsername(newUsername, out string error))
            Debug.LogWarning("[IDENTITY] No se pudo guardar el username: " + error);
    }

    public static bool TryCreateUsername(string newUsername, out string error)
    {
        error = string.Empty;

        if (HasUsername())
        {
            error = "Ya existe un nombre de usuario guardado.";
            return false;
        }

        if (!IsValidUsername(newUsername, out error))
            return false;

        string cleanUsername = newUsername.Trim();

        PlayerPrefs.SetString(PREF_KEY_USERNAME, cleanUsername);
        PlayerPrefs.Save();

        // Garantiza que el usuario también tenga su ID persistente desde este momento.
        GetUserId();

        Debug.Log($"[IDENTITY] Nombre de usuario creado: {cleanUsername}");
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
