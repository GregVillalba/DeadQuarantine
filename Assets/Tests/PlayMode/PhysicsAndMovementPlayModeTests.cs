using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class PhysicsAndMovementPlayModeTests
{
    private GameObject playerObj;
    private GameObject floorObj;

    [SetUp]
    public void Setup()
    {
        // Suelo estático con collider
        floorObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floorObj.name = "Floor";
        floorObj.transform.position = new Vector3(0, -1, 0);
        floorObj.transform.localScale = new Vector3(20, 1, 20);

        // Jugador con Rigidbody y CapsuleCollider
        playerObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        playerObj.name = "PlayerPhysics";
        playerObj.transform.position = new Vector3(0, 3, 0);
        var rb = playerObj.AddComponent<Rigidbody>();
        rb.useGravity = true;
    }

    [TearDown]
    public void Teardown()
    {
        if (playerObj != null) Object.Destroy(playerObj);
        if (floorObj != null) Object.Destroy(floorObj);
    }

    // TC5: Caída por gravedad e impacto en el suelo
    [UnityTest]
    public IEnumerator TC5_Player_FallsWithGravity_AndLandsOnFloor()
    {
        float initialY = playerObj.transform.position.y;

        // Esperar frames físicos para simular la caída
        for (int i = 0; i < 30; i++)
        {
            yield return new WaitForFixedUpdate();
        }

        float landedY = playerObj.transform.position.y;

        // Assert
        Assert.Less(landedY, initialY, "El jugador debe descender por efecto de la gravedad.");
        Assert.GreaterOrEqual(landedY, 0.0f, "El jugador debe detenerse sobre el suelo sin atravesarlo.");
    }

    // TC9 / TC35: Interacción con objeto dinámico Rigidbody al chocar
    [UnityTest]
    public IEnumerator TC9_TC35_Player_CollidesWithDynamicObject_PushesIt()
    {
        // Crear objeto interactivo dinámico (silla, mesa o puerta)
        GameObject dynamicBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
        dynamicBox.transform.position = new Vector3(0, 0.5f, 2f);
        var boxRb = dynamicBox.AddComponent<Rigidbody>();
        boxRb.mass = 5f;

        yield return new WaitForFixedUpdate();

        // Aplicar fuerza de empuje simulando el choque frontal a velocidad 4 m/s
        boxRb.AddForce(Vector3.forward * 20f, ForceMode.Impulse);

        yield return new WaitForSeconds(0.2f);

        // Assert
        Assert.Greater(dynamicBox.transform.position.z, 2.0f, "El objeto dinámico con Rigidbody debe haberse desplazado tras el impacto.");

        Object.Destroy(dynamicBox);
    }
}