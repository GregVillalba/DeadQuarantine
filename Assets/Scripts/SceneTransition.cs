using System;
using System.Collections;
using DeadQuarantine.PvPvE;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class SceneTransition : MonoBehaviour
{
    private const string MenuScene = "PantallasUI";
    private static SceneTransition current;
    private LoadingIndicator loadingView;
    private bool returnToMenu;
    private bool allowExit;
    private Action onExit;

    public static bool IsLoading => current != null;

    public static bool TryLoad(string sceneName, bool showMap = false, Action onFailure = null, Action onExit = null)
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
        current.onExit = onExit;
        current.allowExit = showMap && Application.CanStreamedLevelBeLoaded(MenuScene);
        current.StartCoroutine(current.Load(sceneName, showMap, onFailure));
        return true;
    }

    private IEnumerator Load(string sceneName, bool showMap, Action onFailure)
    {
        Scene origin = SceneManager.GetActiveScene();
        float start = Time.realtimeSinceStartup;
        float longestFrame = 0f;
        bool loaded = false;
        bool mapVisible = false;
        CursorLockMode previousCursorLock = Cursor.lockState;
        bool wasCursorVisible = Cursor.visible;
        ThreadPriority previousPriority = Application.backgroundLoadingPriority;

        try
        {
            mapVisible = showMap && PvPvELoadingScreen.BeginOriginal(sceneName, localTransition: true);
            loadingView = LoadingIndicator.Show(mapVisible ? null : "CARGANDO…", transform,
                allowExit ? RequestReturnToMenu : null);

            // Dejar que Unity dibuje el indicador antes de pedir la carga.
            yield return null;
            Application.backgroundLoadingPriority = ThreadPriority.Low;
            AsyncOperation operation = returnToMenu ? null : StartLoad(sceneName);
            if (operation == null && !returnToMenu)
                yield break;

            while (operation != null && !operation.isDone)
            {
                longestFrame = Mathf.Max(longestFrame, Time.unscaledDeltaTime);
                if (allowExit)
                    LoadingIndicator.MaintainInput(transform);
                if (mapVisible && !returnToMenu)
                    PvPvELoadingScreen.OriginalProgress(operation.progress);
                yield return null;
            }

            if (mapVisible && !returnToMenu)
                PvPvELoadingScreen.OriginalProgress(1f);

            // Incluye el frame de activación y el arranque de la escena nueva.
            longestFrame = Mathf.Max(longestFrame, Time.unscaledDeltaTime);
            yield return null;
            if (returnToMenu)
            {
                // La carga de Unity sigue hasta terminar. Mantener la pantalla encima
                // del juego mientras se abre el menú.
                AsyncOperation menu = StartLoad(MenuScene);
                if (menu == null)
                    yield break;
                while (!menu.isDone)
                {
                    LoadingIndicator.MaintainInput(transform);
                    yield return null;
                }
            }

            loaded = true;
            longestFrame = Mathf.Max(longestFrame, Time.unscaledDeltaTime);
            if (Debug.isDebugBuild && longestFrame >= 0.1f)
            {
                Debug.LogWarning($"[Carga de escenas] {origin.name} → {(returnToMenu ? MenuScene : sceneName)}. " +
                    $"Frame más largo: {longestFrame * 1000f:F0} ms. " +
                    $"Transición: {Time.realtimeSinceStartup - start:F2} s.");
            }
        }
        finally
        {
            Application.backgroundLoadingPriority = previousPriority;
            current = null;
            if (loadingView)
                loadingView.Dismiss();
            if (mapVisible)
                PvPvELoadingScreen.CancelOriginal();
            if (!loaded && allowExit && origin == SceneManager.GetActiveScene())
            {
                Cursor.lockState = previousCursorLock;
                Cursor.visible = wasCursorVisible;
            }
            Destroy(gameObject);
            if (!loaded)
                onFailure?.Invoke();
        }
    }

    private void RequestReturnToMenu()
    {
        if (returnToMenu)
            return;

        returnToMenu = true;
        SceneLoader.pantallaSolicitada = SceneLoader.PantallaApertura.Ninguna;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        loadingView.ShowReturningToMenu();
        PvPvELoadingScreen.OriginalReturningToMenu();
        onExit?.Invoke();
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
