using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public void LoadNetworkLobby()
    {
        SceneManager.LoadScene("NetworkLobby");
    }

    public void LoadPantallasUI()
    {
        SceneManager.LoadScene("PantallasUI");
    }
}