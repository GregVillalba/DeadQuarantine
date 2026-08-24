using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

public class WeaponTests
{
    private GameObject weaponGO;
    private Weapon weapon;
 
    private Camera playerCamera;
    private CharacterController externalCharacterController;
    private PlayerMovement playerMovement;
 
    private List<GameObject> extraObjects;
 
    private const float FovInicial = 60f;
 
    [SetUp]
    public void SetUp()
    {
        extraObjects = new List<GameObject>();
 
        // --- Cámara del jugador ---
        var cameraGO = new GameObject("PlayerCamera");
        playerCamera = cameraGO.AddComponent<Camera>();
        playerCamera.fieldOfView = FovInicial;
        extraObjects.Add(cameraGO);
 
        // --- CharacterController externo (simula el del jugador real) ---
        var ccGO = new GameObject("PlayerCC");
        externalCharacterController = ccGO.AddComponent<CharacterController>();
        externalCharacterController.height = 2f;
        externalCharacterController.center = new Vector3(0f, 1f, 0f);
        extraObjects.Add(ccGO);
 
        // --- PlayerMovement mínimo y válido (dependencia dura de Weapon) ---
        var pmGO = new GameObject("PlayerMovementDep");
        pmGO.SetActive(false);
        playerMovement = pmGO.AddComponent<PlayerMovement>(); // agrega su propio CharacterController
        var pmCameraGO = new GameObject("PMCamera");
        pmCameraGO.transform.SetParent(pmGO.transform);
        SetPrivateField(playerMovement, "cameraTransform", pmCameraGO.transform);
        pmGO.SetActive(true);
        extraObjects.Add(pmGO);
 
        // --- Weapon ---
        weaponGO = new GameObject("Weapon");
        weaponGO.SetActive(false); // para inyectar dependencias antes de Awake()
        weapon = weaponGO.AddComponent<Weapon>();
 
        var firePointGO = new GameObject("FirePoint");
        firePointGO.transform.SetParent(weaponGO.transform);
 
        SetPrivateField(weapon, "firePoint", firePointGO.transform);
        SetPrivateField(weapon, "playerCamera", playerCamera);
        SetPrivateField(weapon, "characterController", externalCharacterController);
        SetPrivateField(weapon, "playerMovement", playerMovement);
 
        weaponGO.SetActive(true); // dispara Awake() y OnEnable()
    }
 
    [TearDown]
    public void TearDown()
    {
        foreach (var go in extraObjects)
            if (go != null) Object.DestroyImmediate(go);
 
        if (weaponGO != null)
            Object.DestroyImmediate(weaponGO);
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
 
    private static void SetAutoPropertyBackingField(object target, string propertyName, object value)
    {
        SetPrivateField(target, $"<{propertyName}>k__BackingField", value);
    }
 
    private static object InvokePrivateMethod(object target, string methodName, params object[] args)
    {
        var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method, $"No se encontró el método '{methodName}'.");
        return method.Invoke(target, args);
    }
 
    private static bool GetControlsPlayerEnabled(Weapon target)
    {
        var controls = GetPrivateField(target, "controls");
        Assert.IsNotNull(controls, "El campo 'controls' no fue inicializado en Awake().");
 
        var playerActionsProp = controls.GetType().GetProperty("Player");
        Assert.IsNotNull(playerActionsProp, "No se encontró la propiedad 'Player' del Input System.");
        var playerActions = playerActionsProp.GetValue(controls);
 
        var enabledProp = playerActions.GetType().GetProperty("enabled");
        Assert.IsNotNull(enabledProp, "No se encontró la propiedad 'enabled' del mapa 'Player'.");
        return (bool)enabledProp.GetValue(playerActions);
    }
 
    // -------------------------- Helpers de escena / movimiento --------------------------
 
    private void CrearPisoBajo(CharacterController cc, float margen = 0.01f)
    {
        float bottomY = cc.transform.position.y + cc.center.y - cc.height / 2f;
        var piso = new GameObject("Piso");
        var box = piso.AddComponent<BoxCollider>();
        box.size = new Vector3(30f, 1f, 30f);
        piso.transform.position = new Vector3(cc.transform.position.x, bottomY - margen - 0.5f, cc.transform.position.z);
        extraObjects.Add(piso);
    }
 
    private IEnumerator AterrizarEn(CharacterController cc, int maxFrames = 180)
    {
        int frames = 0;
        while (!cc.isGrounded && frames < maxFrames)
        {
            cc.Move(new Vector3(0f, -0.05f, 0f));
            yield return null;
            frames++;
        }
        Assert.IsTrue(cc.isGrounded, $"El CharacterController debería haber aterrizado en {maxFrames} frames.");
    }
 
    private IEnumerator MoverDurante(CharacterController cc, Vector3 direccionPorSegundo, float segundos)
    {
        float elapsed = 0f;
        while (elapsed < segundos)
        {
            cc.Move(direccionPorSegundo * Time.deltaTime);
            yield return null;
            elapsed += Time.deltaTime;
        }
    }
 
    // ================================ Awake() ================================
 
    [Test]
    public void Awake_InicializaLaMunicionActual_AlMaximo()
    {
        Assert.AreEqual(weapon.MaxAmmo, weapon.CurrentAmmo);
    }
 
    [Test]
    public void Awake_GuardaLaPosicionLocalInicial_ComoHipPosition()
    {
        Vector3 hipPosition = (Vector3)GetPrivateField(weapon, "hipPosition");
        Vector3 smoothedBasePosition = (Vector3)GetPrivateField(weapon, "smoothedBasePosition");
 
        Assert.AreEqual(weaponGO.transform.localPosition, hipPosition);
        Assert.AreEqual(hipPosition, smoothedBasePosition);
    }
 
    [Test]
    public void Awake_GuardaElFOVInicialDeLaCamara_ComoDefaultWorldFOV()
    {
        float defaultWorldFOV = (float)GetPrivateField(weapon, "defaultWorldFOV");
        Assert.AreEqual(FovInicial, defaultWorldFOV, 0.0001f);
    }
 
    // ========================= OnEnable() / OnDisable() =========================
 
    [Test]
    public void OnEnable_HabilitaElMapaDeAccionesPlayer()
    {
        Assert.IsTrue(GetControlsPlayerEnabled(weapon));
    }
 
    [Test]
    public void OnDisable_DeshabilitaElMapaDeAccionesPlayer()
    {
        weaponGO.SetActive(false);
        Assert.IsFalse(GetControlsPlayerEnabled(weapon));
    }
 
    [Test]
    public void AlternarActivoVariasVeces_NoLanzaExcepcion()
    {
        Assert.DoesNotThrow(() =>
        {
            weaponGO.SetActive(false);
            weaponGO.SetActive(true);
            weaponGO.SetActive(false);
            weaponGO.SetActive(true);
        });
    }
 
    // ============================== HandleAim() ==============================
 
    [UnityTest]
    public IEnumerator HandleAim_SinAimPresionado_IsAimingEsFalso()
    {
        yield return null;
        Assert.IsFalse(weapon.IsAiming);
    }
 
    [UnityTest]
    public IEnumerator HandleAim_EnReposo_LaPosicionConvergeHaciaHipPosition()
    {
        Vector3 hipPosition = (Vector3)GetPrivateField(weapon, "hipPosition");
        SetPrivateField(weapon, "smoothedBasePosition", hipPosition + new Vector3(1f, 1f, 1f));
 
        yield return new WaitForSeconds(1f);
 
        float distancia = Vector3.Distance(weaponGO.transform.localPosition, hipPosition);
        Assert.Less(distancia, 0.05f,
            "Sin apuntar y sin moverse, la posición del arma debería converger a hipPosition.");
    }
 
    [UnityTest]
    public IEnumerator HandleAim_EnReposo_ElFOVConvergeHaciaDefaultWorldFOV()
    {
        playerCamera.fieldOfView = 90f; // lo alejamos del valor guardado en Awake (60)
 
        yield return new WaitForSeconds(1f);
 
        Assert.AreEqual(FovInicial, playerCamera.fieldOfView, 0.5f,
            "Sin apuntar, el FOV debería converger de vuelta al valor guardado en Awake().");
    }
 

    // ============================== UpdateSpread() ==============================
 
    [UnityTest]
    public IEnumerator UpdateSpread_EnReposo_ConvergeHaciaSpreadIdleNormalizado()
    {
        float spreadIdle = (float)GetPrivateField(weapon, "spreadIdle");
        float spreadMoving = (float)GetPrivateField(weapon, "spreadMoving");
        float esperado = spreadIdle / spreadMoving;
 
        yield return new WaitForSeconds(1f);
 
        Assert.AreEqual(esperado, weapon.CurrentSpreadNormalized, 0.03f);
    }
 
    [UnityTest]
    public IEnumerator UpdateSpread_Agachado_ConvergeHaciaSpreadCrouchingNormalizado()
    {
        playerMovement.enabled = false;
        SetAutoPropertyBackingField(playerMovement, "IsCrouching", true);
 
        float spreadCrouching = (float)GetPrivateField(weapon, "spreadCrouching");
        float spreadMoving = (float)GetPrivateField(weapon, "spreadMoving");
        float esperado = spreadCrouching / spreadMoving;
 
        yield return new WaitForSeconds(1f);
 
        Assert.AreEqual(esperado, weapon.CurrentSpreadNormalized, 0.03f);
    }
 
    // ============================== RecoverRecoil() ==============================
 
    [UnityTest]
    public IEnumerator RecoverRecoil_ConRetrocesoInicial_ConvergeACero()
    {
        SetPrivateField(weapon, "recoilOffset", new Vector3(0f, 0.1f, -0.2f));
 
        yield return new WaitForSeconds(1f);
 
        Vector3 recoilOffset = (Vector3)GetPrivateField(weapon, "recoilOffset");
        Assert.Less(recoilOffset.magnitude, 0.02f,
            "El retroceso debería volver a (casi) cero con el tiempo.");
    }
 
    // ========================= ApplySpreadToDirection() =========================
 
    [Test]
    public void ApplySpreadToDirection_ConSpreadCero_DevuelveLaMismaDireccion()
    {
        Vector3 resultado = (Vector3)InvokePrivateMethod(weapon, "ApplySpreadToDirection", Vector3.forward, 0f);
        Assert.AreEqual(Vector3.forward, resultado);
    }
 
    [Test]
    public void ApplySpreadToDirection_ConSpreadPositivo_MantieneMagnitudYAnguloAcotado()
    {
        const float spreadDegrees = 10f;
 
        for (int i = 0; i < 30; i++)
        {
            Vector3 resultado = (Vector3)InvokePrivateMethod(
                weapon, "ApplySpreadToDirection", Vector3.forward, spreadDegrees);
 
            Assert.AreEqual(1f, resultado.magnitude, 0.01f,
                "La rotación no debería alterar la magnitud del vector dirección.");
 
            float angulo = Vector3.Angle(Vector3.forward, resultado);
            Assert.LessOrEqual(angulo, spreadDegrees * 1.5f + 0.5f,
                "El ángulo de dispersión debería mantenerse razonablemente acotado.");
        }
    }
 
    // ================================ Shoot() ================================
 
    [UnityTest]
    public IEnumerator Shoot_ConObjetivoEnRango_RegistraElImpactoEnElLog()
    {
        SetPrivateField(weapon, "currentSpread", 0f); // determinismo: sin dispersión aleatoria
 
        var objetivo = new GameObject("Objetivo");
        objetivo.AddComponent<BoxCollider>();
        objetivo.transform.position = playerCamera.transform.position + playerCamera.transform.forward * 10f;
        extraObjects.Add(objetivo);
 
        yield return new WaitForFixedUpdate(); // que el collider se registre en la física
 
        LogAssert.Expect(LogType.Log, new Regex("^Impacto en:"));
        InvokePrivateMethod(weapon, "Shoot");
    }
 
    [UnityTest]
    public IEnumerator Shoot_SinObjetivoEnRango_NoLanzaExcepcion()
    {
        SetPrivateField(weapon, "currentSpread", 0f);
        yield return new WaitForFixedUpdate();
 
        Assert.DoesNotThrow(() => InvokePrivateMethod(weapon, "Shoot"));
    }
 
    // ================================ OnFire() ================================
 
    [Test]
    public void OnFire_ConMunicionYSinCooldown_DisminuyeLaMunicionEnUno()
    {
        int municionInicial = weapon.CurrentAmmo;
 
        InvokePrivateMethod(weapon, "OnFire", default(InputAction.CallbackContext));
 
        Assert.AreEqual(municionInicial - 1, weapon.CurrentAmmo);
    }
 
    [Test]
    public void OnFire_SinMunicion_NoDisminuyeLaMunicion()
    {
        SetPrivateField(weapon, "currentAmmo", 0);
 
        Assert.DoesNotThrow(() =>
            InvokePrivateMethod(weapon, "OnFire", default(InputAction.CallbackContext)));
 
        Assert.AreEqual(0, weapon.CurrentAmmo);
    }
 
    [Test]
    public void OnFire_RespetaElCooldownDeFireRate_SegundoDisparoInmediatoNoDescuentaMunicion()
    {
        int municionInicial = weapon.CurrentAmmo;
 
        InvokePrivateMethod(weapon, "OnFire", default(InputAction.CallbackContext));
        InvokePrivateMethod(weapon, "OnFire", default(InputAction.CallbackContext)); // mismo instante, sin esperar fireRate
 
        Assert.AreEqual(municionInicial - 1, weapon.CurrentAmmo,
            "El segundo disparo inmediato debería bloquearse por el cooldown de fireRate.");
    }
 
    [Test]
    public void OnFire_MientrasEstaRecargando_NoDispara()
    {
        SetPrivateField(weapon, "isReloading", true);
        int municionInicial = weapon.CurrentAmmo;
 
        InvokePrivateMethod(weapon, "OnFire", default(InputAction.CallbackContext));
 
        Assert.AreEqual(municionInicial, weapon.CurrentAmmo);
    }
 
    // ================================ OnReload() ================================
 
    [Test]
    public void OnReload_ConMunicionIncompleta_IniciaLaRecargaInmediatamente()
    {
        SetPrivateField(weapon, "currentAmmo", weapon.MaxAmmo - 2);
 
        InvokePrivateMethod(weapon, "OnReload", default(InputAction.CallbackContext));
 
        bool isReloading = (bool)GetPrivateField(weapon, "isReloading");
        Assert.IsTrue(isReloading,
            "El código antes del primer 'yield' de la corrutina corre en forma síncrona al iniciarla.");
    }
 
    [Test]
    public void OnReload_ConMunicionCompleta_NoHaceNada()
    {
        InvokePrivateMethod(weapon, "OnReload", default(InputAction.CallbackContext)); // ya está al máximo
 
        bool isReloading = (bool)GetPrivateField(weapon, "isReloading");
        Assert.IsFalse(isReloading);
    }
 
    [Test]
    public void OnReload_MientrasYaEstaRecargando_NoLanzaExcepcion()
    {
        SetPrivateField(weapon, "isReloading", true);
 
        Assert.DoesNotThrow(() =>
            InvokePrivateMethod(weapon, "OnReload", default(InputAction.CallbackContext)));
    }
 
    // ============================== ReloadRoutine() ==============================
 
    [UnityTest]
    public IEnumerator ReloadRoutine_TrasElTiempoDeRecarga_RestauraMunicionYFinalizaRecarga()
    {
        SetPrivateField(weapon, "currentAmmo", 0);
        float reloadTime = (float)GetPrivateField(weapon, "reloadTime");
 
        var routine = (IEnumerator)InvokePrivateMethod(weapon, "ReloadRoutine");
        weapon.StartCoroutine(routine);
 
        bool isReloadingInmediatamente = (bool)GetPrivateField(weapon, "isReloading");
        Assert.IsTrue(isReloadingInmediatamente,
            "isReloading debería quedar en true apenas se inicia la corrutina.");
 
        yield return new WaitForSeconds(reloadTime + 0.2f);
 
        Assert.AreEqual(weapon.MaxAmmo, weapon.CurrentAmmo);
        bool isReloadingFinal = (bool)GetPrivateField(weapon, "isReloading");
        Assert.IsFalse(isReloadingFinal);
    }

}
