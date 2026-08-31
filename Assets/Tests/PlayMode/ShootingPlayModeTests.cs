using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class ShootingPlayModeTests
{
    private GameObject weaponObj;
    private GameObject wallObj;

    [SetUp]
    public void Setup()
    {
        weaponObj = new GameObject("Weapon");
        wallObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wallObj.transform.position = new Vector3(0, 0, 5); // Pared ubicada a 5 metros al frente
    }

    [TearDown]
    public void Teardown()
    {
        Object.Destroy(weaponObj);
        Object.Destroy(wallObj);
    }

    // TC36: El proyectil / Raycast impacta con la pared sólida
    [UnityTest]
    public IEnumerator Weapon_FireRaycast_HitsSolidWall()
    {
        // Arrange
        Ray ray = new Ray(weaponObj.transform.position, Vector3.forward);
        RaycastHit hit;

        yield return null;

        // Act
        bool hasHit = Physics.Raycast(ray, out hit, 10.0f);

        // Assert
        Assert.IsTrue(hasHit, "El disparo debería detectar colisión con la pared en frente.");
        Assert.AreEqual(wallObj, hit.collider.gameObject, "El objeto impactado debe ser la pared.");
    }
}