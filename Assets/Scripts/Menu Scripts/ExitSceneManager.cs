using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ExitSceneManager : MonoBehaviour
{
    private void Start()
    {
        string activeScene = SceneManager.GetActiveScene().name;
        if (activeScene == "GameLobbyScene" || activeScene == "GameScene")
        {
            Button exitButton = GameObject.FindGameObjectWithTag("ExitConnectionButton").GetComponent<Button>();
            if (exitButton != null)
                exitButton.onClick.AddListener(PersistentLobbySessionManager.Instance.ExitGameLobby);
            else Debug.LogError("Cannot find exit button!");
        }
        else Debug.LogWarning("Not in game lobby scene or game scene, no exit connection button required!");
    }

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
