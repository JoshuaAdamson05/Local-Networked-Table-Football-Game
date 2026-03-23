using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using TMPro;
using UnityEngine;

public class PersistentLobbySessionManager : MonoBehaviour
{
    public bool IsHost { get; private set; } = false;
    public string LobbyName { get; private set; }
    public string LocalPlayerName { get; private set; }
    public string ConnectedPlayerName { get; private set; }
    public string LobbyIP { get; private set; }
    public RodAuthorityMode AuthorityMode { get; private set; } = RodAuthorityMode.ClientAuthoritative;
    public ushort GamePort { get; private set; }

    public static PersistentLobbySessionManager Instance;

    private void Awake()
    {
        if (Instance != null)
        {
            Debug.Log("Destroying existing instance of LobbyManager"); 
            Destroy(Instance.gameObject);
        }

        Instance = this;
        DontDestroyOnLoad(Instance);
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
}
