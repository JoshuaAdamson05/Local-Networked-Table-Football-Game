using TMPro;
using UnityEngine;

public class UIPlayerNamesManager : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _hostNameText;
    [SerializeField] private TextMeshProUGUI _clientNameText;

    private void Start()
    {
        if (_hostNameText == null) Debug.LogError("Host name text is missing!");
        if (_clientNameText == null) Debug.LogError("Client name text is missing!");

        if (PersistentLobbySessionManager.Instance != null)
        {
            if (PersistentLobbySessionManager.Instance.IsHost)
            {
                SetPlayerNames(PersistentLobbySessionManager.Instance.LocalPlayerName, PersistentLobbySessionManager.Instance.ConnectedPlayerName);
            }
            else
            {
                SetPlayerNames(PersistentLobbySessionManager.Instance.ConnectedPlayerName, PersistentLobbySessionManager.Instance.LocalPlayerName);
            }
        }
    }

    private void SetPlayerNames(string hostName, string clientName)
    {
        if (_hostNameText != null)
        {
            _hostNameText.text = $"Blue ({hostName})";
        }

        if (_clientNameText != null)
        {
            _clientNameText.text = $"Red ({clientName})";
        }
    }
}
