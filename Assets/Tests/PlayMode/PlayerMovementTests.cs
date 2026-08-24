using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class PlayerMovementTests
{
    private GameObject playerGO;
    private GameObject cameraGO;
    private PlayerMovement player;
    private CharacterController characterController;
    private List<GameObject> extraObjects;
 
    private const float StandingHeight = 2f;
    private const float StandingCenterY = 1f;
    private const float CameraLocalY = 1.6f;
 
    [SetUp]
    public void SetUp()
    {
        extraObjects = new List<GameObject>();
 
        playerGO = new GameObject("Player");
        playerGO.SetActive(false); // para inyectar cameraTransform antes de Awake()
 
        player = playerGO.AddComponent<PlayerMovement>(); // agrega CharacterController vía RequireComponent
        characterController = playerGO.GetComponent<CharacterController>();
        characterController.height = StandingHeight;
        characterController.center = new Vector3(0f, StandingCenterY, 0f);
 
        cameraGO = new GameObject("Camera");
        cameraGO.transform.SetParent(playerGO.transform);
        cameraGO.transform.localPosition = new Vector3(0f, CameraLocalY, 0f);
        SetPrivateField(player, "cameraTransform", cameraGO.transform);
 
        playerGO.transform.position = Vector3.zero;
 
        playerGO.SetActive(true); // dispara Awake() y OnEnable()
    }
 
    [TearDown]
    public void TearDown()
    {
        foreach (var go in extraObjects)
            if (go != null) Object.DestroyImmediate(go);
 
        if (playerGO != null)
            Object.DestroyImmediate(playerGO);
    }
 
    // ---------------------------- Helpers de reflexión ----------------------------
 
    private static object GetPrivateField(object target, string fieldName)
    {
        var field = target.GetType().GetField(fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(field, $"No se encontró el campo '{fieldName}'.");
        return field.GetValue(target);
    }
 
    private static void SetPrivateField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(field, $"No se encontró el campo '{fieldName}'.");
        field.SetValue(target, value);
    }
 
    // Las propiedades auto-implementadas (get; private set;) guardan su valor
    // en un backing field generado por el compilador: "<NombreProp>k__BackingField".
    private static void SetAutoPropertyBackingField(object target, string propertyName, object value)
    {
        SetPrivateField(target, $"<{propertyName}>k__BackingField", value);
    }
 
    private static bool GetControlsPlayerEnabled(PlayerMovement target)
    {
        var controls = GetPrivateField(target, "controls");
        Assert.IsNotNull(controls, "El campo 'controls' no fue inicializado en Awake().");
 
        var playerActionsProp = controls.GetType().GetProperty("Player");
        Assert.IsNotNull(playerActionsProp,
            "No se encontró la propiedad 'Player' generada por el Input System.");
        var playerActions = playerActionsProp.GetValue(controls);
 
        var enabledProp = playerActions.GetType().GetProperty("enabled");
        Assert.IsNotNull(enabledProp,
            "No se encontró la propiedad 'enabled' generada para el mapa 'Player'.");
        return (bool)enabledProp.GetValue(playerActions);
    }
 
    private static IEnumerator WaitFrames(int count)
    {
        for (int i = 0; i < count; i++)
            yield return null;
    }
 
    // ================================ Awake() ================================
 
    [Test]
    public void Awake_InicializaCurrentStamina_AlValorMaximo()
    {
        Assert.AreEqual(player.MaxStamina, player.CurrentStamina,
            "CurrentStamina debería iniciar igual a maxStamina.");
    }
 
    [Test]
    public void Awake_ObtieneElCharacterControllerRequerido()
    {
        Assert.IsNotNull(characterController,
            "RequireComponent(CharacterController) debería garantizar su presencia.");
    }
 
    [Test]
    public void Awake_CalculaPosicionesDeAgachadoYCamara_Correctamente()
    {
        float standingHeight = (float)GetPrivateField(player, "standingHeight");
        Vector3 standingCenter = (Vector3)GetPrivateField(player, "standingCenter");
        Vector3 standingCameraPosition = (Vector3)GetPrivateField(player, "standingCameraPosition");
        Vector3 crouchCenter = (Vector3)GetPrivateField(player, "crouchCenter");
        Vector3 crouchCameraPosition = (Vector3)GetPrivateField(player, "crouchCameraPosition");
 
        Assert.AreEqual(StandingHeight, standingHeight, 0.0001f);
        Assert.AreEqual(new Vector3(0f, StandingCenterY, 0f), standingCenter);
        Assert.AreEqual(new Vector3(0f, CameraLocalY, 0f), standingCameraPosition);
 
        // crouchHeight por defecto en el script = 1f
        Assert.AreEqual(new Vector3(0f, 0.5f, 0f), crouchCenter,
            "crouchCenter debería ser (x/z del standing, crouchHeight/2, x/z del standing).");
        Assert.AreEqual(new Vector3(0f, 0.6f, 0f), crouchCameraPosition,
            "crouchCameraPosition = standingCameraPosition - (0, standingHeight-crouchHeight, 0).");
    }
 
    // ========================= OnEnable() / OnDisable() =========================
 
    [Test]
    public void OnEnable_HabilitaElMapaDeAccionesPlayer()
    {
        Assert.IsTrue(GetControlsPlayerEnabled(player),
            "Tras OnEnable(), el mapa de acciones 'Player' debería estar habilitado.");
    }
 
    [Test]
    public void OnDisable_DeshabilitaElMapaDeAccionesPlayer()
    {
        playerGO.SetActive(false); // dispara OnDisable()
 
        Assert.IsFalse(GetControlsPlayerEnabled(player),
            "Tras OnDisable(), el mapa de acciones 'Player' debería quedar deshabilitado.");
    }
 
    [Test]
    public void AlternarActivoVariasVeces_NoLanzaExcepcion()
    {
        Assert.DoesNotThrow(() =>
        {
            playerGO.SetActive(false);
            playerGO.SetActive(true);
            playerGO.SetActive(false);
            playerGO.SetActive(true);
        });
    }
 
    // ============================== ApplyGravity() ==============================
 
    [UnityTest]
    public IEnumerator ApplyGravity_SinPisoDebajo_AcumulaVelocidadNegativaConElTiempo()
    {
        yield return WaitFrames(1);
        float velocidadTrasUnFrame = (float)GetPrivateField(player, "velocityY");
 
        yield return WaitFrames(10);
        float velocidadTrasVariosFrames = (float)GetPrivateField(player, "velocityY");
 
        Assert.Less(velocidadTrasUnFrame, 0f, "En el aire, la velocidad vertical debería volverse negativa.");
        Assert.Less(velocidadTrasVariosFrames, velocidadTrasUnFrame,
            "La gravedad debería seguir acumulándose cuadro a cuadro mientras esté en el aire.");
        Assert.IsFalse(characterController.isGrounded, "Sin piso debajo, no debería estar 'grounded'.");
    }
 
    [UnityTest]
    public IEnumerator ApplyGravity_AlAterrizarEnElPiso_AplicaGroundedVelocity()
    {
        CrearPiso(alturaSuperficie: -0.01f);
 
        const int maxFrames = 180; // margen amplio para que la caída/física se resuelva
        int frames = 0;
        while (!characterController.isGrounded && frames < maxFrames)
        {
            yield return null;
            frames++;
        }
 
        Assert.IsTrue(characterController.isGrounded,
            $"El personaje debería haber aterrizado en el piso dentro de {maxFrames} frames.");
 
        yield return null; // un frame más ya "grounded" para que se aplique groundedVelocity
 
        float velocityY = (float)GetPrivateField(player, "velocityY");
        Assert.AreEqual(-2f, velocityY, 0.0001f,
            "Estando en el piso con velocidad negativa, debería fijarse a groundedVelocity (-2).");
    }
 
    // ============================== HandleCrouch() ==============================
 
    [UnityTest]
    public IEnumerator HandleCrouch_SiEstabaAgachadoYNoHayObstaculo_SePoneDePie()
    {
        SetAutoPropertyBackingField(player, "IsCrouching", true);
 
        yield return WaitFrames(3);
 
        Assert.IsFalse(player.IsCrouching,
            "Sin obstáculo arriba y sin Crouch presionado, debería volver a pararse.");
    }
 
    [UnityTest]
    public IEnumerator HandleCrouch_SinInputYSinEstarAgachado_PermaneceDePie()
    {
        yield return WaitFrames(3);
 
        Assert.IsFalse(player.IsCrouching,
            "Sin input de Crouch, el personaje no debería agacharse solo.");
    }
 
    // ============================== HandleStamina() ==============================
 
    [UnityTest]
    public IEnumerator HandleStamina_SinSprintPresionado_RegeneraConElTiempo()
    {
        SetAutoPropertyBackingField(player, "CurrentStamina", 1f);
 
        yield return WaitFrames(5);
 
        Assert.Greater(player.CurrentStamina, 1f,
            "Sin sprintear, la estamina debería regenerarse cuadro a cuadro.");
        Assert.IsFalse(player.IsSprinting, "Sin input de Sprint, IsSprinting debería quedar en false.");
    }
 
    [UnityTest]
    public IEnumerator HandleStamina_AlRegenerar_NuncaSuperaElMaximo()
    {
        SetAutoPropertyBackingField(player, "CurrentStamina", player.MaxStamina);
 
        yield return WaitFrames(10);
 
        Assert.AreEqual(player.MaxStamina, player.CurrentStamina, 0.0001f,
            "La estamina no debería superar maxStamina aunque se siga regenerando (Mathf.Clamp).");
    }
 
    [UnityTest]
    public IEnumerator HandleStamina_SeRecuperaDelAgotamiento_AlSuperarElUmbral()
    {
        SetAutoPropertyBackingField(player, "CurrentStamina", 0f);
        SetPrivateField(player, "isExhausted", true);
 
        float umbral = player.MaxStamina * 0.25f; // exhaustedRecoverThreshold por defecto
 
        const int maxFrames = 300;
        int frames = 0;
        while (player.CurrentStamina < umbral && frames < maxFrames)
        {
            yield return null;
            frames++;
        }
 
        Assert.GreaterOrEqual(player.CurrentStamina, umbral,
            $"La estamina debería alcanzar el umbral de recuperación dentro de {maxFrames} frames.");
 
        yield return null; // un frame más para que HandleStamina reevalúe isExhausted
 
        bool isExhausted = (bool)GetPrivateField(player, "isExhausted");
        Assert.IsFalse(isExhausted,
            "Al superar el umbral de recuperación, isExhausted debería volver a false.");
    }
 
    // ------------------------------- Helpers de escena -------------------------------
 
    private void CrearPiso(float alturaSuperficie)
    {
        var piso = new GameObject("Piso");
        var box = piso.AddComponent<BoxCollider>();
        box.size = new Vector3(20f, 1f, 20f);
        piso.transform.position = new Vector3(0f, alturaSuperficie - 0.5f, 0f);
        extraObjects.Add(piso);
    }
 
    private void CrearObstaculo(float centroY, float altoY)
    {
        var obstaculo = new GameObject("Obstaculo");
        var box = obstaculo.AddComponent<BoxCollider>();
        box.size = new Vector3(4f, altoY, 4f);
        obstaculo.transform.position = new Vector3(0f, centroY, 0f);
        extraObjects.Add(obstaculo);
    }
}
