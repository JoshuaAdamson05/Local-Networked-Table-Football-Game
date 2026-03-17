using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

[Serializable]
public struct LobbyDetailsMessage
{
    public ushort GameLobbyPort;
    public string IPAddress;
    public string LobbyName;

    public LobbyDetailsMessage(ushort gameLobbyPort, string ip, string lobbyName)
    {
        GameLobbyPort = gameLobbyPort;
        IPAddress = ip;
        LobbyName = lobbyName;
    }
}

[Serializable]
public struct DiscoveryMessage
{
    public string MessageType;
    public string GameId;
    public byte ProtocolVersion;

    public DiscoveryMessage(string messageType, string gameId, byte protocolVersion)
    {
        MessageType = messageType;
        GameId = gameId;
        ProtocolVersion = protocolVersion;
    }
}

public class ClientLobbyListener : MonoBehaviour
{
    private bool _isListening;
    [SerializeField] private byte _gameProtocolVersion = 1;
    [SerializeField] private ushort _listenerPort = 6777;
    [SerializeField] private int _listenTimeSeconds = 3;
    [SerializeField] private string _gameID = "TABLEFOOTBALL_LAN";
    [SerializeField] private string _discoveryMessageType = "FIND_LOBBY";
    public static event Action<List<LobbyDetailsMessage>> OnLobbyDetailsUpdated;
    public static event Action<bool> OnSearchingAction;
    private float _remainingListeningTime = 0;
    private Task _listenerLoop;
    private UdpClient _udpClient;
    private List<LobbyDetailsMessage> _lobbyDetails;

    private void Start()
    {
        _lobbyDetails = new List<LobbyDetailsMessage>();
        StartLobbyDiscovery();
    }

    private void OnDestroy()
    {
        if (_udpClient != null)
        {
            _isListening = false;
            _udpClient.Close();
            Debug.Log("Closed UDP Client");
        }
    }

    private void Update()
    {
        if (_isListening)
        {
            if (_remainingListeningTime <= 0)
            {
                _isListening = false;
                OnSearchingAction?.Invoke(_isListening);
                _udpClient.Close();
            }
            else
            {
                _remainingListeningTime -= Time.deltaTime;
            }
        }
    }

    public void StartLobbyDiscovery()
    {
        SendDiscoveryMessage();
        OnSearchingAction?.Invoke(true);
        _lobbyDetails.Clear();
        OnLobbyDetailsUpdated?.Invoke(_lobbyDetails);
    }

    private void SendDiscoveryMessage()
    {
        _udpClient = new UdpClient(0);
        _udpClient.EnableBroadcast = true;
        try
        {
            DiscoveryMessage message = new DiscoveryMessage(_discoveryMessageType, _gameID, _gameProtocolVersion);
            string jsonMessage = JsonUtility.ToJson(message);
            byte[] sendBytes = Encoding.UTF8.GetBytes(jsonMessage);
            IPAddress address = GetSubnetBroadcastAddress();
            Debug.Log(address.ToString());
            _udpClient.Send(sendBytes, sendBytes.Length, address.ToString(), _listenerPort);
            _remainingListeningTime = _listenTimeSeconds;
            _isListening = true;
            _listenerLoop = UdpListenerLoop();
        }
        catch(Exception ex)
        {
            Debug.LogException(ex);
        }
    }

    private async Task UdpListenerLoop()
    {
        while (_isListening)
        {
            UdpReceiveResult result = await _udpClient.ReceiveAsync();
            byte[] receivedBytes = result.Buffer;
            string receivedData = Encoding.UTF8.GetString(receivedBytes);
            Debug.Log(receivedData);
            LobbyDetailsMessage receivedMessage = JsonUtility.FromJson<LobbyDetailsMessage>(receivedData);
            Debug.Log($"Lobby Name: {receivedMessage.LobbyName}, Lobby IP: {receivedMessage.IPAddress}, Lobby Port: {receivedMessage.GameLobbyPort}");
            _lobbyDetails.Add(receivedMessage);
            OnLobbyDetailsUpdated?.Invoke(_lobbyDetails);
        }
    }

    private IPAddress GetSubnetBroadcastAddress()
    {
        foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up)
                continue;

            if (ni.NetworkInterfaceType != NetworkInterfaceType.Ethernet &&
                ni.NetworkInterfaceType != NetworkInterfaceType.Wireless80211)
                continue;

            foreach(var address in ni.GetIPProperties().UnicastAddresses)
            {
                if (address.Address.AddressFamily != AddressFamily.InterNetwork)
                    continue;

                byte[] ip = address.Address.GetAddressBytes();
                byte[] mask = address.IPv4Mask.GetAddressBytes();

                byte[] broadcast = new byte[ip.Length];
                for (int i = 0; i < ip.Length; i++)
                {
                    // keep network portion, change host bits to 1
                    broadcast[i] = (byte)(ip[i] | (mask[i] ^ 255));
                }

                return new IPAddress(broadcast);
            }
        }

        return IPAddress.Broadcast;
    }
}
