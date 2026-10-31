using System;
using System.Collections;
using System.Text.RegularExpressions;
using UnityEngine;
using TMPro;

public static class PlayerIdentity
{
    private const string PrefKeyUserId = "DQ_USER_ID";
    private const string PrefKeyUsername = "DQ_USERNAME";

    public const int MaxUsernameLength = 10;
    private static readonly Regex patronAlfanumerico = new Regex("^[a-zA-Z0-9]{1,10}$");

    /// <summary>
    /// Retorna el User ID actual guardado o genera un UUID nuevo si es la primera vez.
    /// </summary>
    public static string GetUserId()
    {
        if (!PlayerPrefs.HasKey(PrefKeyUserId))
        {
            string nuevoId = Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(PrefKeyUserId, nuevoId);
            PlayerPrefs.Save();
            return nuevoId;
        }

        return PlayerPrefs.GetString(PrefKeyUserId);
    }

    /// <summary>
    /// Retorna el nombre de usuario actual o string vacío si no tiene.
    /// </summary>
    public static string GetUsername()
    {
        return PlayerPrefs.GetString(PrefKeyUsername, string.Empty);
    }

    /// <summary>
    /// Indica si el jugador ya configuró un nombre válido previamente.
    /// </summary>
    public static bool HasUsername()
    {
        return !string.IsNullOrWhiteSpace(GetUsername());
    }

    /// <summary>
    /// Valida si un nombre de usuario cumple con los requisitos de formato y longitud.
    /// </summary>
    public static bool IsValidUsername(string username, out string error)
    {
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(username))
        {
            error = "El nombre no puede estar vacío.";
            return false;
        }

        string nombreLimpio = username.Trim();

        if (nombreLimpio.Length > MaxUsernameLength)
        {
            error = $"Máximo {MaxUsernameLength} caracteres.";
            return false;
        }

        if (!patronAlfanumerico.IsMatch(nombreLimpio))
        {
            error = "Solo letras y números (sin espacios).";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Crea el nombre de usuario por primera vez tras validarlo.
    /// </summary>
    public static bool TryCreateUsername(string nuevoNombre, out string error)
    {
        if (!IsValidUsername(nuevoNombre, out error))
        {
            return false;
        }

        PlayerPrefs.SetString(PrefKeyUsername, nuevoNombre.Trim());
        PlayerPrefs.Save();
        return true;
    }

    /// <summary>
    /// Actualiza el nombre de usuario localmente si pasa la validación.
    /// </summary>
    public static bool TryUpdateUsername(string nuevoNombre, out string error)
    {
        if (!IsValidUsername(nuevoNombre, out error))
        {
            return false;
        }

        PlayerPrefs.SetString(PrefKeyUsername, nuevoNombre.Trim());
        PlayerPrefs.Save();
        return true;
    }

    /// <summary>
    /// Pisa el ID local con el ID de una cuenta importada/transferida.
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
        PlayerPrefs.SetString(PrefKeyUserId, idLimpio);
        PlayerPrefs.Save();
        return true;
    }

    /// <summary>
    /// Copia el User ID completo al portapapeles y activa un mensaje temporal en UI.
    /// </summary>
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