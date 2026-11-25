#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class SinglePlayerAutosaveTests
{
    private const string Menu = "PantallasUI";
    private const string Juego = "MainSceneSinglePlayer";
    private const string AvisoGuardado = "GUARDANDO PARTIDA…";
    private const BindingFlags Privado = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo RutaDePrueba = typeof(SaveSystem).GetField(
        "rutaDePrueba", BindingFlags.Static | BindingFlags.NonPublic);
    private static readonly FieldInfo OmitirLogros = typeof(AchievementsManager).GetField(
        "omitirInicializacionEnPruebas", BindingFlags.Static | BindingFlags.NonPublic);

    private string carpetaTemporal;

    [UnitySetUp]
    public IEnumerator PrepararMenu()
    {
        carpetaTemporal = Path.Combine(Path.GetTempPath(), "DQ-Autoguardado-" + Guid.NewGuid());
        Directory.CreateDirectory(carpetaTemporal);
        RutaDePrueba.SetValue(null, Path.Combine(carpetaTemporal, "SaveData.json"));
        OmitirLogros.SetValue(null, true);
        SceneManager.sceneLoaded += DesactivarServiciosDelMenu;
        yield return SceneManager.LoadSceneAsync(Menu);
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator Limpiar()
    {
        Time.timeScale = 1f;
        LoadGameMenuController.FinalizarCargaDesdeMenu();
        if (SceneManager.GetActiveScene().name == Juego)
            yield return SceneManager.LoadSceneAsync(Menu);

        foreach (var network in Object.FindObjectsByType<NetworkManager>(FindObjectsInactive.Include))
            Object.Destroy(network.gameObject);
        yield return null;
        SceneManager.sceneLoaded -= DesactivarServiciosDelMenu;
        RutaDePrueba.SetValue(null, null);
        OmitirLogros.SetValue(null, false);
        Directory.Delete(carpetaTemporal, true);
    }

    [UnityTest]
    [Timeout(720000)]
    public IEnumerator GuardadoDeAmbosModosSeSobreescribeYCargaDesdeElMenu()
    {
        var checkpoint = CheckpointManager.Instance;
        var segundaZona = CheckpointManager.EscenarioCheckpoint.SafeZone2;
        typeof(CheckpointManager).GetField("escenarioActual", Privado).SetValue(checkpoint, segundaZona);
        Assert.IsTrue(SaveSystem.GuardarHistoria(new HistoriaSaveData { checkpoint = (int)segundaZona }));
        Boton("Cargar_Partida").onClick.Invoke();
        Boton("BotonSupervivencia").onClick.Invoke();
        Assert.IsFalse(Boton("BotonCargar").interactable);
        Object.FindAnyObjectByType<LoadGameMenuController>().Volver();

        yield return EmpezarPartida(ConfiguracionPartidaSeleccionada.ModoJuego.Historia);
        Assert.AreEqual(CheckpointManager.EscenarioCheckpoint.Ninguno, checkpoint.EscenarioActual);
        Assert.AreEqual((int)segundaZona, SaveSystem.CargarHistoria().checkpoint);

        var jugador = NetworkManager.Singleton.LocalClient.PlayerObject;
        var recursos = jugador.GetComponentInChildren<PlayerRecursos>();
        recursos.PilaDeLatasNetwork.Value = 17;
        typeof(GestorHistoriaPartida).GetMethod("TrasladarASafeZone1", Privado)
            .Invoke(GestorHistoriaPartida.Instance, new object[] { jugador.transform });
        Assert.AreEqual(1, SaveSystem.CargarHistoria().checkpoint);
        Assert.AreEqual(17, SaveSystem.CargarHistoria().jugador.pilaDeLatas);
        VerificarAviso();

        recursos.PilaDeLatasNetwork.Value = 29;
        typeof(GestorHistoriaPartida).GetMethod("IniciarTransicionASafeZone2", Privado)
            .Invoke(GestorHistoriaPartida.Instance, null);
        yield return Esperar(() => checkpoint.EscenarioActual == segundaZona,
            "El traslado a la segunda zona debe guardar el checkpoint.");
        Assert.AreEqual((int)segundaZona, SaveSystem.CargarHistoria().checkpoint);

        yield return VolverAlMenu();
        CargarModo("BotonHistoria");
        yield return Esperar(() => !SceneTransition.IsLoading && !LoadGameMenuController.CargandoPartidaDesdeMenu &&
            SceneManager.GetActiveScene().name == Juego, "La Historia guardada debe restaurarse.");
        Assert.AreEqual(segundaZona, checkpoint.EscenarioActual);
        Assert.AreEqual(ConfiguracionPartidaSeleccionada.Escenario.Cabana,
            ConfiguracionPartidaSeleccionada.Instancia.escenarioSeleccionado);
        Assert.AreEqual(29, NetworkManager.Singleton.LocalClient.PlayerObject
            .GetComponentInChildren<PlayerRecursos>().PilaDeLatasNetwork.Value);

        yield return VolverAlMenu();
        yield return EmpezarPartida(ConfiguracionPartidaSeleccionada.ModoJuego.Supervivencia);
        var rondas = RoundManager.Instance;
        recursos = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponentInChildren<PlayerRecursos>();

        IniciarCuenta(rondas, 4);
        Assert.AreEqual(5, rondas.CountdownRemaining);
        Assert.IsNull(SaveSystem.CargarSupervivencia());
        Time.timeScale = 8f;
        yield return Esperar(() => !rondas.IsCountingDown, "Debe terminar la cuenta de la ronda 4.");
        yield return null;
        Time.timeScale = 1f;

        recursos.PilaDeLatasNetwork.Value = 39;
        IniciarCuenta(rondas, 5);
        Assert.AreEqual(5, rondas.CountdownRemaining);
        Assert.AreEqual(5, SaveSystem.CargarSupervivencia().rondaActual);
        Assert.AreEqual(39, SaveSystem.CargarSupervivencia().jugador.pilaDeLatas);
        VerificarAviso();
        Time.timeScale = 8f;
        yield return Esperar(() => !rondas.IsCountingDown, "Debe terminar la cuenta de la ronda 5.");
        Time.timeScale = 1f;

        recursos.PilaDeLatasNetwork.Value = 49;
        IniciarCuenta(rondas, 10);
        Assert.AreEqual(5, rondas.CountdownRemaining);
        Assert.AreEqual(10, SaveSystem.CargarSupervivencia().rondaActual);
        Assert.AreEqual(49, SaveSystem.CargarSupervivencia().jugador.pilaDeLatas);
        Assert.AreEqual((int)segundaZona, SaveSystem.CargarHistoria().checkpoint);
        string contenido = File.ReadAllText(Path.Combine(carpetaTemporal, "SaveData.json"));

        yield return VolverAlMenu();
        CargarModo("BotonSupervivencia");
        yield return Esperar(() => !SceneTransition.IsLoading && !LoadGameMenuController.CargandoPartidaDesdeMenu &&
            SceneManager.GetActiveScene().name == Juego, "Supervivencia debe restaurar la ronda guardada.",
            ConfirmarIntroduccion);
        Assert.AreEqual(10, RoundManager.Instance.CurrentRound);
        Assert.AreEqual(49, NetworkManager.Singleton.LocalClient.PlayerObject
            .GetComponentInChildren<PlayerRecursos>().PilaDeLatasNetwork.Value);
        Assert.AreEqual(contenido, File.ReadAllText(Path.Combine(carpetaTemporal, "SaveData.json")),
            "Cargar la ronda guardada no debe volver a escribir el archivo.");
    }

    private static void DesactivarServiciosDelMenu(Scene escena, LoadSceneMode modo)
    {
        if (escena.name != Menu)
            return;

        foreach (var raiz in escena.GetRootGameObjects())
        {
            foreach (var bootstrap in raiz.GetComponentsInChildren<NetworkBootstrap>(true))
                bootstrap.enabled = false;
            foreach (var rename in raiz.GetComponentsInChildren<CharacterRenameManager>(true))
                rename.enabled = false;
        }
    }

    private static IEnumerator EmpezarPartida(ConfiguracionPartidaSeleccionada.ModoJuego modo)
    {
        var config = ConfiguracionPartidaSeleccionada.Instancia;
        config.modoSeleccionado = modo;
        config.escenarioSeleccionado = ConfiguracionPartidaSeleccionada.Escenario.Laboratorio;
        config.dificultadSeleccionada = ConfiguracionPartidaSeleccionada.Dificultad.Normal;
        Assert.IsTrue(SceneTransition.TryLoad(Juego, showMap: true));
        yield return Esperar(() => !SceneTransition.IsLoading && RoundManager.Instance != null &&
            NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost &&
            NetworkManager.Singleton.LocalClient.PlayerObject != null &&
            Object.FindAnyObjectByType<WeaponSwitcher>() is { IsInitialized: true },
            "El jugador local debe estar listo.");
        ConfirmarIntroduccion();
        yield return null;
        yield return null;
    }

    private static IEnumerator VolverAlMenu()
    {
        Time.timeScale = 1f;
        Assert.IsTrue(SceneTransition.TryLoad(Menu));
        yield return Esperar(() => !SceneTransition.IsLoading && NetworkManager.Singleton == null,
            "El host local anterior debe cerrarse al volver al menú.");
    }

    private static void ConfirmarIntroduccion()
    {
        var intro = Object.FindAnyObjectByType<StoryIntroController>();
        if (intro != null && intro.HistoriaActiva)
            intro.ContinuarHistoria();
    }

    private static void CargarModo(string nombre)
    {
        Boton("Cargar_Partida").onClick.Invoke();
        Boton(nombre).onClick.Invoke();
        Assert.IsTrue(Boton("BotonCargar").interactable);
        Boton("BotonCargar").onClick.Invoke();
        Assert.IsTrue(LoadGameMenuController.CargandoPartidaDesdeMenu);
    }

    private static Button Boton(string nombre) => GameObject.Find(nombre).GetComponent<Button>();

    private static void IniciarCuenta(RoundManager rondas, int ronda)
    {
        typeof(RoundManager).GetField("pendingNextRound", Privado).SetValue(rondas, ronda);
        rondas.ConfirmarSiguienteRonda();
    }

    private static void VerificarAviso()
    {
        foreach (var etiqueta in Object.FindObjectsByType<TMP_Text>())
        {
            if (etiqueta.text != AvisoGuardado)
                continue;
            Assert.IsNull(etiqueta.GetComponentInParent<GraphicRaycaster>());
            return;
        }
        Assert.Fail("El autoguardado debe mostrar su barra de estado.");
    }

    private static IEnumerator Esperar(Func<bool> listo, string mensaje, Action mientrasEspera = null)
    {
        float limite = Time.realtimeSinceStartup + 150f;
        while (!listo() && Time.realtimeSinceStartup < limite)
        {
            mientrasEspera?.Invoke();
            yield return null;
        }
        Assert.IsTrue(listo(), mensaje);
    }
}
#endif
