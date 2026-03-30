using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using TMPro;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;

public class PersistentLobbySessionManager : NetworkBehaviour
{  
    private bool _hasClientChosenToLeave = false;
    private bool _hasHostChosenToLeave = false;
    public new bool IsHost { get; private set; } = false;
    public string LobbyName { get; private set; }
    public string LocalPlayerName { get; private set; }
    public string ConnectedPlayerName { get; private set; }
    public string LobbyIP { get; private set; }
    public RodAuthorityMode AuthorityMode { get; private set; } = RodAuthorityMode.ClientAuthoritative;
    public ushort GamePort { get; private set; }
    [SerializeField] private GameObject _persistentDisconnectionManagerPrefab;
    public static PersistentLobbySessionManager Instance;

    private void Awake()
    {
        if (Instance != null)
        {
            Debug.Log("Destroying existing instance of PersistentLobbySessionManager"); 
            Destroy(Instance.gameObject);
        }

        Instance = this;
        DontDestroyOnLoad(Instance);
    }

    public override void OnNetworkSpawn()
    {
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        }
    }

    public override void OnNetworkDespawn()
    {
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }
    }

    public void SetHost(bool isHost)
    {
        IsHost = isHost;
    }

    public bool HostSetLobbyName()
    {
        TMP_InputField lobbyNameInput = FindAnyObjectByType<TMP_InputField>();
        if (lobbyNameInput != null)
        {
            string inputText = lobbyNameInput.text;
            if (!string.IsNullOrWhiteSpace(inputText) && inputText.Length <= 30)
            {
                LobbyName = lobbyNameInput.text;
                Debug.Log($"Set lobby name to {LobbyName}");
                IsHost = true;
                Debug.Log($"Is Host = {IsHost}");
                return true;
            }
        }
        else
            Debug.LogError("Cannot find lobby name input field!");
        return false;
    }

    public bool ClientSetLobbyName(string lobbyName)
    {
        if (!string.IsNullOrWhiteSpace(lobbyName) && lobbyName.Length <= 30)
        {
            LobbyName = lobbyName;
            return true;
        }
        else
            Debug.LogError("Lobby name must contain at least one character and be less than or equal to 30 characters!");
        return false;
    }

    public void SetLobbyIP()
    {
        LobbyIP = GetLocalIPAddress();
    }

    public void SetLobbyIP(string ip)
    {
        LobbyIP = ip;
    }

    public void SetGamePort(ushort port)
    {
        GamePort = port;
    }

    public bool SetLocalPlayerName(string inputName)
    {
        if (!string.IsNullOrWhiteSpace(inputName) && inputName.Length <= 12)
        {
            LocalPlayerName = inputName;
            return true;
        }
        else
        {
            Debug.LogError("Player name must be between 0 - 12 characters!");
        }
        return false;
    }

    public bool SetConnectedPlayerName(string inputName)
    {
        if (!string.IsNullOrWhiteSpace(inputName) && inputName.Length <= 12)
        {
            ConnectedPlayerName = inputName;
            return true;
        }
        else
        {
            Debug.LogError("Player name must be between 0 - 12 characters!");
        }
        return false;
    }

    public void RemoveConnectedPlayerName()
    {
        ConnectedPlayerName = null;
    }

    public bool SetAuthorityMode(RodAuthorityMode rodAuthorityMode)
    {
        AuthorityMode = rodAuthorityMode;
        return true;
    }

    private string GetLocalIPAddress()
    {
        foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up)
                continue;

            // Not real LAN adapter
            if (ni.NetworkInterfaceType != NetworkInterfaceType.Wireless80211 &&
                ni.NetworkInterfaceType != NetworkInterfaceType.Ethernet)
                continue;

            var properties = ni.GetIPProperties();

            foreach (var addr in properties.UnicastAddresses)
            {
                // Get IPV4 addresses
                if (addr.Address.AddressFamily == AddressFamily.InterNetwork &&
                    !IPAddress.IsLoopback(addr.Address))
                {
                    return addr.Address.ToString();
                }
            }
        }

        return "127.0.0.1";
    }   
    
    public void ExitGameLobby()
    {
        if (IsServer)
        {
            DisconnectConnectedClient("Host has Left!");
            _hasHostChosenToLeave = true;
        }
        else _hasClientChosenToLeave = true;

        SceneManager.LoadScene("MainMenuScene");
    }   

    private void DisconnectConnectedClient(string disconnectionReason)
    {
        if (NetworkManager.Singleton != null)
        {
            if (NetworkManager.Singleton.ConnectedClients.Count > 1)
            {
                ulong clientId = NetworkManager.Singleton.ConnectedClientsIds[1];
                NetworkManager.Singleton.DisconnectClient(clientId, disconnectionReason);    
                Debug.Log("Kicking connected client!");
            }
        }
    }
    
    private void OnClientDisconnected(ulong clientId)
    {
        if (IsServer)
        { 
            if (clientId == NetworkManager.Singleton.LocalClientId) return;
            switch (SceneManager.GetActiveScene().name)
            {
                case "GameLobbyScene":
                    if (NetworkedLobbyManager.Instance != null)
                    {
                        NetworkedLobbyManager.Instance.StartListening();
                    }
                    break;

                case "GameScene":
                    if (_hasHostChosenToLeave) return;
                    Instantiate(_persistentDisconnectionManagerPrefab);
                    if (PersistentDisconnectionManager.Instance != null)
                    {
                        PersistentDisconnectionManager.Instance.SetDisconnectionReason("Client has exited the game!");
                        SceneManager.LoadScene("DisconnectedScene");
                    }
                    break;
            }
        }
        else if (!_hasClientChosenToLeave)
        {
            if (clientId != NetworkManager.Singleton.LocalClientId) return; 
            if (_persistentDisconnectionManagerPrefab != null)
                Instantiate(_persistentDisconnectionManagerPrefab);
            else Debug.LogError("Persistent disconnection manager prefab is missing!");
            if (PersistentDisconnectionManager.Instance != null)
                PersistentDisconnectionManager.Instance.SetDisconnectionReason(NetworkManager.Singleton.DisconnectReason);    
            SceneManager.LoadScene("DisconnectedScene");
        }    
    }

    private void OnClientConnected(ulong clientId)
    {
        if (IsServer)
        {
            if (clientId == NetworkManager.Singleton.LocalClientId)
                return;

            if (NetworkManager.Singleton.ConnectedClientsIds.Count > 2)
            {
                Debug.LogWarning("Maximum of 2 players reached. No more players can connect!");
                NetworkManager.Singleton.DisconnectClient(clientId, "Maximum number of players reached!");
                return;
            }

            _hasClientChosenToLeave = false;
            if (SceneManager.GetActiveScene().name != "GameLobbyScene") return;

            if (NetworkedLobbyManager.Instance != null)
            {
                NetworkedLobbyManager.Instance.StopListening();
            }
        }
    }
}
