using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ExitSceneManager : MonoBehaviour
{
    public void ExitScene(string sceneToLoad)
    {
        if (PersistentLobbySessionManager.Instance != null)
        {
            Destroy(PersistentLobbySessionManager.Instance.gameObject);
        }

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
            Destroy(NetworkManager.Singleton.gameObject);
        }

        SceneManager.LoadScene(sceneToLoad);
    }
}
