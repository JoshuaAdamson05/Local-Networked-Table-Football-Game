using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System;

public class NetworkedLobbyManager : MonoBehaviour
{
    private bool _isListening = false;
    [SerializeField] private ushort _gameLobbyPort = 7777;
    [SerializeField] private ushort _listenerPort = 6777;
    [SerializeField] private string _gameID = "TABLEFOOTBALL_LAN";
    [SerializeField] private string _discoveryMessageType = "FIND_LOBBY";
    [SerializeField] private byte _gameProtocolVersion = 1;
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
        if (PersistentLobbyManager.Instance == null)
        {
            Debug.LogError("Cannot find persistent lobby manager!");
            return;
        }

        if (PersistentLobbyManager.Instance.IsHost)
        {
            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.ConnectionData.Address = PersistentLobbyManager.Instance.LobbyIP;
            transport.ConnectionData.Port = _gameLobbyPort;
            transport.ConnectionData.ServerListenAddress = "0.0.0.0";
            NetworkManager.Singleton.StartHost();
            StartListening();
        }
    }

    private void OnDestroy()
    {
        StopListening();
    }

    public void StopListening()
    {
        _isListening = false;
        if (_udpClient != null)
        {
            _udpClient.Close();
            _udpClient = null;
            Debug.Log("UDP client stopped listening!");
        }
    }

    public void StartListening()
    {
        _isListening = true;
        _listenerLoop = UdpListenerLoop();
        Debug.Log("UDP Client listener started!");
    }

    private async Task UdpListenerLoop()
    {
        _udpClient = new UdpClient(_listenerPort);
        _udpClient.EnableBroadcast = true;
        while (_isListening)
        {
            UdpReceiveResult result = await _udpClient.ReceiveAsync();
            byte[] receivedBytes = result.Buffer;
            string receivedData = Encoding.UTF8.GetString(receivedBytes);
            DiscoveryMessage receivedMessage = JsonUtility.FromJson<DiscoveryMessage>(receivedData);
            Debug.Log("Received a message!");
            IPEndPoint endpoint = result.RemoteEndPoint;

            if (receivedMessage.MessageType == _discoveryMessageType
                && receivedMessage.ProtocolVersion == _gameProtocolVersion
                && receivedMessage.GameId == _gameID)
            {
                if (PersistentLobbyManager.Instance == null)
                {
                    Debug.LogError("Persistent Lobby manager instance missing!");
                    return;
                }

                try
                {
                    LobbyDetailsMessage lobbyDetailsMessage = new LobbyDetailsMessage(_gameLobbyPort, PersistentLobbyManager.Instance.LobbyIP, PersistentLobbyManager.Instance.LobbyName);
                    string jsonMessage = JsonUtility.ToJson(lobbyDetailsMessage);
                    receivedBytes = Encoding.UTF8.GetBytes(jsonMessage);
                    _udpClient.Send(receivedBytes, receivedBytes.Length, endpoint);
                    Debug.Log("Sent lobby details to a potential client!");
                }
                catch(Exception ex)
                {
                    Debug.LogException(ex); 
                }
            }
            else Debug.LogError("Received message does not match expected message structure / details");
        }
    }
}
