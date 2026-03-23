using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System;
using UnityEngine.SceneManagement;

public class NetworkedLobbyManager : NetworkBehaviour
{
    private bool _hasClientChosenToLeave = false;
    private bool _isListening = false;
    [SerializeField] private ushort _gameLobbyPort = 7777;
    [SerializeField] private ushort _listenerPort = 6777;
    [SerializeField] private string _gameID = "TABLEFOOTBALL_LAN";
    [SerializeField] private string _discoveryMessageType = "FIND_LOBBY";
    [SerializeField] private byte _gameProtocolVersion = 2;  
    [SerializeField] private GameObject _persistentDisconnectionManagerPrefab;
    public static NetworkedLobbyManager Instance;
    private UdpClient _udpClient;
    private Task _listenerLoop;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Debug.LogError("A networked lobby manager already exists!");
            Destroy(this.gameObject);
        }
    }

    private void Start()
    {
        if (PersistentLobbySessionManager.Instance == null)
        {
            Debug.LogError("Cannot find persistent lobby manager!");
            return;
        }

        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

        if (PersistentLobbySessionManager.Instance.IsHost)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;

            if (!_isListening && NetworkManager.Singleton.ConnectedClients.Count == 0)
            {
                UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                transport.ConnectionData.Address = PersistentLobbySessionManager.Instance.LobbyIP;
                transport.ConnectionData.Port = _gameLobbyPort;
                transport.ConnectionData.ServerListenAddress = "0.0.0.0";
                NetworkManager.Singleton.StartHost();
                StartListening();
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        if (IsServer)
        { 
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            StopListening();
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

            StopListening();

            _hasClientChosenToLeave = false;
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (IsServer)
        {
            if (clientId == NetworkManager.Singleton.LocalClientId) return;   
            StartListening();
        }
        else if (!_hasClientChosenToLeave)
        {
            if (clientId != NetworkManager.Singleton.LocalClientId) return; 
            if (_persistentDisconnectionManagerPrefab != null)
                Instantiate(_persistentDisconnectionManagerPrefab);
            else Debug.LogError("Persistent disconnection manager prefab is missing!");
            if (PersistentDisconnectionManager.Instance != null)
                PersistentDisconnectionManager.Instance.SetDisconnectionReason(NetworkManager.Singleton.DisconnectReason);  
            if (PersistentLobbySessionManager.Instance != null)
                    Destroy(PersistentLobbySessionManager.Instance.gameObject);
            SceneManager.LoadScene("DisconnectedScene");
        }
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
        else _hasClientChosenToLeave = true;

        if (PersistentLobbySessionManager.Instance != null)
        {
            Destroy(PersistentLobbySessionManager.Instance.gameObject);
        }

        SceneManager.LoadScene("MainMenuScene");
    }

    private void StopListening()
    {
        if (!_isListening) return;
        _isListening = false;
        if (_udpClient != null)
        {
            _udpClient.Close();
            _udpClient = null;
            Debug.Log("UDP client stopped listening!");
        }
    }

    private void StartListening()
    {
        if (_isListening) return;
        _udpClient = new UdpClient(_listenerPort);
        _udpClient.EnableBroadcast = true;
        _isListening = true;
        _listenerLoop = UdpListenerLoop();
        Debug.Log("UDP Client listener started!");
    }

    private async Task UdpListenerLoop()
    {
        try
        {
            while (_isListening)
            {
                UdpClient client = _udpClient;
                if (client == null) break;

                UdpReceiveResult result = await client.ReceiveAsync();
                if (!_isListening) break;
                byte[] receivedBytes = result.Buffer;
                string receivedData = Encoding.UTF8.GetString(receivedBytes);
                DiscoveryMessage receivedMessage = JsonUtility.FromJson<DiscoveryMessage>(receivedData);
                Debug.Log("Received a message!");
                IPEndPoint endpoint = result.RemoteEndPoint;

                if (receivedMessage.MessageType != _discoveryMessageType)
                {
                    Debug.LogError("Received message did not match expected message type!");
                    return;
                } 
                
                if (receivedMessage.ProtocolVersion != _gameProtocolVersion)
                {
                    Debug.LogError("Messaging client game protocol version does not match host game protocol version!");
                    return;
                }

                if (receivedMessage.GameId != _gameID)
                {
                    Debug.LogError("Messaging client game ID does not match host game ID!");
                    return;
                }

                if (PersistentLobbySessionManager.Instance == null)
                {
                    Debug.LogError("Persistent Lobby manager instance missing!");
                    return;
                }

                try
                {
                    LobbyDetailsMessage lobbyDetailsMessage = new LobbyDetailsMessage(_gameLobbyPort, PersistentLobbySessionManager.Instance.LobbyIP, PersistentLobbySessionManager.Instance.LobbyName);
                    string jsonMessage = JsonUtility.ToJson(lobbyDetailsMessage);
                    receivedBytes = Encoding.UTF8.GetBytes(jsonMessage);
                    _udpClient.Send(receivedBytes, receivedBytes.Length, endpoint);
                    Debug.Log("Sent lobby details to a potential client!");
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }
        catch(Exception ex)
        {
            Debug.LogException(ex);
        }
    }
}
