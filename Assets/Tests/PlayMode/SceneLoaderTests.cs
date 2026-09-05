using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class SceneLoaderTests
{
    private SceneLoader loader;

    [SetUp]
    public void SetUp()
    {
        loader = new GameObject("SceneLoader").AddComponent<SceneLoader>();
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (loader != null)
            Object.Destroy(loader.gameObject);

        // Vuelve a una escena vacía en runtime para no arrastrar
        // NetworkLobby/PantallasUI cargadas de un test al siguiente.
        yield return SceneManager.LoadSceneAsync(
            SceneManager.CreateScene("TestClean_" + System.Guid.NewGuid()).name);
    }

    [UnityTest]
    public IEnumerator LoadNetworkLobby_CargaLaEscenaNetworkLobby()
    {
        loader.LoadNetworkLobby();

        // LoadScene (sync) ya deja la escena activa al retornar, pero se
        // espera un frame para que el cambio de escena se asiente del todo.
        yield return null;

        Assert.AreEqual("NetworkLobby", SceneManager.GetActiveScene().name);
    }

    [UnityTest]
    public IEnumerator LoadPantallasUI_CargaLaEscenaPantallasUI()
    {
        loader.LoadPantallasUI();

        yield return null;

        Assert.AreEqual("PantallasUI", SceneManager.GetActiveScene().name);
    }
}
