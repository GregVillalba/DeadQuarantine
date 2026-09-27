using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using Unity.Netcode;
using UnityEngine;
 
[TestFixture]
public class MerchantIndicatorUITests
{
    private readonly List<GameObject> creados = new List<GameObject>();
 
    private GameObject indicatorHost;
    private MerchantIndicatorUI indicator;
 
    private GameObject indicadorRootGO;
    private RectTransform flecha;
    private TextMeshProUGUI distanciaText;
 
    [SetUp]
    public void SetUp()
    {
        indicatorHost = CrearGameObject("MerchantIndicator");
        indicator = indicatorHost.AddComponent<MerchantIndicatorUI>();
 
        indicadorRootGO = CrearGameObject("IndicadorRoot");
        indicadorRootGO.SetActive(false);
 
        GameObject flechaGO = CrearGameObject("Flecha", typeof(RectTransform));
        flecha = flechaGO.GetComponent<RectTransform>();
 
        GameObject textoGO = CrearGameObject("DistanciaText", typeof(TextMeshProUGUI));
        distanciaText = textoGO.GetComponent<TextMeshProUGUI>();
 
        SetPrivateField("indicadorRoot", indicadorRootGO);
        SetPrivateField("flecha", flecha);
        SetPrivateField("distanciaText", distanciaText);
        SetPrivateField("distanciaParaOcultar", 3f);
    }
 
    [TearDown]
    public void TearDown()
    {
        foreach (GameObject go in creados)
        {
            if (go != null)
                UnityEngine.Object.DestroyImmediate(go);
        }
 
        creados.Clear();
    }
 
    private GameObject CrearGameObject(string nombre, params Type[] componentes)
    {
        GameObject go = componentes.Length > 0 ? new GameObject(nombre, componentes) : new GameObject(nombre);
        creados.Add(go);
        return go;
    }
 
    private void SetPrivateField(string fieldName, object value)
    {
        typeof(MerchantIndicatorUI)
            .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(indicator, value);
    }
 
    private object GetPrivateField(string fieldName)
    {
        return typeof(MerchantIndicatorUI)
            .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(indicator);
    }
 
    private object InvokePrivateMethod(string methodName, params object[] args)
    {
        MethodInfo method = typeof(MerchantIndicatorUI)
            .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
        return method.Invoke(indicator, args);
    }
 
    // --- Start ---
 
    [Test]
    public void Start_SinNetworkObjectEnPadre_JugadorEsElPropioTransform()
    {
        InvokePrivateMethod("Start");
 
        Transform jugador = (Transform)GetPrivateField("jugador");
 
        Assert.AreEqual(indicatorHost.transform, jugador,
            "Sin NetworkObject en el padre, 'jugador' debe ser el propio transform del indicador.");
    }
 
    [Test]
    public void Start_ConNetworkObjectEnPadre_JugadorEsElTransformDelNetworkObject()
    {
        GameObject padre = CrearGameObject("Jugador", typeof(NetworkObject));
        indicatorHost.transform.SetParent(padre.transform);
 
        InvokePrivateMethod("Start");
 
        Transform jugador = (Transform)GetPrivateField("jugador");
 
        Assert.AreEqual(padre.transform, jugador,
            "Con un NetworkObject en el padre, 'jugador' debe ser el transform de ese NetworkObject.");
    }
 
    // --- BuscarMercader ---
 
    [Test]
    public void BuscarMercader_SinVendedorEnEscena_MercaderQuedaNulo()
    {
        InvokePrivateMethod("BuscarMercader");
 
        Assert.IsNull(GetPrivateField("mercader"));
    }
 
    [Test]
    public void BuscarMercader_ConVendedorEnEscena_AsignaSuTransform()
    {
        // Nota: asume que NPCWeaponVendor puede agregarse standalone (sin
        // NetworkManager activo) sin lanzar excepciones en Awake/OnEnable.
        // Si su implementación depende de estar spawneado en red, este test
        // debería moverse a PlayMode con Netcode real.
        GameObject vendedorGO = CrearGameObject("Vendedor", typeof(NPCWeaponVendor));
 
        InvokePrivateMethod("BuscarMercader");
 
        Assert.AreEqual(vendedorGO.transform, GetPrivateField("mercader"));
    }
 
    // --- MostrarIndicador ---
 
    [Test]
    public void MostrarIndicador_True_ActivaElRoot()
    {
        InvokePrivateMethod("MostrarIndicador", true);
 
        Assert.IsTrue(indicadorRootGO.activeSelf);
    }
 
    [Test]
    public void MostrarIndicador_False_DesactivaElRoot()
    {
        indicadorRootGO.SetActive(true);
 
        InvokePrivateMethod("MostrarIndicador", false);
 
        Assert.IsFalse(indicadorRootGO.activeSelf);
    }
 
    // --- Update ---
 
    [Test]
    public void Update_MercaderLejos_MuestraIndicadorConDistanciaYRotacionCorrectas()
    {
        GameObject mercaderGO = CrearGameObject("Mercader");
        mercaderGO.transform.position = new Vector3(10f, 0f, 0f); // a la derecha
 
        indicatorHost.transform.position = Vector3.zero;
        indicatorHost.transform.rotation = Quaternion.identity; // forward = (0,0,1)
 
        SetPrivateField("jugador", indicatorHost.transform);
        SetPrivateField("mercader", mercaderGO.transform);
 
        InvokePrivateMethod("Update");
 
        Assert.IsTrue(indicadorRootGO.activeSelf, "A más de 3m, el indicador debe mostrarse.");
        Assert.AreEqual("Mercader a 10 m", distanciaText.text);
 
        Quaternion esperada = Quaternion.Euler(0f, 0f, -90f);
        Assert.Less(Quaternion.Angle(esperada, flecha.localRotation), 0.5f,
            "La flecha debería rotar -90° en Z cuando el mercader está a la derecha.");
    }
 
    [Test]
    public void Update_MercaderDentroDelUmbral_OcultaIndicador()
    {
        GameObject mercaderGO = CrearGameObject("Mercader");
        mercaderGO.transform.position = new Vector3(1f, 0f, 0f); // a 1m, umbral es 3m
 
        indicatorHost.transform.position = Vector3.zero;
 
        SetPrivateField("jugador", indicatorHost.transform);
        SetPrivateField("mercader", mercaderGO.transform);
 
        indicadorRootGO.SetActive(true);
 
        InvokePrivateMethod("Update");
 
        Assert.IsFalse(indicadorRootGO.activeSelf,
            "A 1m de distancia (menor al umbral de 3m), el indicador debe ocultarse.");
    }
 
    [Test]
    public void Update_MercaderExactoEnElUmbral_OcultaIndicador()
    {
        // El script oculta cuando distancia <= umbral, así que en el límite
        // exacto (3m) también debe ocultarse.
        GameObject mercaderGO = CrearGameObject("Mercader");
        mercaderGO.transform.position = new Vector3(3f, 0f, 0f);
 
        indicatorHost.transform.position = Vector3.zero;
 
        SetPrivateField("jugador", indicatorHost.transform);
        SetPrivateField("mercader", mercaderGO.transform);
 
        indicadorRootGO.SetActive(true);
 
        InvokePrivateMethod("Update");
 
        Assert.IsFalse(indicadorRootGO.activeSelf);
    }
 
    [Test]
    public void Update_SinMercaderEnEscena_OcultaIndicadorYNoActualizaTexto()
    {
        SetPrivateField("jugador", indicatorHost.transform);
        SetPrivateField("mercader", null);
        distanciaText.text = "texto previo";
 
        InvokePrivateMethod("Update");
 
        Assert.IsFalse(indicadorRootGO.activeSelf);
        Assert.AreEqual("texto previo", distanciaText.text,
            "Sin mercader encontrado, Update() no debería tocar el texto de distancia.");
    }
}
