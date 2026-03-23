using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public class LobbyOptionsSceneManager : MonoBehaviour
{  
    [SerializeField] private GameObject _enterPlayerNameCanvas;
    [SerializeField] private GameObject _lobbyOptionsCanvas;
    [SerializeField] private GameObject _enterLobbyNameCanvas;

    public void AttemptToCreateLobby()
    {
        if (PersistentLobbySessionManager.Instance == null) return;

        if (PersistentLobbySessionManager.Instance.HostSetLobbyName())
        {
            PersistentLobbySessionManager.Instance.SetHost(true);
            PersistentLobbySessionManager.Instance.SetLobbyIP();
            SceneManager.LoadScene("GameLobbyScene");
        }
        else
        {
            Debug.LogError("Could not set host lobby!");
        }
    }

    public void GoToJoinLobbyScene()
    {
        if (PersistentLobbySessionManager.Instance == null) return;
        PersistentLobbySessionManager.Instance.SetHost(false);
        SceneManager.LoadScene("ViewAvailableLobbiesScene");
    }

    public void SetEnterLobbyNameCanvas()
    {
        if (_lobbyOptionsCanvas == null)
        {
            Debug.LogError("Cannot find lobby options canvas!");
            return;
        }

        if (_enterLobbyNameCanvas == null)
        {
            Debug.LogError("Cannot find lobby options canvas!");
            return;
        }

        _lobbyOptionsCanvas.SetActive(false);
        _enterLobbyNameCanvas.SetActive(true);
    }

    public void SetLobbyOptionsCanvas()
    {
        if (PersistentLobbySessionManager.Instance == null) return;

        if (_enterPlayerNameCanvas == null)
        {
            Debug.LogError("Cannot find enter player name canvas!");
            return;
        }

        if (_lobbyOptionsCanvas == null)
        {
            Debug.LogError("Cannot find lobby options canvas!");
            return;
        }

        TMP_InputField playerNameInput = FindAnyObjectByType<TMP_InputField>();
        if (playerNameInput != null)
        {
            string inputText = playerNameInput.text;
            if (PersistentLobbySessionManager.Instance.SetLocalPlayerName(inputText))
            {
                _lobbyOptionsCanvas.SetActive(true);
                _enterPlayerNameCanvas.SetActive(false);
            }
        }
        else
            Debug.LogError("Cannot find player name input field!");
    }
}
