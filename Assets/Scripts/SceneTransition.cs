using System;
using System.Collections;
using DeadQuarantine.PvPvE;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class SceneTransition : MonoBehaviour
{
    private static SceneTransition current;

    public static bool IsLoading => current != null;

    public static bool TryLoad(string sceneName, bool showMap = false, Action onFailure = null)
    {
        if (IsLoading)
            return false;

        if (string.IsNullOrWhiteSpace(sceneName) ||
            !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"[Carga de escenas] '{sceneName}' no está disponible en la build.");
            return false;
        }

        var owner = new GameObject("Scene transition");
        DontDestroyOnLoad(owner);
        current = owner.AddComponent<SceneTransition>();
        current.StartCoroutine(current.Load(sceneName, showMap, onFailure));
        return true;
    }

    private IEnumerator Load(string sceneName, bool showMap, Action onFailure)
    {
        string origin = SceneManager.GetActiveScene().name;
        float start = Time.realtimeSinceStartup;
        float longestFrame = 0f;
        bool loaded = false;
        ThreadPriority previousPriority = Application.backgroundLoadingPriority;

        try
        {
            bool mapVisible = showMap && PvPvELoadingScreen.BeginOriginal(sceneName);
            LoadingIndicator.Show(mapVisible ? null : "CARGANDO…", transform);

            // Dejar que Unity dibuje el indicador antes de pedir la carga.
            yield return null;
            Application.backgroundLoadingPriority = ThreadPriority.Low;
            AsyncOperation operation = StartLoad(sceneName);
            if (operation == null)
                yield break;

            while (!operation.isDone)
            {
                longestFrame = Mathf.Max(longestFrame, Time.unscaledDeltaTime);
                if (mapVisible)
                    PvPvELoadingScreen.OriginalProgress(operation.progress);
                yield return null;
            }

            loaded = true;
            if (mapVisible)
                PvPvELoadingScreen.OriginalProgress(1f);

            // Incluye el frame de activación y el arranque de la escena nueva.
            longestFrame = Mathf.Max(longestFrame, Time.unscaledDeltaTime);
            yield return null;
            longestFrame = Mathf.Max(longestFrame, Time.unscaledDeltaTime);
            if (Debug.isDebugBuild && longestFrame >= 0.1f)
            {
                Debug.LogWarning($"[Carga de escenas] {origin} → {sceneName}. " +
                    $"Frame más largo: {longestFrame * 1000f:F0} ms. " +
                    $"Transición: {Time.realtimeSinceStartup - start:F2} s.");
            }
        }
        finally
        {
            Application.backgroundLoadingPriority = previousPriority;
            current = null;
            Destroy(gameObject);
            if (!loaded)
            {
                if (showMap)
                    PvPvELoadingScreen.CancelOriginal();
                onFailure?.Invoke();
            }
        }
    }

    private static AsyncOperation StartLoad(string sceneName)
    {
        try
        {
            return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            return null;
        }
    }
}
