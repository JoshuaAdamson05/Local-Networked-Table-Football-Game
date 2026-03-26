using Unity.Netcode;
using Unity.VectorGraphics;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    private void Start()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
            Destroy(NetworkManager.Singleton.gameObject);
        }

        if (PersistentLobbySessionManager.Instance != null)
        {
            Destroy(PersistentLobbySessionManager.Instance.gameObject);
        }
    }

    public void GoToLobby()
    {
        SceneManager.LoadScene("LobbyOptionsScene");
    }
}
