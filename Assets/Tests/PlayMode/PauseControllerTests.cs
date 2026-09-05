using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using TMPro;

public class PauseControllerTests
{
    private GameObject playerGO;
    private PauseController controller;

    private PlayerMovement playerMovement;
    private PlayerLook playerLook;
    private Weapon weapon;
    private PlayerHealth playerHealth;
    private PlayerScore playerScore;
    
    private GameObject popupMenuHome;
    private GameObject popupHistoria;
    private GameObject hud;
    private GameObject botonReiniciar;
    private GameObject popupVictoria;
    private GameObject popupDerrota;
    private GameObject popupValoracion;
    private GameObject panelRonda;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        playerGO = new GameObject("Player");

        playerMovement = playerGO.AddComponent<PlayerMovement>();
        playerHealth = playerGO.AddComponent<PlayerHealth>();
        playerScore = playerGO.AddComponent<PlayerScore>();

        var lookGO = CrearHijo("Look");
        playerLook = lookGO.AddComponent<PlayerLook>();

        var weaponGO = CrearHijo("Weapon");
        weapon = weaponGO.AddComponent<Weapon>();

        popupMenuHome = CrearHijo("PopupMenuHome");
        popupHistoria = CrearHijo("PopupHistoria");
        hud = CrearHijo("HUD");
        botonReiniciar = CrearHijo("BotonReiniciar");
        popupVictoria = CrearHijo("PopupVictoria");
        popupDerrota = CrearHijo("PopupDerrota");
        popupValoracion = CrearHijo("PopupValoracion");
        panelRonda = CrearHijo("PanelRonda");

        controller = playerGO.AddComponent<PauseController>();

        SetField("popupMenuHome", popupMenuHome);
        SetField("popupHistoria", popupHistoria);
        SetField("hud", hud);
        SetField("botonReiniciar", botonReiniciar);
        SetField("popupVictoria", popupVictoria);
        SetField("popupDerrota", popupDerrota);
        SetField("popupValoracion", popupValoracion);
        SetField("panelRonda", panelRonda);
        SetField("playerMovement", playerMovement);
        SetField("playerLook", playerLook);
        SetField("weapon", weapon);
        SetField("playerHealth", playerHealth);

        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (playerGO != null)
            Object.Destroy(playerGO);
        yield return null;
    }

    private GameObject CrearHijo(string nombre)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(playerGO.transform);
        return go;
    }

    // ---------------------------------------------------------------
    // Awake
    // ---------------------------------------------------------------

    [Test]
    public void Awake_OcultaTodosLosPopupsIniciales()
    {
        Invoke("Awake");

        Assert.IsFalse(popupMenuHome.activeSelf);
        Assert.IsFalse(popupHistoria.activeSelf);
        Assert.IsFalse(popupVictoria.activeSelf);
        Assert.IsFalse(popupDerrota.activeSelf);
        Assert.IsFalse(popupValoracion.activeSelf);
        Assert.IsFalse(panelRonda.activeSelf);
    }

    // ---------------------------------------------------------------
    // BloquearJugador / HabilitarJugador / RestaurarEstadoJugador
    // ---------------------------------------------------------------

    [Test]
    public void BloquearJugador_DesactivaMovimientoMiradaYArma()
    {
        playerMovement.enabled = true;
        playerLook.enabled = true;
        weapon.enabled = true;

        Invoke("BloquearJugador");

        Assert.IsFalse(playerMovement.enabled);
        Assert.IsFalse(playerLook.enabled);
        Assert.IsFalse(weapon.enabled);
    }

    [Test]
    public void HabilitarJugador_SiElJugadorEstaVivo_ActivaLosControles()
    {
        // Supone PlayerHealth.IsAlive == true por defecto al instanciarlo.
        playerMovement.enabled = false;
        playerLook.enabled = false;
        weapon.enabled = false;

        Invoke("HabilitarJugador");

        Assert.IsTrue(playerMovement.enabled);
        Assert.IsTrue(playerLook.enabled);
        Assert.IsTrue(weapon.enabled);
    }

    [Test]
    public void RestaurarEstadoJugador_DejaCadaControlComoEstabaAntesDeLaPausa()
    {
        SetField("movementWasEnabled", true);
        SetField("lookWasEnabled", false);
        SetField("weaponWasEnabled", true);

        playerMovement.enabled = false;
        playerLook.enabled = true;
        weapon.enabled = false;

        Invoke("RestaurarEstadoJugador");

        Assert.IsTrue(playerMovement.enabled);
        Assert.IsFalse(playerLook.enabled);
        Assert.IsTrue(weapon.enabled);
    }

    // ---------------------------------------------------------------
    // EsSinglePlayer / ActualizarBotonReiniciar
    // ---------------------------------------------------------------

    [UnityTest]
    public IEnumerator EsSinglePlayer_DevuelveTrueEnMainSceneSinglePlayer()
    {
        var escena = SceneManager.CreateScene("MainSceneSinglePlayer");
        SceneManager.SetActiveScene(escena);
        yield return null;

        bool resultado = (bool)InvokeReturning("EsSinglePlayer");

        Assert.IsTrue(resultado);

        yield return SceneManager.UnloadSceneAsync(escena);
    }

    [UnityTest]
    public IEnumerator EsSinglePlayer_DevuelveFalseEnOtraEscena()
    {
        var escena = SceneManager.CreateScene("NetworkLobby");
        SceneManager.SetActiveScene(escena);
        yield return null;

        bool resultado = (bool)InvokeReturning("EsSinglePlayer");

        Assert.IsFalse(resultado);

        yield return SceneManager.UnloadSceneAsync(escena);
    }

    [UnityTest]
    public IEnumerator ActualizarBotonReiniciar_ActivaElBotonSoloEnSinglePlayer()
    {
        var escena = SceneManager.CreateScene("MainSceneSinglePlayer");
        SceneManager.SetActiveScene(escena);
        yield return null;

        botonReiniciar.SetActive(false);
        Invoke("ActualizarBotonReiniciar");

        Assert.IsTrue(botonReiniciar.activeSelf);

        yield return SceneManager.UnloadSceneAsync(escena);
    }

    [UnityTest]
    public IEnumerator ActualizarBotonReiniciar_DesactivaElBotonFueraDeSinglePlayer()
    {
        var escena = SceneManager.CreateScene("NetworkLobby");
        SceneManager.SetActiveScene(escena);
        yield return null;

        botonReiniciar.SetActive(true);
        Invoke("ActualizarBotonReiniciar");

        Assert.IsFalse(botonReiniciar.activeSelf);

        yield return SceneManager.UnloadSceneAsync(escena);
    }

    // ---------------------------------------------------------------
    // MostrarValoracion / OcultarPanelRonda
    // ---------------------------------------------------------------

    [Test]
    public void MostrarValoracion_MuestraElPopupDeValoracion()
    {
        popupValoracion.SetActive(false);

        controller.MostrarValoracion(); // es público, no hace falta reflexión

        Assert.IsTrue(popupValoracion.activeSelf);
    }

    [Test]
    public void OcultarPanelRonda_OcultaElPanelDeRonda()
    {
        panelRonda.SetActive(true);

        controller.OcultarPanelRonda(); // es público, no hace falta reflexión

        Assert.IsFalse(panelRonda.activeSelf);
    }

    // ---------------------------------------------------------------
    // Helpers de reflexión
    // ---------------------------------------------------------------

    private const BindingFlags Flags =
        BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;

    private void SetField(string name, object value)
    {
        var field = typeof(PauseController).GetField(name, Flags);
        Assert.IsNotNull(field, $"No se encontró el campo '{name}' en PauseController");
        field.SetValue(controller, value);
    }

    private void Invoke(string methodName, params object[] args)
    {
        var method = typeof(PauseController).GetMethod(methodName, Flags);
        Assert.IsNotNull(method, $"No se encontró el método '{methodName}' en PauseController");
        method.Invoke(controller, args);
    }

    private object InvokeReturning(string methodName, params object[] args)
    {
        var method = typeof(PauseController).GetMethod(methodName, Flags);
        Assert.IsNotNull(method, $"No se encontró el método '{methodName}' en PauseController");
        return method.Invoke(controller, args);
    }
}