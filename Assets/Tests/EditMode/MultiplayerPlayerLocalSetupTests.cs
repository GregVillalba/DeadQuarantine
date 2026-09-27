using System.Reflection;
using NUnit.Framework;
using UnityEngine;


[TestFixture]
public class MultiplayerPlayerLocalSetupTests
{
 private GameObject host;
    private MultiplayerLocalPlayerSetup setup;
 
    [SetUp]
    public void SetUp()
    {
        host = new GameObject("MultiplayerLocalPlayerSetup");
        setup = host.AddComponent<MultiplayerLocalPlayerSetup>();
    }
 
    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(host);
    }
 
    private void InvokeCambiarLayerRecursivo(GameObject objeto, int layer)
    {
        MethodInfo method = typeof(MultiplayerLocalPlayerSetup)
            .GetMethod("CambiarLayerRecursivo", BindingFlags.NonPublic | BindingFlags.Instance);
        method.Invoke(setup, new object[] { objeto, layer });
    }
 
    [Test]
    public void CambiarLayerRecursivo_AplicaElLayerAlObjetoRaiz()
    {
        GameObject raiz = new GameObject("Raiz");
 
        try
        {
            InvokeCambiarLayerRecursivo(raiz, 7);
            Assert.AreEqual(7, raiz.layer);
        }
        finally
        {
            Object.DestroyImmediate(raiz);
        }
    }
 
    [Test]
    public void CambiarLayerRecursivo_PropagaElLayerATodosLosHijosYNietos()
    {
        GameObject raiz = new GameObject("Raiz");
        GameObject hijoA = new GameObject("HijoA");
        GameObject hijoB = new GameObject("HijoB");
        GameObject nieto = new GameObject("Nieto");
 
        hijoA.transform.SetParent(raiz.transform);
        hijoB.transform.SetParent(raiz.transform);
        nieto.transform.SetParent(hijoA.transform);
 
        try
        {
            InvokeCambiarLayerRecursivo(raiz, 9);
 
            Assert.AreEqual(9, raiz.layer);
            Assert.AreEqual(9, hijoA.layer);
            Assert.AreEqual(9, hijoB.layer);
            Assert.AreEqual(9, nieto.layer,
                "El cambio de layer debe propagarse también a los nietos, no solo a los hijos directos.");
        }
        finally
        {
            Object.DestroyImmediate(raiz); // destruye toda la jerarquía
        }
    }
 
    [Test]
    public void CambiarLayerRecursivo_ObjetoSinHijos_NoLanzaExcepcion()
    {
        GameObject hoja = new GameObject("Hoja");
 
        try
        {
            Assert.DoesNotThrow(() => InvokeCambiarLayerRecursivo(hoja, 0));
        }
        finally
        {
            Object.DestroyImmediate(hoja);
        }
    }
}
