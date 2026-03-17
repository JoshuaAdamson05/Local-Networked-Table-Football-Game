using TMPro;
using Unity.Netcode;
using Unity.Networking.Transport.Error;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameLobbyUIManager : NetworkBehaviour
{ 
    public NetworkVariable<bool> IsHostReady = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<Color> ServerAuthorityButtonColour = new NetworkVariable<Color>(Color.red, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<Color> ClientAuthorityButtonColour = new NetworkVariable<Color>(Color.green, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> IsClientReady = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    [SerializeField] private GameObject _networkedLobbyManager;
    [SerializeField] private GameObject _gameLobbyCanvas;
    [SerializeField] private GameObject _clientObjectPrefab;
    [SerializeField] private GameObject _persistentDisconnectionManagerPrefab;
    [SerializeField] private TextMeshProUGUI _hostNameText;
    [SerializeField] private TextMeshProUGUI _lobbyTitleText;
    [SerializeField] private TextMeshProUGUI _hostReadyStatusText;
    private TextMeshProUGUI _clientObjectNameText;
    private TextMeshProUGUI _clientObjectReadyStatusText;
    [SerializeField] private Button _clientAuthorityButton;
    [SerializeField] private Button _serverAuthorityButton;
    private GameObject _activeClientObject;

    public override void OnNetworkSpawn()
    {
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

        if (!IsServer)
        {
            if (_networkedLobbyManager != null)
                Destroy(_networkedLobbyManager);
            else
                Debug.LogError("Networked lobby manager missing!");

            IsHostReady.OnValueChanged += OnHostReadyToggle;
            IsClientReady.OnValueChanged += OnClientReadyToggle;
            ServerAuthorityButtonColour.OnValueChanged += OnServerAuthorityColourChanged;
            ClientAuthorityButtonColour.OnValueChanged += OnClientAuthorityColourChanged;

            if (_clientAuthorityButton != null)
            {
                _clientAuthorityButton.onClick.RemoveAllListeners();
                _clientAuthorityButton.interactable = false;
                SetButtonColour(_clientAuthorityButton, ClientAuthorityButtonColour.Value);
            }
            else
                Debug.LogError("Client authority button missing!");

            if (_serverAuthorityButton != null)
            {
                _serverAuthorityButton.onClick.RemoveAllListeners();
                _serverAuthorityButton.interactable = false;
                SetButtonColour(_serverAuthorityButton, ServerAuthorityButtonColour.Value);
            }
            else
                Debug.LogError("Server authority button missing");

            ApplyHostReadyStatus(IsHostReady.Value);

            if (_clientObjectPrefab != null)
            {
                _activeClientObject = Instantiate(_clientObjectPrefab, _gameLobbyCanvas.transform);
                _clientObjectNameText = GameObject.FindGameObjectWithTag("ClientNameText").GetComponent<TextMeshProUGUI>();
                _clientObjectReadyStatusText = GameObject.FindGameObjectWithTag("ClientReadyStatusText").GetComponent<TextMeshProUGUI>();

                if (_clientObjectReadyStatusText != null)
                {
                    _clientObjectReadyStatusText.text = "Not Ready";
                }
                else
                    Debug.LogError("Cannot find client object ready status text!");

                if (_clientObjectNameText != null)
                {
                    if (PersistentLobbyManager.Instance != null)
                    {
                        _clientObjectNameText.text = PersistentLobbyManager.Instance.LocalPlayerName;    
                        SetClientNameServerRpc(PersistentLobbyManager.Instance.LocalPlayerName);
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

            if (PersistentLobbyManager.Instance == null)
            {
                Debug.LogError("Cannot find persistent lobby manager instance!");
                return;
            }

            if (_hostNameText != null)
            {
                _hostNameText.text = PersistentLobbyManager.Instance.LocalPlayerName;
            }
            else
                Debug.LogError("Host name text missing!");

            if (_lobbyTitleText != null)
            {
                _lobbyTitleText.text = PersistentLobbyManager.Instance.LobbyName;
            }
            else
                Debug.LogError("Lobby title text missing!");
        }   
    }

    public override void OnNetworkDespawn()
    {
        NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        if (!IsServer)
        {
            IsHostReady.OnValueChanged -= OnHostReadyToggle;
            ServerAuthorityButtonColour.OnValueChanged -= OnServerAuthorityColourChanged;
            ClientAuthorityButtonColour.OnValueChanged -= OnClientAuthorityColourChanged;
        }
        else
        {
            NetworkManager.OnClientConnectedCallback -= OnClientConnected;
        }
    }  

    [ClientRpc]
    private void SendHostNameClientRpc(string hostName)
    {
        if (PersistentLobbyManager.Instance != null)
        {
            PersistentLobbyManager.Instance.SetConnectedPlayerName(hostName);
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
        if (PersistentLobbyManager.Instance != null)
        {
            if (!PersistentLobbyManager.Instance.ClientSetLobbyName(lobbyName))
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
    private void ToggleClientStatusServerRpc()
    {
        IsClientReady.Value = !IsClientReady.Value;
        ApplyClientReadyStatus(IsClientReady.Value);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void SetClientNameServerRpc(string playerName)
    {
        if (PersistentLobbyManager.Instance != null)
        {
            PersistentLobbyManager.Instance.SetConnectedPlayerName(playerName);
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
        if (IsServer)
        {
            if (NetworkManager.Singleton.ConnectedClientsIds.Count > 2)
            {
                Debug.LogWarning("Maximum of 2 players reached. No more players can connect!");
                NetworkManager.Singleton.DisconnectClient(clientId, "Maximum number of players reached!");
                return;
            }

            if (clientId == NetworkManager.Singleton.LocalClientId)
                return;

            if (NetworkedLobbyManager.Instance != null)
                NetworkedLobbyManager.Instance.StopListening();
            else Debug.LogError("Networked lobby manager instance is missing!");

            if (_clientObjectPrefab != null)
            {
                _activeClientObject = _clientObjectPrefab;
                if (_activeClientObject != null)
                {
                    _activeClientObject = Instantiate(_activeClientObject, _gameLobbyCanvas.transform);
                    _clientObjectNameText = GameObject.FindGameObjectWithTag("ClientNameText").GetComponent<TextMeshProUGUI>();
                    _clientObjectReadyStatusText = GameObject.FindGameObjectWithTag("ClientReadyStatusText").GetComponent<TextMeshProUGUI>();
                    if (PersistentLobbyManager.Instance != null)
                    {
                        if (PersistentLobbyManager.Instance.ConnectedPlayerName != null)
                        {
                            _clientObjectNameText.text = PersistentLobbyManager.Instance.ConnectedPlayerName;
                        }
                        else
                            Debug.LogWarning("Connected player name not yet set!");
                    }
                    else
                        Debug.LogError("Peristent lobby manager instance is missing!");
                }
            }

            SendHostNameClientRpc(PersistentLobbyManager.Instance.LocalPlayerName);
            SendLobbyNameClientRpc(PersistentLobbyManager.Instance.LobbyName);
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (IsServer)
        {
            if (_activeClientObject != null)
                Destroy(_activeClientObject.gameObject);

            if (PersistentLobbyManager.Instance != null)
            {
                PersistentLobbyManager.Instance.RemoveConnectedPlayerName();
            }
            else
                Debug.LogError("Persistent lobby manager instance is missing!");

            _activeClientObject = null;
            _clientObjectNameText = null;
            _clientObjectReadyStatusText = null;
            IsClientReady.Value = false;
            IsHostReady.Value = false;
            ApplyHostReadyStatus(IsHostReady.Value);

            if (NetworkedLobbyManager.Instance != null)
                NetworkedLobbyManager.Instance.StartListening();
            else Debug.LogError("Networked lobby manager instance is missing!");
        }

        if (NetworkManager.Singleton == null) return;
        if (clientId != NetworkManager.Singleton.LocalClientId) return;

        if (_persistentDisconnectionManagerPrefab != null)
            Instantiate(_persistentDisconnectionManagerPrefab);
        else Debug.LogError("Persistent disconnection manager prefab is missing!");
        if (PersistentDisconnectionManager.Instance != null)
            PersistentDisconnectionManager.Instance.SetDisconnectionReason(NetworkManager.Singleton.DisconnectReason);

        NetworkManager.Singleton.Shutdown();
        Destroy(NetworkManager.Singleton.gameObject);

        if (PersistentLobbyManager.Instance != null)
            Destroy(PersistentLobbyManager.Instance.gameObject);

        SceneManager.LoadScene("DisconnectedScene");
    }   
    
    public void ExitGameLobby()
    {
        if (IsServer)
        {
            if (NetworkManager.Singleton != null)
            {
                if (NetworkManager.Singleton.ConnectedClients.Count > 1)
                {
                    ulong clientId = NetworkManager.Singleton.ConnectedClientsIds[1];
                    NetworkManager.Singleton.DisconnectClient(clientId, "The host has left the lobby!");
                }
            }
        }

        if (PersistentLobbyManager.Instance != null)
        {
            Destroy(PersistentLobbyManager.Instance.gameObject);
        }

        SceneManager.LoadScene("MainMenuScene");
    }

    public void ToggleReadyStatus()
    {
        if (IsServer)
        {
            IsHostReady.Value = !IsHostReady.Value;
            ApplyHostReadyStatus(IsHostReady.Value);
        }
        else
        {
            ToggleClientStatusServerRpc();
        }
    }

    private void OnClientReadyToggle(bool oldValue, bool newValue)
    {
        ApplyClientReadyStatus(newValue);
    }

    private void OnHostReadyToggle(bool previousValue, bool newValue)
    {
        ApplyHostReadyStatus(newValue);
    }

    private void ApplyHostReadyStatus(bool isReady)
    {
        if (_hostReadyStatusText == null)
        {
            Debug.LogError("Host ready status text missing!");
        }

        if (isReady)
        {
            _hostReadyStatusText.text = "Ready";
        }
        else
        {
            _hostReadyStatusText.text = "Not Ready";
        }
    }

    private void ApplyClientReadyStatus(bool isReady)
    {
        if (_clientObjectReadyStatusText == null)
        {
            Debug.LogError("Client object ready status text is missing!");
            return;
        }

        if (isReady)
            _clientObjectReadyStatusText.text = "Ready";
        else
            _clientObjectReadyStatusText.text = "Not Ready";
    }

    public void SetClientAuthority()
    {
        if (IsServer)
        {
            SetAuthorityMode(RodAuthorityMode.ClientAuthoritative);
        }
    }

    private void OnClientAuthorityColourChanged(Color oldColour, Color newColour)
    {
        if (_clientAuthorityButton != null)
            SetButtonColour(_clientAuthorityButton, newColour);
    }

    public void SetServerAuthority()
    {
        SetAuthorityMode(RodAuthorityMode.ServerAuthoritative);
    }

    private void OnServerAuthorityColourChanged(Color oldColour, Color newColour)
    {
        if (_serverAuthorityButton != null)
            SetButtonColour(_serverAuthorityButton, newColour);
    }

    private void SetAuthorityMode(RodAuthorityMode rodAuthorityMode)
    {
        if (!IsServer) return;
        if (PersistentLobbyManager.Instance == null) return;
        if (_clientAuthorityButton == null) return;
        if (_serverAuthorityButton == null) return;
        if (PersistentLobbyManager.Instance.SetAuthorityMode(rodAuthorityMode))
        {
            switch (PersistentLobbyManager.Instance.AuthorityMode)
            {
                case RodAuthorityMode.ServerAuthoritative:
                    SetButtonColour(_serverAuthorityButton, Color.green);
                    ServerAuthorityButtonColour.Value = Color.green;
                    SetButtonColour(_clientAuthorityButton, Color.red);
                    ClientAuthorityButtonColour.Value = Color.red;
                    break;

                case RodAuthorityMode.ClientAuthoritative:
                    SetButtonColour(_clientAuthorityButton, Color.green);
                    ClientAuthorityButtonColour.Value = Color.green;
                    SetButtonColour(_serverAuthorityButton, Color.red);
                    ServerAuthorityButtonColour.Value = Color.red;
                    break;
            }
        }
        else
            Debug.LogError("Cannot toggle authority mode!");
    }   
    
    private void SetButtonColour(Button button, Color colour)
    {
        ColorBlock cb = button.colors;
        if (IsServer)
        {
            cb.normalColor = colour;
            cb.pressedColor = colour;
            cb.selectedColor = colour;
        }
        else
        {
            cb.disabledColor = colour;
        }
        button.colors = cb;
    }
}
