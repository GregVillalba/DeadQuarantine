using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

[TestFixture]
public class TutorialZombieRespawnTests
{
    private GameObject zombieObject;
    private TutorialZombieRespawn respawnController;
   // private MockZombieHealth mockZombieHealth; // Nota: Requiere acceso al componente o un stub/mock si ZombieHealth es testeable

    [SetUp]
    public void SetUp()
    {
        // 1. Arrange: Crear la jerarquía y componentes de prueba
        zombieObject = new GameObject("TestZombie");
        zombieObject.transform.position = new Vector3(10f, 0f, 5f);
        zombieObject.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

        // Simulamos o agregamos ZombieHealth (asumiendo que existe en el proyecto)
        // Si ZombieHealth requiere otros componentes, asegúrate de añadirlos aquí.
        zombieObject.AddComponent<ZombieHealth>();

        respawnController = zombieObject.AddComponent<TutorialZombieRespawn>();
    }

    [TearDown]
    public void TearDown()
    {
        if (zombieObject != null)
            Object.DestroyImmediate(zombieObject);
    }


    [UnityTest]
    public IEnumerator Awake_CapturaPosicionYRotacionInicialesCorrectamente()
    {
        // Act: Forzar o disparar Awake mediante la activación del GameObject o llamada explícita
        InvokePrivateMethod("Awake");
        yield return null;

        // Assert: Validar que las variables privadas de posición y rotación se guardaron bien
        Vector3 savedPosition = (Vector3)GetPrivateField("spawnPosition");
        Quaternion savedRotation = (Quaternion)GetPrivateField("spawnRotation");

        Assert.AreEqual(new Vector3(10f, 0f, 5f), savedPosition, "La posición inicial guardada no coincide.");
        Assert.AreEqual(Quaternion.Euler(0f, 90f, 0f), savedRotation, "La rotación inicial guardada no coincide.");
    }

    [UnityTest]
    public IEnumerator RespawnRoutine_EsperaElDelayYReseteaElEstadoDelZombie()
    {
        // Arrange
        // Modificamos el respawnDelay a un valor bajo para que el test corra rápido (ej. 0.1 segundos)
        SetPrivateField("respawnDelay", 0.1f);
        SetPrivateField("respawnScheduled", true);

        Vector3 nuevaPosicionPrueba = new Vector3(0f, 0f, 0f);
        zombieObject.transform.position = nuevaPosicionPrueba; // Movemos al zombie lejos del spawn original

        // Act: Iniciamos la corrutina de resurgimiento mediante reflexión
        IEnumerator rutina = (IEnumerator)InvokePrivateMethod("RespawnRoutine");
        
        // Ejecutamos la corrutina en un contenedor de prueba de Unity
        // Como PlayMode soporta startCoroutine, podemos levantarla con un componente temporal o yield de tiempo:
        CoroutineRunner runner = zombieObject.AddComponent<CoroutineRunner>();
        var coroutineHandle = runner.StartCoroutine(rutina);

        // Esperamos un tiempo superior al delay configurado (0.1s + holgura)
        yield return new WaitForSeconds(0.2f);

        // Assert: Validar que el flag de resguardo se haya liberado
        bool respawnScheduled = (bool)GetPrivateField("respawnScheduled");
        Assert.IsFalse(respawnScheduled, "respawnScheduled debería volver a false al finalizar el resawn.");

        Object.DestroyImmediate(runner);
    }


    private void SetPrivateField(string fieldName, object value)
    {
        typeof(TutorialZombieRespawn)
            .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?.SetValue(respawnController, value);
    }

    private object GetPrivateField(string fieldName)
    {
        return typeof(TutorialZombieRespawn)
            .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?.GetValue(respawnController);
    }

    private object InvokePrivateMethod(string methodName, params object[] args)
    {
        MethodInfo method = typeof(TutorialZombieRespawn)
            .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
        return method?.Invoke(respawnController, args);
    }

    // Clase auxiliar interna para correr corrutinas aisladas en los tests si se requiere
    private class CoroutineRunner : MonoBehaviour { }
}
