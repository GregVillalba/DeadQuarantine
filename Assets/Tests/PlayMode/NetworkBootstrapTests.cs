using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

[TestFixture]
public class NetworkBootstrapTests
{
    private GameObject goA;
    private GameObject goB;
 
    [TearDown]
    public void TearDown()
    {
        if (goA != null) Object.DestroyImmediate(goA);
        if (goB != null) Object.DestroyImmediate(goB);
 
        ResetSingletonInstance();
    }
 
    private void ResetSingletonInstance()
    {
        typeof(NetworkBootstrap)
            .GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
            .GetSetMethod(true)
            .Invoke(null, new object[] { null });
    }
 
    [UnityTest]
    public IEnumerator Awake_SegundaInstancia_NoReemplazaElSingletonYSeAutodestruye()
    {
        goA = new GameObject("BootstrapA");
        NetworkBootstrap bootstrapA = goA.AddComponent<NetworkBootstrap>();
 
        Assert.AreSame(bootstrapA, NetworkBootstrap.Instance,
            "La primera instancia creada debe convertirse en el singleton.");
 
        goB = new GameObject("BootstrapB");
        goB.AddComponent<NetworkBootstrap>();
 
        Assert.AreSame(bootstrapA, NetworkBootstrap.Instance,
            "Al crear una segunda instancia, el singleton no debe reemplazarse.");
 
        // Destroy() es diferido en Unity: esperamos un frame para que se complete de verdad.
        yield return null;
 
        Assert.IsTrue(goB == null,
            "El GameObject duplicado debería haberse destruido.");
    }
}
 
