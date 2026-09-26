using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using TMPro;

public class FiltroEstadoLogrosTests
{
    private GameObject filtroGameObject;
    private FiltroEstadoLogros filtroScript;
    private Transform contenedor;
    private GameObject textoNoItems;

    [SetUp]
    public void Setup()
    {
        // Crear la estructura de GameObjects necesaria para el test
        filtroGameObject = new GameObject("FiltroEstadoLogros");
        filtroScript = filtroGameObject.AddComponent<FiltroEstadoLogros>();

        contenedor = new GameObject("ContenedorContent").transform;
        contenedor.SetParent(filtroGameObject.transform);
        filtroScript.contenedorContent = contenedor;

        textoNoItems = new GameObject("TextoNoItems");
        textoNoItems.AddComponent<TextMeshProUGUI>();
        textoNoItems.transform.SetParent(filtroGameObject.transform);
        filtroScript.textoNoItems = textoNoItems;
    }

    [TearDown]
    public void Teardown()
    {
        Object.Destroy(filtroGameObject);
    }

    [UnityTest]
    public IEnumerator FiltrarPorEstado_MuestraCorrectamenteBloqueadosYDesbloqueados()
    {
        // Arrange: Crear tarjetas de prueba con textos específicos
        GameObject tarjeta1 = CrearTarjetaConTexto("Logro 1: Bloqueado");
        GameObject tarjeta2 = CrearTarjetaConTexto("Logro 2: Desbloqueado");

        // Act: Filtrar por índice 1 (Bloqueados)
        filtroScript.FiltrarPorEstado(1);
        yield return null;

        // Assert
        Assert.IsTrue(tarjeta1.activeSelf, "La tarjeta bloqueada debería estar visible.");
        Assert.IsFalse(tarjeta2.activeSelf, "La tarjeta desbloqueada debería estar oculta.");
        Assert.IsFalse(textoNoItems.activeSelf, "El texto de 'No items' debe estar oculto.");

        // Act: Filtrar por índice 2 (Desbloqueados)
        filtroScript.FiltrarPorEstado(2);
        yield return null;

        // Assert
        Assert.IsFalse(tarjeta1.activeSelf, "La tarjeta bloqueada debería estar oculta.");
        Assert.IsTrue(tarjeta2.activeSelf, "La tarjeta desbloqueada debería estar visible.");
    }

    [UnityTest]
    public IEnumerator FiltrarPorEstado_ActivaTextoNoItems_CuandoNoHayCoidencias()
    {
        // Arrange: Crear tarjeta bloqueada pero filtrar buscando desbloqueados
        GameObject tarjeta1 = CrearTarjetaConTexto("Logro 1: Bloqueado");

        // Act: Filtrar por índice 2 (Desbloqueados), sabiendo que solo hay bloqueadas
        filtroScript.FiltrarPorEstado(2);
        yield return null;

        // Assert
        Assert.IsFalse(tarjeta1.activeSelf);
        Assert.IsTrue(textoNoItems.activeSelf, "El texto 'No items' debería activarse si no hay tarjetas visibles.");
    }

    // Método auxiliar para construir tarjetas de prueba rápidamente
    private GameObject CrearTarjetaConTexto(string contenidoTexto)
    {
        GameObject tarjeta = new GameObject("TarjetaLogro");
        tarjeta.transform.SetParent(contenedor);

        GameObject textoObj = new GameObject("TextoTMP");
        textoObj.transform.SetParent(tarjeta.transform);
        TextMeshProUGUI tmp = textoObj.AddComponent<TextMeshProUGUI>();
        tmp.text = contenidoTexto;

        return tarjeta;
    }
}
