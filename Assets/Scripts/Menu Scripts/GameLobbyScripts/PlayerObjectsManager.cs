using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerObjectsManager : NetworkBehaviour
{ 
    [SerializeField] private GameObject _gameLobbyCanvas;
    [SerializeField] private GameObject _clientObjectPrefab;
    [SerializeField] private TextMeshProUGUI _hostNameText;
    [SerializeField] private TextMeshProUGUI _lobbyTitleText;
    private TextMeshProUGUI _clientObjectNameText;
    private GameObject _activeClientObject;

    public override void OnNetworkSpawn()
    {
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

        if (!IsServer)
        {
            if (_clientObjectPrefab != null)
            {
                _activeClientObject = Instantiate(_clientObjectPrefab, _gameLobbyCanvas.transform);
                _clientObjectNameText = GameObject.FindGameObjectWithTag("ClientNameText").GetComponent<TextMeshProUGUI>();

                if (_clientObjectNameText != null)
                {
                    if (PersistentLobbySessionManager.Instance != null)
                    {
                        _clientObjectNameText.text = PersistentLobbySessionManager.Instance.LocalPlayerName;    
                        SetClientNameServerRpc(PersistentLobbySessionManager.Instance.LocalPlayerName);
                    }
                    else
                        Debug.LogError("Persistent lobby manager missing!");
                }
            }
            else
                Debug.LogError("Client object prefab missing!");
        }

        if (IsServer)
        {
            NetworkManager.OnClientConnectedCallback += OnClientConnected;

            if (PersistentLobbySessionManager.Instance == null)
            {
                Debug.LogError("Cannot find persistent lobby manager instance!");
                return;
            }

            if (_hostNameText != null)
            {
                _hostNameText.text = PersistentLobbySessionManager.Instance.LocalPlayerName;
            }
            else
                Debug.LogError("Host name text missing!");

            if (_lobbyTitleText != null)
            {
                _lobbyTitleText.text = PersistentLobbySessionManager.Instance.LobbyName;
            }
            else
                Debug.LogError("Lobby title text missing!");

            if (NetworkManager.Singleton.ConnectedClients.Count > 1)
                SetUpClientObject();
        }   
    }

    public override void OnNetworkDespawn()
    {
        NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;

        if (IsServer)
        {
            NetworkManager.OnClientConnectedCallback -= OnClientConnected;
        }
    }  

    [ClientRpc]
    private void SendHostNameClientRpc(string hostName)
    {
        if (PersistentLobbySessionManager.Instance != null)
        {
            PersistentLobbySessionManager.Instance.SetConnectedPlayerName(hostName);
        }
        else
            Debug.LogError("Persistent lobby manager instance is missing!");

        if (_hostNameText != null)
            _hostNameText.text = hostName;
        else
            Debug.LogError("Host name text missing!");
    }

    [ClientRpc]
    private void SendLobbyNameClientRpc(string lobbyName)
    {
        if (PersistentLobbySessionManager.Instance != null)
        {
            if (!PersistentLobbySessionManager.Instance.ClientSetLobbyName(lobbyName))
            {
                Debug.LogError("Cannot set lobby name!");
            }
        }

        if (_lobbyTitleText != null)
            _lobbyTitleText.text = lobbyName;
        else
            Debug.LogError("Lobby title text missing!");
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void SetClientNameServerRpc(string playerName)
    {
        if (PersistentLobbySessionManager.Instance != null)
        {
            PersistentLobbySessionManager.Instance.SetConnectedPlayerName(playerName);
        }
        else
        {
            Debug.LogError("Persistent lobby manager instance is null!");
        }

        if (_clientObjectNameText != null)
        {
            _clientObjectNameText.text = playerName;
        }
        else
            Debug.LogWarning("Client object name text missing");
    }

    private void OnClientConnected(ulong clientId)
    {
        if (clientId == NetworkManager.Singleton.LocalClientId) return;
        SetUpClientObject();
    }

    private void SetUpClientObject()
    {
        if (IsServer)
        {
            if (_clientObjectPrefab != null)
            {
                _activeClientObject = Instantiate(_clientObjectPrefab, _gameLobbyCanvas.transform);
                _clientObjectNameText = GameObject.FindGameObjectWithTag("ClientNameText").GetComponent<TextMeshProUGUI>();
                if (PersistentLobbySessionManager.Instance != null)
                {
                    if (PersistentLobbySessionManager.Instance.ConnectedPlayerName != null)
                    {
                        _clientObjectNameText.text = PersistentLobbySessionManager.Instance.ConnectedPlayerName;
                    }
                    else
                        Debug.LogWarning("Connected player name not yet set!");
                }
                else
                    Debug.LogError("Peristent lobby manager instance is missing!");
            }

            SendHostNameClientRpc(PersistentLobbySessionManager.Instance.LocalPlayerName);
            SendLobbyNameClientRpc(PersistentLobbySessionManager.Instance.LobbyName);
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (clientId == NetworkManager.Singleton.LocalClientId) return;

        if (IsServer)
        {
            if (_activeClientObject != null)
                Destroy(_activeClientObject.gameObject);

            if (PersistentLobbySessionManager.Instance != null)
            {
                PersistentLobbySessionManager.Instance.RemoveConnectedPlayerName();
            }
            else
                Debug.LogError("Persistent lobby manager instance is missing!");

            _activeClientObject = null;
            _clientObjectNameText = null;
        }
    }   
}
