using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class DoorTests
{
   private GameObject root;
    private Door door;
 
    private GameObject doorLeafGO;
    private Transform doorLeaf;
 
    private GameObject cameraGO;
    private Camera playerCamera;
 
    private const float OpenSpeedDeTest = 50f; // acelera RotateDoor en los tests
    private const float ToleranciaAngular = 1f; // grados
 
    [UnitySetUp]
    public IEnumerator UnitySetUp()
    {
        root = new GameObject("DoorTestObject");
        root.transform.position = Vector3.zero;
 
        doorLeafGO = new GameObject("DoorLeaf", typeof(BoxCollider));
        doorLeafGO.transform.SetParent(root.transform);
        doorLeafGO.transform.localPosition = Vector3.zero;
        doorLeafGO.transform.localRotation = Quaternion.identity;
        doorLeaf = doorLeafGO.transform;
 
        cameraGO = new GameObject("PlayerCamera", typeof(Camera));
        playerCamera = cameraGO.GetComponent<Camera>();
        cameraGO.transform.position = new Vector3(0f, 0f, -2f);
        cameraGO.transform.rotation = Quaternion.identity; // forward = +z, mirando a la puerta
 
        // root arranca inactivo para inyectar doorLeaf/playerCamera ANTES de Awake().
        root.SetActive(false);
        door = root.AddComponent<Door>();
 
        SetField("doorLeaf", doorLeaf);
        SetField("playerCamera", playerCamera);
        SetField("openSpeed", OpenSpeedDeTest);
 
        root.SetActive(true); // dispara Awake()
 
        yield return null;
    }
 
    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(root);
        Object.DestroyImmediate(doorLeafGO);
        Object.DestroyImmediate(cameraGO);
    }
 
    private void SetField(string fieldName, object value)
    {
        FieldInfo field = typeof(Door).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"No se encontró el campo privado '{fieldName}' en Door.");
        field.SetValue(door, value);
    }
 
    private object GetField(string fieldName)
    {
        FieldInfo field = typeof(Door).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"No se encontró el campo privado '{fieldName}' en Door.");
        return field.GetValue(door);
    }
 
    private void InvokePrivate(string methodName)
    {
        MethodInfo method = typeof(Door).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(method, $"No se encontró el método privado '{methodName}' en Door.");
        method.Invoke(door, null);
    }
 
    // ---------------- Awake ----------------
 
    [Test]
    public void Awake_CalculaRotacionAbiertaAPartirDeLaCerradaYElAngulo()
    {
        var closed = (Quaternion)GetField("closedRotation");
        var open = (Quaternion)GetField("openRotation");
 
        Quaternion esperadaAbierta = closed * Quaternion.Euler(0f, 90f, 0f); // openAngle default = 90
 
        Assert.Less(Quaternion.Angle(esperadaAbierta, open), ToleranciaAngular,
            "openRotation debería ser closedRotation rotada openAngle grados en Y.");
    }
 
    [Test]
    public void Awake_InicializaStartRotation_IgualALaCerrada()
    {
        var closed = (Quaternion)GetField("closedRotation");
        var start = (Quaternion)GetField("startRotation");
 
        Assert.Less(Quaternion.Angle(closed, start), ToleranciaAngular,
            "startRotation debería inicializarse igual a closedRotation.");
    }
 
    // ---------------- RotateDoor (vía reflection, sin pasar por Update/Input) ----------------
 
    [UnityTest]
    public IEnumerator RotateDoor_AbreLaPuerta_HastaLlegarAOpenRotation()
    {
        var openRotation = (Quaternion)GetField("openRotation");
        var closedRotation = (Quaternion)GetField("closedRotation");
 
        SetField("isOpen", true);
        SetField("startRotation", closedRotation);
        SetField("timeElapsed", 0f);
 
        float elapsed = (float)GetField("timeElapsed");
        while (elapsed < 1f)
        {
            InvokePrivate("RotateDoor");
            yield return null;
            elapsed = (float)GetField("timeElapsed");
        }
 
        Assert.Less(Quaternion.Angle(openRotation, doorLeaf.localRotation), ToleranciaAngular,
            "Tras completar la animación con isOpen = true, el door leaf debería quedar en openRotation.");
    }
 
    [UnityTest]
    public IEnumerator RotateDoor_CierraLaPuerta_HastaLlegarAClosedRotation()
    {
        var openRotation = (Quaternion)GetField("openRotation");
        var closedRotation = (Quaternion)GetField("closedRotation");
 
        // Simula que la puerta estaba abierta y ahora se cierra.
        doorLeaf.localRotation = openRotation;
        SetField("isOpen", false);
        SetField("startRotation", openRotation);
        SetField("timeElapsed", 0f);
 
        float elapsed = (float)GetField("timeElapsed");
        while (elapsed < 1f)
        {
            InvokePrivate("RotateDoor");
            yield return null;
            elapsed = (float)GetField("timeElapsed");
        }
 
        Assert.Less(Quaternion.Angle(closedRotation, doorLeaf.localRotation), ToleranciaAngular,
            "Tras completar la animación con isOpen = false, el door leaf debería volver a closedRotation.");
    }
 
    [Test]
    public void RotateDoor_SiTimeElapsedYaCompleto_NoModificaLaRotacion()
    {
        SetField("timeElapsed", 1f);
        Quaternion rotacionArbitraria = Quaternion.Euler(12f, 34f, 56f);
        doorLeaf.localRotation = rotacionArbitraria;
 
        InvokePrivate("RotateDoor");
 
        Assert.AreEqual(rotacionArbitraria, doorLeaf.localRotation,
            "RotateDoor() debería salir de inmediato (return temprano) si timeElapsed ya llegó a 1, sin tocar la rotación.");
    }
 
    // ---------------- CheckIfPlayerIsLooking (requiere Physics.Raycast real) ----------------
 
    [UnityTest]
    public IEnumerator CheckIfPlayerIsLooking_DetectaLaPuerta_DentroDelRango()
    {
        yield return new WaitForFixedUpdate(); // asegura que el collider ya esté registrado en la physics scene
 
        InvokePrivate("CheckIfPlayerIsLooking");
 
        Assert.IsTrue((bool)GetField("isPlayerLooking"),
            "Si la cámara apunta directo al doorLeaf y está dentro de interactRange, debería detectarlo.");
    }
 
    [UnityTest]
    public IEnumerator CheckIfPlayerIsLooking_FueraDeRango_NoLaDetecta()
    {
        cameraGO.transform.position = new Vector3(0f, 0f, -10f); // interactRange default = 3
        yield return new WaitForFixedUpdate();
 
        InvokePrivate("CheckIfPlayerIsLooking");
 
        Assert.IsFalse((bool)GetField("isPlayerLooking"),
            "Si la puerta está más lejos que interactRange, no debería detectarla aunque esté en línea de mira.");
    }
 
    [UnityTest]
    public IEnumerator CheckIfPlayerIsLooking_MirandoHaciaOtroLado_NoLaDetecta()
    {
        cameraGO.transform.rotation = Quaternion.Euler(0f, 180f, 0f); // forward ahora apunta lejos de la puerta
        yield return new WaitForFixedUpdate();
 
        InvokePrivate("CheckIfPlayerIsLooking");
 
        Assert.IsFalse((bool)GetField("isPlayerLooking"),
            "Si la cámara mira hacia otro lado, el raycast no debería impactar la puerta.");
    }
 
    [UnityTest]
    public IEnumerator CheckIfPlayerIsLooking_DetectaColliderHijoDeLaPuerta_AunqueNoSeaElDoorLeaf()
    {
        // Aislamos el escenario: desactivamos el collider del doorLeaf y ponemos
        // un collider "pomo" hijo de root, exactamente en el mismo lugar.
        doorLeafGO.GetComponent<BoxCollider>().enabled = false;
 
        var pomoGO = new GameObject("Pomo", typeof(BoxCollider));
        pomoGO.transform.SetParent(root.transform);
        pomoGO.transform.localPosition = Vector3.zero;
 
        yield return new WaitForFixedUpdate();
 
        InvokePrivate("CheckIfPlayerIsLooking");
 
        Assert.IsTrue((bool)GetField("isPlayerLooking"),
            "Un collider que es hijo de la puerta (aunque no sea el doorLeaf) también debería contar como 'mirando la puerta' (IsChildOf).");
 
        Object.DestroyImmediate(pomoGO);
    }
 
    [UnityTest]
    public IEnumerator CheckIfPlayerIsLooking_ColliderNoRelacionado_NoLaDetecta()
    {
        // Aislamos el escenario: desactivamos el collider del doorLeaf y ponemos
        // un collider de un objeto AJENO (no hijo de root) en el mismo lugar.
        doorLeafGO.GetComponent<BoxCollider>().enabled = false;
 
        var paredGO = new GameObject("ParedAjena", typeof(BoxCollider));
        paredGO.transform.position = Vector3.zero; // no es hijo de root
 
        yield return new WaitForFixedUpdate();
 
        InvokePrivate("CheckIfPlayerIsLooking");
 
        Assert.IsFalse((bool)GetField("isPlayerLooking"),
            "Un collider que no es el doorLeaf ni hijo de la puerta no debería contar como 'mirando la puerta'.");
 
        Object.DestroyImmediate(paredGO);
    }
}
